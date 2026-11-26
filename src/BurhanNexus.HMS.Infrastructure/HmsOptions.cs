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

    /// <summary>Phase 6: folder on the hospital server where radiology images / documents are stored (only metadata is in the database).</summary>
    public string RadiologyStorageDirectory { get; set; } = "radiology-files";

    /// <summary>Apply database migrations automatically at start-up.</summary>
    public bool AutoMigrate { get; set; } = true;
    /// <summary>Update 15: one complete automatic backup per day (the server checks every 30 minutes).</summary>
    public bool AutoBackup { get; set; } = true;
    /// <summary>Update 15: automatic (scheduled) backups older than this many days are deleted after a new one succeeds.</summary>
    public int AutoBackupRetentionDays { get; set; } = 30;

    public string ResolveDataDirectory() => Resolve(DataDirectory);
    public string ResolveBackupDirectory() => Resolve(BackupDirectory);
    public string ResolveRadiologyStorageDirectory() => Resolve(RadiologyStorageDirectory);

    private static string Resolve(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
}
