using System.Security.Cryptography;
using System.Text.Json;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BurhanNexus.HMS.Infrastructure.Licensing;

public interface ILicenseService
{
    /// <summary>Creates the 30-day trial on first installation. Safe to call repeatedly.</summary>
    Task EnsureInitializedAsync(CancellationToken ct = default);
    Task<LicenseStatus> GetStatusAsync(CancellationToken ct = default);
    Task<ServiceResult<LicenseStatus>> ActivateAsync(string activationKey, CancellationToken ct = default);
}

/// <summary>
/// Licence handling. IMPORTANT: licence problems only ever switch the system to read-only mode.
/// Nothing in this class deletes or alters hospital data.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    public const int TrialDays = 30;
    private const string CacheKey = "hms:license";

    private sealed record CachedStatus(LicenseStatus Status, DateTime ComputedUtc);

    private sealed class SealedLicense
    {
        public Guid InstallationId { get; set; }
        public LicenseType Type { get; set; }
        public string? LicensedTo { get; set; }
        public DateTime TrialStartedUtc { get; set; }
        public DateTime? ActivatedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public string? Serial { get; set; }
        public DateTime HighestSeenUtc { get; set; }
    }

    private readonly HmsDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILicenseKeyVerifier _verifier;
    private readonly IAuditService _audit;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;

    public LicenseService(HmsDbContext db, IDataProtectionProvider dp, ILicenseKeyVerifier verifier, IAuditService audit,
        IMemoryCache cache, TimeProvider time)
    {
        _db = db;
        _protector = dp.CreateProtector("BurhanNexus.HMS.License.v1");
        _verifier = verifier;
        _audit = audit;
        _cache = cache;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (await _db.LicenseInformation.AnyAsync(ct)) return;
        var now = Now;
        var sealedState = new SealedLicense
        {
            InstallationId = Guid.NewGuid(),
            Type = LicenseType.Trial,
            TrialStartedUtc = now,
            ExpiresUtc = now.AddDays(TrialDays),
            HighestSeenUtc = now
        };
        var row = new LicenseInformation { Id = LicenseInformation.SingletonId };
        Apply(row, sealedState);
        _db.LicenseInformation.Add(row);
        _audit.Add(AuditActions.LicenseTrialStarted, "License", sealedState.InstallationId.ToString(),
            $"{TrialDays}-day trial started", newValues: new { sealedState.TrialStartedUtc, sealedState.ExpiresUtc });
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }

    public async Task<LicenseStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var now = Now;
        if (_cache.TryGetValue(CacheKey, out CachedStatus? cached) && cached is not null
            && now >= cached.ComputedUtc && now - cached.ComputedUtc < TimeSpan.FromMinutes(1))
            return cached.Status;
        var status = await ComputeAsync(ct);
        _cache.Set(CacheKey, new CachedStatus(status, now), TimeSpan.FromMinutes(5));
        return status;
    }

    private async Task<LicenseStatus> ComputeAsync(CancellationToken ct)
    {
        var row = await _db.LicenseInformation.FirstOrDefaultAsync(ct);
        if (row is null)
            return new LicenseStatus(LicenseState.NotInitialized, null, null, Guid.Empty, null, null, null, null, "Licence not initialised.");

        var state = Unseal(row);
        if (state is null || state.InstallationId != row.InstallationId)
            return new LicenseStatus(LicenseState.Invalid, row.LicenseType, row.LicensedTo, row.InstallationId, row.TrialStartedUtc,
                row.ActivatedUtc, row.ExpiresUtc, null,
                "Licence information could not be verified on this server. Your data is safe; please enter an activation key to continue editing.");

        var now = Now;
        if (now < state.HighestSeenUtc.AddHours(-24))
            return new LicenseStatus(LicenseState.Invalid, state.Type, state.LicensedTo, state.InstallationId, state.TrialStartedUtc,
                state.ActivatedUtc, state.ExpiresUtc, null,
                "The server clock appears to have been set back. Please correct the date and time of the server computer.");

        if (now > state.HighestSeenUtc.AddHours(1))
        {
            state.HighestSeenUtc = now;
            Apply(row, state);
            await _db.SaveChangesAsync(ct);
        }

        int? days = state.ExpiresUtc is { } exp ? Math.Max(0, (int)Math.Ceiling((exp - now).TotalDays)) : null;
        return state.Type switch
        {
            LicenseType.Trial when now < state.ExpiresUtc => new LicenseStatus(LicenseState.TrialActive, state.Type, null, state.InstallationId,
                state.TrialStartedUtc, null, state.ExpiresUtc, days, $"Trial period: {days} day(s) remaining."),
            LicenseType.Trial => new LicenseStatus(LicenseState.TrialExpired, state.Type, null, state.InstallationId,
                state.TrialStartedUtc, null, state.ExpiresUtc, 0,
                "The trial period has ended. All data is kept safely; the system is read-only until a licence is activated."),
            LicenseType.Lifetime => new LicenseStatus(LicenseState.Active, state.Type, state.LicensedTo, state.InstallationId,
                state.TrialStartedUtc, state.ActivatedUtc, null, null, "Lifetime licence active."),
            _ when state.ExpiresUtc is { } e && now < e => new LicenseStatus(LicenseState.Active, state.Type, state.LicensedTo, state.InstallationId,
                state.TrialStartedUtc, state.ActivatedUtc, e, days, $"Annual licence active: {days} day(s) remaining."),
            _ => new LicenseStatus(LicenseState.Expired, state.Type, state.LicensedTo, state.InstallationId,
                state.TrialStartedUtc, state.ActivatedUtc, state.ExpiresUtc, 0,
                "The annual licence has expired. All data is kept safely; the system is read-only until the licence is renewed.")
        };
    }

    public async Task<ServiceResult<LicenseStatus>> ActivateAsync(string activationKey, CancellationToken ct = default)
    {
        var row = await _db.LicenseInformation.FirstOrDefaultAsync(ct);
        if (row is null) return ServiceResult<LicenseStatus>.Fail("Licence is not initialised. Complete the setup first.");

        if (!_verifier.TryVerify(activationKey, out var payload, out var error) || payload is null)
            return await FailActivation(error);

        if (payload.InstallationId != row.InstallationId)
            return await FailActivation("This activation key was issued for a different installation. Please send your Installation ID to Burhan Nexus.");

        var now = Now;
        if (payload.Type == LicenseType.Annual && payload.ExpiresUtc <= now)
            return await FailActivation("This annual activation key has already expired.");

        var current = Unseal(row);
        var state = new SealedLicense
        {
            InstallationId = row.InstallationId,
            Type = payload.Type,
            LicensedTo = payload.LicensedTo,
            TrialStartedUtc = current?.TrialStartedUtc ?? row.TrialStartedUtc,
            ActivatedUtc = now,
            ExpiresUtc = payload.Type == LicenseType.Lifetime ? null : payload.ExpiresUtc,
            Serial = payload.Serial,
            HighestSeenUtc = now
        };
        var old = new { row.LicenseType, row.ExpiresUtc, row.LicensedTo };
        Apply(row, state);
        row.KeyFingerprint = EcdsaLicenseKeyVerifier.Fingerprint(activationKey);
        _audit.Add(AuditActions.LicenseActivated, "License", row.InstallationId.ToString(),
            $"{payload.Type} licence activated for {payload.LicensedTo}", old,
            new { state.Type, state.ExpiresUtc, state.LicensedTo, payload.Serial });
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
        return ServiceResult<LicenseStatus>.Ok(await GetStatusAsync(ct));
    }

    private async Task<ServiceResult<LicenseStatus>> FailActivation(string error)
    {
        await _audit.LogAsync(AuditActions.LicenseActivationFailed, "License", null, error, success: false);
        return ServiceResult<LicenseStatus>.Fail(error);
    }

    private void Apply(LicenseInformation row, SealedLicense s)
    {
        row.InstallationId = s.InstallationId;
        row.LicenseType = s.Type;
        row.LicensedTo = s.LicensedTo;
        row.TrialStartedUtc = s.TrialStartedUtc;
        row.ActivatedUtc = s.ActivatedUtc;
        row.ExpiresUtc = s.ExpiresUtc;
        row.HighestSeenUtc = s.HighestSeenUtc;
        row.UpdatedUtc = Now;
        row.SealedState = _protector.Protect(JsonSerializer.Serialize(s));
    }

    private SealedLicense? Unseal(LicenseInformation row)
    {
        if (string.IsNullOrEmpty(row.SealedState)) return null;
        try
        {
            return JsonSerializer.Deserialize<SealedLicense>(_protector.Unprotect(row.SealedState));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
