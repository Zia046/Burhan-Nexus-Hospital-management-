using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ot;

public sealed class ConsentInput
{
    public int ConsentTypeId { get; set; }
    public string? GivenBy { get; set; }
    public string? Relation { get; set; }
    public DateTime? ConsentLocal { get; set; }
    public ConsentStatus Status { get; set; } = ConsentStatus.Obtained;
    public string? ResponsibleStaff { get; set; }
    public string? Witness { get; set; }
    public string? Notes { get; set; }
}

public sealed class DrugInput
{
    public string Phase { get; set; } = "Anesthesia";
    public OtDrugKind Kind { get; set; } = OtDrugKind.Drug;
    public string? Name { get; set; }
    public string? Dose { get; set; }
    public string? Route { get; set; }
    public DateTime? GivenLocal { get; set; }
    public string? GivenBy { get; set; }
    public string? Notes { get; set; }
}

public sealed class NoteInput
{
    public string? Positioning { get; set; }
    public string? Site { get; set; }
    public string? ProcedurePerformed { get; set; }
    public string? OperativeFindings { get; set; }
    public string? Technique { get; set; }
    public int? BloodLossMl { get; set; }
    public string? IvFluids { get; set; }
    public string? DrugsAdministered { get; set; }
    public string? Specimens { get; set; }
    public string? Implants { get; set; }
    public string? Complications { get; set; }
    public string? AdditionalProcedures { get; set; }
    public string? Observations { get; set; }
    public string? PreOpDiagnosis { get; set; }
    public string? PostOpDiagnosis { get; set; }
    public string? PostOpInstructions { get; set; }
    public string? SurgeonNotes { get; set; }
    public string? SurgeonName { get; set; }
}

public sealed class SpecimenInput
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public DateTime? CollectedLocal { get; set; }
    public string? CollectedBy { get; set; }
    public string? Destination { get; set; }
    public long? LabOrderId { get; set; }
    public string? Remarks { get; set; }
}

public sealed class RecoveryInput
{
    public RecoveryStatus Status { get; set; }
    public string? Nurse { get; set; }
    public string? Destination { get; set; }
    public string? HandoverStaff { get; set; }
    public string? HandoverNotes { get; set; }
    public string? DecisionBy { get; set; }
    public string? Notes { get; set; }
}

public interface IOtClinicalService
{
    Task<ServiceResult<long>> AddConsentAsync(long surgeryId, ConsentInput input, CancellationToken ct = default);
    Task<ServiceResult> SetConsentStatusAsync(long consentId, ConsentStatus status, string? note, CancellationToken ct = default);
    Task<ServiceResult<long>> UploadAsync(long surgeryId, long? consentId, string kind, string? title, string? originalName, Stream content, CancellationToken ct = default);

    Task<ServiceResult> AnswerChecklistAsync(long surgeryId, ChecklistStage stage, IDictionary<int, ChecklistAnswer> answers, IDictionary<int, string?> remarks, CancellationToken ct = default);
    Task<ServiceResult> CompleteStageAsync(long surgeryId, ChecklistStage stage, string? remarks, bool exception, string? exceptionReason, bool canAuthorize, CancellationToken ct = default);

    Task<ServiceResult> AnesthesiaTimesAsync(long surgeryId, DateTime? startLocal, DateTime? endLocal, string? provider, CancellationToken ct = default);
    Task<ServiceResult<long>> AddDrugAsync(long surgeryId, DrugInput input, CancellationToken ct = default);

    Task<ServiceResult> StartAsync(long surgeryId, DateTime? startLocal, bool emergencyException, string? reason, bool canAuthorize, CancellationToken ct = default);
    Task<ServiceResult> EndAsync(long surgeryId, DateTime? endLocal, bool emergencyException, string? reason, bool canAuthorize, CancellationToken ct = default);

    Task<ServiceResult<long>> SaveNoteAsync(long surgeryId, NoteInput input, CancellationToken ct = default);
    Task<ServiceResult> FinalizeNoteAsync(long surgeryId, CancellationToken ct = default);
    Task<ServiceResult<long>> AmendNoteAsync(long surgeryId, NoteInput input, string? reason, CancellationToken ct = default);

    Task<ServiceResult<long>> AddSpecimenAsync(long surgeryId, SpecimenInput input, CancellationToken ct = default);
    Task<ServiceResult> SpecimenStatusAsync(long specimenId, SpecimenStatus status, string? by, string? remarks, CancellationToken ct = default);

    Task<ServiceResult> ConsumeAsync(long surgeryId, int medicineId, int quantity, long? batchId, string? note, bool canSelectBatch, CancellationToken ct = default);
    Task<ServiceResult> ReturnAsync(long consumptionId, int quantity, string? note, CancellationToken ct = default);

    Task<ServiceResult> RecoveryAsync(long surgeryId, RecoveryInput input, CancellationToken ct = default);
}

