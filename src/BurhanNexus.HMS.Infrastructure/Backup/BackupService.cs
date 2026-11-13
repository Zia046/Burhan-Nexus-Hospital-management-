using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Backup;

public interface IBackupService
{
    Task<IReadOnlyList<BackupHistory>> ListAsync(CancellationToken ct = default);
    Task<BackupHistory?> GetAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<BackupHistory>> CreateAsync(BackupKind kind, string? notes, CancellationToken ct = default);
    /// <summary>Returns the full path of a completed backup file after verifying it is inside the backup folder.</summary>
    Task<ServiceResult<string>> GetFileForDownloadAsync(int id, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Restores the database. Always creates a safety backup of the current data first.</summary>
    Task<ServiceResult<string>> RestoreAsync(int id, string actorPassword, CancellationToken ct = default);
    string BackupDirectory { get; }
    string? ToolsStatus();
}

/// <summary>Serialises backup and restore operations across all requests on the server.</summary>
public sealed class BackupLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public sealed class BackupService : IBackupService
{
    private readonly HmsDbContext _db;
    private readonly HmsOptions _options;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly IPasswordService _passwords;
    private readonly IMemoryCache _cache;
    private readonly SetupStateCache _setupCache;
    private readonly BackupLock _lock;
    private readonly TimeProvider _time;
    private readonly ILogger<BackupService> _logger;

    public BackupService(HmsDbContext db, IOptions<HmsOptions> options, IAuditService audit, IRequestContext ctx,
        IPasswordService passwords, IMemoryCache cache, SetupStateCache setupCache, BackupLock backupLock,
        TimeProvider time, ILogger<BackupService> logger)
    {
        _db = db;
        _options = options.Value;
        _audit = audit;
        _ctx = ctx;
        _passwords = passwords;
        _cache = cache;
        _setupCache = setupCache;
        _lock = backupLock;
        _time = time;
        _logger = logger;
    }

    public string BackupDirectory => _options.ResolveBackupDirectory();
    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<BackupHistory>> ListAsync(CancellationToken ct = default)
    {
        await SyncFolderAsync(ct);
        return await _db.BackupHistory.AsNoTracking().OrderByDescending(b => b.StartedUtc).Take(500).ToListAsync(ct);
    }

    public Task<BackupHistory?> GetAsync(int id, CancellationToken ct = default) =>
        _db.BackupHistory.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);

    public string? ToolsStatus()
    {
        var dump = FindTool("pg_dump");
        var restore = FindTool("pg_restore");
        if (dump is null || restore is null)
            return "PostgreSQL tools (pg_dump / pg_restore) were not found. Set 'Hms:PostgresBinDirectory' in appsettings.json.";
        return null;
    }

    public async Task<ServiceResult<BackupHistory>> CreateAsync(BackupKind kind, string? notes, CancellationToken ct = default)
    {
        if (!await _lock.Semaphore.WaitAsync(TimeSpan.FromSeconds(2), ct))
            return ServiceResult<BackupHistory>.Fail("Another backup or restore is already running. Please wait and try again.");
        try
        {
            return await CreateCoreAsync(kind, notes, ct);
        }
        finally
        {
            _lock.Semaphore.Release();
        }
    }

