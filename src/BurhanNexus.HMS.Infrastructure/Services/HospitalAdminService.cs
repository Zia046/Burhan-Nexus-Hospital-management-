using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Services;

public sealed class HospitalInfoInput
{
    public string HospitalName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? ReceiptHeader { get; set; }
    public string? ReceiptFooter { get; set; }
    public string? ReportHeader { get; set; }
    public string? ReportFooter { get; set; }
}

public sealed class PreferencesInput
{
    public string CurrencyCode { get; set; } = "PKR";
    public string CurrencySymbol { get; set; } = "Rs.";
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string TimeFormat { get; set; } = "hh:mm tt";
    public string DefaultLanguage { get; set; } = "en";
}

public sealed class PrintInput
{
    public string? ReceiptPrinterName { get; set; }
    public string ReceiptPaperSize { get; set; } = "80mm";
    public int ReceiptCopies { get; set; } = 1;
    public string? ReportPrinterName { get; set; }
    public string ReportPaperSize { get; set; } = "A4";
    public string ReportOrientation { get; set; } = "Portrait";
}

public sealed class OperationalRulesInput
{
    public bool PharmacyPricingEnabled { get; set; }
    public bool PharmacyRetailSalesEnabled { get; set; }
    public bool PharmacyProfitTrackingEnabled { get; set; }
    public bool GovernmentStockIssueEnabled { get; set; }
    public bool PatientChargesEnabled { get; set; }
}

