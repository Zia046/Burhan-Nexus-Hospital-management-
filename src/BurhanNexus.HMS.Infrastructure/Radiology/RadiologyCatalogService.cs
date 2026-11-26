using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Radiology;

public sealed class RadiologyServiceInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public RadiologyModality Modality { get; set; } = RadiologyModality.XRay;
    public string? BodyPart { get; set; }
    public string? Description { get; set; }
    public string? PreparationInstructions { get; set; }
    public int? DurationMinutes { get; set; }
    public string? ReportTitle { get; set; }
    public int? DefaultTemplateId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    // Fee (Phase 3 service). Only applied when the user may manage radiology fees.
    public bool IsFree { get; set; }
    public decimal Fee { get; set; }
    public DateOnly? FeeEffectiveFrom { get; set; }
    public bool AllowPartialPayment { get; set; }
}

public sealed class RadiologyTemplateInput
{
    public string? Name { get; set; }
    public RadiologyModality Modality { get; set; } = RadiologyModality.XRay;
    public string? Technique { get; set; }
    public string? Findings { get; set; }
    public string? Impression { get; set; }
    public string? Recommendations { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class RadiologySettingsInput
{
    public string? DepartmentName { get; set; }
    public string? HospitalName { get; set; }
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? ReportHeader { get; set; }
    public string? ReportFooter { get; set; }
    public bool PrintHeader { get; set; } = true;
    public int LetterheadTopMarginMm { get; set; } = 45;
    public string? DefaultReportingDoctor { get; set; }
    public string? DefaultDesignation { get; set; }
    public bool ShowVerifiedBy { get; set; } = true;
    public bool AllowSelfVerification { get; set; }
    public bool RequirePaymentBeforeRelease { get; set; }
    public bool AllowBookingWithoutVisit { get; set; } = true;
    public int VisitValidDays { get; set; } = 7;
    public int MaxUploadMb { get; set; } = 25;
}

public sealed record RadiologyServiceRow(RadiologyService Service, decimal Fee, bool AllowPartial);

public interface IRadiologyCatalogService
{
    Task<bool> IsPrivateAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RadiologyServiceRow>> ServicesAsync(bool activeOnly, RadiologyModality? modality = null, CancellationToken ct = default);
    Task<RadiologyServiceRow?> ServiceAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveServiceAsync(int? id, RadiologyServiceInput input, bool canManageFees, CancellationToken ct = default);

    Task<IReadOnlyList<RadiologyTemplate>> TemplatesAsync(bool activeOnly, RadiologyModality? modality = null, CancellationToken ct = default);
    Task<RadiologyTemplate?> TemplateAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveTemplateAsync(int? id, RadiologyTemplateInput input, CancellationToken ct = default);

    Task<RadiologySettings> SettingsAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveSettingsAsync(RadiologySettingsInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> SetImageAsync(string kind, byte[]? data, string? contentType, CancellationToken ct = default);
}

public sealed class RadiologyCatalogService : IRadiologyCatalogService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _finance;
    private readonly IRequestContext _ctx;

    public RadiologyCatalogService(HmsDbContext db, IAuditService audit, HospitalClock clock, IFinanceSetupService finance, IRequestContext ctx)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _finance = finance;
        _ctx = ctx;
    }

    internal static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    public static string Label(RadiologyModality m) => m switch { RadiologyModality.XRay => "X-Ray", RadiologyModality.Ecg => "ECG", _ => "Ultrasound" };
    /// <summary>Token prefix: X-12, U-3, E-5.</summary>
    public static string Short(RadiologyModality m) => m switch { RadiologyModality.XRay => "X", RadiologyModality.Ecg => "E", _ => "U" };
    public static string Icon(RadiologyModality m) => m switch { RadiologyModality.XRay => "scan", RadiologyModality.Ecg => "activity", _ => "wave" };
    public static ServiceKind KindOf(RadiologyModality m) => m switch { RadiologyModality.XRay => ServiceKind.XRay, RadiologyModality.Ecg => ServiceKind.Ecg, _ => ServiceKind.Ultrasound };

    public async Task<bool> IsPrivateAsync(CancellationToken ct = default) =>
        await _db.HospitalConfigurations.AsNoTracking().Select(c => c.HospitalType).FirstOrDefaultAsync(ct) == HospitalType.Private;

    // =====================================================================================
    // Services
    // =====================================================================================

    public async Task<IReadOnlyList<RadiologyServiceRow>> ServicesAsync(bool activeOnly, RadiologyModality? modality = null, CancellationToken ct = default)
    {
        var list = await _db.RadiologyServices.AsNoTracking().Include(s => s.ServiceItem).Include(s => s.DefaultTemplate)
            .Where(s => (!activeOnly || s.IsActive && (s.Modality != RadiologyModality.Ecg || s.ServiceItem!.IsActive)) && (modality == null || s.Modality == modality))
            .OrderBy(s => s.Modality).ThenBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(ct);
        var today = await _clock.TodayAsync();
        // Modality is stored as text: order X-Ray before Ultrasound in memory, not alphabetically.
        list = list.OrderBy(s => (int)s.Modality).ThenBy(s => s.SortOrder).ThenBy(s => s.Name).ToList();
        var rows = new List<RadiologyServiceRow>();
        foreach (var s in list) rows.Add(new RadiologyServiceRow(s, await _finance.FeeOnAsync(s.ServiceItemId, today, ct), s.ServiceItem!.AllowPartialPayment));
        return rows;
    }

    public async Task<RadiologyServiceRow?> ServiceAsync(int id, CancellationToken ct = default)
    {
        var s = await _db.RadiologyServices.AsNoTracking().Include(x => x.ServiceItem).Include(x => x.DefaultTemplate).FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : new RadiologyServiceRow(s, await _finance.FeeOnAsync(s.ServiceItemId, await _clock.TodayAsync(), ct), s.ServiceItem!.AllowPartialPayment);
    }

    private async Task<int?> DepartmentAsync(RadiologyModality m, CancellationToken ct)
    {
        var name = m == RadiologyModality.Ecg ? "Radiology" : Label(m);
        return await _db.Departments.Where(d => d.Name == name).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct)
               ?? await _db.Departments.Where(d => d.Name == "Radiology").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<ServiceResult<int>> SaveServiceAsync(int? id, RadiologyServiceInput i, bool canManageFees, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code, 24)?.ToUpperInvariant().Replace(' ', '-');
        var name = T(i.Name, 150);
        if (code is null) errors.Add("Service code is required.");
        if (name is null) errors.Add("Service name is required.");
        if (!Enum.IsDefined(i.Modality)) errors.Add("Select X-Ray, Ultrasound or ECG.");
        if (code is not null && await _db.RadiologyServices.AnyAsync(s => s.Id != id && s.Code == code, ct)) errors.Add("Another radiology service already uses this code.");
        if (i.DurationMinutes is < 0 or > 600) errors.Add("Estimated duration must be between 0 and 600 minutes.");
        if (i.DefaultTemplateId is int tid && !await _db.RadiologyTemplates.AnyAsync(t => t.Id == tid && t.Modality == i.Modality, ct))
            errors.Add("The selected template does not belong to this modality.");
        if (id is null && !canManageFees) { i.IsFree = false; i.Fee = 0; } // Update 13: "fee not set" – set later by a user with "Manage Radiology Fees"
        if (canManageFees && !i.IsFree && i.Fee <= 0) errors.Add("Enter the fee, or mark the service as free.");
        if (canManageFees && !FinanceMath.IsMoney(i.Fee)) errors.Add("The fee may have at most 2 decimals.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());

        var now = _clock.UtcNow;
        var kind = KindOf(i.Modality);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        RadiologyService s;
        object? old = null;
        if (id is null)
        {
            var svc = await _finance.SaveServiceAsync(null, new ServiceInput
            {
                Code = "RAD-" + code, Name = name, Kind = kind, DepartmentId = await DepartmentAsync(i.Modality, ct), IsFree = i.IsFree, Fee = i.IsFree ? 0 : i.Fee, FeePending = !canManageFees,
                AllowPartialPayment = i.AllowPartialPayment, IsActive = i.IsActive, Description = $"{Label(i.Modality)} examination"
            }, ct);
            if (!svc.Succeeded) return ServiceResult<int>.Fail(svc.Errors.ToArray());
            s = new RadiologyService { CreatedUtc = now, ServiceItemId = svc.Value };
            _db.RadiologyServices.Add(s);
        }
        else
        {
            s = await _db.RadiologyServices.Include(x => x.ServiceItem).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
            if (s.Modality != i.Modality && await _db.RadiologyExams.AnyAsync(e => e.ServiceId == s.Id, ct))
                return ServiceResult<int>.Fail("The modality of a service that was already booked cannot be changed. Create a new service instead.");
            old = new { s.Code, s.Name, s.Modality, s.BodyPart, s.IsActive, s.DefaultTemplateId };
            var item = s.ServiceItem!;
            var currentFee = await _finance.FeeOnAsync(item.Id, await _clock.TodayAsync(), ct);
            var svc = await _finance.SaveServiceAsync(item.Id, new ServiceInput
            {
                Code = item.Code, Name = name, Kind = kind, DepartmentId = item.DepartmentId ?? await DepartmentAsync(i.Modality, ct),
                IsFree = canManageFees ? i.IsFree : item.IsFree, Fee = canManageFees ? (i.IsFree ? 0 : i.Fee) : currentFee, FeePending = !canManageFees,
                FeeEffectiveFrom = canManageFees ? i.FeeEffectiveFrom : null, FeeNote = "Radiology examination fee",
                AllowPartialPayment = canManageFees ? i.AllowPartialPayment : item.AllowPartialPayment, IsActive = i.IsActive,
                Description = item.Description, CounterIds = await _db.ServiceCounters.Where(c => c.ServiceItemId == item.Id).Select(c => c.CounterId).ToListAsync(ct)
            }, ct);
            if (!svc.Succeeded) return ServiceResult<int>.Fail(svc.Errors.ToArray());
        }
        s.Code = code!; s.Name = name!; s.Modality = i.Modality; s.BodyPart = T(i.BodyPart, 100); s.Description = T(i.Description, 500);
        s.PreparationInstructions = T(i.PreparationInstructions, 1000); s.DurationMinutes = i.DurationMinutes; s.ReportTitle = T(i.ReportTitle, 150);
        s.DefaultTemplateId = i.DefaultTemplateId; s.IsActive = i.IsActive; s.SortOrder = i.SortOrder; s.UpdatedUtc = now;
        await _db.SaveChangesAsync(ct);
        _audit.Add(AuditActions.RadServiceSaved, "RadiologyService", s.Code, $"{Label(s.Modality)} service {s.Name} {(id is null ? "created" : "updated")}",
            old, new { s.Code, s.Name, s.Modality, s.BodyPart, s.IsActive, s.DefaultTemplateId });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(s.Id);
    }

    // =====================================================================================
    // Templates
    // =====================================================================================

    public async Task<IReadOnlyList<RadiologyTemplate>> TemplatesAsync(bool activeOnly, RadiologyModality? modality = null, CancellationToken ct = default) =>
        await _db.RadiologyTemplates.AsNoTracking().Where(t => (!activeOnly || t.IsActive) && (modality == null || t.Modality == modality))
            .OrderBy(t => t.Modality).ThenBy(t => t.Name).ToListAsync(ct);

    public async Task<RadiologyTemplate?> TemplateAsync(int id, CancellationToken ct = default) =>
        await _db.RadiologyTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<ServiceResult<int>> SaveTemplateAsync(int? id, RadiologyTemplateInput i, CancellationToken ct = default)
    {
        var name = T(i.Name, 120);
        if (name is null) return ServiceResult<int>.Fail("Template name is required.");
        if (!Enum.IsDefined(i.Modality)) return ServiceResult<int>.Fail("Select X-Ray or Ultrasound.");
        if (T(i.Findings, 8000) is null && T(i.Impression, 2000) is null) return ServiceResult<int>.Fail("Enter the findings and / or impression text of the template.");
        if (await _db.RadiologyTemplates.AnyAsync(t => t.Id != id && t.Modality == i.Modality && t.Name == name, ct)) return ServiceResult<int>.Fail("A template with this name already exists.");
        var t = id is null ? _db.RadiologyTemplates.Add(new RadiologyTemplate()).Entity : await _db.RadiologyTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return ServiceResult<int>.Fail("Template not found.");
        if (id is not null && t.Modality != i.Modality && await _db.RadiologyServices.AnyAsync(s => s.DefaultTemplateId == t.Id, ct))
            return ServiceResult<int>.Fail("This template is the default of a service of the other modality. Change the service first.");
        t.Name = name; t.Modality = i.Modality; t.Technique = T(i.Technique, 2000); t.Findings = T(i.Findings, 8000); t.Impression = T(i.Impression, 2000);
        t.Recommendations = T(i.Recommendations, 2000); t.IsActive = i.IsActive; t.UpdatedUtc = _clock.UtcNow; t.UpdatedByUsername = _ctx.Username;
        _audit.Add(AuditActions.RadTemplateSaved, "RadiologyTemplate", name, $"{Label(t.Modality)} report template {name} {(id is null ? "created" : "updated")} (existing reports are not changed)");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(t.Id);
    }

    // =====================================================================================
    // Settings
    // =====================================================================================

    public async Task<RadiologySettings> SettingsAsync(CancellationToken ct = default) =>
        await _db.RadiologySettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new RadiologySettings();

    public async Task<ServiceResult> SaveSettingsAsync(RadiologySettingsInput i, uint rowVersion, CancellationToken ct = default)
    {
        var errors = new List<string>();
        if (T(i.Email, 150) is { } mail && !mail.Contains('@')) errors.Add("Email address is not valid.");
        if (i.LetterheadTopMarginMm is < 0 or > 120) errors.Add("Letterhead space must be between 0 and 120 mm.");
        if (i.VisitValidDays is < 1 or > 90) errors.Add("Visit validity must be between 1 and 90 days.");
        if (i.MaxUploadMb is < 1 or > 200) errors.Add("Maximum upload size must be between 1 and 200 MB.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var s = await _db.RadiologySettings.FirstAsync(ct);
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var old = new { s.DepartmentName, s.PrintHeader, s.AllowSelfVerification, s.RequirePaymentBeforeRelease, s.AllowBookingWithoutVisit, s.VisitValidDays, s.MaxUploadMb };
        s.DepartmentName = T(i.DepartmentName, 150); s.HospitalName = T(i.HospitalName, 200); s.Address = T(i.Address, 300); s.ContactNumber = T(i.ContactNumber, 100);
        s.Email = T(i.Email, 150); s.ReportHeader = T(i.ReportHeader, 300); s.ReportFooter = T(i.ReportFooter, 500); s.PrintHeader = i.PrintHeader;
        s.LetterheadTopMarginMm = i.LetterheadTopMarginMm; s.DefaultReportingDoctor = T(i.DefaultReportingDoctor, 150); s.DefaultDesignation = T(i.DefaultDesignation, 150);
        s.ShowVerifiedBy = i.ShowVerifiedBy; s.AllowSelfVerification = i.AllowSelfVerification; s.RequirePaymentBeforeRelease = i.RequirePaymentBeforeRelease;
        s.AllowBookingWithoutVisit = i.AllowBookingWithoutVisit; s.VisitValidDays = i.VisitValidDays; s.MaxUploadMb = i.MaxUploadMb; s.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.RadSettingsChanged, "RadiologySettings", "1", "Radiology settings changed", old,
            new { s.DepartmentName, s.PrintHeader, s.AllowSelfVerification, s.RequirePaymentBeforeRelease, s.AllowBookingWithoutVisit, s.VisitValidDays, s.MaxUploadMb });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("Settings were changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetImageAsync(string kind, byte[]? data, string? contentType, CancellationToken ct = default)
    {
        if (data is { Length: > 1_000_000 }) return ServiceResult.Fail("Image is too large (max 1 MB).");
        if (data is not null && contentType is not ("image/png" or "image/jpeg")) return ServiceResult.Fail("Use a PNG or JPEG image.");
        if (data is not null && RadiologyFileStore.Sniff(data) is not ("image/png" or "image/jpeg")) return ServiceResult.Fail("The file is not a valid PNG or JPEG image.");
        var s = await _db.RadiologySettings.FirstAsync(ct);
        if (kind == "signature") { s.Signature = data; s.SignatureContentType = data is null ? null : contentType; }
        else { s.Logo = data; s.LogoContentType = data is null ? null : contentType; }
        s.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.RadSettingsChanged, "RadiologySettings", "1", $"Radiology report {kind} {(data is null ? "removed" : "uploaded")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Seed: settings, report templates and a starter catalogue (inactive, free) the hospital reviews, prices and activates.
    // =====================================================================================

    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        if (!await db.RadiologySettings.AnyAsync(ct))
        {
            var isGovernment = await db.HospitalConfigurations.AnyAsync(c => c.HospitalType == HospitalType.Government, ct);
            db.RadiologySettings.Add(new RadiologySettings { Id = 1, UpdatedUtc = now, AllowBookingWithoutVisit = !isGovernment });
        }
        if (!await db.RadiologyTemplates.AnyAsync(ct))
        {
            foreach (var t in StarterTemplates()) { t.UpdatedUtc = now; t.UpdatedByUsername = "SYSTEM"; db.RadiologyTemplates.Add(t); }
        }
        await db.SaveChangesAsync(ct);
        if (!await db.RadiologyServices.AnyAsync(s => s.Modality != RadiologyModality.Ecg, ct)) await SeedStarterServicesAsync(db, now, ct);
        await EnsureEcgAsync(db, now, ct);
    }

    private static async Task SeedStarterServicesAsync(HmsDbContext db, DateTime now, CancellationToken ct)
    {

        var templates = await db.RadiologyTemplates.ToDictionaryAsync(t => t.Name, t => t.Id, ct);
        var xray = await db.Departments.Where(d => d.Name == "X-Ray").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        var us = await db.Departments.Where(d => d.Name == "Ultrasound").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        var sort = 0;
        foreach (var (code, name, modality, body, minutes, template, prep) in StarterCatalogue())
        {
            if (await db.ServiceItems.AnyAsync(s => s.Code == "RAD-" + code, ct)) continue;
            var svc = new ServiceItem
            {
                Code = "RAD-" + code, Name = name, Category = EncounterType.ServiceOnly, Kind = KindOf(modality),
                DepartmentId = modality == RadiologyModality.XRay ? xray : us, DefaultFee = 0, IsActive = false, CreatedUtc = now, UpdatedUtc = now,
                Description = $"{Label(modality)} examination (starter catalogue)"
            };
            db.ServiceItems.Add(svc);
            await db.SaveChangesAsync(ct);
            db.ServiceFees.Add(new ServiceFee { ServiceItemId = svc.Id, Fee = 0, EffectiveFrom = DateOnly.MinValue, CreatedUtc = now, CreatedByUsername = "SYSTEM", Note = "Starter catalogue – set the fee" });
            db.RadiologyServices.Add(new RadiologyService
            {
                Code = code, Name = name, Modality = modality, BodyPart = body, DurationMinutes = minutes, PreparationInstructions = prep,
                DefaultTemplateId = template is null ? null : templates.GetValueOrDefault(template), ServiceItemId = svc.Id, IsActive = false,
                SortOrder = ++sort * 10, CreatedUtc = now, UpdatedUtc = now
            });
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Update 15: ECG service (both hospital types). The fee and "enabled" live in Settings → Fees &amp; Services (service "ECG"); the service
    /// starts disabled with no fee until the administrator sets it. Existing installations get it on the next start.
    /// </summary>
    public static async Task EnsureEcgAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        if (!await db.RadiologyTemplates.AnyAsync(t => t.Modality == RadiologyModality.Ecg, ct))
        {
            db.RadiologyTemplates.Add(new RadiologyTemplate
            {
                Name = "ECG – 12 lead", Modality = RadiologyModality.Ecg, Technique = "Resting 12-lead ECG.",
                Findings = "Rhythm: \nRate:  /min\nPR interval: \nQRS: \nQT / QTc: \nAxis: \nST-T: ", Impression = "", IsActive = true, UpdatedUtc = now, UpdatedByUsername = "SYSTEM"
            });
            await db.SaveChangesAsync(ct);
        }
        var existing = await db.RadiologyServices.Include(s => s.ServiceItem).FirstOrDefaultAsync(s => s.Modality == RadiologyModality.Ecg, ct);
        if (existing is not null)
        {
            if (existing.ServiceItem is { ServiceCategoryId: null } si) { si.ServiceCategoryId = await Finance.ServiceCatalogue.CategoryIdAsync(db, ServiceKind.Ecg, ct); await db.SaveChangesAsync(ct); }
            return;
        }
        var item = await db.ServiceItems.FirstOrDefaultAsync(s => s.Code == "SVC-RAD-ECG", ct) ?? await db.ServiceItems.FirstOrDefaultAsync(s => s.Code == "RAD-ECG", ct);
        if (item is null)
        {
            item = new ServiceItem
            {
                Code = "RAD-ECG", Name = "ECG", Category = EncounterType.ServiceOnly, Kind = ServiceKind.Ecg, DefaultFee = 0, IsActive = false,
                CreatedUtc = now, UpdatedUtc = now, Description = "ECG (electrocardiography) – set the fee and enable it"
            };
            db.ServiceItems.Add(item);
            await db.SaveChangesAsync(ct);
            db.ServiceFees.Add(new ServiceFee { ServiceItemId = item.Id, Fee = 0, EffectiveFrom = DateOnly.MinValue, CreatedUtc = now, CreatedByUsername = "SYSTEM", Note = "ECG – set the fee" });
        }
        item.Kind = ServiceKind.Ecg;
        item.ChargeUnit = ChargeUnit.PerTest;
        item.ServiceCategoryId ??= await Finance.ServiceCatalogue.CategoryIdAsync(db, ServiceKind.Ecg, ct);
        item.DepartmentId ??= await db.Departments.Where(d => d.Name == "Radiology" || d.Name == "X-Ray").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        var template = await db.RadiologyTemplates.Where(t => t.Modality == RadiologyModality.Ecg).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        db.RadiologyServices.Add(new RadiologyService
        {
            Code = "ECG", Name = "ECG (12-lead)", Modality = RadiologyModality.Ecg, BodyPart = "Heart", DurationMinutes = 10, DefaultTemplateId = template,
            ServiceItemId = item.Id, IsActive = true, SortOrder = 10, CreatedUtc = now, UpdatedUtc = now
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Booking rule that follows the hospital type: Government books only against an OPD / Emergency visit, Private also other patients.</summary>
    public static async Task ApplyHospitalTypeDefaultsAsync(HmsDbContext db, HospitalType type, CancellationToken ct = default)
    {
        var s = await db.RadiologySettings.FirstOrDefaultAsync(ct);
        if (s is null) return;
        s.AllowBookingWithoutVisit = type == HospitalType.Private;
        await db.SaveChangesAsync(ct);
    }

    private static IEnumerable<(string Code, string Name, RadiologyModality Modality, string Body, int Minutes, string? Template, string? Prep)> StarterCatalogue()
    {
        const RadiologyModality X = RadiologyModality.XRay, U = RadiologyModality.Ultrasound;
        yield return ("XR-CXR-PA", "Chest X-Ray PA View", X, "Chest", 10, "Chest X-Ray – normal", "Remove metal objects / jewellery from the chest.");
        yield return ("XR-CXR-AP", "Chest X-Ray AP View", X, "Chest", 10, "Chest X-Ray – normal", null);
        yield return ("XR-ABD", "Abdomen X-Ray", X, "Abdomen", 10, "X-Ray – general", null);
        yield return ("XR-PELVIS", "Pelvis X-Ray", X, "Pelvis", 10, "X-Ray – general", null);
        yield return ("XR-SKULL", "Skull X-Ray", X, "Skull", 10, "X-Ray – general", null);
        yield return ("XR-CSPINE", "Cervical Spine X-Ray", X, "Cervical spine", 10, "X-Ray – general", null);
        yield return ("XR-LSPINE", "Lumbar Spine X-Ray", X, "Lumbar spine", 10, "X-Ray – general", null);
        yield return ("XR-KUB", "X-Ray KUB", X, "Kidneys, ureters, bladder", 10, "X-Ray – general", null);
        yield return ("XR-KNEE", "Knee Joint X-Ray", X, "Knee", 10, "X-Ray – general", null);
        yield return ("XR-SHOULDER", "Shoulder Joint X-Ray", X, "Shoulder", 10, "X-Ray – general", null);
        yield return ("US-ABD", "Ultrasound Abdomen", U, "Abdomen", 20, "Ultrasound Abdomen – normal", "Fasting for 6 hours.");
        yield return ("US-PELVIS", "Ultrasound Pelvis", U, "Pelvis", 20, "Ultrasound – general", "Full bladder: drink 4–5 glasses of water one hour before.");
        yield return ("US-KUB", "Ultrasound KUB", U, "Kidneys, ureters, bladder", 15, "Ultrasound – general", "Full bladder.");
        yield return ("US-OBS", "Obstetric Ultrasound", U, "Obstetric", 20, "Obstetric Ultrasound", null);
        yield return ("US-PREG", "Pregnancy Profile", U, "Obstetric", 25, "Obstetric Ultrasound", null);
        yield return ("US-THYROID", "Ultrasound Thyroid", U, "Thyroid", 15, "Ultrasound – general", null);
        yield return ("US-BREAST", "Ultrasound Breast", U, "Breast", 20, "Ultrasound – general", null);
        yield return ("US-SOFT", "Ultrasound Soft Tissue", U, "Soft tissue", 15, "Ultrasound – general", null);
    }

    /// <summary>Starting texts only – the radiologist reviews and adapts every template before use.</summary>
    private static IEnumerable<RadiologyTemplate> StarterTemplates()
    {
        yield return new RadiologyTemplate
        {
            Name = "Chest X-Ray – normal", Modality = RadiologyModality.XRay, Technique = "PA erect view of the chest.",
            Findings = "Trachea is central.\nBoth lung fields are clear. No focal consolidation, mass or pleural effusion.\nCostophrenic angles are clear.\nCardiac size and contour are within normal limits.\nBony thorax appears intact.",
            Impression = "No significant abnormality detected."
        };
        yield return new RadiologyTemplate
        {
            Name = "X-Ray – general", Modality = RadiologyModality.XRay, Technique = "Standard views.",
            Findings = "Bones show normal density and alignment.\nNo fracture or dislocation seen.\nJoint spaces are maintained.\nSoft tissues are unremarkable.",
            Impression = "No bony abnormality detected."
        };
        yield return new RadiologyTemplate
        {
            Name = "Ultrasound Abdomen – normal", Modality = RadiologyModality.Ultrasound, Technique = "Real-time B-mode scan of the abdomen.",
            Findings = "Liver: normal in size and echotexture. No focal lesion. Intrahepatic ducts not dilated.\nGallbladder: well distended, normal wall, no calculus.\nCBD: not dilated.\nPancreas: visualised parts normal.\nSpleen: normal in size and echotexture.\nRight kidney: normal size, cortical echogenicity and corticomedullary differentiation. No calculus or hydronephrosis.\nLeft kidney: normal size, cortical echogenicity and corticomedullary differentiation. No calculus or hydronephrosis.\nUrinary bladder: normal wall, no calculus.\nNo free fluid in the abdomen.",
            Impression = "Normal ultrasound abdomen."
        };
        yield return new RadiologyTemplate
        {
            Name = "Ultrasound – general", Modality = RadiologyModality.Ultrasound, Technique = "Real-time B-mode ultrasound.",
            Findings = "Region examined appears normal in size, outline and echotexture.\nNo focal lesion or collection seen.",
            Impression = "No significant abnormality detected."
        };
        yield return new RadiologyTemplate
        {
            Name = "Obstetric Ultrasound", Modality = RadiologyModality.Ultrasound, Technique = "Transabdominal obstetric scan.",
            Findings = "Single live intrauterine fetus.\nFetal heart rate: ___ bpm, regular.\nPresentation: ___\nBPD: ___ mm  HC: ___ mm  AC: ___ mm  FL: ___ mm\nEstimated gestational age: ___ weeks ___ days\nEstimated fetal weight: ___ g\nPlacenta: ___, grade ___. Not low lying.\nLiquor: adequate (AFI ___ cm).",
            Impression = "Single live intrauterine pregnancy of ___ weeks ___ days.",
            Recommendations = "Follow-up scan as advised by the treating doctor."
        };
    }
}
