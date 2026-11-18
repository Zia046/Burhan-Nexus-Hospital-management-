using BurhanNexus.HMS.Core.Domain;

namespace BurhanNexus.HMS.Core.Licensing;

/// <summary>Content of a Burhan Nexus activation key (signed by the vendor's private key).</summary>
public sealed class LicenseKeyPayload
{
    /// <summary>Format version.</summary>
    public int V { get; set; } = 1;
    /// <summary>Product code – must be "BNHMS".</summary>
    public string Product { get; set; } = "BNHMS";
    public LicenseType Type { get; set; }
    /// <summary>Installation the key is bound to.</summary>
    public Guid InstallationId { get; set; }
    public string LicensedTo { get; set; } = string.Empty;
    public DateTime IssuedUtc { get; set; }
    /// <summary>Required for Annual licences, null for Lifetime.</summary>
    public DateTime? ExpiresUtc { get; set; }
    /// <summary>Unique key serial (licence ID) for tracking.</summary>
    public string Serial { get; set; } = string.Empty;
    /// <summary>Update 13: "Yearly", "Custom" or "Lifetime" – how the expiry was chosen (display only; older keys have none).</summary>
    public string? Term { get; set; }
    /// <summary>Update 13: optional machine binding – when present the key works only on the computer with this Machine ID.</summary>
    public string? MachineId { get; set; }
}

public enum LicenseState
{
    TrialActive,
    TrialExpired,
    Active,
    Expired,
    Invalid,
    NotInitialized
}

public sealed record LicenseStatus(
    LicenseState State,
    LicenseType? Type,
    string? LicensedTo,
    Guid InstallationId,
    DateTime? StartedUtc,
    DateTime? ActivatedUtc,
    DateTime? ExpiresUtc,
    int? DaysRemaining,
    string Message,
    string? LicenseId = null,
    string? Term = null,
    bool Deactivated = false)
{
    /// <summary>Update 13: days before expiry when every page starts warning.</summary>
    public const int WarnDays = 30;

    /// <summary>An active time-limited licence close to its expiry date.</summary>
    public bool ExpiresSoon => State == LicenseState.Active && ExpiresUtc is not null && DaysRemaining is int d && d <= WarnDays;

    /// <summary>Data can be added/changed only while this is true. Data is never deleted either way.</summary>
    public bool AllowsWrite => State is LicenseState.TrialActive or LicenseState.Active;

    public string DisplayName => State switch
    {
        LicenseState.TrialActive => "Trial (active)",
        LicenseState.TrialExpired => "Trial expired",
        LicenseState.Active => Type == LicenseType.Lifetime ? "Lifetime licence" : Term == "Custom" ? "Licence (custom period)" : "Yearly licence",
        LicenseState.Expired => "Licence expired",
        LicenseState.Invalid => Deactivated ? "Licence deactivated" : "Licence verification required",
        _ => "Not initialised"
    };
}

/// <summary>
/// Verifies activation keys. The default implementation checks a vendor ECDSA signature
/// with an embedded public key; it can be replaced by an online activation service later.
/// </summary>
public interface ILicenseKeyVerifier
{
    bool TryVerify(string activationKey, out LicenseKeyPayload? payload, out string error);
}
