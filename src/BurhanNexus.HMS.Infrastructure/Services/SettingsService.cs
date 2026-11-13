using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BurhanNexus.HMS.Infrastructure.Services;

public sealed record SecuritySettings(int SessionTimeoutMinutes, int MaxFailedLoginAttempts, int LockoutMinutes);

/// <summary>Read-only snapshot used by views (no tracking, cached).</summary>
public sealed record HospitalProfile(
    string HospitalName,
    HospitalType HospitalType,
    string Address, string City, string Province, string ContactNumber, string Email, string? Website,
    string? RegistrationNumber,
    bool HasLogo, long LogoVersion,
    string? ReceiptHeader, string? ReceiptFooter, string? ReportHeader, string? ReportFooter,
    string CurrencyCode, string CurrencySymbol, string TimeZoneId, string DateFormat, string TimeFormat, string DefaultLanguage,
    string ReceiptPaperSize, string? ReceiptPrinterName, int ReceiptCopies,
    string ReportPaperSize, string ReportOrientation, string? ReportPrinterName);

public interface ISettingsService
{
    Task<HospitalProfile?> GetProfileAsync(CancellationToken ct = default);
    Task<SecuritySettings> GetSecurityAsync(CancellationToken ct = default);
    Task<ServiceResult> UpdateSecurityAsync(SecuritySettings settings, CancellationToken ct = default);
    void Invalidate();
}

public sealed class SettingsService : ISettingsService
{
    public const string KeySessionTimeout = "Security.SessionTimeoutMinutes";
    public const string KeyMaxFailed = "Security.MaxFailedLoginAttempts";
    public const string KeyLockout = "Security.LockoutMinutes";

    private const string ProfileCacheKey = "hms:profile";
    private const string SecurityCacheKey = "hms:security";

    private readonly HmsDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly TimeProvider _time;

    public SettingsService(HmsDbContext db, IMemoryCache cache, IAuditService audit, IRequestContext ctx, TimeProvider time)
    {
        _db = db;
        _cache = cache;
        _audit = audit;
        _ctx = ctx;
        _time = time;
    }

    public async Task<HospitalProfile?> GetProfileAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(ProfileCacheKey, out HospitalProfile? cached) && cached is not null) return cached;

        var s = await _db.HospitalSettings.AsNoTracking()
            .Select(x => new
            {
                x.HospitalName, x.Address, x.City, x.Province, x.ContactNumber, x.Email, x.Website, x.RegistrationNumber,
                HasLogo = x.LogoContent != null, x.LogoUpdatedUtc,
                x.ReceiptHeader, x.ReceiptFooter, x.ReportHeader, x.ReportFooter
            })
            .FirstOrDefaultAsync(ct);
        var c = await _db.HospitalConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null || c is null) return null;

        var profile = new HospitalProfile(
            s.HospitalName, c.HospitalType, s.Address, s.City, s.Province, s.ContactNumber, s.Email, s.Website, s.RegistrationNumber,
            s.HasLogo, s.LogoUpdatedUtc?.Ticks ?? 0,
            s.ReceiptHeader, s.ReceiptFooter, s.ReportHeader, s.ReportFooter,
            c.CurrencyCode, c.CurrencySymbol, c.TimeZoneId, c.DateFormat, c.TimeFormat, c.DefaultLanguage,
            c.ReceiptPaperSize, c.ReceiptPrinterName, c.ReceiptCopies, c.ReportPaperSize, c.ReportOrientation, c.ReportPrinterName);

        _cache.Set(ProfileCacheKey, profile, TimeSpan.FromMinutes(10));
        return profile;
    }

    public async Task<SecuritySettings> GetSecurityAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(SecurityCacheKey, out SecuritySettings? cached) && cached is not null) return cached;

        var values = await _db.SystemSettings.AsNoTracking()
            .Where(x => x.Key.StartsWith("Security."))
            .ToDictionaryAsync(x => x.Key, x => x.Value, ct);

        var settings = new SecuritySettings(
            ReadInt(values, KeySessionTimeout, 30, 5, 720),
            ReadInt(values, KeyMaxFailed, 5, 3, 20),
            ReadInt(values, KeyLockout, 15, 1, 1440));
        _cache.Set(SecurityCacheKey, settings, TimeSpan.FromMinutes(10));
        return settings;
    }

    public async Task<ServiceResult> UpdateSecurityAsync(SecuritySettings s, CancellationToken ct = default)
    {
        if (s.SessionTimeoutMinutes is < 5 or > 720) return ServiceResult.Fail("Session timeout must be between 5 and 720 minutes.");
        if (s.MaxFailedLoginAttempts is < 3 or > 20) return ServiceResult.Fail("Failed login attempts must be between 3 and 20.");
        if (s.LockoutMinutes is < 1 or > 1440) return ServiceResult.Fail("Lockout duration must be between 1 and 1440 minutes.");

        var old = await GetSecurityAsync(ct);
        await UpsertAsync(KeySessionTimeout, s.SessionTimeoutMinutes.ToString(), "Idle minutes before automatic logout", ct);
        await UpsertAsync(KeyMaxFailed, s.MaxFailedLoginAttempts.ToString(), "Failed logins before temporary lock", ct);
        await UpsertAsync(KeyLockout, s.LockoutMinutes.ToString(), "Minutes an account stays locked", ct);
        _audit.Add(AuditActions.SecuritySettingsChanged, "SystemSettings", "Security", "Security settings changed", old, s);
        await _db.SaveChangesAsync(ct);
        Invalidate();
        return ServiceResult.Ok();
    }

    public void Invalidate()
    {
        _cache.Remove(ProfileCacheKey);
        _cache.Remove(SecurityCacheKey);
    }

    private async Task UpsertAsync(string key, string value, string description, CancellationToken ct)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (row is null)
        {
            row = new SystemSetting { Key = key, Description = description };
            _db.SystemSettings.Add(row);
        }
        row.Value = value;
        row.UpdatedUtc = _time.GetUtcNow().UtcDateTime;
        row.UpdatedByUserId = _ctx.UserId;
    }

    private static int ReadInt(Dictionary<string, string> values, string key, int fallback, int min, int max) =>
        values.TryGetValue(key, out var v) && int.TryParse(v, out var n) && n >= min && n <= max ? n : fallback;
}
