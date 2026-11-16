using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ipd;

public sealed class PregnancyInput
{
    public DateOnly? Lmp { get; set; }
    public DateOnly? Edd { get; set; }
    public int? Gravida { get; set; }
    public int? Para { get; set; }
    public int? Abortions { get; set; }
    public int? LivingChildren { get; set; }
    public string? PreviousComplications { get; set; }
    public string? MedicalHistory { get; set; }
    public string? RiskFactors { get; set; }
}

public sealed class HighRiskInput
{
    public bool IsHighRisk { get; set; }
    public string? Reason { get; set; }
    public string? Assessment { get; set; }
    public string? Referral { get; set; }
    public string? FollowUpPlan { get; set; }
}

public sealed class LabourInput
{
    public long? PregnancyId { get; set; }
    public string? GestationalAge { get; set; }
    public string? PresentingComplaints { get; set; }
    public DateTime? OnsetLocal { get; set; }
    public string? OnsetInfo { get; set; }
    public string? MembraneStatus { get; set; }
    public DateTime? MembranesRupturedLocal { get; set; }
    public string? ClinicalHistory { get; set; }
    public string? InitialAssessment { get; set; }
    public string? ResponsibleStaff { get; set; }
}

public sealed class BabyInput
{
    public Gender Sex { get; set; } = Gender.Female;
    public string? Name { get; set; }
    public int? BirthWeightGrams { get; set; }
    public int? Apgar1 { get; set; }
    public int? Apgar5 { get; set; }
    public int? Apgar10 { get; set; }
    public BirthCondition Condition { get; set; } = BirthCondition.LiveBirth;
    public string? Resuscitation { get; set; }
    public string? CongenitalObservations { get; set; }
    public string? Examination { get; set; }
    public string? ResponsibleStaff { get; set; }
    public NewbornPlacement Placement { get; set; } = NewbornPlacement.WithMother;
    public int? NicuBedId { get; set; }
    public string? NicuReason { get; set; }
}

public sealed class DeliveryInput
{
    public Guid IdempotencyKey { get; set; }
    public DateTime? DeliveredLocal { get; set; }
    public DeliveryType Type { get; set; } = DeliveryType.Nvd;
    public string? TypeDetail { get; set; }
    public string? Outcome { get; set; }
    public string? AttendingStaff { get; set; }
    public string? DeliveringDoctor { get; set; }
    public string? Notes { get; set; }
    public string? Complications { get; set; }
    public string? Placenta { get; set; }
    public int? EstimatedBloodLossMl { get; set; }
    public string? Perineum { get; set; }
    public string? MaternalCondition { get; set; }
    public string? ImmediateCare { get; set; }
    public string? GestationalAge { get; set; }
    public List<BabyInput> Babies { get; set; } = new();
}

public sealed class NicuInput
{
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public long? NewbornRecordId { get; set; }
    public int BedId { get; set; }
    public AdmissionSource Source { get; set; } = AdmissionSource.DeliveryRoom;
    public string? GestationalAge { get; set; }
    public int? BirthWeightGrams { get; set; }
    public int? AdmissionWeightGrams { get; set; }
    public string? Reason { get; set; }
    public string? ClinicalCondition { get; set; }
    public string? BirthHistory { get; set; }
    public string? ReferralDetails { get; set; }
    public string? ResponsibleClinician { get; set; }
    public string? AttendantName { get; set; }
    public string? AttendantPhone { get; set; }
}

public interface IMaternityService
{
    Task<ServiceResult<long>> RegisterPregnancyAsync(long patientId, PregnancyInput input, CancellationToken ct = default);
    Task<ServiceResult> UpdatePregnancyAsync(long id, PregnancyInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> SetHighRiskAsync(long id, HighRiskInput input, CancellationToken ct = default);
    Task<ServiceResult> ClosePregnancyAsync(long id, PregnancyStatus status, string? note, CancellationToken ct = default);

    Task<ServiceResult<IpdAdmission>> AdmitToLabourAsync(AdmitInput admit, LabourInput labour, CancellationToken ct = default);
    Task<ServiceResult> MoveToLabourAsync(long admissionId, int bedId, LabourInput input, CancellationToken ct = default);
    Task<ServiceResult> UpdateLabourAsync(long labourId, LabourInput input, CancellationToken ct = default);
    Task<ServiceResult> SetLabourStatusAsync(long labourId, LabourStatus status, string? note, CancellationToken ct = default);
    Task<ServiceResult<DeliveryRecord>> RecordDeliveryAsync(long admissionId, DeliveryInput input, CancellationToken ct = default);

    Task<ServiceResult<IpdAdmission>> AdmitToNicuAsync(NicuInput input, CancellationToken ct = default);
    Task<ServiceResult> ReturnToMotherAsync(long babyAdmissionId, string? reason, CancellationToken ct = default);
}

public sealed class MaternityService : IMaternityService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IIpdService _ipd;
    private readonly IPatientService _patients;

