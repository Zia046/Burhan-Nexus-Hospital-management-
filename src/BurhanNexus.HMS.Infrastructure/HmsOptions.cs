namespace BurhanNexus.HMS.Infrastructure;

/// <summary>Server-side configuration (section "Hms" in appsettings.json).</summary>
public sealed class HmsOptions
{
    public const string SectionName = "Hms";

    /// <summary>Folder for server-only data: encryption keys, licence keys. Never shared with clients.</summary>
    public string DataDirectory { get; set; } = "data";

    /// <summary>Folder where database backups are written.</summary>
    public string BackupDirectory { get; set; } = "backups";

    /// <summary>Folder containing pg_dump / pg_restore. Empty = auto-detect.</summary>
    public string? PostgresBinDirectory { get; set; }

    /// <summary>Apply database migrations automatically at start-up.</summary>
    public bool AutoMigrate { get; set; } = true;

    public string ResolveDataDirectory() => Resolve(DataDirectory);
    public string ResolveBackupDirectory() => Resolve(BackupDirectory);

    private static string Resolve(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
}