public interface IHospitalAdminService
{
    Task<HospitalSettings> GetSettingsAsync(CancellationToken ct = default);
    Task<HospitalConfiguration> GetConfigurationAsync(CancellationToken ct = default);
    Task<(byte[] Content, string ContentType, DateTime? Updated)?> GetLogoAsync(CancellationToken ct = default);
    Task<ServiceResult> UpdateInfoAsync(HospitalInfoInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> UpdateLogoAsync(byte[]? content, CancellationToken ct = default);
    Task<ServiceResult> UpdatePreferencesAsync(PreferencesInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> UpdatePrintingAsync(PrintInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> ChangeHospitalTypeAsync(HospitalType type, bool applyDefaults, CancellationToken ct = default);
    Task<ServiceResult> UpdateOperationalRulesAsync(OperationalRulesInput input, uint rowVersion, CancellationToken ct = default);
}

public sealed class HospitalAdminService : IHospitalAdminService
{
    public const int MaxLogoBytes = 2 * 1024 * 1024;

    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly ISettingsService _settings;
    private readonly TimeProvider _time;

    public HospitalAdminService(HmsDbContext db, IAuditService audit, ISettingsService settings, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<HospitalSettings> GetSettingsAsync(CancellationToken ct = default) =>
        await _db.HospitalSettings.AsNoTracking().FirstAsync(ct);

    public async Task<HospitalConfiguration> GetConfigurationAsync(CancellationToken ct = default) =>
        await _db.HospitalConfigurations.AsNoTracking().FirstAsync(ct);

    public async Task<(byte[] Content, string ContentType, DateTime? Updated)?> GetLogoAsync(CancellationToken ct = default)
    {
        var logo = await _db.HospitalSettings.AsNoTracking()
            .Select(s => new { s.LogoContent, s.LogoContentType, s.LogoUpdatedUtc }).FirstOrDefaultAsync(ct);
        if (logo?.LogoContent is not { Length: > 0 } bytes) return null;
        return (bytes, logo.LogoContentType ?? "image/png", logo.LogoUpdatedUtc);
    }

    public async Task<ServiceResult> UpdateInfoAsync(HospitalInfoInput i, uint rowVersion, CancellationToken ct = default)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(i.HospitalName)) e.Add("Hospital name is required.");
        if (string.IsNullOrWhiteSpace(i.Address)) e.Add("Address is required.");
        if (string.IsNullOrWhiteSpace(i.City)) e.Add("City is required.");
        if (string.IsNullOrWhiteSpace(i.Province)) e.Add("Province is required.");
        if (string.IsNullOrWhiteSpace(i.ContactNumber) || !Validation.IsValidPhone(i.ContactNumber)) e.Add("A valid contact number is required.");
        if (string.IsNullOrWhiteSpace(i.Email) || !Validation.IsValidEmail(i.Email)) e.Add("A valid email address is required.");
        if (!string.IsNullOrWhiteSpace(i.Website) && !Validation.IsValidUrl(i.Website)) e.Add("Website address is not valid.");
        if ((i.HospitalName?.Length ?? 0) > 200 || (i.Address?.Length ?? 0) > 500) e.Add("Hospital name or address is too long.");
        if ((i.ReceiptHeader?.Length ?? 0) > 500 || (i.ReceiptFooter?.Length ?? 0) > 500) e.Add("Receipt header/footer must not exceed 500 characters.");
        if ((i.ReportHeader?.Length ?? 0) > 1000 || (i.ReportFooter?.Length ?? 0) > 1000) e.Add("Report header/footer must not exceed 1000 characters.");
        if (e.Count > 0) return ServiceResult.Fail(e.ToArray());

        var s = await _db.HospitalSettings.FirstAsync(ct);
        var old = Snapshot(s);
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = rowVersion;
        s.HospitalName = i.HospitalName!.Trim();
        s.RegistrationNumber = Clean(i.RegistrationNumber);
        s.Address = i.Address!.Trim();
        s.City = i.City!.Trim();
        s.Province = i.Province!.Trim();
        s.ContactNumber = i.ContactNumber!.Trim();
        s.Email = i.Email!.Trim();
        s.Website = Clean(i.Website);
        s.ReceiptHeader = Clean(i.ReceiptHeader);
        s.ReceiptFooter = Clean(i.ReceiptFooter);
        s.ReportHeader = Clean(i.ReportHeader);
        s.ReportFooter = Clean(i.ReportFooter);
        s.UpdatedUtc = Now;
        _audit.Add(AuditActions.HospitalSettingsChanged, "HospitalSettings", "1", "Hospital information updated", old, Snapshot(s));
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> UpdateLogoAsync(byte[]? content, CancellationToken ct = default)
    {
        string? type = null;
        if (content is { Length: > 0 })
        {
            if (content.Length > MaxLogoBytes) return ServiceResult.Fail("The logo must be smaller than 2 MB.");
            type = Validation.DetectImageType(content);
            if (type is null) return ServiceResult.Fail("The logo must be a PNG, JPEG, GIF or WebP image.");
        }
        var s = await _db.HospitalSettings.FirstAsync(ct);
        var hadLogo = s.LogoContent is { Length: > 0 };
        s.LogoContent = content is { Length: > 0 } ? content : null;
        s.LogoContentType = type;
        s.LogoUpdatedUtc = Now;
        s.UpdatedUtc = Now;
        _audit.Add(AuditActions.LogoChanged, "HospitalSettings", "1", s.LogoContent is null ? "Hospital logo removed" : "Hospital logo changed",
            new { HadLogo = hadLogo }, new { HasLogo = s.LogoContent is not null, Bytes = content?.Length ?? 0, ContentType = type });
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> UpdatePreferencesAsync(PreferencesInput i, uint rowVersion, CancellationToken ct = default)
    {
        var e = new List<string>();
        if (!Options.Currencies.Contains(i.CurrencyCode)) e.Add("Unsupported currency.");
        if (string.IsNullOrWhiteSpace(i.CurrencySymbol) || i.CurrencySymbol.Length > 10) e.Add("Currency symbol is required (max 10 characters).");
        if (!Options.TimeZones.Contains(i.TimeZoneId)) e.Add("Unsupported time zone.");
        if (!Options.DateFormats.Contains(i.DateFormat)) e.Add("Unsupported date format.");
        if (!Options.TimeFormats.Contains(i.TimeFormat)) e.Add("Unsupported time format.");
        if (!Options.Languages.ContainsKey(i.DefaultLanguage)) e.Add("Unsupported language.");
        if (e.Count > 0) return ServiceResult.Fail(e.ToArray());

        var c = await _db.HospitalConfigurations.FirstAsync(ct);
        var old = new { c.CurrencyCode, c.CurrencySymbol, c.TimeZoneId, c.DateFormat, c.TimeFormat, c.DefaultLanguage };
        _db.Entry(c).Property(x => x.RowVersion).OriginalValue = rowVersion;
        c.CurrencyCode = i.CurrencyCode;
        c.CurrencySymbol = i.CurrencySymbol.Trim();
        c.TimeZoneId = i.TimeZoneId;
        c.DateFormat = i.DateFormat;
        c.TimeFormat = i.TimeFormat;
        c.DefaultLanguage = i.DefaultLanguage;
        c.UpdatedUtc = Now;
        _audit.Add(AuditActions.PreferencesChanged, "HospitalConfiguration", "1", "System preferences updated", old,
            new { c.CurrencyCode, c.CurrencySymbol, c.TimeZoneId, c.DateFormat, c.TimeFormat, c.DefaultLanguage });
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> UpdatePrintingAsync(PrintInput i, uint rowVersion, CancellationToken ct = default)
    {
        var e = new List<string>();
        if (!Options.ReceiptPaperSizes.Contains(i.ReceiptPaperSize)) e.Add("Unsupported receipt paper size.");
        if (!Options.ReportPaperSizes.Contains(i.ReportPaperSize)) e.Add("Unsupported report paper size.");
        if (!Options.Orientations.Contains(i.ReportOrientation)) e.Add("Unsupported orientation.");
        if (i.ReceiptCopies is < 1 or > 5) e.Add("Receipt copies must be between 1 and 5.");
        if ((i.ReceiptPrinterName?.Length ?? 0) > 200 || (i.ReportPrinterName?.Length ?? 0) > 200) e.Add("Printer name is too long.");
        if (e.Count > 0) return ServiceResult.Fail(e.ToArray());

        var c = await _db.HospitalConfigurations.FirstAsync(ct);
        var old = new { c.ReceiptPrinterName, c.ReceiptPaperSize, c.ReceiptCopies, c.ReportPrinterName, c.ReportPaperSize, c.ReportOrientation };
        _db.Entry(c).Property(x => x.RowVersion).OriginalValue = rowVersion;
        c.ReceiptPrinterName = Clean(i.ReceiptPrinterName);
        c.ReceiptPaperSize = i.ReceiptPaperSize;
        c.ReceiptCopies = i.ReceiptCopies;
        c.ReportPrinterName = Clean(i.ReportPrinterName);
        c.ReportPaperSize = i.ReportPaperSize;
        c.ReportOrientation = i.ReportOrientation;
        c.UpdatedUtc = Now;
        _audit.Add(AuditActions.PrintSettingsChanged, "HospitalConfiguration", "1", "Printer settings updated", old,
            new { c.ReceiptPrinterName, c.ReceiptPaperSize, c.ReceiptCopies, c.ReportPrinterName, c.ReportPaperSize, c.ReportOrientation });
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> ChangeHospitalTypeAsync(HospitalType type, bool applyDefaults, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(type)) return ServiceResult.Fail("Unknown hospital type.");
        var c = await _db.HospitalConfigurations.FirstAsync(ct);
        if (c.HospitalType == type) return ServiceResult.Fail($"The hospital is already configured as {type}.");
        var old = RulesSnapshot(c);
        c.HospitalType = type;
        if (applyDefaults) c.ApplyHospitalTypeDefaults();
        c.UpdatedUtc = Now;
        _audit.Add(AuditActions.HospitalTypeChanged, "HospitalConfiguration", "1",
            $"Hospital type changed to {type}" + (applyDefaults ? " (default rules applied)" : " (existing rules kept)"), old, RulesSnapshot(c));
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> UpdateOperationalRulesAsync(OperationalRulesInput i, uint rowVersion, CancellationToken ct = default)
    {
        var c = await _db.HospitalConfigurations.FirstAsync(ct);
        var old = RulesSnapshot(c);
        _db.Entry(c).Property(x => x.RowVersion).OriginalValue = rowVersion;
        c.PharmacyPricingEnabled = i.PharmacyPricingEnabled;
        c.PharmacyRetailSalesEnabled = i.PharmacyRetailSalesEnabled && i.PharmacyPricingEnabled;
        c.PharmacyProfitTrackingEnabled = i.PharmacyProfitTrackingEnabled && i.PharmacyPricingEnabled;
        c.GovernmentStockIssueEnabled = i.GovernmentStockIssueEnabled;
        c.PatientChargesEnabled = i.PatientChargesEnabled;
        c.UpdatedUtc = Now;
        _audit.Add(AuditActions.OperationalRulesChanged, "HospitalConfiguration", "1", "Operational rules updated", old, RulesSnapshot(c));
        return await SaveAsync(ct);
    }

    private static object Snapshot(HospitalSettings s) => new
    {
        s.HospitalName, s.RegistrationNumber, s.Address, s.City, s.Province, s.ContactNumber, s.Email, s.Website,
        s.ReceiptHeader, s.ReceiptFooter, s.ReportHeader, s.ReportFooter
    };

    private static object RulesSnapshot(HospitalConfiguration c) => new
    {
        HospitalType = c.HospitalType.ToString(), c.PharmacyPricingEnabled, c.PharmacyRetailSalesEnabled,
        c.PharmacyProfitTrackingEnabled, c.GovernmentStockIssueEnabled, c.PatientChargesEnabled
    };

    private async Task<ServiceResult> SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            _settings.Invalidate();
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("These settings were changed on another computer. Please reload the page and try again.");
        }
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
