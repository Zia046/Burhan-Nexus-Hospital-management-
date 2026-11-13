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
    /// <summary>Unique key serial for tracking.</summary>
    public string Serial { get; set; } = string.Empty;
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
    string Message)
{
    /// <summary>Data can be added/changed only while this is true. Data is never deleted either way.</summary>
    public bool AllowsWrite => State is LicenseState.TrialActive or LicenseState.Active;

    public string DisplayName => State switch
    {
        LicenseState.TrialActive => "Trial (active)",
        LicenseState.TrialExpired => "Trial expired",
        LicenseState.Active => Type == LicenseType.Lifetime ? "Lifetime licence" : "Annual licence",
        LicenseState.Expired => "Licence expired",
        LicenseState.Invalid => "Licence verification required",
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
