using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ipd;

public sealed class WardInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public WardType Type { get; set; } = WardType.General;
    public int? DepartmentId { get; set; }
    public string? Location { get; set; }
    public int Capacity { get; set; }
    public string? ResponsibleStaff { get; set; }
    public int? BedChargeServiceItemId { get; set; }
    public int? StockLocationId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class RoomInput
{
    public int WardId { get; set; }
    public string? Name { get; set; }
    public RoomType Type { get; set; } = RoomType.General;
    public int BedCapacity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public sealed class BedInput
{
    public int RoomId { get; set; }
    public string? Code { get; set; }
    public BedType Type { get; set; } = BedType.Standard;
    public string? EquipmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class FieldInput
{
    public RecordKind Kind { get; set; }
    public string? Key { get; set; }
    public string? Label { get; set; }
    public string? Section { get; set; }
    public FieldType Type { get; set; } = FieldType.Text;
    public string? Unit { get; set; }
    public string? Choices { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public decimal? AlertLow { get; set; }
    public decimal? AlertHigh { get; set; }
    public string? AlertSource { get; set; }
}

public sealed class IpdSettingsInput
{
    public int? AdmissionServiceItemId { get; set; }
    public bool AllowDirectAdmission { get; set; } = true;
    public bool RequireBillClearanceForDischarge { get; set; }
    public bool PrintHeader { get; set; } = true;
    public int LetterheadTopMarginMm { get; set; } = 45;
    public string? FormFooter { get; set; }
    public bool RequireMarConfirmations { get; set; } = true;
    /// <summary>Update 10: bed charging policy (empty = keep).</summary>
    public BedChargePolicy? BedChargePolicy { get; set; }
    public TimeOnly? BedChargeCutoff { get; set; }
    public int? HalfDayHours { get; set; }
}

public interface IIpdSetupService
{
    Task<IReadOnlyList<IpdWard>> WardsAsync(bool activeOnly, CancellationToken ct = default);
    Task<IReadOnlyList<IpdRoom>> RoomsAsync(int? wardId, bool activeOnly, CancellationToken ct = default);
    Task<IReadOnlyList<IpdBed>> BedsAsync(int? wardId, bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveWardAsync(int? id, WardInput input, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveRoomAsync(int? id, RoomInput input, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveBedAsync(int? id, BedInput input, CancellationToken ct = default);
    Task<ServiceResult> SetBedStatusAsync(int bedId, BedStatus status, string? note, CancellationToken ct = default);

    Task<IReadOnlyList<ClinicalFormField>> FieldsAsync(RecordKind? kind, bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveFieldAsync(int? id, FieldInput input, CancellationToken ct = default);

    Task<IpdSettings> SettingsAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveSettingsAsync(IpdSettingsInput input, uint rowVersion, CancellationToken ct = default);
    Task<bool> IsPrivateAsync(CancellationToken ct = default);
}

public sealed class IpdSetupService : IIpdSetupService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly HospitalClock _clock;
    private readonly IRequestContext _ctx;

    public IpdSetupService(HmsDbContext db, IAuditService audit, HospitalClock clock, IRequestContext ctx)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _ctx = ctx;
    }

    internal static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    public async Task<bool> IsPrivateAsync(CancellationToken ct = default) =>
        await _db.HospitalConfigurations.AsNoTracking().Select(c => c.HospitalType).FirstOrDefaultAsync(ct) == HospitalType.Private;

    public async Task<IReadOnlyList<IpdWard>> WardsAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.IpdWards.AsNoTracking().Include(w => w.Department).Include(w => w.BedChargeServiceItem).Where(w => !activeOnly || w.IsActive)
            .OrderBy(w => w.SortOrder).ThenBy(w => w.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<IpdRoom>> RoomsAsync(int? wardId, bool activeOnly, CancellationToken ct = default) =>
        await _db.IpdRooms.AsNoTracking().Include(r => r.Ward).Where(r => (wardId == null || r.WardId == wardId) && (!activeOnly || r.IsActive))
            .OrderBy(r => r.Ward!.SortOrder).ThenBy(r => r.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<IpdBed>> BedsAsync(int? wardId, bool activeOnly, CancellationToken ct = default) =>
        await _db.IpdBeds.AsNoTracking().Include(b => b.Room).Include(b => b.Ward).Where(b => (wardId == null || b.WardId == wardId) && (!activeOnly || b.IsActive))
            .OrderBy(b => b.Ward!.SortOrder).ThenBy(b => b.Ward!.Name).ThenBy(b => b.Room!.Name).ThenBy(b => b.SortOrder).ThenBy(b => b.Code).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveWardAsync(int? id, WardInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code, 20)?.ToUpperInvariant();
        var name = T(i.Name, 100);
        if (code is null) errors.Add("Ward code is required.");
        if (name is null) errors.Add("Ward name is required.");
        if (!Enum.IsDefined(i.Type)) errors.Add("Select the ward type.");
        if (i.Capacity is < 0 or > 500) errors.Add("Capacity must be between 0 and 500.");
        if (code is not null && await _db.IpdWards.AnyAsync(w => w.Id != id && w.Code == code, ct)) errors.Add("Another ward uses this code.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) errors.Add("Select a valid department.");
        if (i.BedChargeServiceItemId is int s && !await _db.ServiceItems.AnyAsync(x => x.Id == s && x.IsActive, ct)) errors.Add("Select an active bed-charge service.");
        if (i.StockLocationId is int l && !await _db.StockLocations.AnyAsync(x => x.Id == l && x.Kind == StockLocationKind.Department, ct))
            errors.Add("Select a department stock location for ward stock.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var w = id is null ? _db.IpdWards.Add(new IpdWard()).Entity : await _db.IpdWards.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return ServiceResult<int>.Fail("Ward not found.");
        if (id is not null && !i.IsActive && w.IsActive && await _db.IpdAdmissions.AnyAsync(a => a.CurrentWardId == w.Id && a.DischargedUtc == null && a.Status != AdmissionStatus.Cancelled, ct))
            return ServiceResult<int>.Fail("Patients are still admitted in this ward – transfer them before deactivating it.");
        var old = id is null ? null : new { w.Code, w.Name, w.Type, w.DepartmentId, w.Capacity, w.IsActive, w.BedChargeServiceItemId, w.StockLocationId };
        w.Code = code!; w.Name = name!; w.Type = i.Type; w.DepartmentId = i.DepartmentId; w.Location = T(i.Location, 100); w.Capacity = i.Capacity;
        w.ResponsibleStaff = T(i.ResponsibleStaff, 200); w.BedChargeServiceItemId = i.BedChargeServiceItemId; w.StockLocationId = i.StockLocationId;
        w.IsActive = i.IsActive; w.SortOrder = i.SortOrder; w.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdWardSaved, "IpdWard", code!, $"Ward {name} ({code}) {(id is null ? "created" : "updated")}", old,
            new { w.Code, w.Name, w.Type, w.DepartmentId, w.Capacity, w.IsActive, w.BedChargeServiceItemId, w.StockLocationId });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(w.Id);
    }

    public async Task<ServiceResult<int>> SaveRoomAsync(int? id, RoomInput i, CancellationToken ct = default)
    {
        var name = T(i.Name, 50);
        if (name is null) return ServiceResult<int>.Fail("Room number / name is required.");
        if (!Enum.IsDefined(i.Type)) return ServiceResult<int>.Fail("Select the room type.");
        if (i.BedCapacity is < 0 or > 100) return ServiceResult<int>.Fail("Bed capacity must be between 0 and 100.");
        if (!await _db.IpdWards.AnyAsync(w => w.Id == i.WardId, ct)) return ServiceResult<int>.Fail("Select the ward.");
        if (await _db.IpdRooms.AnyAsync(r => r.Id != id && r.WardId == i.WardId && r.Name == name, ct)) return ServiceResult<int>.Fail("This ward already has a room with this name.");
        var r = id is null ? _db.IpdRooms.Add(new IpdRoom()).Entity : await _db.IpdRooms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return ServiceResult<int>.Fail("Room not found.");
        if (id is not null && r.WardId != i.WardId && await _db.IpdBeds.AnyAsync(b => b.RoomId == r.Id, ct))
            return ServiceResult<int>.Fail("A room with beds cannot be moved to another ward.");
        r.WardId = i.WardId; r.Name = name; r.Type = i.Type; r.BedCapacity = i.BedCapacity; r.IsActive = i.IsActive; r.Notes = T(i.Notes, 200);
        _audit.Add(AuditActions.IpdRoomSaved, "IpdRoom", name, $"Room {name} {(id is null ? "created" : "updated")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(r.Id);
    }

    public async Task<ServiceResult<int>> SaveBedAsync(int? id, BedInput i, CancellationToken ct = default)
    {
        var code = T(i.Code, 30)?.ToUpperInvariant();
        if (code is null) return ServiceResult<int>.Fail("Bed number is required.");
        if (!Enum.IsDefined(i.Type)) return ServiceResult<int>.Fail("Select the bed type.");
        var room = await _db.IpdRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == i.RoomId, ct);
        if (room is null) return ServiceResult<int>.Fail("Select the room.");
        if (await _db.IpdBeds.AnyAsync(b => b.Id != id && b.Code == code, ct)) return ServiceResult<int>.Fail("Bed numbers must be unique – this number is already used.");
        var b = id is null ? _db.IpdBeds.Add(new IpdBed { Status = BedStatus.Available }).Entity : await _db.IpdBeds.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return ServiceResult<int>.Fail("Bed not found.");
        if (id is not null && b.CurrentAdmissionId is not null && (b.RoomId != i.RoomId || !i.IsActive))
            return ServiceResult<int>.Fail("An occupied bed cannot be moved or deactivated.");
        if (id is null && await _db.IpdBeds.CountAsync(x => x.RoomId == room.Id, ct) >= room.BedCapacity && room.BedCapacity > 0)
            return ServiceResult<int>.Fail($"Room {room.Name} already has its {room.BedCapacity} bed(s). Increase the room's bed capacity first.");
        b.Code = code; b.RoomId = room.Id; b.WardId = room.WardId; b.Type = i.Type; b.EquipmentId = T(i.EquipmentId, 60); b.IsActive = i.IsActive;
        b.SortOrder = i.SortOrder; b.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdBedSaved, "IpdBed", code, $"{i.Type} {code} {(id is null ? "created" : "updated")} in room {room.Name}" + (b.EquipmentId is null ? "" : $" (equipment {b.EquipmentId})"));
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(b.Id);
    }

    public async Task<ServiceResult> SetBedStatusAsync(int bedId, BedStatus status, string? note, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(status) || status == BedStatus.Occupied) return ServiceResult.Fail("A bed becomes occupied only by allocating it to an admission.");
        var b = await _db.IpdBeds.FirstOrDefaultAsync(x => x.Id == bedId, ct);
        if (b is null) return ServiceResult.Fail("Bed not found.");
        if (b.CurrentAdmissionId is not null || b.Status == BedStatus.Occupied) return ServiceResult.Fail("The bed is occupied. Transfer or discharge the patient first.");
        if (b.Status == status) return ServiceResult.Ok();
        if (status is BedStatus.UnderMaintenance or BedStatus.TemporarilyUnavailable && T(note, 200) is null) return ServiceResult.Fail("Enter the reason.");
        _db.IpdBedStatusChanges.Add(new IpdBedStatusChange { BedId = b.Id, From = b.Status, To = status, Note = T(note, 200), CreatedUtc = _clock.UtcNow, Username = _ctx.Username });
        _audit.Add(AuditActions.IpdBedStatusChanged, "IpdBed", b.Code, $"Bed {b.Code}: {b.Status} → {status}{(T(note, 200) is { } n ? " – " + n : "")}");
        b.Status = status;
        b.StatusNote = T(note, 200);
        b.UpdatedUtc = _clock.UtcNow;
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The bed was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Configurable clinical forms
    // =====================================================================================

    public async Task<IReadOnlyList<ClinicalFormField>> FieldsAsync(RecordKind? kind, bool activeOnly, CancellationToken ct = default) =>
        await _db.ClinicalFormFields.AsNoTracking().Where(f => (kind == null || f.Kind == kind) && (!activeOnly || f.IsActive))
            .OrderBy(f => f.Kind).ThenBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveFieldAsync(int? id, FieldInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var key = T(i.Key, 40)?.ToLowerInvariant().Replace(' ', '_');
        var label = T(i.Label, 100);
        if (key is null || !key.All(c => char.IsLetterOrDigit(c) || c == '_')) errors.Add("Field key is required (letters, digits, _).");
        if (label is null) errors.Add("Field label is required.");
        if (!Enum.IsDefined(i.Kind) || !Enum.IsDefined(i.Type)) errors.Add("Select the form and field type.");
        if (i.Type == FieldType.Choice && (T(i.Choices, 500)?.Split('|', StringSplitOptions.RemoveEmptyEntries).Length ?? 0) < 2) errors.Add("Enter at least two choices separated by '|'.");
        if (i.AlertLow is not null || i.AlertHigh is not null)
        {
            if (i.Type != FieldType.Number) errors.Add("Alert limits apply to number fields only.");
            if (T(i.AlertSource, 200) is null) errors.Add("Alert limits must name the approved hospital protocol they come from.");
            if (i.AlertLow is decimal lo && i.AlertHigh is decimal hi && lo >= hi) errors.Add("The low limit must be below the high limit.");
        }
        if (key is not null && await _db.ClinicalFormFields.AnyAsync(f => f.Id != id && f.Kind == i.Kind && f.Key == key, ct)) errors.Add("This form already has a field with this key.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var f = id is null ? _db.ClinicalFormFields.Add(new ClinicalFormField()).Entity : await _db.ClinicalFormFields.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return ServiceResult<int>.Fail("Field not found.");
        if (id is not null && (f.Key != key || f.Kind != i.Kind) && await _db.ClinicalRecords.AnyAsync(r => r.Kind == f.Kind, ct))
            return ServiceResult<int>.Fail("The key of a field that is already used in records cannot be changed – add a new field instead.");
        var old = id is null ? null : new { f.Label, f.Type, f.IsRequired, f.IsActive, f.AlertLow, f.AlertHigh, f.AlertSource };
        f.Kind = i.Kind; f.Key = key!; f.Label = label!; f.Section = T(i.Section, 60); f.Type = i.Type; f.Unit = T(i.Unit, 20); f.Choices = T(i.Choices, 500);
        f.IsRequired = i.IsRequired; f.IsActive = i.IsActive; f.SortOrder = i.SortOrder; f.AlertLow = i.AlertLow; f.AlertHigh = i.AlertHigh;
        f.AlertSource = i.AlertLow is null && i.AlertHigh is null ? null : T(i.AlertSource, 200); f.UpdatedUtc = _clock.UtcNow; f.UpdatedBy = _ctx.Username;
        _audit.Add(AuditActions.IpdFormFieldSaved, "ClinicalFormField", $"{f.Kind}.{f.Key}", $"Form field {f.Label} ({f.Kind}) {(id is null ? "added" : "changed")}" +
            (f.AlertLow is not null || f.AlertHigh is not null ? $" – alert limits {f.AlertLow}–{f.AlertHigh} per {f.AlertSource}" : ""), old,
            new { f.Label, f.Type, f.IsRequired, f.IsActive, f.AlertLow, f.AlertHigh, f.AlertSource });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(f.Id);
    }

    // =====================================================================================
    // Settings
    // =====================================================================================

    public async Task<IpdSettings> SettingsAsync(CancellationToken ct = default) => await _db.IpdSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new IpdSettings();

    public async Task<ServiceResult> SaveSettingsAsync(IpdSettingsInput i, uint rowVersion, CancellationToken ct = default)
    {
        if (i.LetterheadTopMarginMm is < 0 or > 120) return ServiceResult.Fail("Letterhead space must be between 0 and 120 mm.");
        if (i.BedChargePolicy is { } bp && !Enum.IsDefined(bp)) return ServiceResult.Fail("Select the bed charging policy.");
        if (i.HalfDayHours is < 1 or > 23) return ServiceResult.Fail("Half-day hours must be between 1 and 23.");
        if (i.AdmissionServiceItemId is int s && !await _db.ServiceItems.AnyAsync(x => x.Id == s && x.IsActive, ct)) return ServiceResult.Fail("Select an active admission service.");
        var x = await _db.IpdSettings.FirstAsync(ct);
        _db.Entry(x).Property(p => p.RowVersion).OriginalValue = rowVersion;
        var old = new { x.AdmissionServiceItemId, x.AllowDirectAdmission, x.RequireBillClearanceForDischarge, x.PrintHeader, x.RequireMarConfirmations, x.BedChargePolicy, x.BedChargeCutoff, x.HalfDayHours };
        x.BedChargePolicy = i.BedChargePolicy ?? x.BedChargePolicy; x.BedChargeCutoff = i.BedChargeCutoff ?? x.BedChargeCutoff; x.HalfDayHours = i.HalfDayHours ?? x.HalfDayHours;
        x.AdmissionServiceItemId = i.AdmissionServiceItemId; x.AllowDirectAdmission = i.AllowDirectAdmission; x.RequireBillClearanceForDischarge = i.RequireBillClearanceForDischarge;
        x.PrintHeader = i.PrintHeader; x.LetterheadTopMarginMm = i.LetterheadTopMarginMm; x.FormFooter = T(i.FormFooter, 300); x.RequireMarConfirmations = i.RequireMarConfirmations;
        x.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdSettingsChanged, "IpdSettings", "1", "IPD settings changed", old,
            new { x.AdmissionServiceItemId, x.AllowDirectAdmission, x.RequireBillClearanceForDischarge, x.PrintHeader, x.RequireMarConfirmations, x.BedChargePolicy, x.BedChargeCutoff, x.HalfDayHours });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("Settings were changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Seed: settings, starter wards (with a room each, no beds) and the default form fields.
    // Default fields contain NO alert limits – those are set only by an authorised clinical administrator.
    // =====================================================================================

    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        if (!await db.IpdSettings.AnyAsync(ct)) db.IpdSettings.Add(new IpdSettings { Id = 1, UpdatedUtc = now });
        await db.SaveChangesAsync(ct);
        if (!await db.ClinicalFormFields.AnyAsync(ct))
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
        if (await db.IpdWards.AnyAsync(ct) || !await db.Departments.AnyAsync(ct)) return;
        // Departments used by the inpatient units (added once, after the setup wizard created the standard departments).
        foreach (var (name, code) in new[] { ("IPD", "IPD"), ("Gynae", "GYN"), ("NICU", "NICU") })
            if (!await db.Departments.AnyAsync(d => d.Name == name, ct))
                db.Departments.Add(new Department { Name = name, Code = code, Description = "Inpatient unit (Phase 7)", IsActive = true, CreatedUtc = now, UpdatedUtc = now });
        await db.SaveChangesAsync(ct);
        var depts = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Name, d => d.Id, ct);
        int? D(string n) => depts.TryGetValue(n, out var v) ? v : null;
        var sortW = 0;
        foreach (var (code, name, type, dept, roomType) in new[]
                 {
                     ("MGW", "Male General Ward", WardType.MaleGeneral, "IPD", RoomType.General),
                     ("FGW", "Female General Ward", WardType.FemaleGeneral, "IPD", RoomType.General),
                     ("CHW", "Children Ward", WardType.Children, "IPD", RoomType.General),
                     ("GYN", "Gynae Ward", WardType.Gynae, "Gynae", RoomType.General),
                     ("LR", "Labour Room", WardType.LabourRoom, "Gynae", RoomType.LabourRoom),
                     ("PNW", "Postnatal Ward", WardType.Postnatal, "Gynae", RoomType.General),
                     ("NICU", "NICU", WardType.Nicu, "NICU", RoomType.Nicu),
                 })
        {
            var w = new IpdWard { Code = code, Name = name, Type = type, DepartmentId = D(dept) ?? D("IPD"), Capacity = 0, IsActive = true, SortOrder = ++sortW * 10, UpdatedUtc = now };
            db.IpdWards.Add(w);
            await db.SaveChangesAsync(ct);
            db.IpdRooms.Add(new IpdRoom { WardId = w.Id, Name = "Room 1", Type = roomType, BedCapacity = 10, IsActive = true });
        }
        await db.SaveChangesAsync(ct);
    }

    private static ClinicalFormField F(string key, string label, FieldType type = FieldType.Text, string? unit = null, string? choices = null, string? section = null, bool required = false) =>
        new() { Key = key, Label = label, Type = type, Unit = unit, Choices = choices, Section = section, IsRequired = required, IsActive = true };
    private static ClinicalFormField N(string key, string label, string? unit = null, string? section = null) => F(key, label, FieldType.Number, unit, section: section);
    private static ClinicalFormField L(string key, string label, string? section = null, bool required = false) => F(key, label, FieldType.LongText, section: section, required: required);

    private static IEnumerable<(RecordKind, ClinicalFormField[])> DefaultForms()
    {
        ClinicalFormField[] Vitals(string section = "Vital signs") => new[]
        {
            N("temp", "Temperature", "°C", section), N("pulse", "Pulse", "/min", section), N("resp", "Respiratory rate", "/min", section),
            N("bp_sys", "BP systolic", "mmHg", section), N("bp_dia", "BP diastolic", "mmHg", section), N("spo2", "SpO₂", "%", section),
        };
        yield return (RecordKind.ProgressNote, new[] { L("condition", "Patient condition"), L("assessment", "Clinical assessment"), L("plan", "Treatment plan / changes"), L("investigations", "Investigation results reviewed") });
        yield return (RecordKind.Vitals, Vitals().Concat(new[] { N("pain", "Pain score (0–10)"), N("weight", "Weight", "kg"), N("intake_ml", "Fluid intake", "ml"), N("output_ml", "Urine output", "ml") }).ToArray());
        yield return (RecordKind.NursingAssessment, Vitals().Concat(new[]
        {
            N("pain", "Pain score (0–10)"), F("mobility", "Mobility", FieldType.Choice, choices: "Independent|Needs assistance|Bed-bound"),
            F("nutrition", "Nutrition"), F("hygiene", "Hygiene"), F("skin", "Skin assessment"), L("condition", "Patient condition"), L("care_plan", "Nursing care plan"),
        }).ToArray());
        yield return (RecordKind.NursingNote, new[] { L("condition", "Patient condition"), L("observations", "Observations"), L("interventions", "Nursing interventions"), L("response", "Patient response"), L("escalation", "Escalation / referral") });
        yield return (RecordKind.GynaeHistory, new[]
        {
            F("marital_status", "Marital status", FieldType.Choice, choices: "Married|Unmarried|Widowed|Divorced|Not recorded"),
            L("menstrual", "Menstrual history"), L("obstetric", "Obstetric history (pregnancies, deliveries)"), L("losses", "Miscarriages / pregnancy losses"),
            L("gynae", "Gynaecological history"), L("operations", "Previous operations"), F("allergies", "Allergies"), L("medical", "Relevant medical history"),
        });
        yield return (RecordKind.GynaeAssessment, new[] { L("complaints", "Presenting complaints", required: true), L("history", "Clinical history") }
            .Concat(Vitals()).Concat(new[] { L("examination", "Examination findings"), L("diagnosis", "Provisional diagnosis"), L("investigations", "Investigations requested"), L("plan", "Treatment plan") }).ToArray());
        yield return (RecordKind.AncVisit, new[]
        {
            F("ga", "Gestational age (as assessed)"), N("weight", "Weight", "kg"), N("bp_sys", "BP systolic", "mmHg"), N("bp_dia", "BP diastolic", "mmHg"),
            N("pulse", "Pulse", "/min"), N("temp", "Temperature", "°C"), L("complaints", "Complaints"), L("examination", "Examination findings"),
            N("fundal_height", "Fundal height", "cm"), N("fhr", "Fetal heart rate", "/min"), L("investigations", "Investigation results"), L("ultrasound", "Ultrasound information"),
            L("assessment", "Clinical assessment"), L("plan", "Plan"), F("next_visit", "Follow-up date"),
        });
        yield return (RecordKind.LabourAssessment, Vitals("Maternal").Concat(new[]
        {
            N("fhr", "Fetal heart rate", "/min", "Fetal"), F("presentation", "Presentation", section: "Fetal"), N("cervix", "Cervical dilatation", "cm", "Labour"),
            L("findings", "Examination findings"), L("assessment", "Initial assessment"),
        }).ToArray());
        yield return (RecordKind.LabourObservation, new[]
        {
            N("cervix", "Cervical dilatation", "cm", "Labour progress"), N("descent", "Descent (fifths palpable)", "/5", "Labour progress"),
            N("contractions", "Contractions per 10 min", null, "Labour progress"), N("contraction_secs", "Contraction duration", "sec", "Labour progress"),
            N("fhr", "Fetal heart rate", "/min", "Fetal"), F("membranes", "Membranes", FieldType.Choice, choices: "Intact|Ruptured", section: "Fetal"),
            F("liquor", "Liquor", FieldType.Choice, choices: "Intact|Clear|Meconium-stained|Blood-stained|Absent", section: "Fetal"),
            F("moulding", "Moulding", FieldType.Choice, choices: "0|+|++|+++", section: "Fetal"),
            N("pulse", "Pulse", "/min", "Maternal"), N("bp_sys", "BP systolic", "mmHg", "Maternal"), N("bp_dia", "BP diastolic", "mmHg", "Maternal"),
            N("temp", "Temperature", "°C", "Maternal"), N("resp", "Respiratory rate", "/min", "Maternal"), N("urine_ml", "Urine output", "ml", "Maternal"),
            F("oxytocin", "Oxytocin / drugs / IV fluids (as ordered)", section: "Maternal"),
        });
        yield return (RecordKind.PostnatalAssessment, Vitals().Concat(new[]
        {
            F("uterus", "Uterine assessment"), F("lochia", "Bleeding / lochia"), N("pain", "Pain score (0–10)"),
            F("breastfeeding", "Breastfeeding", FieldType.Choice, choices: "Established|Some difficulty|Not started|Not breastfeeding"),
            F("mobility", "Mobility"), F("urination", "Urination / bowel"), F("wound", "Wound / perineal care"), L("treatment", "Medication / treatment given"),
            L("nursing", "Nursing notes"), F("ready", "Discharge readiness (as assessed)", FieldType.Choice, choices: "Not yet|Ready for discharge"),
        }).ToArray());
        yield return (RecordKind.NicuAssessment, new[]
        {
            N("temp", "Temperature", "°C"), N("hr", "Heart rate", "/min"), N("rr", "Respiratory rate", "/min"), N("spo2", "Oxygen saturation", "%"),
            N("glucose", "Blood glucose", "mg/dL"), N("weight_g", "Weight", "g"), F("feeding", "Feeding status"), L("condition", "General condition"),
        });
        yield return (RecordKind.NicuObservation, new[]
        {
            N("temp", "Temperature", "°C"), N("hr", "Heart rate", "/min"), N("rr", "Respiratory rate", "/min"), N("spo2", "Oxygen saturation", "%"),
            N("glucose", "Blood glucose", "mg/dL"), N("weight_g", "Weight", "g"), F("observation", "Other observation"),
        });
        yield return (RecordKind.NicuFeeding, new[]
        {
            F("method", "Feeding method", FieldType.Choice, choices: "Breastfeeding|Expressed breast milk|Formula|Tube feeding|IV fluids only|Nil by mouth"),
            N("feed_ml", "Feed volume", "ml"), N("iv_ml", "IV fluid volume", "ml"), F("iv_fluid", "IV fluid (as ordered)"),
            N("urine_ml", "Urine output", "ml"), N("stool", "Stools (count)"), F("vomiting", "Vomiting / aspirate"),
        });
        yield return (RecordKind.NicuCare, new[]
        {
            F("oxygen", "Oxygen / respiratory support", FieldType.Choice, choices: "None|Nasal cannula|Head box|CPAP|Ventilator"), N("o2_flow", "O₂ flow", "L/min"),
            N("fio2", "FiO₂", "%"), F("phototherapy", "Phototherapy", FieldType.YesNo), N("incubator_temp", "Incubator temperature", "°C"), N("humidity", "Humidity", "%"),
            L("observations", "Nursing observations"),
        });
        yield return (RecordKind.NicuProgress, new[]
        {
            L("assessment", "Clinical assessment", required: true), N("weight_g", "Current weight", "g"), L("vitals", "Vital observations"), L("feeding", "Feeding"),
            L("intake_output", "Intake / output"), L("investigations", "Relevant investigations"), L("plan", "Treatment plan"),
        });
    }
}