/// <summary>
/// Pre-operative consent, safety checklists, anesthesia, intra-operative record, operation notes, specimens, major OT consumption
/// and recovery. The system records clinical decisions – it never takes them: no thresholds, no automatic readiness or discharge.
/// </summary>
public sealed class OtClinicalService : IOtClinicalService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly ISurgeryService _surgeries;
    private readonly RadiologyFileStore _files;
    private readonly IOtSetupService _setup;

    public OtClinicalService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, ISurgeryService surgeries, RadiologyFileStore files, IOtSetupService setup)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _surgeries = surgeries;
        _files = files;
        _setup = setup;
    }

    private static string? T(string? v, int max) => OtSetupService.T(v, max);
    private async Task<DateTime?> UtcAsync(DateTime? local) => local is DateTime l ? await _clock.LocalToUtcAsync(l) : null;
    private async Task<Surgery?> OpenAsync(long id, CancellationToken ct) => await _db.Surgeries.Include(s => s.Patient).FirstOrDefaultAsync(s => s.Id == id, ct);

    // ===================================================================== consent
    public async Task<ServiceResult<long>> AddConsentAsync(long surgeryId, ConsentInput i, CancellationToken ct = default)
    {
        var g = await OpenAsync(surgeryId, ct);
        if (g is null) return ServiceResult<long>.Fail("Surgery not found.");
        if (g.Status == SurgeryStatus.Cancelled) return ServiceResult<long>.Fail("This surgery was cancelled.");
        var type = await _db.OtConsentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == i.ConsentTypeId && t.IsActive, ct);
        var errors = new List<string>();
        if (type is null) errors.Add("Select the consent type.");
        if (T(i.GivenBy, 150) is null) errors.Add("Enter the patient / authorised representative who gave the consent.");
        if (!Enum.IsDefined(i.Status)) errors.Add("Select the consent status.");
        var when = await UtcAsync(i.ConsentLocal) ?? _clock.UtcNow;
        if (when > _clock.UtcNow.AddMinutes(5)) errors.Add("The consent time cannot be in the future.");
        if (T(i.ResponsibleStaff, 150) is null) errors.Add("Enter the responsible staff member.");
        if (errors.Count > 0) return ServiceResult<long>.Fail(errors.ToArray());
        var c = new SurgeryConsent
        {
            SurgeryId = g.Id, ConsentTypeId = type!.Id, ConsentTypeName = type.Name, GivenBy = T(i.GivenBy, 150)!, Relation = T(i.Relation, 60), ConsentUtc = when, Status = i.Status,
            ResponsibleStaff = T(i.ResponsibleStaff, 150), Witness = T(i.Witness, 150), Notes = T(i.Notes, 1000),
            CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username, UpdatedUtc = _clock.UtcNow, UpdatedBy = _ctx.Username
        };
        _db.SurgeryConsents.Add(c);
        await _db.SaveChangesAsync(ct);
        _surgeries.Event(g.Id, "Consent", type.Name, null, i.Status.ToString(), $"by {c.GivenBy} {c.Relation}");
        _audit.Add(AuditActions.OtConsentRecorded, "Surgery", g.Number, $"{type.Name} ({i.Status}) documented for {g.Number} – given by {c.GivenBy}, staff {c.ResponsibleStaff}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(c.Id);
    }

    public async Task<ServiceResult> SetConsentStatusAsync(long consentId, ConsentStatus status, string? note, CancellationToken ct = default)
    {
        note = T(note, 300);
        if (note is null) return ServiceResult.Fail("Enter the reason for the change.");
        var c = await _db.SurgeryConsents.FirstOrDefaultAsync(x => x.Id == consentId, ct);
        if (c is null) return ServiceResult.Fail("Consent record not found.");
        if (!Enum.IsDefined(status) || c.Status == status) return ServiceResult.Fail("Select a different status.");
        var g = await _db.Surgeries.AsNoTracking().FirstAsync(s => s.Id == c.SurgeryId, ct);
        var from = c.Status;
        c.Status = status; c.UpdatedUtc = _clock.UtcNow; c.UpdatedBy = _ctx.Username;
        c.Notes = T($"{c.Notes}\n[{_clock.UtcNow:dd MMM HH:mm} UTC {_ctx.Username}] {from} → {status}: {note}", 1000);
        _surgeries.Event(g.Id, "Consent", c.ConsentTypeName, from.ToString(), status.ToString(), note);
        _audit.Add(AuditActions.OtConsentChanged, "Surgery", g.Number, $"{c.ConsentTypeName} of {g.Number}: {from} → {status} – {note}", new { Status = from.ToString() }, new { Status = status.ToString() });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The consent was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<long>> UploadAsync(long surgeryId, long? consentId, string kind, string? title, string? originalName, Stream content, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult<long>.Fail("Surgery not found.");
        if (consentId is long cid && !await _db.SurgeryConsents.AnyAsync(c => c.Id == cid && c.SurgeryId == surgeryId, ct)) return ServiceResult<long>.Fail("Consent record not found.");
        kind = kind is "Consent" or "PostOpInstruction" or "Other" ? kind : "Other";
        var max = (await _setup.SettingsAsync(ct)).MaxUploadMb * 1024L * 1024L;
        var saved = await _files.SaveAsync(content, max, _clock.UtcNow, ct);
        if (saved is null) return ServiceResult<long>.Fail($"The file is larger than {max / 1024 / 1024} MB.");
        var (path, type, size, sha) = saved.Value;
        if (size == 0 || type is not ("application/pdf" or "image/png" or "image/jpeg")) return ServiceResult<long>.Fail("Upload a PDF, JPG or PNG scan.");
        var d = new OtDocument
        {
            SurgeryId = surgeryId, ConsentId = consentId, Kind = kind, Title = T(title, 150) ?? (kind == "Consent" ? "Signed consent" : kind == "PostOpInstruction" ? "Handwritten post-operative instructions" : "Document"),
            StoredPath = path, ContentType = type, Size = size, Sha256 = sha, OriginalName = T(Path.GetFileName(originalName ?? ""), 200), UploadedUtc = _clock.UtcNow, UploadedBy = _ctx.Username
        };
        _db.OtDocuments.Add(d);
        await _db.SaveChangesAsync(ct);
        _surgeries.Event(surgeryId, "Document", "Uploaded", null, d.Title, $"{type}, {size} bytes");
        _audit.Add(AuditActions.OtDocumentUploaded, "Surgery", g.Number, $"{d.Title} uploaded for {g.Number} ({type}, {size} bytes, SHA-256 {sha[..12]}…)");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(d.Id);
    }

    // ===================================================================== checklists (pre-op + sign in / time out / sign out)
    public async Task<ServiceResult> AnswerChecklistAsync(long surgeryId, ChecklistStage stage, IDictionary<int, ChecklistAnswer> answers, IDictionary<int, string?> remarks, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is SurgeryStatus.Cancelled) return ServiceResult.Fail("This surgery was cancelled.");
        if (await _db.SurgeryChecklistStages.AnyAsync(s => s.SurgeryId == surgeryId && s.Stage == stage, ct)) return ServiceResult.Fail("This checklist stage is already completed.");
        var items = await _db.OtChecklistItems.AsNoTracking().Where(x => x.Stage == stage && x.IsActive).ToListAsync(ct);
        var latest = await LatestAnswersAsync(surgeryId, stage, ct);
        var n = 0;
        foreach (var item in items)
        {
            if (!answers.TryGetValue(item.Id, out var a) || !Enum.IsDefined(a)) continue;
            var rem = remarks.TryGetValue(item.Id, out var r) ? T(r, 300) : null;
            if (a == ChecklistAnswer.NotApplicable && item.IsMandatory && rem is null) return ServiceResult.Fail($"“{item.Label}”: a remark is required when a mandatory item is not applicable.");
            if (latest.TryGetValue(item.Id, out var prev) && prev.Answer == a && prev.Remarks == rem) continue;
            _db.SurgeryChecklistEntries.Add(new SurgeryChecklistEntry
            {
                SurgeryId = surgeryId, ItemId = item.Id, Stage = stage, ItemLabel = item.Label, Answer = a, Remarks = rem, RecordedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username
            });
            n++;
        }
        if (n == 0) return ServiceResult.Ok();
        _audit.Add(AuditActions.OtChecklistAnswered, "Surgery", g.Number, $"{stage} checklist of {g.Number}: {n} item(s) recorded");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    internal async Task<Dictionary<int, SurgeryChecklistEntry>> LatestAnswersAsync(long surgeryId, ChecklistStage stage, CancellationToken ct) =>
        (await _db.SurgeryChecklistEntries.AsNoTracking().Where(e => e.SurgeryId == surgeryId && e.Stage == stage).OrderBy(e => e.Id).ToListAsync(ct))
        .GroupBy(e => e.ItemId).ToDictionary(x => x.Key, x => x.Last());

    public async Task<ServiceResult> CompleteStageAsync(long surgeryId, ChecklistStage stage, string? remarks, bool exception, string? exceptionReason, bool canAuthorize, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is SurgeryStatus.Cancelled or SurgeryStatus.Postponed) return ServiceResult.Fail($"This surgery is {g.Status.ToString().ToLowerInvariant()}.");
        if (await _db.SurgeryChecklistStages.AnyAsync(s => s.SurgeryId == surgeryId && s.Stage == stage, ct)) return ServiceResult.Fail("This checklist stage is already completed.");
        var order = new[] { ChecklistStage.SignIn, ChecklistStage.TimeOut, ChecklistStage.SignOut };
        var idx = Array.IndexOf(order, stage);
        if (idx > 0 && !await _db.SurgeryChecklistStages.AnyAsync(s => s.SurgeryId == surgeryId && s.Stage == order[idx - 1], ct))
            return ServiceResult.Fail($"Complete the {order[idx - 1]} stage first.");
        var items = await _db.OtChecklistItems.AsNoTracking().Where(x => x.Stage == stage && x.IsActive && x.IsMandatory).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var latest = await LatestAnswersAsync(surgeryId, stage, ct);
        var pending = items.Where(x => !latest.TryGetValue(x.Id, out var a) || a.Answer == ChecklistAnswer.Pending).Select(x => x.Label).ToList();
        exceptionReason = T(exceptionReason, 500);
        if (pending.Count > 0)
        {
            // Never silently bypassed: completing with pending mandatory items is an exception that needs authority and a reason.
            if (!exception) return ServiceResult.Fail(new[] { $"Mandatory items are still pending in {stage}:" }.Concat(pending).ToArray());
            if (!canAuthorize) return ServiceResult.Fail("Only a user with “Authorize Safety Exceptions” can complete a stage with pending mandatory items.");
            if (exceptionReason is null) return ServiceResult.Fail("Record the reason for the exception (e.g. the emergency).");
        }
        _db.SurgeryChecklistStages.Add(new SurgeryChecklistStage
        {
            SurgeryId = surgeryId, Stage = stage, CompletedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username, Remarks = T(remarks, 300),
            IsException = pending.Count > 0, ExceptionReason = pending.Count > 0 ? exceptionReason : null, PendingItems = pending.Count > 0 ? T(string.Join("; ", pending), 500) : null
        });
        _surgeries.Event(surgeryId, "Checklist", stage.ToString(), null, pending.Count > 0 ? "Completed with exception" : "Completed", pending.Count > 0 ? $"{exceptionReason} – pending: {string.Join("; ", pending)}" : T(remarks, 300));
        _audit.Add(pending.Count > 0 ? AuditActions.OtSafetyException : AuditActions.OtChecklistCompleted, "Surgery", g.Number,
            pending.Count > 0 ? $"{stage} of {g.Number} completed by exception ({exceptionReason}); pending: {string.Join("; ", pending)}" : $"{stage} checklist of {g.Number} completed");
        if (stage == ChecklistStage.PreOp && g.Status is SurgeryStatus.Scheduled)
        {
            var tracked = await _db.Surgeries.FirstAsync(s => s.Id == surgeryId, ct);
            tracked.Status = SurgeryStatus.PreOp; tracked.UpdatedUtc = _clock.UtcNow;
            _surgeries.Event(surgeryId, "Status", "Status", SurgeryService.Label(SurgeryStatus.Scheduled), SurgeryService.Label(SurgeryStatus.PreOp), "Pre-operative checklist completed");
        }
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // ===================================================================== anesthesia
    public async Task<ServiceResult> AnesthesiaTimesAsync(long surgeryId, DateTime? startLocal, DateTime? endLocal, string? provider, CancellationToken ct = default)
    {
        var g = await OpenAsync(surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is SurgeryStatus.Cancelled or SurgeryStatus.Postponed) return ServiceResult.Fail($"This surgery is {g.Status.ToString().ToLowerInvariant()}.");
        var s = await UtcAsync(startLocal); var e = await UtcAsync(endLocal);
        if (s > _clock.UtcNow.AddMinutes(5) || e > _clock.UtcNow.AddMinutes(5)) return ServiceResult.Fail("Times cannot be in the future.");
        if (s is not null && e is not null && e < s) return ServiceResult.Fail("Anesthesia end must be after the start.");
        var old = new { g.AnesthesiaStartUtc, g.AnesthesiaEndUtc, g.AnesthesiaProvider };
        g.AnesthesiaStartUtc = s ?? g.AnesthesiaStartUtc; g.AnesthesiaEndUtc = e ?? g.AnesthesiaEndUtc; g.AnesthesiaProvider = T(provider, 150) ?? g.AnesthesiaProvider; g.UpdatedUtc = _clock.UtcNow;
        _surgeries.Event(g.Id, "Anesthesia", "Times", null, $"{g.AnesthesiaStartUtc:HH:mm}–{g.AnesthesiaEndUtc:HH:mm} UTC", g.AnesthesiaProvider);
        _audit.Add(AuditActions.OtRecordAdded, "Surgery", g.Number, $"Anesthesia times of {g.Number} recorded", old, new { g.AnesthesiaStartUtc, g.AnesthesiaEndUtc, g.AnesthesiaProvider });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The surgery was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<long>> AddDrugAsync(long surgeryId, DrugInput i, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult<long>.Fail("Surgery not found.");
        if (g.Status is SurgeryStatus.Cancelled) return ServiceResult<long>.Fail("This surgery was cancelled.");
        var name = T(i.Name, 150);
        if (name is null) return ServiceResult<long>.Fail("Enter the drug / fluid as given.");
        if (!Enum.IsDefined(i.Kind)) return ServiceResult<long>.Fail("Select drug or fluid.");
        var phase = i.Phase is "Anesthesia" or "IntraOp" or "Recovery" ? i.Phase : "Anesthesia";
        var when = await UtcAsync(i.GivenLocal) ?? _clock.UtcNow;
        if (when > _clock.UtcNow.AddMinutes(5)) return ServiceResult<long>.Fail("The time cannot be in the future.");
        var d = new OtDrugEntry
        {
            SurgeryId = surgeryId, Phase = phase, Kind = i.Kind, Name = name, Dose = T(i.Dose, 60), Route = T(i.Route, 40), GivenUtc = when,
            GivenBy = T(i.GivenBy, 150) ?? _ctx.Username, Notes = T(i.Notes, 300), CreatedUtc = _clock.UtcNow, Username = _ctx.Username
        };
        _db.OtDrugEntries.Add(d);
        _audit.Add(AuditActions.OtDrugRecorded, "Surgery", g.Number, $"{phase}: {name} {d.Dose} {d.Route} recorded for {g.Number}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(d.Id);
    }

    // ===================================================================== start / end
    public async Task<ServiceResult> StartAsync(long surgeryId, DateTime? startLocal, bool emergencyException, string? reason, bool canAuthorize, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, "OT-SCHEDULE", ct);
        var g = await OpenAsync(surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is not (SurgeryStatus.Scheduled or SurgeryStatus.PreOp or SurgeryStatus.Ready))
            return ServiceResult.Fail($"A surgery that is {SurgeryService.Label(g.Status).ToLowerInvariant()} cannot be started.");
        if (await _db.Surgeries.AnyAsync(s => s.Id != g.Id && s.TheatreId == g.TheatreId && s.Status == SurgeryStatus.InProgress, ct))
            return ServiceResult.Fail("Another surgery is in progress in this theatre.");
        var stages = await _db.SurgeryChecklistStages.AsNoTracking().Where(s => s.SurgeryId == g.Id).Select(s => s.Stage).ToListAsync(ct);
        var missing = new[] { ChecklistStage.SignIn, ChecklistStage.TimeOut }.Where(s => !stages.Contains(s)).ToList();
        if (!stages.Contains(ChecklistStage.PreOp) && g.Priority != SurgeryPriority.Emergency) missing.Insert(0, ChecklistStage.PreOp);
        reason = T(reason, 500);
        if (missing.Count > 0)
        {
            if (!emergencyException) return ServiceResult.Fail($"Complete the surgical safety checklist before incision: {string.Join(", ", missing)}.");
            if (!canAuthorize) return ServiceResult.Fail("Starting with incomplete safety stages needs “Authorize Safety Exceptions”.");
            if (reason is null) return ServiceResult.Fail("Record the reason for the emergency exception.");
        }
        var start = await UtcAsync(startLocal) ?? _clock.UtcNow;
        if (start > _clock.UtcNow.AddMinutes(5)) return ServiceResult.Fail("The start time cannot be in the future.");
        var from = g.Status;
        g.Status = SurgeryStatus.InProgress; g.ActualStartUtc = start; g.UpdatedUtc = _clock.UtcNow;
        _surgeries.Event(g.Id, "Status", "Surgery started", SurgeryService.Label(from), SurgeryService.Label(g.Status),
            missing.Count > 0 ? $"EMERGENCY EXCEPTION – {reason}; incomplete: {string.Join(", ", missing)}" : null);
        if (missing.Count > 0)
            _audit.Add(AuditActions.OtSafetyException, "Surgery", g.Number, $"Surgery {g.Number} started with incomplete checklist ({string.Join(", ", missing)}) – {reason}");
        _audit.Add(AuditActions.OtSurgeryStarted, "Surgery", g.Number, $"Surgery {g.Number} started at {start:HH:mm} UTC");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The surgery was changed by someone else. Please reload."); }
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> EndAsync(long surgeryId, DateTime? endLocal, bool emergencyException, string? reason, bool canAuthorize, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var g = await OpenAsync(surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status != SurgeryStatus.InProgress) return ServiceResult.Fail("Only a surgery in progress can be ended.");
        var signOut = await _db.SurgeryChecklistStages.AnyAsync(s => s.SurgeryId == g.Id && s.Stage == ChecklistStage.SignOut, ct);
        reason = T(reason, 500);
        if (!signOut)
        {
            if (!emergencyException) return ServiceResult.Fail("Complete the Sign Out checklist before the patient leaves the theatre.");
            if (!canAuthorize) return ServiceResult.Fail("Ending without Sign Out needs “Authorize Safety Exceptions”.");
            if (reason is null) return ServiceResult.Fail("Record the reason for the exception.");
        }
        var end = await UtcAsync(endLocal) ?? _clock.UtcNow;
        if (end > _clock.UtcNow.AddMinutes(5)) return ServiceResult.Fail("The end time cannot be in the future.");
        if (g.ActualStartUtc is DateTime st && end < st) return ServiceResult.Fail("The end time is before the start time.");
        g.Status = SurgeryStatus.Completed; g.ActualEndUtc = end; g.UpdatedUtc = _clock.UtcNow;
        _surgeries.Event(g.Id, "Status", "Surgery ended", SurgeryService.Label(SurgeryStatus.InProgress), SurgeryService.Label(SurgeryStatus.Completed), signOut ? null : $"EXCEPTION – no Sign Out: {reason}");
        if (!await _db.RecoveryEpisodes.AnyAsync(r => r.SurgeryId == g.Id, ct))
            _db.RecoveryEpisodes.Add(new RecoveryEpisode
            {
                SurgeryId = g.Id, PatientId = g.PatientId, Status = RecoveryStatus.Awaiting, CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username, UpdatedUtc = _clock.UtcNow,
                AnesthesiaSummary = T($"{g.AnesthesiaType} {g.AnesthesiaDetail} {g.AnesthesiaProvider}".Trim(), 500)
            });
        if (!signOut) _audit.Add(AuditActions.OtSafetyException, "Surgery", g.Number, $"Surgery {g.Number} ended without Sign Out – {reason}");
        _audit.Add(AuditActions.OtSurgeryEnded, "Surgery", g.Number, $"Surgery {g.Number} ended at {end:HH:mm} UTC – patient awaiting recovery");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The surgery was changed by someone else. Please reload."); }
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // ===================================================================== operation notes (draft → final → amendment)
    private static void Apply(OperativeNote n, NoteInput i)
    {
        n.Positioning = T(i.Positioning, 200); n.Site = T(i.Site, 150); n.ProcedurePerformed = T(i.ProcedurePerformed, 300); n.OperativeFindings = T(i.OperativeFindings, 4000);
        n.Technique = T(i.Technique, 4000); n.BloodLossMl = i.BloodLossMl; n.IvFluids = T(i.IvFluids, 1000); n.DrugsAdministered = T(i.DrugsAdministered, 1000);
        n.Specimens = T(i.Specimens, 1000); n.Implants = T(i.Implants, 1000); n.Complications = T(i.Complications, 1000); n.AdditionalProcedures = T(i.AdditionalProcedures, 1000);
        n.Observations = T(i.Observations, 2000); n.PreOpDiagnosis = T(i.PreOpDiagnosis, 500); n.PostOpDiagnosis = T(i.PostOpDiagnosis, 500);
        n.PostOpInstructions = T(i.PostOpInstructions, 2000); n.SurgeonNotes = T(i.SurgeonNotes, 2000); n.SurgeonName = T(i.SurgeonName, 150);
    }

    public async Task<ServiceResult<long>> SaveNoteAsync(long surgeryId, NoteInput i, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult<long>.Fail("Surgery not found.");
        if (g.Status is not (SurgeryStatus.InProgress or SurgeryStatus.Completed)) return ServiceResult<long>.Fail("The operation note is written once the surgery has started.");
        if (i.BloodLossMl is < 0 or > 20000) return ServiceResult<long>.Fail("Blood loss estimate is not valid.");
        var draft = await _db.OperativeNotes.FirstOrDefaultAsync(n => n.SurgeryId == surgeryId && n.Status == NoteStatus.Draft, ct);
        if (draft is null)
        {
            if (await _db.OperativeNotes.AnyAsync(n => n.SurgeryId == surgeryId && n.Status == NoteStatus.Final, ct))
                return ServiceResult<long>.Fail("The operation note is finalized – use “Amend” (with a reason) to correct it.");
            draft = new OperativeNote { SurgeryId = surgeryId, Version = 1, Status = NoteStatus.Draft, CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username };
            _db.OperativeNotes.Add(draft);
        }
        Apply(draft, i);
        draft.UpdatedUtc = _clock.UtcNow; draft.UpdatedBy = _ctx.Username;
        _audit.Add(AuditActions.OtNoteSaved, "Surgery", g.Number, $"Operation note draft (version {draft.Version}) of {g.Number} saved");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult<long>.Fail("The note was changed by someone else. Please reload."); }
        return ServiceResult<long>.Ok(draft.Id);
    }

    public async Task<ServiceResult> FinalizeNoteAsync(long surgeryId, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        var draft = await _db.OperativeNotes.FirstOrDefaultAsync(n => n.SurgeryId == surgeryId && n.Status == NoteStatus.Draft, ct);
        if (draft is null) return ServiceResult.Fail("There is no draft to finalize.");
        var errors = new List<string>();
        if (draft.ProcedurePerformed is null) errors.Add("Procedure performed is required before finalizing.");
        if (draft.PostOpDiagnosis is null) errors.Add("Post-operative diagnosis is required before finalizing.");
        if (draft.SurgeonName is null) errors.Add("Enter the responsible surgeon.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        draft.Status = NoteStatus.Final; draft.FinalizedUtc = _clock.UtcNow; draft.FinalizedBy = _ctx.Username; draft.UpdatedUtc = _clock.UtcNow; draft.UpdatedBy = _ctx.Username;
        _surgeries.Event(surgeryId, "Operation note", "Finalized", null, $"Version {draft.Version}", draft.AmendReason);
        _audit.Add(AuditActions.OtNoteFinalized, "Surgery", g.Number, $"Operation note version {draft.Version} of {g.Number} finalized by {_ctx.Username}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The note was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<long>> AmendNoteAsync(long surgeryId, NoteInput i, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult<long>.Fail("Enter the reason for the amendment.");
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult<long>.Fail("Surgery not found.");
        if (await _db.OperativeNotes.AnyAsync(n => n.SurgeryId == surgeryId && n.Status == NoteStatus.Draft, ct))
            return ServiceResult<long>.Fail("An amendment draft is already open – finalize it first.");
        var last = await _db.OperativeNotes.AsNoTracking().Where(n => n.SurgeryId == surgeryId && n.Status == NoteStatus.Final).OrderByDescending(n => n.Version).FirstOrDefaultAsync(ct);
        if (last is null) return ServiceResult<long>.Fail("Only a finalized note can be amended.");
        var n = new OperativeNote
        {
            SurgeryId = surgeryId, Version = last.Version + 1, Status = NoteStatus.Draft, AmendsNoteId = last.Id, AmendReason = reason,
            CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username, UpdatedUtc = _clock.UtcNow, UpdatedBy = _ctx.Username
        };
        Apply(n, i);
        _db.OperativeNotes.Add(n);
        _surgeries.Event(surgeryId, "Operation note", "Amendment started", $"Version {last.Version}", $"Version {n.Version}", reason);
        _audit.Add(AuditActions.OtNoteAmended, "Surgery", g.Number, $"Operation note of {g.Number} amended (version {last.Version} → {n.Version}) – {reason}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(n.Id);
    }

    // ===================================================================== specimens
    public async Task<ServiceResult<long>> AddSpecimenAsync(long surgeryId, SpecimenInput i, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult<long>.Fail("Surgery not found.");
        if (g.Status is not (SurgeryStatus.InProgress or SurgeryStatus.Completed)) return ServiceResult<long>.Fail("Specimens are recorded during / after the surgery.");
        var name = T(i.Name, 150);
        if (name is null) return ServiceResult<long>.Fail("Enter the specimen name.");
        if (i.LabOrderId is long lo && !await _db.LabOrders.AnyAsync(o => o.Id == lo && o.PatientId == g.PatientId, ct)) return ServiceResult<long>.Fail("The laboratory order does not belong to this patient.");
        var when = await UtcAsync(i.CollectedLocal) ?? _clock.UtcNow;
        if (when > _clock.UtcNow.AddMinutes(5)) return ServiceResult<long>.Fail("The collection time cannot be in the future.");
        var s = new SurgerySpecimen
        {
            SurgeryId = surgeryId, PatientId = g.PatientId, Name = name, Type = T(i.Type, 60), CollectedUtc = when, CollectedBy = T(i.CollectedBy, 150) ?? _ctx.Username,
            Destination = T(i.Destination, 150), LabOrderId = i.LabOrderId, Remarks = T(i.Remarks, 500), CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username
        };
        _db.SurgerySpecimens.Add(s);
        await _db.SaveChangesAsync(ct);
        _surgeries.Event(surgeryId, "Specimen", "Collected", null, name, s.Destination);
        _audit.Add(AuditActions.OtSpecimenRecorded, "Surgery", g.Number, $"Specimen {name} collected in {g.Number} for {s.Destination}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(s.Id);
    }

    public async Task<ServiceResult> SpecimenStatusAsync(long specimenId, SpecimenStatus status, string? by, string? remarks, CancellationToken ct = default)
    {
        var s = await _db.SurgerySpecimens.FirstOrDefaultAsync(x => x.Id == specimenId, ct);
        if (s is null) return ServiceResult.Fail("Specimen not found.");
        if (!(s.Status == SpecimenStatus.Collected && status == SpecimenStatus.Dispatched || s.Status == SpecimenStatus.Dispatched && status == SpecimenStatus.Received))
            return ServiceResult.Fail($"A {s.Status.ToString().ToLowerInvariant()} specimen cannot be marked {status.ToString().ToLowerInvariant()}.");
        if (status == SpecimenStatus.Received && T(by, 150) is null) return ServiceResult.Fail("Enter who received the specimen at the laboratory.");
        var g = await _db.Surgeries.AsNoTracking().FirstAsync(x => x.Id == s.SurgeryId, ct);
        if (status == SpecimenStatus.Dispatched) { s.DispatchedUtc = _clock.UtcNow; s.DispatchedBy = _ctx.Username; }
        else { s.ReceivedUtc = _clock.UtcNow; s.ReceivedBy = T(by, 150); }
        if (T(remarks, 300) is { } r) s.Remarks = T($"{s.Remarks} {r}", 500);
        var from = s.Status;
        s.Status = status;
        _surgeries.Event(g.Id, "Specimen", s.Name, from.ToString(), status.ToString(), remarks);
        _audit.Add(AuditActions.OtSpecimenStatus, "Surgery", g.Number, $"Specimen {s.Name} of {g.Number}: {from} → {status}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The specimen was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // ===================================================================== major OT consumption (Phase 5 stock, single ledger)
    public async Task<ServiceResult> ConsumeAsync(long surgeryId, int medicineId, int quantity, long? batchId, string? note, bool canSelectBatch, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var g = await _db.Surgeries.Include(s => s.Theatre).FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is not (SurgeryStatus.PreOp or SurgeryStatus.Ready or SurgeryStatus.InProgress or SurgeryStatus.Completed))
            return ServiceResult.Fail("Consumption is recorded for a surgery being prepared, in progress or completed.");
        if (g.Theatre!.StockLocationId is not int loc) return ServiceResult.Fail($"{g.Theatre.Name} has no OT stock location – set it under OT Rooms.");
        var med = await _db.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == medicineId && m.IsActive, ct);
        if (med is null) return ServiceResult.Fail("Select a medicine / consumable from the pharmacy catalogue.");
        if (quantity is < 1 or > 10000) return ServiceResult.Fail("Enter the quantity used.");
        var today = await _clock.TodayAsync();
        var (alloc, err) = await StockEngine.AllocateAsync(_db, loc, med, quantity, batchId, canSelectBatch, today, ct);
        if (err is not null) return ServiceResult.Fail("OT stock: " + err);
        var now = _clock.UtcNow;
        foreach (var x in alloc!)
        {
            var c = new OtConsumption
            {
                SurgeryId = g.Id, PatientId = g.PatientId, LocationId = loc, MedicineId = med.Id, MedicineName = med.DisplayName, BatchId = x.Batch.Id, BatchNumber = x.Batch.BatchNumber,
                Quantity = x.Quantity, Kind = OtConsumptionKind.Use, Note = T(note, 300), CreatedUtc = now, UserId = _ctx.UserId, Username = _ctx.Username
            };
            _db.OtConsumptions.Add(c);
            await _db.SaveChangesAsync(ct);
            // Stock decreases only together with the saved consumption (same transaction).
            if (await StockEngine.PostAsync(_db, new StockMove(InventoryTxnType.OtConsumption, loc, med.Id, x.Batch.Id, -x.Quantity, "OT", c.Id, g.Number, PatientId: g.PatientId,
                    Note: $"{g.ProcedureName}"), today, now, _ctx.UserId, _ctx.Username, ct) is null)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return ServiceResult.Fail("OT stock is not sufficient – it was used by someone else just now.");
            }
        }
        _surgeries.Event(g.Id, "Consumption", "Used", null, $"{med.DisplayName} × {quantity}", note);
        _audit.Add(AuditActions.OtConsumptionRecorded, "Surgery", g.Number, $"{med.DisplayName} × {quantity} used from OT stock for {g.Number}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReturnAsync(long consumptionId, int quantity, string? note, CancellationToken ct = default)
    {
        note = T(note, 300);
        if (note is null) return ServiceResult.Fail("Enter the reason for the return.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"OTCONS|{consumptionId}", ct);
        var use = await _db.OtConsumptions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == consumptionId && c.Kind == OtConsumptionKind.Use, ct);
        if (use is null) return ServiceResult.Fail("Consumption entry not found.");
        var returned = await _db.OtConsumptions.Where(c => c.ReturnOfId == use.Id).SumAsync(c => (int?)c.Quantity, ct) ?? 0;
        if (quantity < 1 || quantity > use.Quantity - returned) return ServiceResult.Fail($"At most {use.Quantity - returned} can be returned.");
        var g = await _db.Surgeries.AsNoTracking().FirstAsync(s => s.Id == use.SurgeryId, ct);
        var now = _clock.UtcNow;
        var r = new OtConsumption
        {
            SurgeryId = use.SurgeryId, PatientId = use.PatientId, LocationId = use.LocationId, MedicineId = use.MedicineId, MedicineName = use.MedicineName, BatchId = use.BatchId,
            BatchNumber = use.BatchNumber, Quantity = quantity, Kind = OtConsumptionKind.Return, ReturnOfId = use.Id, Note = note, CreatedUtc = now, UserId = _ctx.UserId, Username = _ctx.Username
        };
        _db.OtConsumptions.Add(r);
        await _db.SaveChangesAsync(ct);
        await StockEngine.PostAsync(_db, new StockMove(InventoryTxnType.OtReturn, use.LocationId, use.MedicineId, use.BatchId, quantity, "OT", r.Id, g.Number, PatientId: use.PatientId, Note: note),
            await _clock.TodayAsync(), now, _ctx.UserId, _ctx.Username, ct);
        _surgeries.Event(g.Id, "Consumption", "Returned", null, $"{use.MedicineName} × {quantity}", note);
        _audit.Add(AuditActions.OtConsumptionReturned, "Surgery", g.Number, $"{use.MedicineName} × {quantity} returned to OT stock from {g.Number} – {note}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // ===================================================================== recovery / PACU
    public async Task<ServiceResult> RecoveryAsync(long surgeryId, RecoveryInput i, CancellationToken ct = default)
    {
        var r = await _db.RecoveryEpisodes.FirstOrDefaultAsync(x => x.SurgeryId == surgeryId, ct);
        if (r is null) return ServiceResult.Fail("The patient has not left the theatre yet (end the surgery first).");
        if (!r.IsOpen) return ServiceResult.Fail("The recovery episode is closed.");
        if (!Enum.IsDefined(i.Status)) return ServiceResult.Fail("Select the recovery status.");
        var g = await _db.Surgeries.AsNoTracking().FirstAsync(s => s.Id == surgeryId, ct);
        var closing = i.Status is RecoveryStatus.TransferredWard or RecoveryStatus.TransferredIcu or RecoveryStatus.Discharged;
        var errors = new List<string>();
        if (i.Status != RecoveryStatus.Awaiting && r.ArrivalUtc is null && T(i.Nurse, 150) is null && r.Nurse is null) errors.Add("Enter the responsible recovery nurse.");
        if (closing)
        {
            // The disposition is the clinician's decision – never derived from observations by the system.
            if (T(i.DecisionBy, 150) is null) errors.Add("Enter the clinician who decided the transfer / discharge from recovery.");
            if (T(i.HandoverStaff, 150) is null) errors.Add("Enter the receiving / handover staff.");
            if (i.Status != RecoveryStatus.Discharged && T(i.Destination, 150) is null) errors.Add("Enter the transfer destination.");
        }
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var from = r.Status;
        if (r.ArrivalUtc is null && i.Status != RecoveryStatus.Awaiting) r.ArrivalUtc = _clock.UtcNow;
        r.Status = i.Status; r.Nurse = T(i.Nurse, 150) ?? r.Nurse; r.Notes = T(i.Notes, 1000) ?? r.Notes; r.UpdatedUtc = _clock.UtcNow;
        if (closing)
        {
            r.Destination = T(i.Destination, 150); r.HandoverStaff = T(i.HandoverStaff, 150); r.HandoverNotes = T(i.HandoverNotes, 1000);
            r.DecisionBy = T(i.DecisionBy, 150); r.TransferUtc = _clock.UtcNow;
        }
        _surgeries.Event(surgeryId, "Recovery", "Status", from.ToString(), i.Status.ToString(), closing ? $"{r.Destination} – handover to {r.HandoverStaff}, decided by {r.DecisionBy}" : r.Notes);
        _audit.Add(closing ? AuditActions.OtRecoveryTransferred : AuditActions.OtRecoveryChanged, "Surgery", g.Number, $"Recovery of {g.Number}: {from} → {i.Status}" + (closing ? $" ({r.Destination}, decided by {r.DecisionBy})" : ""));
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The recovery record was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }
}
