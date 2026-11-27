using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ot;

public sealed class TheatreInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public OtType Type { get; set; } = OtType.General;
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? Equipment { get; set; }
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    public int CleaningMinutes { get; set; } = 30;
    public bool IsAvailable { get; set; } = true;
    public string? UnavailableReason { get; set; }
    public bool IsActive { get; set; } = true;
    public int? StockLocationId { get; set; }
    public int? ChargeServiceItemId { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ProcedureInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int? DepartmentId { get; set; }
    public string? Specialty { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public List<AnesthesiaType> AnesthesiaTypes { get; set; } = new();
    public OtType? DefaultOtType { get; set; }
    public int? ServiceItemId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class EquipmentInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int? TheatreId { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;
    public DateOnly? LastMaintenance { get; set; }
    public DateOnly? NextMaintenance { get; set; }
    public string? ServiceNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}

public sealed class OtSettingsInput
{
    public int? SurgeonFeeItemId { get; set; }
    public int? AssistantFeeItemId { get; set; }
    public int? AnesthesiaFeeItemId { get; set; }
    public int? AnesthesiaSuppliesItemId { get; set; }
    public int? RecoveryItemId { get; set; }
    public int? EquipmentItemId { get; set; }
    public int? ConsumablesItemId { get; set; }
    public int DefaultCleaningMinutes { get; set; } = 30;
    public bool PrintHeader { get; set; } = true;
    public int LetterheadTopMarginMm { get; set; } = 45;
    public string? FormFooter { get; set; }
    public int MaxUploadMb { get; set; } = 10;
}

public interface IOtSetupService
{
    Task<IReadOnlyList<OtTheatre>> TheatresAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveTheatreAsync(int? id, TheatreInput input, CancellationToken ct = default);
    Task<IReadOnlyList<OtProcedure>> ProceduresAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveProcedureAsync(int? id, ProcedureInput input, CancellationToken ct = default);
    Task<IReadOnlyList<OtConsentType>> ConsentTypesAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveConsentTypeAsync(int? id, string? name, string? template, bool active, int sort, CancellationToken ct = default);
    Task<IReadOnlyList<OtChecklistItem>> ChecklistAsync(ChecklistStage? stage, bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveChecklistItemAsync(int? id, ChecklistStage stage, string? label, bool mandatory, bool active, int sort, CancellationToken ct = default);
    Task<IReadOnlyList<OtEquipment>> EquipmentAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveEquipmentAsync(int? id, EquipmentInput input, CancellationToken ct = default);
    Task<OtSettings> SettingsAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveSettingsAsync(OtSettingsInput input, uint rowVersion, CancellationToken ct = default);
    Task<bool> IsPrivateAsync(CancellationToken ct = default);
}

public sealed class OtSetupService : IOtSetupService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly HospitalClock _clock;
    private readonly IRequestContext _ctx;

    public OtSetupService(HmsDbContext db, IAuditService audit, HospitalClock clock, IRequestContext ctx)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _ctx = ctx;
    }

    internal static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    /// <summary>Update 16: one unified HMS – services are charged as configured (the stored hospital type only drives the pharmacy).</summary>
    public Task<bool> IsPrivateAsync(CancellationToken ct = default) => Task.FromResult(true);

    // ---------------------------------------------------------------- theatres
    public async Task<IReadOnlyList<OtTheatre>> TheatresAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.OtTheatres.AsNoTracking().Include(t => t.StockLocation).Include(t => t.ChargeServiceItem).Where(t => !activeOnly || t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveTheatreAsync(int? id, TheatreInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code, 20)?.ToUpperInvariant();
        var name = T(i.Name, 100);
        if (code is null) errors.Add("OT ID / code is required.");
        if (name is null) errors.Add("OT name / number is required.");
        if (!Enum.IsDefined(i.Type)) errors.Add("Select the OT type.");
        if (i.CleaningMinutes is < 0 or > 480) errors.Add("Preparation / cleaning interval must be 0 to 480 minutes.");
        if (i.OpensAt is not null != i.ClosesAt is not null) errors.Add("Enter both opening and closing time (or neither for 24 hours).");
        if (i.OpensAt is { } o && i.ClosesAt is { } c && c <= o) errors.Add("Closing time must be after opening time.");
        if (!i.IsAvailable && T(i.UnavailableReason, 200) is null) errors.Add("Enter the reason why the theatre is unavailable.");
        if (code is not null && await _db.OtTheatres.AnyAsync(t => t.Id != id && t.Code == code, ct)) errors.Add("Another theatre uses this code.");
        if (i.StockLocationId is int l && !await _db.StockLocations.AnyAsync(x => x.Id == l && x.Kind == StockLocationKind.Department, ct))
            errors.Add("Select a department stock location for OT stock.");
        if (i.ChargeServiceItemId is int s && !await _db.ServiceItems.AnyAsync(x => x.Id == s && x.IsActive, ct)) errors.Add("Select an active OT-charge service.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var t = id is null ? _db.OtTheatres.Add(new OtTheatre()).Entity : await _db.OtTheatres.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return ServiceResult<int>.Fail("Theatre not found.");
        if (id is not null && !i.IsActive && t.IsActive && await _db.Surgeries.AnyAsync(x => x.TheatreId == t.Id && x.PlannedEndUtc > _clock.UtcNow
                && (x.Status == SurgeryStatus.Requested || x.Status == SurgeryStatus.Scheduled || x.Status == SurgeryStatus.PreOp || x.Status == SurgeryStatus.Ready || x.Status == SurgeryStatus.InProgress), ct))
            return ServiceResult<int>.Fail("Surgeries are still booked in this theatre – move them before deactivating it.");
        var old = id is null ? null : new { t.Code, t.Name, t.Type, t.IsAvailable, t.UnavailableReason, t.IsActive, t.CleaningMinutes, t.OpensAt, t.ClosesAt, t.ChargeServiceItemId, t.StockLocationId };
        t.Code = code!; t.Name = name!; t.Type = i.Type; t.Location = T(i.Location, 100); t.Description = T(i.Description, 500); t.Equipment = T(i.Equipment, 1000);
        t.OpensAt = i.OpensAt; t.ClosesAt = i.ClosesAt; t.CleaningMinutes = i.CleaningMinutes; t.IsAvailable = i.IsAvailable;
        t.UnavailableReason = i.IsAvailable ? null : T(i.UnavailableReason, 200); t.IsActive = i.IsActive; t.StockLocationId = i.StockLocationId;
        t.ChargeServiceItemId = i.ChargeServiceItemId; t.SortOrder = i.SortOrder; t.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.OtTheatreSaved, "OtTheatre", code!, $"Operation theatre {name} ({code}) {(id is null ? "created" : "updated")}", old,
            new { t.Code, t.Name, t.Type, t.IsAvailable, t.UnavailableReason, t.IsActive, t.CleaningMinutes, t.OpensAt, t.ClosesAt, t.ChargeServiceItemId, t.StockLocationId });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult<int>.Fail("The theatre was changed by someone else. Please reload."); }
        return ServiceResult<int>.Ok(t.Id);
    }

    // ---------------------------------------------------------------- procedures
    public async Task<IReadOnlyList<OtProcedure>> ProceduresAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.OtProcedures.AsNoTracking().Include(p => p.Department).Include(p => p.ServiceItem).Where(p => !activeOnly || p.IsActive)
            .OrderBy(p => p.Specialty).ThenBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveProcedureAsync(int? id, ProcedureInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code, 20)?.ToUpperInvariant();
        var name = T(i.Name, 150);
        if (code is null) errors.Add("Procedure code is required.");
        if (name is null) errors.Add("Procedure name is required.");
        if (i.DurationMinutes is < 5 or > 1440) errors.Add("Typical duration must be 5 to 1440 minutes.");
        if (i.AnesthesiaTypes.Any(a => !Enum.IsDefined(a))) errors.Add("Unknown anesthesia type.");
        if (code is not null && await _db.OtProcedures.AnyAsync(p => p.Id != id && p.Code == code, ct)) errors.Add("Another procedure uses this code.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) errors.Add("Select a valid department.");
        if (i.ServiceItemId is int s && !await _db.ServiceItems.AnyAsync(x => x.Id == s && x.IsActive, ct)) errors.Add("Select an active charge service.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var p = id is null ? _db.OtProcedures.Add(new OtProcedure()).Entity : await _db.OtProcedures.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return ServiceResult<int>.Fail("Procedure not found.");
        var old = id is null ? null : new { p.Code, p.Name, p.DurationMinutes, p.ServiceItemId, p.IsActive, p.AnesthesiaTypes };
        p.Code = code!; p.Name = name!; p.DepartmentId = i.DepartmentId; p.Specialty = T(i.Specialty, 60); p.Category = T(i.Category, 60);
        p.Description = T(i.Description, 500); p.DurationMinutes = i.DurationMinutes;
        p.AnesthesiaTypes = i.AnesthesiaTypes.Count == 0 ? null : string.Join(",", i.AnesthesiaTypes.Distinct().Select(a => a.ToString()));
        p.DefaultOtType = i.DefaultOtType; p.ServiceItemId = i.ServiceItemId; p.IsActive = i.IsActive; p.SortOrder = i.SortOrder; p.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.OtProcedureSaved, "OtProcedure", code!, $"Procedure {name} ({code}) {(id is null ? "created" : "updated")}", old,
            new { p.Code, p.Name, p.DurationMinutes, p.ServiceItemId, p.IsActive, p.AnesthesiaTypes });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult<int>.Fail("The procedure was changed by someone else. Please reload."); }
        return ServiceResult<int>.Ok(p.Id);
    }

    // ---------------------------------------------------------------- consent types & checklist items
    public async Task<IReadOnlyList<OtConsentType>> ConsentTypesAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.OtConsentTypes.AsNoTracking().Where(t => !activeOnly || t.IsActive).OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveConsentTypeAsync(int? id, string? name, string? template, bool active, int sort, CancellationToken ct = default)
    {
        name = T(name, 100);
        if (name is null) return ServiceResult<int>.Fail("Consent type name is required.");
        if (await _db.OtConsentTypes.AnyAsync(t => t.Id != id && t.Name == name, ct)) return ServiceResult<int>.Fail("This consent type already exists.");
        var t = id is null ? _db.OtConsentTypes.Add(new OtConsentType()).Entity : await _db.OtConsentTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return ServiceResult<int>.Fail("Consent type not found.");
        t.Name = name; t.TemplateText = T(template, 4000); t.IsActive = active; t.SortOrder = sort;
        _audit.Add(AuditActions.OtConsentTypeSaved, "OtConsentType", name, $"Consent type {name} {(id is null ? "created" : "updated")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(t.Id);
    }

    public async Task<IReadOnlyList<OtChecklistItem>> ChecklistAsync(ChecklistStage? stage, bool activeOnly, CancellationToken ct = default) =>
        await _db.OtChecklistItems.AsNoTracking().Where(x => (stage == null || x.Stage == stage) && (!activeOnly || x.IsActive))
            .OrderBy(x => x.Stage).ThenBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveChecklistItemAsync(int? id, ChecklistStage stage, string? label, bool mandatory, bool active, int sort, CancellationToken ct = default)
    {
        label = T(label, 200);
        if (label is null) return ServiceResult<int>.Fail("Checklist item text is required.");
        if (!Enum.IsDefined(stage)) return ServiceResult<int>.Fail("Select the checklist stage.");
        if (await _db.OtChecklistItems.AnyAsync(x => x.Id != id && x.Stage == stage && x.Label == label, ct)) return ServiceResult<int>.Fail("This item already exists in the stage.");
        var x = id is null ? _db.OtChecklistItems.Add(new OtChecklistItem()).Entity : await _db.OtChecklistItems.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (x is null) return ServiceResult<int>.Fail("Checklist item not found.");
        var old = id is null ? null : new { x.Stage, x.Label, x.IsMandatory, x.IsActive };
        x.Stage = stage; x.Label = label; x.IsMandatory = mandatory; x.IsActive = active; x.SortOrder = sort; x.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.OtChecklistItemSaved, "OtChecklistItem", label, $"{stage} checklist item “{label}” {(id is null ? "created" : "updated")}", old, new { x.Stage, x.Label, x.IsMandatory, x.IsActive });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(x.Id);
    }

    // ---------------------------------------------------------------- equipment
    public async Task<IReadOnlyList<OtEquipment>> EquipmentAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.OtEquipment.AsNoTracking().Include(e => e.Theatre).Where(e => !activeOnly || e.IsActive).OrderBy(e => e.Theatre!.Name).ThenBy(e => e.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveEquipmentAsync(int? id, EquipmentInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code, 30)?.ToUpperInvariant();
        var name = T(i.Name, 150);
        if (code is null) errors.Add("Equipment ID is required.");
        if (name is null) errors.Add("Equipment name is required.");
        if (!Enum.IsDefined(i.Status)) errors.Add("Select the status.");
        if (i.Status is EquipmentStatus.UnderMaintenance or EquipmentStatus.OutOfService && T(i.Note, 500) is null && T(i.ServiceNotes, 1000) is null)
            errors.Add("Enter a note for maintenance / out-of-service status.");
        if (i.LastMaintenance is { } lm && i.NextMaintenance is { } nm && nm < lm) errors.Add("Next maintenance must be after the last maintenance.");
        if (code is not null && await _db.OtEquipment.AnyAsync(e => e.Id != id && e.Code == code, ct)) errors.Add("Another item uses this equipment ID.");
        if (i.TheatreId is int th && !await _db.OtTheatres.AnyAsync(t => t.Id == th, ct)) errors.Add("Select a valid theatre.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var e = id is null ? _db.OtEquipment.Add(new OtEquipment()).Entity : await _db.OtEquipment.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return ServiceResult<int>.Fail("Equipment not found.");
        var oldStatus = id is null ? (EquipmentStatus?)null : e.Status;
        e.Code = code!; e.Name = name!; e.TheatreId = i.TheatreId; e.Status = i.Status; e.LastMaintenance = i.LastMaintenance; e.NextMaintenance = i.NextMaintenance;
        e.ServiceNotes = T(i.ServiceNotes, 1000); e.IsActive = i.IsActive; e.UpdatedUtc = _clock.UtcNow;
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult<int>.Fail("The equipment was changed by someone else. Please reload."); }
        _db.OtEquipmentLogs.Add(new OtEquipmentLog
        {
            EquipmentId = e.Id, Event = id is null ? "Registered" : oldStatus != e.Status ? "Status" : "Updated", FromValue = oldStatus?.ToString(), ToValue = e.Status.ToString(),
            Note = T(i.Note, 500), CreatedUtc = _clock.UtcNow, Username = _ctx.Username
        });
        _audit.Add(AuditActions.OtEquipmentSaved, "OtEquipment", code!, $"OT equipment {name} ({code}) {(id is null ? "registered" : "updated")} – {e.Status}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(e.Id);
    }

    // ---------------------------------------------------------------- settings
    public async Task<OtSettings> SettingsAsync(CancellationToken ct = default) => await _db.OtSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OtSettings();

    public async Task<ServiceResult> SaveSettingsAsync(OtSettingsInput i, uint rowVersion, CancellationToken ct = default)
    {
        if (i.DefaultCleaningMinutes is < 0 or > 480) return ServiceResult.Fail("Default cleaning interval must be 0 to 480 minutes.");
        if (i.LetterheadTopMarginMm is < 0 or > 120) return ServiceResult.Fail("Letterhead margin must be 0 to 120 mm.");
        if (i.MaxUploadMb is < 1 or > 50) return ServiceResult.Fail("Maximum upload size must be 1 to 50 MB.");
        foreach (var item in new[] { i.SurgeonFeeItemId, i.AssistantFeeItemId, i.AnesthesiaFeeItemId, i.AnesthesiaSuppliesItemId, i.RecoveryItemId, i.EquipmentItemId, i.ConsumablesItemId })
            if (item is int s && !await _db.ServiceItems.AnyAsync(x => x.Id == s && x.IsActive, ct)) return ServiceResult.Fail("Select active Phase 3 services for the OT charges.");
        var s0 = await _db.OtSettings.FirstOrDefaultAsync(ct);
        if (s0 is null) { s0 = new OtSettings(); _db.OtSettings.Add(s0); }
        else _db.Entry(s0).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var old = new { s0.SurgeonFeeItemId, s0.AssistantFeeItemId, s0.AnesthesiaFeeItemId, s0.AnesthesiaSuppliesItemId, s0.RecoveryItemId, s0.EquipmentItemId, s0.ConsumablesItemId, s0.DefaultCleaningMinutes, s0.PrintHeader };
        s0.SurgeonFeeItemId = i.SurgeonFeeItemId; s0.AssistantFeeItemId = i.AssistantFeeItemId; s0.AnesthesiaFeeItemId = i.AnesthesiaFeeItemId;
        s0.AnesthesiaSuppliesItemId = i.AnesthesiaSuppliesItemId; s0.RecoveryItemId = i.RecoveryItemId; s0.EquipmentItemId = i.EquipmentItemId; s0.ConsumablesItemId = i.ConsumablesItemId;
        s0.DefaultCleaningMinutes = i.DefaultCleaningMinutes; s0.PrintHeader = i.PrintHeader; s0.LetterheadTopMarginMm = i.LetterheadTopMarginMm;
        s0.FormFooter = T(i.FormFooter, 300); s0.MaxUploadMb = i.MaxUploadMb; s0.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.OtSettingsChanged, "OtSettings", "1", "OT settings changed", old,
            new { s0.SurgeonFeeItemId, s0.AssistantFeeItemId, s0.AnesthesiaFeeItemId, s0.AnesthesiaSuppliesItemId, s0.RecoveryItemId, s0.EquipmentItemId, s0.ConsumablesItemId, s0.DefaultCleaningMinutes, s0.PrintHeader });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The settings were changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // ---------------------------------------------------------------- first-start seed (configuration only, never patient data)
    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        if (!await db.OtSettings.AnyAsync(ct)) { db.OtSettings.Add(new OtSettings { Id = 1, UpdatedUtc = now }); await db.SaveChangesAsync(ct); }
        if (!await db.OtProcedures.AnyAsync(ct))
        {
            var sort = 0;
            foreach (var (code, name, spec, cat, minutes, anest, type) in StarterProcedures())
                db.OtProcedures.Add(new OtProcedure
                {
                    Code = code, Name = name, Specialty = spec, Category = cat, DurationMinutes = minutes, AnesthesiaTypes = anest, DefaultOtType = type,
                    IsActive = true, SortOrder = ++sort * 10, UpdatedUtc = now
                });
            await db.SaveChangesAsync(ct);
        }
        if (!await db.OtConsentTypes.AnyAsync(ct))
        {
            var sort = 0;
            foreach (var n in new[] { "General Surgical Consent", "Anesthesia Consent", "Blood Transfusion Consent", "High-Risk Procedure Consent" })
                db.OtConsentTypes.Add(new OtConsentType
                {
                    Name = n, IsActive = true, SortOrder = ++sort * 10,
                    TemplateText = $"I, the undersigned, patient / authorised representative of {{patient}}, confirm that the {n.ToLowerInvariant().Replace(" consent", "")} for {{procedure}} at {{hospital}} " +
                                   "has been explained to me in a language I understand, including its nature, purpose, expected benefits, risks, possible complications and alternatives. " +
                                   "I had the opportunity to ask questions, which were answered. I give my consent voluntarily."
                });
            await db.SaveChangesAsync(ct);
        }
        if (!await db.OtChecklistItems.AnyAsync(ct))
        {
            foreach (var (stage, items) in StarterChecklist())
            {
                var sort = 0;
                foreach (var label in items)
                    db.OtChecklistItems.Add(new OtChecklistItem { Stage = stage, Label = label, IsMandatory = true, IsActive = true, SortOrder = ++sort * 10, UpdatedUtc = now });
            }
            await db.SaveChangesAsync(ct);
        }
        if (!await db.ClinicalFormFields.AnyAsync(f => f.Kind == RecordKind.PreOpAssessment || f.Kind == RecordKind.PacuObservation, ct))
        {
            foreach (var (kind, fields) in DefaultForms())
            {
                var sort = 0;
                foreach (var f in fields)
                {
                    f.Kind = kind; f.SortOrder = ++sort * 10; f.UpdatedUtc = now; f.UpdatedBy = "SYSTEM";
                    db.ClinicalFormFields.Add(f);
                }
            }
            await db.SaveChangesAsync(ct);
        }
        if (!await db.Departments.AnyAsync(d => d.Name == "Operation Theatre", ct) && await db.Departments.AnyAsync(ct))
        {
            db.Departments.Add(new Department { Name = "Operation Theatre", Code = "OT", Description = "Operation theatre & recovery (Phase 8)", IsActive = true, CreatedUtc = now, UpdatedUtc = now });
            await db.SaveChangesAsync(ct);
        }
    }

    private static IEnumerable<(string, string, string, string, int, string?, OtType)> StarterProcedures()
    {
        const string ga = "General,Spinal", gs = "General,Spinal,Regional,Local", loc = "Local,Sedation,General";
        yield return ("GS-APP", "Appendectomy", "General Surgery", "Major", 60, ga, OtType.General);
        yield return ("GS-HERN", "Hernia Repair", "General Surgery", "Major", 75, gs, OtType.General);
        yield return ("GS-CHOL", "Cholecystectomy", "General Surgery", "Major", 90, "General", OtType.General);
        yield return ("GS-ID", "Incision and Drainage", "General Surgery", "Minor", 30, loc, OtType.Minor);
        yield return ("GS-LUMP", "Excision of Lump", "General Surgery", "Minor", 30, loc, OtType.Minor);
        yield return ("GS-HYD", "Hydrocele Repair", "General Surgery", "Major", 45, gs, OtType.General);
        yield return ("GS-CIRC", "Circumcision", "General Surgery", "Minor", 30, loc, OtType.Minor);
        yield return ("OB-LSCS", "Caesarean Section (C-Section)", "Gynaecology / Obstetrics", "Major", 60, "Spinal,General,Epidural", OtType.Gynaecology);
        yield return ("OB-DC", "Dilatation and Curettage (D&C)", "Gynaecology / Obstetrics", "Minor", 30, "Sedation,General,Spinal", OtType.Gynaecology);
        yield return ("OB-HYST", "Hysterectomy", "Gynaecology / Obstetrics", "Major", 120, "General,Spinal", OtType.Gynaecology);
        yield return ("OB-ECT", "Ectopic Pregnancy Surgery", "Gynaecology / Obstetrics", "Major", 75, "General", OtType.Gynaecology);
        yield return ("OR-FIX", "Fracture Fixation", "Orthopedics", "Major", 90, "General,Spinal,Regional", OtType.Orthopedic);
        yield return ("OR-ORIF", "Open Reduction and Internal Fixation (ORIF)", "Orthopedics", "Major", 120, "General,Spinal,Regional", OtType.Orthopedic);
        yield return ("OR-IMPR", "Implant Removal", "Orthopedics", "Major", 60, "General,Spinal,Regional", OtType.Orthopedic);
        yield return ("MN-SUT", "Wound Suturing", "Minor Procedures", "Minor", 20, "Local", OtType.Minor);
        yield return ("MN-DEB", "Wound Debridement", "Minor Procedures", "Minor", 30, "Local,Sedation,General", OtType.Minor);
        yield return ("MN-ABS", "Abscess Drainage", "Minor Procedures", "Minor", 30, "Local,Sedation,General", OtType.Minor);
    }

    private static IEnumerable<(ChecklistStage, string[])> StarterChecklist()
    {
        yield return (ChecklistStage.PreOp, new[]
        {
            "Patient identity confirmed", "Planned procedure confirmed", "Consent documented", "Allergies reviewed", "Relevant investigations reviewed",
            "Fasting status documented", "Blood availability reviewed (where applicable)", "Required equipment checked", "Anesthesia assessment documented",
        });
        yield return (ChecklistStage.SignIn, new[]
        {
            "Patient identity confirmed", "Procedure confirmed", "Surgical site confirmed", "Consent checked", "Allergy status reviewed",
            "Anesthesia safety checks completed", "Required equipment available", "Relevant investigations reviewed",
        });
        yield return (ChecklistStage.TimeOut, new[]
        {
            "Entire surgical team present", "Patient identity reconfirmed", "Procedure reconfirmed", "Surgical site reconfirmed",
            "Antibiotic prophylaxis documented (where applicable)", "Expected critical events discussed", "Required imaging available", "Equipment and instruments checked",
        });
        yield return (ChecklistStage.SignOut, new[]
        {
            "Procedure performed confirmed", "Instrument count documented", "Sponge count documented", "Needle count documented",
            "Specimen labelling confirmed (where applicable)", "Equipment problems recorded", "Post-operative plan documented", "Recovery handover completed",
        });
    }

    private static ClinicalFormField F(string key, string label, FieldType type = FieldType.Text, string? unit = null, string? choices = null, string? section = null) =>
        new() { Key = key, Label = label, Type = type, Unit = unit, Choices = choices, Section = section, IsActive = true };
    private static ClinicalFormField N(string key, string label, string? unit = null, string? section = null) => F(key, label, FieldType.Number, unit, section: section);
    private static ClinicalFormField L(string key, string label, string? section = null) => F(key, label, FieldType.LongText, section: section);

    /// <summary>Default documentation fields – no alert limits (only the hospital may configure them, with their protocol).</summary>
    private static IEnumerable<(RecordKind, ClinicalFormField[])> DefaultForms()
    {
        ClinicalFormField[] Vitals(string s) => new[]
        {
            N("bp_sys", "BP systolic", "mmHg", s), N("bp_dia", "BP diastolic", "mmHg", s), N("pulse", "Pulse", "/min", s), N("temp", "Temperature", "°C", s),
            N("resp", "Respiratory rate", "/min", s), N("spo2", "SpO₂", "%", s),
        };
        yield return (RecordKind.PreOpAssessment, new[]
        {
            L("history", "Patient history", "Assessment"), L("complaints", "Presenting complaints", "Assessment"), F("diagnosis", "Diagnosis", section: "Assessment"),
            L("previous_surgeries", "Previous surgeries", "Assessment"), L("medical_history", "Relevant medical history", "Assessment"), L("medications", "Existing medications", "Assessment"),
            F("allergies", "Known allergies", section: "Assessment"), F("drug_allergy", "Drug allergy details", section: "Assessment"), L("family_history", "Relevant family history", "Assessment"),
            L("examination", "Physical examination notes", "Assessment"),
        }.Concat(Vitals("Vital signs")).Concat(new[]
        {
            N("weight", "Weight", "kg", "Vital signs"), N("height", "Height", "cm", "Vital signs"),
            F("cbc", "CBC", section: "Investigations"), F("blood_group", "Blood group", section: "Investigations"), F("sugar", "Blood sugar", section: "Investigations"),
            F("renal", "Renal function", section: "Investigations"), F("liver", "Liver function", section: "Investigations"), F("coag", "Coagulation profile", section: "Investigations"),
            F("ecg", "ECG", section: "Investigations"), F("cxr", "Chest X-ray", section: "Investigations"), L("other_inv", "Other investigations", "Investigations"),
            F("fasting", "Fasting status", section: "Readiness"),
        }).ToArray());
        yield return (RecordKind.AnesthesiaAssessment, new[]
        {
            L("assessment", "Anesthesia assessment"), L("airway", "Airway assessment"), L("medical_history", "Relevant medical history"), F("allergies", "Allergies"),
            L("previous_anesthesia", "Previous anesthesia history"), L("investigations", "Relevant investigation findings"),
            F("asa", "ASA classification (if used)", FieldType.Choice, choices: "I|II|III|IV|V|VI|Not used"), L("plan", "Anesthesia plan (anesthetist's decision)"), L("notes", "Anesthetist notes"),
        });
        yield return (RecordKind.IntraOpMonitoring, Vitals("Monitoring").Concat(new[] { F("ecg", "ECG observation", section: "Monitoring"), F("etco2", "EtCO₂ / other monitoring", section: "Monitoring") }).ToArray());
        yield return (RecordKind.PacuObservation, Vitals("Vital signs").Concat(new[]
        {
            N("pain", "Pain score (0–10)", null, "Assessment"), F("consciousness", "Consciousness / response", section: "Assessment"), F("oxygen", "Oxygen support", section: "Support"),
            F("iv_fluids", "IV fluids", section: "Support"), F("events", "Relevant events", section: "Assessment"), F("complications", "Complications", section: "Assessment"),
        }).ToArray());
        yield return (RecordKind.PostOpRecord, new[]
        {
            F("source", "Source of instruction", FieldType.Choice, choices: "Handwritten by doctor|Verbal order|Direct entry by clinician"),
            F("diagnosis", "Post-operative diagnosis"), F("condition", "Patient condition"), L("instructions", "Post-operative instructions"), L("medication_plan", "Medication plan (as prescribed)"),
            L("wound_care", "Wound care instructions"), F("diet", "Diet instructions"), L("monitoring", "Monitoring instructions"), L("follow_up", "Follow-up recommendations"),
            F("complications", "Complications"),
        });
    }
}