    public MaternityService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IIpdService ipd, IPatientService patients)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _ipd = ipd;
        _patients = patients;
    }

    private static string? T(string? v, int max) => IpdSetupService.T(v, max);

    public static string Label(LabourStatus s) => s switch
    {
        LabourStatus.UnderObservation => "Under observation",
        LabourStatus.EarlyLabour => "Early labour",
        LabourStatus.ActiveLabour => "Active labour",
        LabourStatus.DeliveryCompleted => "Delivery completed",
        _ => s.ToString(),
    };

    public static string Label(DeliveryType t) => t switch
    {
        DeliveryType.Nvd => "Normal vaginal delivery (NVD)",
        DeliveryType.AssistedVaginal => "Assisted vaginal delivery",
        DeliveryType.CaesareanSection => "Caesarean section",
        _ => "Other",
    };

    // =====================================================================================
    // Pregnancy & ANC
    // =====================================================================================

    private static List<string> Validate(PregnancyInput i, DateOnly today)
    {
        var e = new List<string>();
        if (i.Lmp is { } l && (l > today || l < today.AddDays(-320))) e.Add("LMP must be within the last 45 weeks.");
        if (i.Edd is { } d && i.Lmp is { } l2 && d <= l2) e.Add("EDD must be after the LMP.");
        foreach (var (v, n) in new[] { (i.Gravida, "Gravida"), (i.Para, "Para"), (i.Abortions, "Abortions"), (i.LivingChildren, "Living children") })
            if (v is < 0 or > 30) e.Add($"{n} must be between 0 and 30.");
        return e;
    }

    public async Task<ServiceResult<long>> RegisterPregnancyAsync(long patientId, PregnancyInput i, CancellationToken ct = default)
    {
        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId, ct);
        if (p is null) return ServiceResult<long>.Fail("Patient not found.");
        if (p.Gender != Gender.Female) return ServiceResult<long>.Fail("Pregnancy records can only be registered for a female patient.");
        var today = await _clock.TodayAsync();
        var errors = Validate(i, today);
        if (await _db.Pregnancies.AnyAsync(x => x.PatientId == patientId && x.Status == PregnancyStatus.Active, ct))
            errors.Add("This patient already has an ongoing pregnancy record. Close it (delivered / ended) before registering a new one.");
        if (errors.Count > 0) return ServiceResult<long>.Fail(errors.ToArray());
        var now = _clock.UtcNow;
        var g = new Pregnancy
        {
            Number = await FinanceDb.NextNumberAsync(_db, HmsDbContext.PregnancySequence, "PRG", today, ct), PatientId = patientId, RegisteredDate = today,
            Lmp = i.Lmp, Edd = i.Edd ?? i.Lmp?.AddDays(280), Gravida = i.Gravida, Para = i.Para, Abortions = i.Abortions, LivingChildren = i.LivingChildren,
            PreviousComplications = T(i.PreviousComplications, 1000), MedicalHistory = T(i.MedicalHistory, 2000), RiskFactors = T(i.RiskFactors, 1000),
            CreatedUtc = now, CreatedBy = _ctx.Username, UpdatedUtc = now
        };
        _db.Pregnancies.Add(g);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_Pregnancies_OneActive"))
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<long>.Fail("This patient already has an ongoing pregnancy record.");
        }
        _ipd.Event(null, g.Id, "Pregnancy", "Registered", null, null, $"LMP {g.Lmp:dd MMM yyyy} · EDD {g.Edd:dd MMM yyyy} · G{g.Gravida} P{g.Para}");
        _audit.Add(AuditActions.IpdPregnancyRegistered, "Pregnancy", g.Number, $"Pregnancy {g.Number} registered for {p.PatientNumber}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(g.Id);
    }

    public async Task<ServiceResult> UpdatePregnancyAsync(long id, PregnancyInput i, uint rowVersion, CancellationToken ct = default)
    {
        var g = await _db.Pregnancies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return ServiceResult.Fail("Pregnancy record not found.");
        if (g.Status != PregnancyStatus.Active) return ServiceResult.Fail("A closed pregnancy record is kept as it is.");
        var errors = Validate(i, await _clock.TodayAsync());
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        _db.Entry(g).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var old = new { g.Lmp, g.Edd, g.Gravida, g.Para, g.Abortions, g.LivingChildren, g.PreviousComplications, g.MedicalHistory, g.RiskFactors };
        g.Lmp = i.Lmp; g.Edd = i.Edd ?? i.Lmp?.AddDays(280); g.Gravida = i.Gravida; g.Para = i.Para; g.Abortions = i.Abortions; g.LivingChildren = i.LivingChildren;
        g.PreviousComplications = T(i.PreviousComplications, 1000); g.MedicalHistory = T(i.MedicalHistory, 2000); g.RiskFactors = T(i.RiskFactors, 1000); g.UpdatedUtc = _clock.UtcNow;
        _ipd.Event(null, g.Id, "Pregnancy", "Updated", null, null, $"LMP {g.Lmp:dd MMM yyyy} · EDD {g.Edd:dd MMM yyyy}");
        _audit.Add(AuditActions.IpdPregnancyUpdated, "Pregnancy", g.Number, $"Pregnancy {g.Number} updated", old,
            new { g.Lmp, g.Edd, g.Gravida, g.Para, g.Abortions, g.LivingChildren, g.PreviousComplications, g.MedicalHistory, g.RiskFactors });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The record was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetHighRiskAsync(long id, HighRiskInput i, CancellationToken ct = default)
    {
        var g = await _db.Pregnancies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return ServiceResult.Fail("Pregnancy record not found.");
        if (i.IsHighRisk && T(i.Reason, 500) is null) return ServiceResult.Fail("Enter the reason for the high-risk classification (as assessed by the clinician).");
        var old = new { g.IsHighRisk, g.HighRiskReason, g.HighRiskAssessment, g.HighRiskReferral, g.FollowUpPlan };
        g.IsHighRisk = i.IsHighRisk; g.HighRiskReason = T(i.Reason, 500); g.HighRiskAssessment = T(i.Assessment, 1000);
        g.HighRiskReferral = T(i.Referral, 300); g.FollowUpPlan = T(i.FollowUpPlan, 500); g.UpdatedUtc = _clock.UtcNow;
        _ipd.Event(null, g.Id, "Pregnancy", i.IsHighRisk ? "High-risk flag set" : "High-risk flag removed", old.IsHighRisk.ToString(), g.IsHighRisk.ToString(), g.HighRiskReason);
        _audit.Add(AuditActions.IpdPregnancyUpdated, "Pregnancy", g.Number, $"Pregnancy {g.Number}: high-risk {(g.IsHighRisk ? "set – " + g.HighRiskReason : "removed")}", old,
            new { g.IsHighRisk, g.HighRiskReason, g.HighRiskAssessment, g.HighRiskReferral, g.FollowUpPlan });
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ClosePregnancyAsync(long id, PregnancyStatus status, string? note, CancellationToken ct = default)
    {
        if (status is not (PregnancyStatus.Ended or PregnancyStatus.TransferredOut)) return ServiceResult.Fail("A pregnancy is closed as delivered by recording the delivery.");
        if (T(note, 300) is null) return ServiceResult.Fail("Enter a note.");
        var g = await _db.Pregnancies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null || g.Status != PregnancyStatus.Active) return ServiceResult.Fail("Only an ongoing pregnancy record can be closed.");
        _ipd.Event(null, g.Id, "Pregnancy", "Closed", g.Status.ToString(), status.ToString(), note);
        g.Status = status;
        g.StatusNote = T(note, 300);
        g.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdPregnancyUpdated, "Pregnancy", g.Number, $"Pregnancy {g.Number} closed ({status}): {note}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Labour Room
    // =====================================================================================

    public async Task<ServiceResult<IpdAdmission>> AdmitToLabourAsync(AdmitInput admit, LabourInput l, CancellationToken ct = default)
    {
        admit.Kind = AdmissionKind.Labour;
        if (l.PregnancyId is long pg)
        {
            var preg = await _db.Pregnancies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pg, ct);
            if (preg is null || preg.PatientId != admit.PatientId) return ServiceResult<IpdAdmission>.Fail("The pregnancy record does not belong to this patient.");
            if (preg.Status != PregnancyStatus.Active) return ServiceResult<IpdAdmission>.Fail("This pregnancy record is closed.");
            admit.PregnancyId = pg;
        }
        if (admit.BedId is int bed && !await _db.IpdBeds.AnyAsync(b => b.Id == bed && b.Ward!.Type == WardType.LabourRoom, ct))
            return ServiceResult<IpdAdmission>.Fail("Select a Labour Room bed.");
        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admit.PatientId, ct);
        if (p is null) return ServiceResult<IpdAdmission>.Fail("Select the patient.");
        if (p.Gender != Gender.Female) return ServiceResult<IpdAdmission>.Fail("Only a female patient can be admitted to the Labour Room.");
        // An already admitted mother (e.g. Gynae ward) moves to the Labour Room by transfer – not a second admission.
        if (await _db.IpdAdmissions.FirstOrDefaultAsync(a => a.PatientId == p.Id && IpdService.ActiveStatuses.Contains(a.Status), ct) is { } current)
            return ServiceResult<IpdAdmission>.Fail($"{p.PatientNumber} is already admitted ({current.AdmissionNumber}). Open the admission and use “Move to Labour Room”.");
        var r = await _ipd.AdmitAsync(admit, ct);
        if (!r.Succeeded) return r;
        if (await _db.LabourRecords.AnyAsync(x => x.AdmissionId == r.Value!.Id, ct)) return r; // repeated submit
        await CreateLabourAsync(r.Value!, l, ct);
        return r;
    }

    private async Task CreateLabourAsync(IpdAdmission a, LabourInput l, CancellationToken ct)
    {
        var rec = new LabourRecord
        {
            AdmissionId = a.Id, PregnancyId = a.PregnancyId ?? l.PregnancyId, Status = LabourStatus.Admitted,
            GestationalAge = T(l.GestationalAge, 20) ?? ((a.PregnancyId ?? l.PregnancyId) is long pid
                ? (await _db.Pregnancies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct))?.GestationOn(await _clock.TodayAsync()) : null),
            PresentingComplaints = T(l.PresentingComplaints, 1000), OnsetUtc = l.OnsetLocal is DateTime o ? await _clock.LocalToUtcAsync(o) : null, OnsetInfo = T(l.OnsetInfo, 200),
            MembraneStatus = T(l.MembraneStatus, 100), MembranesRupturedUtc = l.MembranesRupturedLocal is DateTime m ? await _clock.LocalToUtcAsync(m) : null,
            ClinicalHistory = T(l.ClinicalHistory, 1000), InitialAssessment = T(l.InitialAssessment, 1000), ResponsibleStaff = T(l.ResponsibleStaff, 150) ?? _ctx.Username,
            UpdatedUtc = _clock.UtcNow
        };
        _db.LabourRecords.Add(rec);
        _ipd.Event(a.Id, rec.PregnancyId, "Labour", "Labour admission", null, LabourStatus.Admitted.ToString(), rec.PresentingComplaints);
        _audit.Add(AuditActions.IpdLabourAdmitted, "IpdAdmission", a.AdmissionNumber, $"Labour Room admission {a.AdmissionNumber}");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>An already admitted mother (e.g. Gynae ward) moves to a Labour Room bed: same admission, transfer recorded, labour episode started.</summary>
    public async Task<ServiceResult> MoveToLabourAsync(long admissionId, int bedId, LabourInput l, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null || !a.IsActive) return ServiceResult.Fail("Active admission not found.");
        if (a.Patient!.Gender != Gender.Female) return ServiceResult.Fail("Only a female patient can be moved to the Labour Room.");
        if (await _db.LabourRecords.AnyAsync(x => x.AdmissionId == a.Id, ct)) return ServiceResult.Fail("This admission already has a Labour Room episode.");
        if (!await _db.IpdBeds.AnyAsync(b => b.Id == bedId && b.Ward!.Type == WardType.LabourRoom, ct)) return ServiceResult.Fail("Select a Labour Room bed.");
        if (l.PregnancyId is long pg && !await _db.Pregnancies.AnyAsync(x => x.Id == pg && x.PatientId == a.PatientId && x.Status == PregnancyStatus.Active, ct))
            return ServiceResult.Fail("Select the mother's ongoing pregnancy record.");
        var t = await _ipd.TransferAsync(a.Id, bedId, null, "Moved to Labour Room", "Labour Room", ct);
        if (!t.Succeeded) return t;
        a = await _db.IpdAdmissions.FirstAsync(x => x.Id == admissionId, ct);
        a.Kind = AdmissionKind.Labour;
        a.PregnancyId ??= l.PregnancyId;
        await CreateLabourAsync(a, l, ct);
        return ServiceResult.Ok();
    }

    /// <summary>Updates the labour admission details (onset, membranes, assessment).</summary>
    public async Task<ServiceResult> UpdateLabourAsync(long labourId, LabourInput i, CancellationToken ct = default)
    {
        var l = await _db.LabourRecords.FirstOrDefaultAsync(x => x.Id == labourId, ct);
        if (l is null) return ServiceResult.Fail("Labour record not found.");
        l.GestationalAge = T(i.GestationalAge, 20) ?? l.GestationalAge; l.PresentingComplaints = T(i.PresentingComplaints, 1000) ?? l.PresentingComplaints;
        if (i.OnsetLocal is DateTime o) l.OnsetUtc = await _clock.LocalToUtcAsync(o);
        l.OnsetInfo = T(i.OnsetInfo, 200) ?? l.OnsetInfo; l.MembraneStatus = T(i.MembraneStatus, 100) ?? l.MembraneStatus;
        if (i.MembranesRupturedLocal is DateTime m) l.MembranesRupturedUtc = await _clock.LocalToUtcAsync(m);
        l.ClinicalHistory = T(i.ClinicalHistory, 1000) ?? l.ClinicalHistory; l.InitialAssessment = T(i.InitialAssessment, 1000) ?? l.InitialAssessment;
        l.ResponsibleStaff = T(i.ResponsibleStaff, 150) ?? l.ResponsibleStaff; l.UpdatedUtc = _clock.UtcNow;
        _ipd.Event(l.AdmissionId, l.PregnancyId, "Labour", "Details updated", null, null, l.MembraneStatus);
        _audit.Add(AuditActions.IpdLabourStatus, "LabourRecord", l.Id.ToString(), "Labour details updated");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetLabourStatusAsync(long labourId, LabourStatus status, string? note, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(status) || status is LabourStatus.DeliveryCompleted) return ServiceResult.Fail("Delivery completed is set by recording the delivery.");
        if (status is LabourStatus.Referred or LabourStatus.Transferred or LabourStatus.Discharged)
            return ServiceResult.Fail("Referral, transfer and discharge are done from the admission (discharge or transfer).");
        var l = await _db.LabourRecords.FirstOrDefaultAsync(x => x.Id == labourId, ct);
        if (l is null) return ServiceResult.Fail("Labour record not found.");
        if (l.Status is LabourStatus.DeliveryCompleted or LabourStatus.Referred or LabourStatus.Transferred or LabourStatus.Discharged)
            return ServiceResult.Fail("This labour episode is closed.");
        _ipd.Event(l.AdmissionId, l.PregnancyId, "Labour", "Status", l.Status.ToString(), status.ToString(), T(note, 300));
        var a = await _db.IpdAdmissions.FirstAsync(x => x.Id == l.AdmissionId, ct);
        _audit.Add(AuditActions.IpdLabourStatus, "IpdAdmission", a.AdmissionNumber, $"{a.AdmissionNumber} labour status: {Label(l.Status)} → {Label(status)}");
        l.Status = status;
        l.UpdatedUtc = _clock.UtcNow;
        if (a.Status is AdmissionStatus.Admitted) { _ipd.Event(a.Id, a.PregnancyId, "Admission", "Status", a.Status.ToString(), AdmissionStatus.UnderTreatment.ToString(), null); a.Status = AdmissionStatus.UnderTreatment; }
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The record was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Delivery, newborns and the mother–baby link
    // =====================================================================================

    public async Task<ServiceResult<DeliveryRecord>> RecordDeliveryAsync(long admissionId, DeliveryInput i, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<DeliveryRecord>.Fail("The form has expired. Please reload the page.");
        if (await _db.DeliveryRecords.AsNoTracking().FirstOrDefaultAsync(d => d.IdempotencyKey == i.IdempotencyKey, ct) is { } dup)
        {
            await _audit.LogAsync(AuditActions.IpdDuplicateBlocked, "DeliveryRecord", dup.Number, $"Repeated delivery entry {dup.Number} ignored (no second delivery / newborn)", ct: ct);
            return ServiceResult<DeliveryRecord>.Ok(dup);
        }
        var mother = await _db.IpdAdmissions.Include(a => a.Patient).AsNoTracking().FirstOrDefaultAsync(a => a.Id == admissionId, ct);
        if (mother is null) return ServiceResult<DeliveryRecord>.Fail("Admission not found.");
        var errors = new List<string>();
        if (!mother.IsActive) errors.Add("The mother's admission is closed.");
        if (mother.Patient!.Gender != Gender.Female) errors.Add("A delivery is recorded on the mother's admission.");
        if (!Enum.IsDefined(i.Type)) errors.Add("Select the delivery type.");
        if (i.Type == DeliveryType.Other && T(i.TypeDetail, 100) is null) errors.Add("Describe the delivery type.");
        if (i.Babies.Count is < 1 or > 6) errors.Add("Enter every baby born (1 to 6).");
        if (i.EstimatedBloodLossMl is < 0 or > 10000) errors.Add("Estimated blood loss is not valid.");
        var localNow = await _clock.LocalNowAsync();
        var delivered = i.DeliveredLocal ?? localNow;
        if (delivered > localNow.AddMinutes(5)) errors.Add("The delivery time cannot be in the future.");
        for (var n = 0; n < i.Babies.Count; n++)
        {
            var b = i.Babies[n];
            var tag = i.Babies.Count > 1 ? $"Baby {n + 1}: " : "";
            if (!Enum.IsDefined(b.Sex) || !Enum.IsDefined(b.Condition) || !Enum.IsDefined(b.Placement)) errors.Add(tag + "complete sex, birth condition and placement.");
            if (b.BirthWeightGrams is < 200 or > 7000) errors.Add(tag + "birth weight must be between 200 and 7000 g.");
            foreach (var ap in new[] { b.Apgar1, b.Apgar5, b.Apgar10 }) if (ap is < 0 or > 10) errors.Add(tag + "APGAR scores are 0 to 10.");
            if (b.Condition == BirthCondition.LiveBirth && b.Placement == NewbornPlacement.Nicu && b.NicuBedId is null) errors.Add(tag + "select the NICU cot / incubator.");
            if (b.Condition == BirthCondition.LiveBirth && b.Placement == NewbornPlacement.WithMother && mother.CurrentBedId is null) errors.Add(tag + "the mother has no bed for rooming-in.");
        }
        var nicuBeds = i.Babies.Where(b => b.Placement == NewbornPlacement.Nicu && b.NicuBedId is not null).Select(b => b.NicuBedId).ToList();
        if (nicuBeds.Count != nicuBeds.Distinct().Count()) errors.Add("Two babies cannot share one NICU cot / incubator.");
        if (errors.Count > 0) return ServiceResult<DeliveryRecord>.Fail(errors.Distinct().ToArray());

        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        var deliveredUtc = await _clock.LocalToUtcAsync(delivered);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await FinanceDb.LockAsync(_db, $"IPDADM|{admissionId}", ct);
            var labour = await _db.LabourRecords.FirstOrDefaultAsync(l => l.AdmissionId == admissionId, ct);
            var pregnancyId = mother.PregnancyId ?? labour?.PregnancyId;
            var d = new DeliveryRecord
            {
                Number = await FinanceDb.NextNumberAsync(_db, HmsDbContext.DeliverySequence, "DEL", today, ct), IdempotencyKey = i.IdempotencyKey, AdmissionId = mother.Id,
                MotherPatientId = mother.PatientId, PregnancyId = pregnancyId, LabourRecordId = labour?.Id, DeliveredUtc = deliveredUtc, DeliveryDate = DateOnly.FromDateTime(delivered),
                Type = i.Type, TypeDetail = T(i.TypeDetail, 100), Outcome = T(i.Outcome, 200), AttendingStaff = T(i.AttendingStaff, 300), DeliveringDoctor = T(i.DeliveringDoctor, 150),
                Notes = T(i.Notes, 2000), Complications = T(i.Complications, 1000), Placenta = T(i.Placenta, 500), EstimatedBloodLossMl = i.EstimatedBloodLossMl,
                Perineum = T(i.Perineum, 500), MaternalCondition = T(i.MaternalCondition, 500), ImmediateCare = T(i.ImmediateCare, 1000), BabyCount = i.Babies.Count,
                CreatedUtc = now, CreatedBy = _ctx.Username
            };
            _db.DeliveryRecords.Add(d);
            await _db.SaveChangesAsync(ct);
            var mp = mother.Patient!;
            var motherAdmission = await _db.IpdAdmissions.FirstAsync(a => a.Id == mother.Id, ct);
            for (var n = 0; n < i.Babies.Count; n++)
            {
                var b = i.Babies[n];
                var rec = new NewbornRecord
                {
                    DeliveryId = d.Id, BirthOrder = n + 1, MotherPatientId = mp.Id, PregnancyId = pregnancyId, Name = T(b.Name, 150), Sex = b.Sex, BirthUtc = deliveredUtc,
                    BirthWeightGrams = b.BirthWeightGrams, GestationalAge = T(i.GestationalAge, 20) ?? labour?.GestationalAge, Apgar1 = b.Apgar1, Apgar5 = b.Apgar5, Apgar10 = b.Apgar10,
                    Condition = b.Condition, Resuscitation = T(b.Resuscitation, 500), CongenitalObservations = T(b.CongenitalObservations, 500), Examination = T(b.Examination, 1000),
                    ResponsibleStaff = T(b.ResponsibleStaff, 150) ?? T(i.AttendingStaff, 150), Placement = b.Condition == BirthCondition.LiveBirth ? b.Placement : NewbornPlacement.Other,
                    CreatedUtc = now, CreatedBy = _ctx.Username
                };
                if (b.Condition == BirthCondition.LiveBirth)
                {
                    // Each baby gets its own permanent Patient ID through the normal registration – never the mother's ID.
                    var multi = i.Babies.Count > 1 ? $" ({(n == 0 ? "first" : n == 1 ? "second" : n == 2 ? "third" : $"#{n + 1}")} baby)" : "";
                    var created = await _patients.CreateWithinTransactionAsync(new PatientInput
                    {
                        FullName = T(b.Name, 120) ?? $"Baby of {mp.FullName}{multi}", GuardianRelation = b.Sex == Gender.Male ? GuardianRelation.SonOf : GuardianRelation.DaughterOf,
                        GuardianName = mp.FullName, Gender = b.Sex, DateOfBirth = DateOnly.FromDateTime(delivered), MobileNumber = mp.MobileNumber,
                        Address = mp.Address ?? "—", CityOrVillage = mp.CityOrVillage, Notes = $"Born {delivered:dd MMM yyyy HH:mm} – mother {mp.PatientNumber}, delivery {d.Number}"
                    }, minimal: false, ct);
                    if (!created.Succeeded)
                    {
                        await tx.RollbackAsync(ct);
                        _db.ChangeTracker.Clear();
                        return ServiceResult<DeliveryRecord>.Fail(created.Errors.Select(e => $"Baby {n + 1}: {e}").ToArray());
                    }
                    rec.NewbornPatientId = created.Value!.Id;
                    _audit.Add(AuditActions.IpdNewbornRegistered, "NewbornRecord", created.Value.PatientNumber,
                        $"Newborn {created.Value.PatientNumber} ({b.Sex}, {b.BirthWeightGrams} g) registered from delivery {d.Number}");
                    _audit.Add(AuditActions.IpdMotherBabyLinked, "NewbornRecord", created.Value.PatientNumber, $"Baby {created.Value.PatientNumber} linked to mother {mp.PatientNumber} (delivery {d.Number})");
                }
                _db.NewbornRecords.Add(rec);
                await _db.SaveChangesAsync(ct);
                if (rec.NewbornPatientId is long babyId)
                {
                    var baby = await _db.Patients.AsNoTracking().FirstAsync(x => x.Id == babyId, ct);
                    if (rec.Placement == NewbornPlacement.WithMother)
                    {
                        // Rooming-in: the baby's own admission shares the mother's bed (no false double occupancy).
                        var ba = await _ipd.CreateAdmissionWithinAsync(new AdmitInput
                        {
                            IdempotencyKey = Guid.NewGuid(), PatientId = babyId, Kind = AdmissionKind.Newborn, Source = AdmissionSource.DeliveryRoom, DepartmentId = motherAdmission.DepartmentId,
                            MotherAdmissionId = mother.Id, AdmissionDiagnosis = "Newborn with mother", AttendantName = mp.FullName, AttendantRelation = "Mother", AttendantPhone = mp.MobileNumber
                        }, baby, today, ct);
                        if (await _ipd.AllocateWithinAsync(ba, motherAdmission.CurrentBedId!.Value, "With mother", true, mother.Id, ct) is { } err)
                        { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); return ServiceResult<DeliveryRecord>.Fail(err); }
                        rec.NewbornAdmissionId = ba.Id;
                    }
                    else if (rec.Placement == NewbornPlacement.Nicu)
                    {
                        var na = await CreateNicuWithinAsync(new NicuInput
                        {
                            PatientId = babyId, NewbornRecordId = rec.Id, BedId = b.NicuBedId!.Value, Source = AdmissionSource.DeliveryRoom, GestationalAge = rec.GestationalAge,
                            BirthWeightGrams = rec.BirthWeightGrams, AdmissionWeightGrams = rec.BirthWeightGrams, Reason = T(b.NicuReason, 500), BirthHistory = $"Delivery {d.Number}: {Label(d.Type)}",
                            AttendantName = mp.FullName, AttendantPhone = mp.MobileNumber
                        }, baby, motherAdmission, today, ct);
                        if (na.Error is not null) { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); return ServiceResult<DeliveryRecord>.Fail(na.Error); }
                        rec.NewbornAdmissionId = na.Admission!.Id;
                    }
                    await _db.SaveChangesAsync(ct);
                }
            }
            if (labour is not null && labour.Status != LabourStatus.DeliveryCompleted)
            {
                _ipd.Event(mother.Id, pregnancyId, "Labour", "Status", labour.Status.ToString(), LabourStatus.DeliveryCompleted.ToString(), d.Number);
                labour.Status = LabourStatus.DeliveryCompleted;
                labour.UpdatedUtc = now;
            }
            if (pregnancyId is long pgid && await _db.Pregnancies.FirstOrDefaultAsync(x => x.Id == pgid, ct) is { Status: PregnancyStatus.Active } preg)
            {
                preg.Status = PregnancyStatus.Delivered;
                preg.StatusNote = $"Delivered {delivered:dd MMM yyyy} – {d.Number}";
                preg.UpdatedUtc = now;
            }
            _ipd.Event(mother.Id, pregnancyId, "Delivery", "Delivery", null, d.Type.ToString(), $"{d.Number}: {Label(d.Type)}, {d.BabyCount} baby(ies)");
            _audit.Add(AuditActions.IpdDeliveryRecorded, "DeliveryRecord", d.Number,
                $"Delivery {d.Number} for {mp.PatientNumber} ({mother.AdmissionNumber}): {Label(d.Type)}, {d.BabyCount} baby(ies)");
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<DeliveryRecord>.Ok(d);
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_DeliveryRecords_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<DeliveryRecord>.Ok(await _db.DeliveryRecords.AsNoTracking().FirstAsync(x => x.IdempotencyKey == i.IdempotencyKey, ct));
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_IpdBedAllocations_OpenBed"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<DeliveryRecord>.Fail("A selected NICU cot / incubator was just taken. Choose another one.");
        }
    }

    // =====================================================================================
    // NICU
    // =====================================================================================

    private async Task<(IpdAdmission? Admission, string? Error)> CreateNicuWithinAsync(NicuInput i, Patient baby, IpdAdmission? motherAdmission, DateOnly today, CancellationToken ct)
    {
        var a = await _ipd.CreateAdmissionWithinAsync(new AdmitInput
        {
            IdempotencyKey = i.IdempotencyKey == Guid.Empty ? Guid.NewGuid() : i.IdempotencyKey, PatientId = baby.Id, Kind = AdmissionKind.Nicu, Source = i.Source,
            DepartmentId = await _db.Departments.Where(d => d.Name == "NICU").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct) ?? motherAdmission?.DepartmentId,
            MotherAdmissionId = motherAdmission?.Id, AdmissionDiagnosis = T(i.Reason, 500), InitialCondition = T(i.ClinicalCondition, 500),
            AttendantName = T(i.AttendantName, 150), AttendantRelation = "Mother", AttendantPhone = T(i.AttendantPhone, 30), AdmittingDoctorName = T(i.ResponsibleClinician, 150)
        }, baby, today, ct);
        if (await _ipd.AllocateWithinAsync(a, i.BedId, "NICU admission", false, null, ct) is { } err) return (null, err);
        await AddNicuDetailsAsync(a, i, ct);
        return (a, null);
    }

    private async Task AddNicuDetailsAsync(IpdAdmission a, NicuInput i, CancellationToken ct)
    {
        var nb = i.NewbornRecordId is long rid ? await _db.NewbornRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, ct) : null;
        _db.NicuDetails.Add(new NicuDetails
        {
            AdmissionId = a.Id, NewbornRecordId = nb?.Id, MotherPatientId = nb?.MotherPatientId, GestationalAge = T(i.GestationalAge, 20) ?? nb?.GestationalAge,
            BirthWeightGrams = i.BirthWeightGrams ?? nb?.BirthWeightGrams, AdmissionWeightGrams = i.AdmissionWeightGrams, Reason = T(i.Reason, 500),
            ClinicalCondition = T(i.ClinicalCondition, 500), BirthHistory = T(i.BirthHistory, 1000), ReferralDetails = T(i.ReferralDetails, 300),
            ResponsibleClinician = T(i.ResponsibleClinician, 150)
        });
        _ipd.Event(a.Id, null, "NICU", "NICU admission", null, null, T(i.Reason, 300));
        _audit.Add(AuditActions.IpdNicuAdmitted, "IpdAdmission", a.AdmissionNumber, $"NICU admission {a.AdmissionNumber}: {T(i.Reason, 200)}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult<IpdAdmission>> AdmitToNicuAsync(NicuInput i, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<IpdAdmission>.Fail("The form has expired. Please reload the page.");
        if (await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(a => a.IdempotencyKey == i.IdempotencyKey, ct) is { } dup) return ServiceResult<IpdAdmission>.Ok(dup);
        var baby = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == i.PatientId, ct);
        if (baby is null) return ServiceResult<IpdAdmission>.Fail("Select the newborn patient.");
        if (T(i.Reason, 500) is null) return ServiceResult<IpdAdmission>.Fail("Enter the admission reason.");
        if (i.NewbornRecordId is long rid && !await _db.NewbornRecords.AnyAsync(r => r.Id == rid && r.NewbornPatientId == baby.Id, ct))
            return ServiceResult<IpdAdmission>.Fail("The birth record does not belong to this baby.");
        if (!await _db.IpdBeds.AnyAsync(b => b.Id == i.BedId && b.Ward!.Type == WardType.Nicu, ct)) return ServiceResult<IpdAdmission>.Fail("Select a NICU cot / incubator.");
        i.NewbornRecordId ??= await _db.NewbornRecords.Where(r => r.NewbornPatientId == baby.Id).Select(r => (long?)r.Id).FirstOrDefaultAsync(ct);
        var today = await _clock.TodayAsync();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var current = await _db.IpdAdmissions.FirstOrDefaultAsync(a => a.PatientId == baby.Id && IpdService.ActiveStatuses.Contains(a.Status), ct);
            IpdAdmission result;
            if (current is not null)
            {
                // A baby already admitted (rooming-in / ward) moves to the NICU cot: one continuous admission, the bed history is kept.
                if (current.Kind == AdmissionKind.Nicu) return ServiceResult<IpdAdmission>.Fail($"The baby is already in the NICU ({current.AdmissionNumber}).");
                await tx.CommitAsync(ct);
                var t = await _ipd.TransferAsync(current.Id, i.BedId, await _db.Departments.Where(d => d.Name == "NICU").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct), "NICU admission: " + i.Reason, "NICU", ct);
                if (!t.Succeeded) return ServiceResult<IpdAdmission>.Fail(t.Errors.ToArray());
                var a = await _db.IpdAdmissions.FirstAsync(x => x.Id == current.Id, ct);
                a.Kind = AdmissionKind.Nicu;
                if (!await _db.NicuDetails.AnyAsync(n => n.AdmissionId == a.Id, ct)) await AddNicuDetailsAsync(a, i, ct);
                await _db.SaveChangesAsync(ct);
                return ServiceResult<IpdAdmission>.Ok(a);
            }
            var mother = i.NewbornRecordId is long r2
                ? await _db.NewbornRecords.Where(x => x.Id == r2).Select(x => x.Delivery!.AdmissionId).FirstOrDefaultAsync(ct) : 0;
            var motherAdmission = mother > 0 ? await _db.IpdAdmissions.FirstOrDefaultAsync(x => x.Id == mother && IpdService.ActiveStatuses.Contains(x.Status), ct) : null;
            var created = await CreateNicuWithinAsync(i, baby, motherAdmission, today, ct);
            if (created.Error is not null) { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); return ServiceResult<IpdAdmission>.Fail(created.Error); }
            result = created.Admission!;
            if (i.NewbornRecordId is long r3 && await _db.NewbornRecords.FirstOrDefaultAsync(x => x.Id == r3, ct) is { NewbornAdmissionId: null } nbr) nbr.NewbornAdmissionId = result.Id;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<IpdAdmission>.Ok(result);
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_IpdBedAllocations_OpenBed"))
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<IpdAdmission>.Fail("This cot / incubator was just taken. Choose another one.");
        }
    }

    /// <summary>NICU / ward baby back to the mother (Postnatal Ward): the baby shares the mother's bed again.</summary>
    public async Task<ServiceResult> ReturnToMotherAsync(long babyAdmissionId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for the transfer.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var baby = await _db.IpdAdmissions.FirstOrDefaultAsync(a => a.Id == babyAdmissionId, ct);
        if (baby is null || !baby.IsActive) return ServiceResult.Fail("Active baby admission not found.");
        var motherPatient = await _db.NewbornRecords.Where(r => r.NewbornPatientId == baby.PatientId).Select(r => (long?)r.MotherPatientId).FirstOrDefaultAsync(ct);
        var mother = motherPatient is long mp
            ? await _db.IpdAdmissions.FirstOrDefaultAsync(a => a.PatientId == mp && IpdService.ActiveStatuses.Contains(a.Status), ct) : null;
        if (mother?.CurrentBedId is not int bed) return ServiceResult.Fail("The mother is not admitted with a bed – transfer the baby to a ward bed or discharge instead.");
        var fromBed = baby.CurrentBedId is int fb ? await _db.IpdBeds.AsNoTracking().Include(b => b.Ward).Include(b => b.Room).FirstAsync(b => b.Id == fb, ct) : null;
        var motherBed = await _db.IpdBeds.AsNoTracking().Include(b => b.Ward).Include(b => b.Room).FirstAsync(b => b.Id == bed, ct);
        // Release the NICU cot (needs cleaning), then room in with the mother.
        if (await _db.IpdBedAllocations.FirstOrDefaultAsync(x => x.AdmissionId == baby.Id && x.ToUtc == null, ct) is { } open)
        {
            open.ToUtc = _clock.UtcNow;
            open.ReleasedBy = _ctx.Username;
            if (!open.IsShared && await _db.IpdBeds.FirstOrDefaultAsync(b => b.Id == open.BedId, ct) is { } cot && cot.CurrentAdmissionId == baby.Id)
            {
                _db.IpdBedStatusChanges.Add(new IpdBedStatusChange { BedId = cot.Id, From = cot.Status, To = BedStatus.Available, Note = $"Released by {baby.AdmissionNumber}", CreatedUtc = _clock.UtcNow, Username = _ctx.Username });
                cot.Status = BedStatus.Available;
                cot.CurrentAdmissionId = null;
            }
            await _db.SaveChangesAsync(ct);
        }
        baby.CurrentBedId = null;
        baby.MotherAdmissionId = mother.Id;
        baby.Kind = AdmissionKind.Newborn;
        if (await _ipd.AllocateWithinAsync(baby, bed, "With mother", true, mother.Id, ct) is { } err) { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); return ServiceResult.Fail(err); }
        baby.DepartmentId = mother.DepartmentId;
        _db.IpdTransfers.Add(new IpdTransfer
        {
            AdmissionId = baby.Id, Kind = TransferKind.Ward, FromWardId = fromBed?.WardId, FromBedId = fromBed?.Id, ToWardId = motherBed.WardId, ToBedId = motherBed.Id,
            FromText = fromBed is null ? "—" : $"{fromBed.Ward!.Name} / {fromBed.Room!.Name} / bed {fromBed.Code}", ToText = $"With mother: {motherBed.Ward!.Name} / bed {motherBed.Code}",
            Reason = reason, ReceivingDepartment = motherBed.Ward.Name, TransferUtc = _clock.UtcNow, Username = _ctx.Username
        });
        _audit.Add(AuditActions.IpdTransferred, "IpdAdmission", baby.AdmissionNumber, $"{baby.AdmissionNumber} transferred to the mother ({mother.AdmissionNumber}, bed {motherBed.Code}): {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }
}
