using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

public class LoginHistory
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    [MaxLength(64)] public string UsernameAttempted { get; set; } = string.Empty;
    public DateTime AttemptedUtc { get; set; }
    public bool Success { get; set; }
    [MaxLength(200)] public string? FailureReason { get; set; }
    [MaxLength(64)] public string? IpAddress { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
    [MaxLength(500)] public string? UserAgent { get; set; }
    /// <summary>Server-side session identifier (only for successful logins).</summary>
    public Guid? SessionId { get; set; }
    public DateTime? LastActivityUtc { get; set; }
    public DateTime? LogoutUtc { get; set; }
    [MaxLength(50)] public string? LogoutReason { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
    [MaxLength(100)] public string Action { get; set; } = string.Empty;
    [MaxLength(100)] public string? EntityType { get; set; }
    [MaxLength(100)] public string? EntityId { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    [MaxLength(64)] public string? IpAddress { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
    [MaxLength(500)] public string? UserAgent { get; set; }
    public bool Success { get; set; } = true;
}

public class SystemSetting
{
    [MaxLength(100)] public string Key { get; set; } = string.Empty;
    [MaxLength(2000)] public string Value { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public class BackupHistory
{
    public int Id { get; set; }
    [MaxLength(260)] public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    [MaxLength(64)] public string? Sha256 { get; set; }
    public BackupStatus Status { get; set; }
    public BackupKind Kind { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    [MaxLength(2000)] public string? ErrorMessage { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public DateTime? LastRestoredUtc { get; set; }
}

/// <summary>
/// Licence record (single row). The human-readable columns are for display only;
/// validation relies on <see cref="SealedState"/>, which is encrypted and authenticated
/// with server-side keys that are not stored in the database.
/// </summary>
public class LicenseInformation
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public Guid InstallationId { get; set; }
    public LicenseType LicenseType { get; set; }
    [MaxLength(200)] public string? LicensedTo { get; set; }
    public DateTime TrialStartedUtc { get; set; }
    public DateTime? ActivatedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    [MaxLength(64)] public string? KeyFingerprint { get; set; }
    public string SealedState { get; set; } = string.Empty;
    public DateTime HighestSeenUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
