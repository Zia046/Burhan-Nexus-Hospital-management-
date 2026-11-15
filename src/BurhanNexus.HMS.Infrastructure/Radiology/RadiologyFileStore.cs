using System.IO.Compression;
using System.Security.Cryptography;
using BurhanNexus.HMS.Core.Domain;
using Microsoft.Extensions.Options;

namespace BurhanNexus.HMS.Infrastructure.Radiology;

/// <summary>
/// Radiology files on the hospital server (configurable folder "Hms:RadiologyStorageDirectory").
/// Files are never stored in database columns; the database keeps the relative path, size and SHA-256 checksum.
/// Files are written once under a new random name and never overwritten or deleted by the application.
/// </summary>
public sealed class RadiologyFileStore
{
    private readonly HmsOptions _options;
    public RadiologyFileStore(IOptions<HmsOptions> options) => _options = options.Value;

    public string Root => _options.ResolveRadiologyStorageDirectory();

    /// <summary>Content type from the file's own bytes (the browser's claim is not trusted).</summary>
    public static string? Sniff(ReadOnlySpan<byte> d)
    {
        if (d.Length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return "image/png";
        if (d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length > 5 && d[0] == 0x25 && d[1] == 0x50 && d[2] == 0x44 && d[3] == 0x46 && d[4] == 0x2D) return "application/pdf";
        // DICOM Part 10: 128-byte preamble followed by "DICM".
        if (d.Length > 132 && d[128] == 0x44 && d[129] == 0x49 && d[130] == 0x43 && d[131] == 0x4D) return "application/dicom";
        return null;
    }

    public static RadiologyFileKind KindOf(string contentType) => contentType switch
    {
        "application/pdf" => RadiologyFileKind.Pdf,
        "application/dicom" => RadiologyFileKind.Dicom,
        _ => RadiologyFileKind.Image,
    };

    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "application/pdf" => ".pdf",
        _ => ".dcm",
    };

    /// <summary>Full path of a stored file, refusing anything outside the storage folder.</summary>
    public string? PathOf(string storedPath)
    {
        var root = Path.GetFullPath(Root);
        var full = Path.GetFullPath(Path.Combine(root, storedPath.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>Copies the upload to a temporary file, checks type and size, then moves it to its final name.</summary>
    public async Task<(string StoredPath, string ContentType, long Size, string Sha256)?> SaveAsync(Stream input, long maxBytes, DateTime utc, CancellationToken ct)
    {
        var folder = Path.Combine(Root, utc.ToString("yyyy"), utc.ToString("MM"));
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".part");
        try
        {
            long size = 0;
            var header = new byte[136];
            var headerLen = 0;
            using var sha = SHA256.Create();
            await using (var outFile = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > maxBytes) return null;
                    if (headerLen < header.Length)
                    {
                        var n = Math.Min(read, header.Length - headerLen);
                        Array.Copy(buffer, 0, header, headerLen, n);
                        headerLen += n;
                    }
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    await outFile.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                await outFile.FlushAsync(ct);
            }
            var type = Sniff(header.AsSpan(0, headerLen));
            if (size == 0 || type is null) return ("", "", 0, "");
            var name = Guid.NewGuid().ToString("N") + Extension(type);
            File.Move(temp, Path.Combine(folder, name));
            return ($"{utc:yyyy}/{utc:MM}/{name}", type, size, Convert.ToHexString(sha.Hash!).ToLowerInvariant());
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public bool HasFiles() => Directory.Exists(Root) && Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Any(f => !Path.GetFileName(f).StartsWith('.'));

    /// <summary>Backup: copies every stored file into a zip next to the database backup.</summary>
    public int ArchiveTo(string zipPath)
    {
        var root = Path.GetFullPath(Root);
        var count = 0;
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f).StartsWith('.')) continue;
            zip.CreateEntryFromFile(f, Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/'), CompressionLevel.NoCompression);
            count++;
        }
        return count;
    }

    /// <summary>Restore: puts back files from a backup archive. Existing files are never overwritten (names are unique).</summary>
    public int RestoreFrom(string zipPath)
    {
        var root = Path.GetFullPath(Root);
        var count = 0;
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var e in zip.Entries)
        {
            if (string.IsNullOrEmpty(e.Name)) continue;
            var target = PathOf(e.FullName);
            if (target is null || File.Exists(target)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, overwrite: false);
            count++;
        }
        return count;
    }

    public static string ArchiveNameFor(string backupPath) => Path.ChangeExtension(backupPath, ".radiology-files.zip");
}
