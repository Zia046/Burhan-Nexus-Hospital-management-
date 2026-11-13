using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Services;

public sealed class AuditQuery
{
    public string? Search { get; set; }
    public string? Action { get; set; }
    public int? UserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public bool? Success { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class LoginHistoryQuery
{
    public int? UserId { get; set; }
    public bool? Success { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditLog>> SearchAsync(AuditQuery q, CancellationToken ct = default);
    IAsyncEnumerable<AuditLog> StreamAsync(AuditQuery q, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ActionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> RecentAsync(int count, CancellationToken ct = default);
    Task<PagedResult<LoginHistory>> LoginHistoryAsync(LoginHistoryQuery q, CancellationToken ct = default);
}

/// <summary>Read-only access to audit and login records. There is intentionally no update or delete method.</summary>
public sealed class AuditQueryService : IAuditQueryService
{
    private readonly HmsDbContext _db;

    public AuditQueryService(HmsDbContext db) => _db = db;

    private IQueryable<AuditLog> Filter(AuditQuery q)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = "%" + q.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(a => (a.Username != null && EF.Functions.ILike(a.Username, term))
                                     || (a.Description != null && EF.Functions.ILike(a.Description, term))
                                     || (a.EntityId != null && EF.Functions.ILike(a.EntityId, term))
                                     || (a.Workstation != null && EF.Functions.ILike(a.Workstation, term))
                                     || (a.IpAddress != null && EF.Functions.ILike(a.IpAddress, term)));
        }
        if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(a => a.Action == q.Action);
        if (q.UserId.HasValue) query = query.Where(a => a.UserId == q.UserId);
        if (q.FromUtc.HasValue) query = query.Where(a => a.TimestampUtc >= q.FromUtc);
        if (q.ToUtc.HasValue) query = query.Where(a => a.TimestampUtc < q.ToUtc);
        if (q.Success.HasValue) query = query.Where(a => a.Success == q.Success);
        return query;
    }

    public async Task<PagedResult<AuditLog>> SearchAsync(AuditQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 10, 200);
        var query = Filter(q);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AuditLog>(items, total, page, size);
    }

    public IAsyncEnumerable<AuditLog> StreamAsync(AuditQuery q, CancellationToken ct = default) =>
        Filter(q).OrderByDescending(a => a.Id).Take(100_000).AsAsyncEnumerable();

    public async Task<IReadOnlyList<string>> ActionsAsync(CancellationToken ct = default) =>
        await _db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> RecentAsync(int count, CancellationToken ct = default) =>
        await _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Id).Take(count).ToListAsync(ct);

    public async Task<PagedResult<LoginHistory>> LoginHistoryAsync(LoginHistoryQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 10, 200);
        var query = _db.LoginHistory.AsNoTracking().AsQueryable();
        if (q.UserId.HasValue) query = query.Where(l => l.UserId == q.UserId);
        if (q.Success.HasValue) query = query.Where(l => l.Success == q.Success);
        if (q.FromUtc.HasValue) query = query.Where(l => l.AttemptedUtc >= q.FromUtc);
        if (q.ToUtc.HasValue) query = query.Where(l => l.AttemptedUtc < q.ToUtc);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<LoginHistory>(items, total, page, size);
    }
}

public sealed record SystemHealth(bool DatabaseConnected, string? DatabaseVersion, string? DatabaseName, long? DatabaseSizeBytes,
    int ActiveSessions, string? Error);

public interface ISystemStatusService
{
    Task<SystemHealth> CheckAsync(CancellationToken ct = default);
}

public sealed class SystemStatusService : ISystemStatusService
{
    private readonly HmsDbContext _db;
    private readonly TimeProvider _time;
    private readonly ISettingsService _settings;

    public SystemStatusService(HmsDbContext db, TimeProvider time, ISettingsService settings)
    {
        _db = db;
        _time = time;
        _settings = settings;
    }

    public async Task<SystemHealth> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var version = await _db.Database.SqlQueryRaw<string>("SELECT current_setting('server_version') AS \"Value\"").FirstAsync(ct);
            var name = await _db.Database.SqlQueryRaw<string>("SELECT current_database()::text AS \"Value\"").FirstAsync(ct);
            var size = await _db.Database.SqlQueryRaw<long>("SELECT pg_database_size(current_database()) AS \"Value\"").FirstAsync(ct);
            var timeout = (await _settings.GetSecurityAsync(ct)).SessionTimeoutMinutes;
            var since = _time.GetUtcNow().UtcDateTime.AddMinutes(-timeout);
            var sessions = await _db.LoginHistory.CountAsync(l => l.Success && l.LogoutUtc == null && l.LastActivityUtc >= since, ct);
            return new SystemHealth(true, version, name, size, sessions, null);
        }
        catch (Exception ex)
        {
            return new SystemHealth(false, null, null, null, 0, ex.Message);
        }
    }
}