    private async Task<ServiceResult<BackupHistory>> CreateCoreAsync(BackupKind kind, string? notes, CancellationToken ct)
    {
        var pgDump = FindTool("pg_dump");
        if (pgDump is null) return ServiceResult<BackupHistory>.Fail(ToolsStatus()!);

        Directory.CreateDirectory(BackupDirectory);
        var started = Now;
        var suffix = kind == BackupKind.PreRestoreSafety ? "_before-restore" : string.Empty;
        var fileName = $"BNHMS_{started:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}{suffix}.backup";
        var path = Path.Combine(BackupDirectory, fileName);

        var record = new BackupHistory
        {
            FileName = fileName, Status = BackupStatus.InProgress, Kind = kind, StartedUtc = started,
            CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 500)]
        };
        _db.BackupHistory.Add(record);
        await _db.SaveChangesAsync(ct);

        var cs = new NpgsqlConnectionStringBuilder(_db.Database.GetConnectionString());
        var args = new List<string>
        {
            "--format=custom", "--compress=6", "--no-owner", "--no-privileges",
            "--host", cs.Host ?? "localhost", "--port", cs.Port.ToString(),
            "--username", cs.Username ?? "postgres", "--dbname", cs.Database ?? string.Empty,
            "--file", path
        };
        var (exit, error) = await RunAsync(pgDump, args, cs.Password, TimeSpan.FromMinutes(60), ct);

        if (exit != 0 || !File.Exists(path))
        {
            record.Status = BackupStatus.Failed;
            record.CompletedUtc = Now;
            record.ErrorMessage = Truncate(string.IsNullOrWhiteSpace(error) ? $"pg_dump exited with code {exit}" : error, 2000);
            _audit.Add(AuditActions.BackupFailed, "Backup", record.Id.ToString(), $"Backup failed: {record.ErrorMessage}", success: false);
            await _db.SaveChangesAsync(CancellationToken.None);
            TryDelete(path);
            return ServiceResult<BackupHistory>.Fail("Backup failed: " + record.ErrorMessage);
        }

        record.SizeBytes = new FileInfo(path).Length;
        record.Sha256 = await HashFileAsync(path, ct);
        record.Status = BackupStatus.Completed;
        record.CompletedUtc = Now;
        _audit.Add(AuditActions.BackupCreated, "Backup", record.Id.ToString(),
            $"{kind} backup created: {fileName} ({record.SizeBytes:N0} bytes)", newValues: new { fileName, record.SizeBytes, record.Sha256 });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<BackupHistory>.Ok(record);
    }

    public async Task<ServiceResult<string>> GetFileForDownloadAsync(int id, CancellationToken ct = default)
    {
        var b = await _db.BackupHistory.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null || b.Status != BackupStatus.Completed) return ServiceResult<string>.Fail("Backup not found.");
        var path = SafePath(b.FileName);
        if (path is null || !File.Exists(path)) return ServiceResult<string>.Fail("The backup file is missing from the server backup folder.");
        await _audit.LogAsync(AuditActions.BackupDownloaded, "Backup", id.ToString(), $"Backup downloaded: {b.FileName}", ct: ct);
        return ServiceResult<string>.Ok(path);
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var b = await _db.BackupHistory.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return ServiceResult.Fail("Backup not found.");
        if (b.Status == BackupStatus.InProgress) return ServiceResult.Fail("A backup that is still running cannot be deleted.");
        var path = SafePath(b.FileName);
        if (path is not null) TryDelete(path);
        _db.BackupHistory.Remove(b);
        _audit.Add(AuditActions.BackupDeleted, "Backup", id.ToString(), $"Backup deleted: {b.FileName}", new { b.FileName, b.SizeBytes, b.StartedUtc });
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<string>> RestoreAsync(int id, string actorPassword, CancellationToken ct = default)
    {
        if (_ctx.UserId is not int actorId) return ServiceResult<string>.Fail("You must be logged in.");
        var hash = await _db.UserCredentials.Where(c => c.UserId == actorId).Select(c => c.PasswordHash).FirstOrDefaultAsync(ct);
        if (hash is null || !_passwords.Verify(hash, actorPassword ?? string.Empty, out _))
        {
            await _audit.LogAsync(AuditActions.BackupRestoreFailed, "Backup", id.ToString(), "Restore rejected: wrong password", success: false, ct: ct);
            return ServiceResult<string>.Fail("Your password is incorrect. Restore was not started.");
        }

        var b = await _db.BackupHistory.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null || b.Status != BackupStatus.Completed) return ServiceResult<string>.Fail("Backup not found or not completed.");
        var path = SafePath(b.FileName);
        if (path is null || !File.Exists(path)) return ServiceResult<string>.Fail("The backup file is missing from the server backup folder.");
        if (b.Sha256 is not null && !string.Equals(await HashFileAsync(path, ct), b.Sha256, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<string>.Fail("The backup file is damaged or was modified (checksum mismatch). Restore was not started.");

        var pgRestore = FindTool("pg_restore");
        if (pgRestore is null) return ServiceResult<string>.Fail(ToolsStatus()!);

        if (!await _lock.Semaphore.WaitAsync(TimeSpan.FromSeconds(2), ct))
            return ServiceResult<string>.Fail("Another backup or restore is already running. Please wait and try again.");
        try
        {
            // 1. Safety backup of the current data – restore is aborted if this fails.
            var safety = await CreateCoreAsync(BackupKind.PreRestoreSafety, $"Automatic safety backup before restoring {b.FileName}", ct);
            if (!safety.Succeeded) return ServiceResult<string>.Fail("Safety backup failed, restore was not started. " + safety.ErrorText);

            var actorName = _ctx.Username;
            var cs = new NpgsqlConnectionStringBuilder(_db.Database.GetConnectionString());
            var connectionString = cs.ConnectionString;

            // 2. Release pooled connections so pg_restore can replace tables.
            await _db.Database.CloseConnectionAsync();
            NpgsqlConnection.ClearAllPools();

            var args = new List<string>
            {
                "--clean", "--if-exists", "--single-transaction", "--no-owner", "--no-privileges", "--exit-on-error",
                "--host", cs.Host ?? "localhost", "--port", cs.Port.ToString(),
                "--username", cs.Username ?? "postgres", "--dbname", cs.Database ?? string.Empty,
                path
            };
            var (exit, error) = await RunAsync(pgRestore, args, cs.Password, TimeSpan.FromMinutes(120), CancellationToken.None);
            NpgsqlConnection.ClearAllPools();
            _db.ChangeTracker.Clear();

            if (exit != 0)
            {
                _logger.LogError("Restore failed: {Error}", error);
                await _audit.LogAsync(AuditActions.BackupRestoreFailed, "Backup", id.ToString(),
                    Truncate($"Restore of {b.FileName} failed; no changes were applied (single transaction). {error}", 1000), success: false, ct: CancellationToken.None);
                return ServiceResult<string>.Fail("Restore failed and no changes were applied. " + Truncate(error, 500));
            }

            // 3. Bring the restored database up to the current schema and record the restore.
            await _db.Database.MigrateAsync(CancellationToken.None);
            await DatabaseHardening.ApplyAsync(_db, CancellationToken.None);
            await SyncFolderAsync(CancellationToken.None);
            var restoredRow = await _db.BackupHistory.FirstOrDefaultAsync(x => x.FileName == b.FileName, CancellationToken.None);
            if (restoredRow is not null) restoredRow.LastRestoredUtc = Now;
            _db.AuditLogs.Add(new AuditLog
            {
                TimestampUtc = Now, UserId = actorId, Username = actorName, Action = AuditActions.BackupRestored,
                EntityType = "Backup", EntityId = b.FileName,
                Description = $"Database restored from {b.FileName}. Safety backup of previous data: {safety.Value!.FileName}",
                IpAddress = _ctx.IpAddress, Workstation = _ctx.Workstation
            });
            await _db.SaveChangesAsync(CancellationToken.None);

            if (_cache is MemoryCache mc) mc.Compact(1.0);
            _setupCache.Completed = false;
            return ServiceResult<string>.Ok(safety.Value!.FileName);
        }
        finally
        {
            _lock.Semaphore.Release();
        }
    }

    /// <summary>Registers backup files that exist on disk but not in the history (e.g. after a restore).</summary>
    private async Task SyncFolderAsync(CancellationToken ct)
    {
        if (!Directory.Exists(BackupDirectory)) return;
        var known = (await _db.BackupHistory.Select(b => b.FileName).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;
        foreach (var file in Directory.EnumerateFiles(BackupDirectory, "*.backup"))
        {
            var name = Path.GetFileName(file);
            if (known.Contains(name)) continue;
            var info = new FileInfo(file);
            _db.BackupHistory.Add(new BackupHistory
            {
                FileName = name, SizeBytes = info.Length, Sha256 = await HashFileAsync(file, ct),
                Status = BackupStatus.Completed, Kind = BackupKind.Imported,
                StartedUtc = info.CreationTimeUtc, CompletedUtc = info.LastWriteTimeUtc,
                Notes = "Found in backup folder"
            });
            added = true;
        }
        if (added) await _db.SaveChangesAsync(ct);
    }

    private string? SafePath(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(name) || name != fileName) return null;
        var full = Path.GetFullPath(Path.Combine(BackupDirectory, name));
        return full.StartsWith(Path.GetFullPath(BackupDirectory), StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private string? FindTool(string tool)
    {
        var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? tool + ".exe" : tool;
        if (!string.IsNullOrWhiteSpace(_options.PostgresBinDirectory))
        {
            var p = Path.Combine(_options.PostgresBinDirectory, exe);
            return File.Exists(p) ? p : null;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL");
            if (Directory.Exists(root))
            {
                var best = Directory.GetDirectories(root)
                    .Select(d => (Dir: d, Ver: int.TryParse(Path.GetFileName(d).Split('.')[0], out var v) ? v : 0))
                    .OrderByDescending(x => x.Ver)
                    .Select(x => Path.Combine(x.Dir, "bin", exe))
                    .FirstOrDefault(File.Exists);
                if (best is not null) return best;
            }
        }
        else
        {
            var pgRoot = "/usr/lib/postgresql";
            if (Directory.Exists(pgRoot))
            {
                var best = Directory.GetDirectories(pgRoot)
                    .OrderByDescending(d => int.TryParse(Path.GetFileName(d), out var v) ? v : 0)
                    .Select(d => Path.Combine(d, "bin", exe))
                    .FirstOrDefault(File.Exists);
                if (best is not null) return best;
            }
        }
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            try
            {
                var p = Path.Combine(dir, exe);
                if (File.Exists(p)) return p;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    private static async Task<(int ExitCode, string Error)> RunAsync(string exe, IEnumerable<string> args, string? password, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (!string.IsNullOrEmpty(password)) psi.Environment["PGPASSWORD"] = password;

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {exe}");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* ignore */ }
            return (-1, "Operation timed out or was cancelled.");
        }
        await stdoutTask;
        return (process.ExitCode, (await stderrTask).Trim());
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
