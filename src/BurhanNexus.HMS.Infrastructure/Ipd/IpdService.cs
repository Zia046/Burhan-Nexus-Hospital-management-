using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text.Json;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ipd;

public sealed class AdmitInput
{
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public AdmissionKind Kind { get; set; } = AdmissionKind.General;
    public AdmissionSource Source { get; set; } = AdmissionSource.Opd;
    public long? SourceEncounterId { get; set; }
    public int? DepartmentId { get; set; }
    public string? ReferringDoctor { get; set; }
    public int? AdmittingDoctorId { get; set; }
    public string? AdmittingDoctorName { get; set; }
    public string? AdmissionDiagnosis { get; set; }
    public string? InitialCondition { get; set; }
    public string? MedicalHistory { get; set; }
    public string? Allergies { get; set; }
    public string? AttendantName { get; set; }
    public string? AttendantRelation { get; set; }
    public string? AttendantPhone { get; set; }
    public int? BedId { get; set; }
    public DateTime? AdmittedLocal { get; set; }
    public long? PregnancyId { get; set; }
    public long? MotherAdmissionId { get; set; }
}

public sealed class DischargeInput
{
    public DischargeType? Type { get; set; }
    public string? FinalDiagnosis { get; set; }
    public string? TreatmentSummary { get; set; }
    public string? Procedures { get; set; }
    public string? InvestigationSummary { get; set; }
    public string? ConditionAtDischarge { get; set; }
    public string? FollowUp { get; set; }
    public string? DischargeMedications { get; set; }
    public string? ReferredTo { get; set; }
    public string? DischargeDoctor { get; set; }
}

public sealed class RecordInput
{
    public RecordKind Kind { get; set; }
    public long PatientId { get; set; }
    public long? AdmissionId { get; set; }
    public long? PregnancyId { get; set; }
    /// <summary>Phase 8: surgery the record belongs to.</summary>
    public long? SurgeryId { get; set; }
    public DateTime? RecordedLocal { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new();
    public string? Notes { get; set; }
    public string? ResponsibleName { get; set; }
    public long? CorrectsRecordId { get; set; }
    public string? CorrectionReason { get; set; }
}

public sealed class HandoverInput
{
    public string? Shift { get; set; }
    public string? PatientCondition { get; set; }
    public string? Diagnosis { get; set; }
    public string? PendingInvestigations { get; set; }
    public string? MedicationDue { get; set; }
    public string? IvFluids { get; set; }
    public string? ImportantObservations { get; set; }
    public string? PendingProcedures { get; set; }
    public string? SpecialCare { get; set; }
    public string? OutstandingTasks { get; set; }
}

public sealed class MedicationOrderInput
{
    public int MedicineId { get; set; }
    public string? Dose { get; set; }
    public string? Route { get; set; }
    public string? Frequency { get; set; }
    public string? Times { get; set; }
    public int? DurationDays { get; set; }
    public DateTime? StartLocal { get; set; }
    public string? Prescriber { get; set; }
    public OrderSource Source { get; set; } = OrderSource.HandwrittenPrescription;
    public string? Instructions { get; set; }
    public int StockUnitsPerDose { get; set; }
}

public sealed class DoseInput
{
    public DoseStatus Status { get; set; }
    public DateTime? GivenLocal { get; set; }
    public string? DoseGiven { get; set; }
    public string? RouteGiven { get; set; }
    public string? Nurse { get; set; }
    public string? Reason { get; set; }
    public string? Remarks { get; set; }
    public bool ConfirmPatient { get; set; }
    public bool ConfirmMedicine { get; set; }
    public bool ConfirmDose { get; set; }
    public bool ConfirmRoute { get; set; }
    public bool ConfirmTime { get; set; }
}

/// <summary>Inpatient core: admission, beds, transfers, discharge, clinical records, nursing, MAR and IPD charges.</summary>
public interface IIpdService
{
    Task<ServiceResult<IpdAdmission>> AdmitAsync(AdmitInput input, CancellationToken ct = default);
    Task<ServiceResult> EditAdmissionAsync(long id, AdmitInput input, CancellationToken ct = default);
    Task<ServiceResult> CancelAdmissionAsync(long id, string? reason, CancellationToken ct = default);
    Task<ServiceResult> SetUnderTreatmentAsync(long id, bool attention, string? note, CancellationToken ct = default);
    Task<ServiceResult> TransferAsync(long id, int? toBedId, int? toDepartmentId, string? reason, string? receiving, CancellationToken ct = default);
    Task<ServiceResult> RequestDischargeAsync(long id, DischargeInput input, CancellationToken ct = default);
    Task<ServiceResult> DischargeAsync(long id, DischargeInput input, CancellationToken ct = default);

    Task<ServiceResult<long>> AddRecordAsync(RecordInput input, CancellationToken ct = default);
    Task<ServiceResult<long>> AddTaskAsync(long admissionId, NursingTaskType type, string? description, DateTime? dueLocal, CancellationToken ct = default);
    Task<ServiceResult> CompleteTaskAsync(long taskId, bool cancel, string? note, CancellationToken ct = default);
    Task<ServiceResult<long>> HandoverAsync(long admissionId, HandoverInput input, CancellationToken ct = default);
    Task<ServiceResult> AcknowledgeHandoverAsync(long handoverId, string? note, CancellationToken ct = default);

    Task<ServiceResult<long>> OrderMedicationAsync(long admissionId, MedicationOrderInput input, CancellationToken ct = default);
    Task<ServiceResult> StopMedicationAsync(long orderId, string? reason, CancellationToken ct = default);
    Task<ServiceResult> RecordDoseAsync(long doseId, DoseInput input, CancellationToken ct = default);

    Task<ServiceResult<int>> PostBedChargesAsync(long admissionId, bool atDischarge, CancellationToken ct = default);
    /// <summary>Update 10: final bill before discharge – bed charges up to now and running equipment stopped, so the clearance balance is final.</summary>
    Task<ServiceResult<int>> FinaliseBillAsync(long admissionId, CancellationToken ct = default);
    Task<ServiceResult> AddServiceChargeAsync(long admissionId, int serviceItemId, int quantity, decimal discount, string? reason, bool canDiscount, CancellationToken ct = default);

    /// <summary>Opens a bed for an admission inside the caller's transaction (used by admission, maternity and NICU).</summary>
    Task<string?> AllocateWithinAsync(IpdAdmission a, int bedId, string reason, bool shared, long? sharedWith, CancellationToken ct);
    Task<IpdAdmission> CreateAdmissionWithinAsync(AdmitInput i, Patient patient, DateOnly today, CancellationToken ct);
    void Event(long? admissionId, long? pregnancyId, string category, string evt, string? from, string? to, string? note);
}

public sealed class IpdService : IIpdService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _finance;
    private readonly IIpdSetupService _setup;
    private readonly IServiceProvider _services;

    public IpdService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IFinanceSetupService finance, IIpdSetupService setup, IServiceProvider services)
    {
        _services = services;
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _finance = finance;
        _setup = setup;
    }

    private static string? T(string? v, int max) => IpdSetupService.T(v, max);
    private static string Cut(string v, int max) => v.Length > max ? v[..max] : v;

    public static readonly AdmissionStatus[] ActiveStatuses =
        { AdmissionStatus.Registered, AdmissionStatus.Admitted, AdmissionStatus.UnderTreatment, AdmissionStatus.Transferred, AdmissionStatus.DischargeRequested };

    public static string Label(AdmissionStatus s) => s switch
    {
        AdmissionStatus.UnderTreatment => "Under treatment",
        AdmissionStatus.DischargeRequested => "Discharge requested",
        _ => s.ToString(),
    };

    public void Event(long? admissionId, long? pregnancyId, string category, string evt, string? from, string? to, string? note) =>
        _db.IpdEvents.Add(new IpdEvent
        {
            AdmissionId = admissionId, PregnancyId = pregnancyId, Category = category, Event = evt, FromValue = from, ToValue = to,
            Note = note is null ? null : note[..Math.Min(500, note.Length)], CreatedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username
        });

    private void Status(IpdAdmission a, AdmissionStatus to, string? note)
    {
        if (a.Status == to) return;
        Event(a.Id, a.PregnancyId, "Admission", "Status", a.Status.ToString(), to.ToString(), note);
        a.Status = to;
    }

    private async Task<IpdAdmission?> LockAdmissionAsync(long id, CancellationToken ct)
    {
        await FinanceDb.LockAsync(_db, $"IPDADM|{id}", ct);
        return await _db.IpdAdmissions.Include(a => a.Patient).FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    private static string Where(IpdBed? bed) => bed is null ? "—" : $"{bed.Ward?.Name} / {bed.Room?.Name} / bed {bed.Code}";

    // =====================================================================================
    // Admission
    // =====================================================================================

    public async Task<ServiceResult<IpdAdmission>> AdmitAsync(AdmitInput i, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<IpdAdmission>.Fail("The form has expired. Please reload the page.");
        if (await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(a => a.IdempotencyKey == i.IdempotencyKey, ct) is { } dup)
        {
            await _audit.LogAsync(AuditActions.IpdDuplicateBlocked, "IpdAdmission", dup.AdmissionNumber, $"Repeated admission of {dup.AdmissionNumber} ignored (no second admission)", ct: ct);
            return ServiceResult<IpdAdmission>.Ok(dup);
        }
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == i.PatientId, ct);
        if (patient is null) return ServiceResult<IpdAdmission>.Fail("Select the patient.");
        var errors = await ValidateAdmitAsync(i, patient, ct);
        if (errors.Count > 0) return ServiceResult<IpdAdmission>.Fail(errors.ToArray());
        var today = await _clock.TodayAsync();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var a = await CreateAdmissionWithinAsync(i, patient, today, ct);
            if (i.BedId is int bed && await AllocateWithinAsync(a, bed, "Admission", false, null, ct) is { } err)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return ServiceResult<IpdAdmission>.Fail(err);
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<IpdAdmission>.Ok(a);
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_IpdAdmissions_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<IpdAdmission>.Ok(await _db.IpdAdmissions.AsNoTracking().FirstAsync(a => a.IdempotencyKey == i.IdempotencyKey, ct));
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_IpdAdmissions_OneActive"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<IpdAdmission>.Fail($"{patient.PatientNumber} is already admitted. Use the current admission (transfer instead of a second admission).");
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_IpdBedAllocations_OpenBed"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<IpdAdmission>.Fail("This bed was just allocated to another patient. Choose another bed.");
        }
    }

    /// <summary>How far back an admission time may be entered (late paperwork after a night admission).</summary>
    public const int MaxBackdatedAdmissionDays = 7;

    private async Task<List<string>> ValidateAdmitAsync(AdmitInput i, Patient patient, CancellationToken ct)
    {
        var errors = new List<string>();
        if (patient.Status != PatientStatus.Active) errors.Add($"{patient.PatientNumber} is {patient.Status.ToString().ToLowerInvariant()} – it cannot be admitted.");
        if (!Enum.IsDefined(i.Kind) || !Enum.IsDefined(i.Source)) errors.Add("Select the admission source.");
        var settings = await _setup.SettingsAsync(ct);
        if (i.Source == AdmissionSource.Direct && !settings.AllowDirectAdmission) errors.Add("Direct admission is not allowed by the hospital policy – admit through OPD / Emergency.");
        if (i.SourceEncounterId is long eid && !await _db.PatientEncounters.AnyAsync(e => e.Id == eid && e.PatientId == patient.Id, ct))
            errors.Add("The OPD / Emergency visit does not belong to this patient.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) errors.Add("Select a valid department.");
        // Update 11: the admission date/time is entered (defaults to the current hospital time) and validated – never silently replaced.
        if (i.AdmittedLocal is DateTime at)
        {
            var localNow = await _clock.LocalNowAsync();
            if (at > localNow.AddMinutes(5)) errors.Add("The admission time cannot be in the future.");
            else if (at < localNow.AddDays(-MaxBackdatedAdmissionDays)) errors.Add($"The admission time cannot be more than {MaxBackdatedAdmissionDays} days in the past.");
        }
        if (i.AdmittingDoctorId is int doc && !await _db.Doctors.AnyAsync(x => x.Id == doc, ct)) errors.Add("Select a valid admitting doctor.");
        if (i.PregnancyId is long pg && !await _db.Pregnancies.AnyAsync(p => p.Id == pg && p.PatientId == patient.Id, ct)) errors.Add("The pregnancy record does not belong to this patient.");
        if (await _db.IpdAdmissions.AnyAsync(a => a.PatientId == patient.Id && ActiveStatuses.Contains(a.Status), ct))
            errors.Add($"{patient.PatientNumber} is already admitted. Use the current admission (transfer instead of a second admission).");
        return errors;
    }

    public async Task<IpdAdmission> CreateAdmissionWithinAsync(AdmitInput i, Patient patient, DateOnly today, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var local = i.AdmittedLocal;
        var admittedUtc = local is DateTime l ? await _clock.LocalToUtcAsync(l) : now;
        if (admittedUtc > now) admittedUtc = now; // within the 5-minute tolerance
        var adjusted = Math.Abs((admittedUtc - now).TotalMinutes) > 2;
        var a = new IpdAdmission
        {
            AdmissionNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.IpdAdmissionSequence, "IPD", today, ct),
            IdempotencyKey = i.IdempotencyKey == Guid.Empty ? Guid.NewGuid() : i.IdempotencyKey, PatientId = patient.Id, Kind = i.Kind, Source = i.Source,
            SourceEncounterId = i.SourceEncounterId, DepartmentId = i.DepartmentId, ReferringDoctor = T(i.ReferringDoctor, 150), AdmittingDoctorId = i.AdmittingDoctorId,
            AdmittingDoctorName = T(i.AdmittingDoctorName, 150), AdmissionDiagnosis = T(i.AdmissionDiagnosis, 500), InitialCondition = T(i.InitialCondition, 500),
            MedicalHistory = T(i.MedicalHistory, 2000), Allergies = T(i.Allergies, 500), AttendantName = T(i.AttendantName, 150), AttendantRelation = T(i.AttendantRelation, 50),
            AttendantPhone = T(i.AttendantPhone, 30), PregnancyId = i.PregnancyId, MotherAdmissionId = i.MotherAdmissionId,
            Status = AdmissionStatus.Registered, AdmittedUtc = admittedUtc, AdmissionDate = await _clock.LocalDateOfAsync(admittedUtc),
            CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username, UpdatedUtc = now
        };
        _db.IpdAdmissions.Add(a);
        await _db.SaveChangesAsync(ct);
        Event(a.Id, a.PregnancyId, "Admission", "Registered", null, AdmissionStatus.Registered.ToString(), $"{a.Kind} admission from {a.Source}");
        // Phase 3: admission fee (configurable) on the patient's IPD bill.
        var settings = await _setup.SettingsAsync(ct);
        if (settings.AdmissionServiceItemId is int svc && a.Kind is not AdmissionKind.Newborn)
            a.AdmissionFeeChargeId = (await ChargeAsync(a, svc, 1, 0, null, today, ct, (await _db.ServiceItems.Where(s => s.Id == svc).Select(s => s.Name).FirstAsync(ct)))).Id;
        _audit.Add(AuditActions.IpdAdmitted, "IpdAdmission", a.AdmissionNumber,
            $"{a.Kind} admission {a.AdmissionNumber} for {patient.PatientNumber} from {a.Source}" + (a.AdmissionDiagnosis is null ? "" : $" – {a.AdmissionDiagnosis}")
            + $"; admitted at {TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(admittedUtc, DateTimeKind.Utc), await _clock.ZoneAsync()):dd-MM-yyyy HH:mm}" + (adjusted ? $" (time entered by {_ctx.Username})" : ""));
        await _db.SaveChangesAsync(ct);
        return a;
    }

    /// <summary>Locks the bed row, checks it is free and opens the allocation. The unique index is the final guard against two patients in one bed.</summary>
    public async Task<string?> AllocateWithinAsync(IpdAdmission a, int bedId, string reason, bool shared, long? sharedWith, CancellationToken ct)
    {
        var bed = await _db.IpdBeds.FromSqlRaw("SELECT b.*, b.xmin FROM \"IpdBeds\" b WHERE b.\"Id\" = {0} FOR UPDATE", bedId).Include(b => b.Ward).Include(b => b.Room).FirstOrDefaultAsync(ct);
        if (bed is null || !bed.IsActive || bed.Ward is not { IsActive: true }) return "Select an active bed.";
        if (!shared)
        {
            if (bed.CurrentAdmissionId is not null || bed.Status == BedStatus.Occupied) return $"Bed {bed.Code} is already occupied.";
            if (bed.Status is not (BedStatus.Available or BedStatus.Reserved)) return $"Bed {bed.Code} is not available ({bed.Status}).";
            if (a.Kind == AdmissionKind.Nicu && bed.Ward.Type != WardType.Nicu) return "A NICU admission needs a NICU cot / incubator.";
            if (a.Kind != AdmissionKind.Nicu && bed.Ward.Type == WardType.Nicu) return "NICU cots / incubators are only for NICU admissions.";
        }
        var now = _clock.UtcNow;
        if (await _db.IpdBedAllocations.FirstOrDefaultAsync(x => x.AdmissionId == a.Id && x.ToUtc == null, ct) is { } open)
        {
            open.ToUtc = now;
            open.ReleasedBy = _ctx.Username;
        }
        _db.IpdBedAllocations.Add(new IpdBedAllocation
        {
            AdmissionId = a.Id, BedId = bed.Id, WardId = bed.WardId, FromUtc = now, IsShared = shared, SharedWithAdmissionId = sharedWith,
            Reason = T(reason, 200), AllocatedBy = _ctx.Username
        });
        if (!shared)
        {
            if (bed.Status != BedStatus.Occupied)
                _db.IpdBedStatusChanges.Add(new IpdBedStatusChange { BedId = bed.Id, From = bed.Status, To = BedStatus.Occupied, Note = a.AdmissionNumber, CreatedUtc = now, Username = _ctx.Username });
            bed.Status = BedStatus.Occupied;
            bed.CurrentAdmissionId = a.Id;
            bed.UpdatedUtc = now;
        }
        a.CurrentBedId = bed.Id;
        a.CurrentWardId = bed.WardId;
        a.UpdatedUtc = now;
        if (a.Status == AdmissionStatus.Registered) Status(a, AdmissionStatus.Admitted, $"Bed {bed.Code}");
        _audit.Add(AuditActions.IpdBedAllocated, "IpdAdmission", a.AdmissionNumber, $"{a.AdmissionNumber}: {(shared ? "rooming-in with mother in" : "allocated")} {Where(bed)} ({reason})");
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Closes the admission's open bed; the released bed becomes vacant (Available) at once (Update 11).</summary>
    private async Task ReleaseBedAsync(IpdAdmission a, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        if (await _db.IpdBedAllocations.FirstOrDefaultAsync(x => x.AdmissionId == a.Id && x.ToUtc == null, ct) is { } open)
        {
            open.ToUtc = now;
            open.ReleasedBy = _ctx.Username;
            if (!open.IsShared && await _db.IpdBeds.FirstOrDefaultAsync(b => b.Id == open.BedId, ct) is { } bed && bed.CurrentAdmissionId == a.Id)
            {
                _db.IpdBedStatusChanges.Add(new IpdBedStatusChange { BedId = bed.Id, From = bed.Status, To = BedStatus.Available, Note = $"Released by {a.AdmissionNumber}", CreatedUtc = now, Username = _ctx.Username });
                bed.Status = BedStatus.Available;
                bed.CurrentAdmissionId = null;
                bed.UpdatedUtc = now;
            }
        }
        a.CurrentBedId = null;
    }

    public async Task<ServiceResult> EditAdmissionAsync(long id, AdmitInput i, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!a.IsActive) return ServiceResult.Fail("A closed admission cannot be edited.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) return ServiceResult.Fail("Select a valid department.");
        if (i.AdmittingDoctorId is int doc && !await _db.Doctors.AnyAsync(x => x.Id == doc, ct)) return ServiceResult.Fail("Select a valid admitting doctor.");
        var old = new { a.DepartmentId, a.ReferringDoctor, a.AdmittingDoctorId, a.AdmittingDoctorName, a.AdmissionDiagnosis, a.InitialCondition, a.MedicalHistory, a.Allergies, a.AttendantName, a.AttendantRelation, a.AttendantPhone };
        a.DepartmentId = i.DepartmentId; a.ReferringDoctor = T(i.ReferringDoctor, 150); a.AdmittingDoctorId = i.AdmittingDoctorId; a.AdmittingDoctorName = T(i.AdmittingDoctorName, 150);
        a.AdmissionDiagnosis = T(i.AdmissionDiagnosis, 500); a.InitialCondition = T(i.InitialCondition, 500); a.MedicalHistory = T(i.MedicalHistory, 2000); a.Allergies = T(i.Allergies, 500);
        a.AttendantName = T(i.AttendantName, 150); a.AttendantRelation = T(i.AttendantRelation, 50); a.AttendantPhone = T(i.AttendantPhone, 30); a.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdAdmissionEdited, "IpdAdmission", a.AdmissionNumber, $"Admission {a.AdmissionNumber} details changed", old,
            new { a.DepartmentId, a.ReferringDoctor, a.AdmittingDoctorId, a.AdmittingDoctorName, a.AdmissionDiagnosis, a.InitialCondition, a.MedicalHistory, a.Allergies, a.AttendantName, a.AttendantRelation, a.AttendantPhone });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The admission was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelAdmissionAsync(long id, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for cancelling the admission.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var a = await LockAdmissionAsync(id, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!a.IsActive) return ServiceResult.Fail("This admission is already closed.");
        if (await _db.ClinicalRecords.AnyAsync(r => r.AdmissionId == id, ct) || await _db.DeliveryRecords.AnyAsync(d => d.AdmissionId == id, ct)
            || await _db.MedicationDoses.AnyAsync(d => d.AdmissionId == id && d.Status == DoseStatus.Administered, ct))
            return ServiceResult.Fail("Clinical records exist for this admission – discharge it (e.g. as referred) instead of cancelling.");
        if (await _db.IpdAdmissions.AnyAsync(b => b.MotherAdmissionId == id && ActiveStatuses.Contains(b.Status), ct))
            return ServiceResult.Fail("A newborn is still linked to this admission.");
        var charges = await _db.Charges.Where(c => c.IpdAdmissionId == id && c.Status != ChargeStatus.Cancelled).ToListAsync(ct);
        if (charges.Any(c => c.PaidAmount > 0)) return ServiceResult.Fail("Payments were received for this admission – refund them first (Admin refund).");
        foreach (var c in charges) c.Status = ChargeStatus.Cancelled;
        await ReleaseBedAsync(a, ct);
        Status(a, AdmissionStatus.Cancelled, reason);
        a.CancelReason = reason;
        a.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdAdmissionCancelled, "IpdAdmission", a.AdmissionNumber, $"Admission {a.AdmissionNumber} cancelled: {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetUnderTreatmentAsync(long id, bool attention, string? note, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!a.IsActive) return ServiceResult.Fail("This admission is closed.");
        if (a.Status is AdmissionStatus.Admitted or AdmissionStatus.Transferred) Status(a, AdmissionStatus.UnderTreatment, null);
        if (a.NeedsAttention != attention)
        {
            Event(a.Id, a.PregnancyId, "Admission", attention ? "Needs attention" : "Attention cleared", null, null, T(note, 200));
            a.NeedsAttention = attention;
            a.AttentionNote = attention ? T(note, 200) : null;
        }
        a.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdStatusChanged, "IpdAdmission", a.AdmissionNumber, $"{a.AdmissionNumber}: {Label(a.Status)}{(attention ? " – needs attention: " + T(note, 200) : "")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Transfer
    // =====================================================================================

    public async Task<ServiceResult> TransferAsync(long id, int? toBedId, int? toDepartmentId, string? reason, string? receiving, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for the transfer.");
        if (toBedId is null && toDepartmentId is null) return ServiceResult.Fail("Select the new bed and / or department.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var a = await LockAdmissionAsync(id, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!a.IsActive) return ServiceResult.Fail("This admission is closed.");
        var fromBed = a.CurrentBedId is int fb ? await _db.IpdBeds.AsNoTracking().Include(b => b.Ward).Include(b => b.Room).FirstAsync(b => b.Id == fb, ct) : null;
        var fromDept = a.DepartmentId;
        var shared = fromBed is not null && await _db.IpdBedAllocations.AnyAsync(x => x.AdmissionId == a.Id && x.ToUtc == null && x.IsShared, ct);
        IpdBed? toBed = null;
        if (toBedId is int nb)
        {
            if (nb == a.CurrentBedId && !shared) return ServiceResult.Fail("The patient is already in this bed.");
            // A baby rooming-in with the mother moves to a bed of its own (e.g. NICU) – the mother's bed stays hers.
            if (shared) await CloseSharedAsync(a, ct);
            else await ReleaseBedAsync(a, ct);
            if (await AllocateWithinAsync(a, nb, "Transfer: " + reason, false, null, ct) is { } err)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return ServiceResult.Fail(err);
            }
            toBed = await _db.IpdBeds.AsNoTracking().Include(b => b.Ward).Include(b => b.Room).FirstAsync(b => b.Id == nb, ct);
            if (a.Kind == AdmissionKind.Newborn && toBed.Ward!.Type == WardType.Nicu) a.Kind = AdmissionKind.Nicu;
            await MoveRoomingInBabiesAsync(a, nb, ct);
        }
        if (toDepartmentId is int dep)
        {
            if (!await _db.Departments.AnyAsync(d => d.Id == dep, ct)) return ServiceResult.Fail("Select a valid department.");
            a.DepartmentId = dep;
        }
        var kind = toBed is null ? TransferKind.Department
            : fromBed is null || fromBed.WardId != toBed.WardId ? TransferKind.Ward
            : fromBed.RoomId != toBed.RoomId ? TransferKind.Room : TransferKind.Bed;
        if (toDepartmentId is not null && toDepartmentId != fromDept && kind != TransferKind.Ward) kind = TransferKind.Department;
        var deptNames = await _db.Departments.AsNoTracking().Where(d => d.Id == fromDept || d.Id == a.DepartmentId).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        string Dn(int? d) => d is int x && deptNames.TryGetValue(x, out var n) ? n : "";
        _db.IpdTransfers.Add(new IpdTransfer
        {
            AdmissionId = a.Id, Kind = kind, FromWardId = fromBed?.WardId, FromBedId = fromBed?.Id, FromDepartmentId = fromDept,
            ToWardId = toBed?.WardId ?? fromBed?.WardId, ToBedId = toBed?.Id ?? fromBed?.Id, ToDepartmentId = a.DepartmentId,
            FromText = Cut(Where(fromBed) + (Dn(fromDept) is { Length: > 0 } f ? $" ({f})" : ""), 200),
            ToText = Cut((toBed is null ? Where(fromBed) : Where(toBed)) + (Dn(a.DepartmentId) is { Length: > 0 } t ? $" ({t})" : ""), 200),
            Reason = reason, ReceivingDepartment = T(receiving, 150) ?? Dn(a.DepartmentId), TransferUtc = _clock.UtcNow, Username = _ctx.Username
        });
        if (a.Status is AdmissionStatus.Admitted or AdmissionStatus.UnderTreatment) Status(a, AdmissionStatus.Transferred, $"{kind} transfer");
        a.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdTransferred, "IpdAdmission", a.AdmissionNumber,
            $"{a.AdmissionNumber} transferred ({kind}): {Where(fromBed)} → {(toBed is null ? Where(fromBed) : Where(toBed))} – {reason}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_IpdBedAllocations_OpenBed"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("This bed was just allocated to another patient. Choose another bed.");
        }
        return ServiceResult.Ok();
    }

    private async Task CloseSharedAsync(IpdAdmission a, CancellationToken ct)
    {
        if (await _db.IpdBedAllocations.FirstOrDefaultAsync(x => x.AdmissionId == a.Id && x.ToUtc == null, ct) is { } open)
        {
            open.ToUtc = _clock.UtcNow;
            open.ReleasedBy = _ctx.Username;
        }
        a.CurrentBedId = null;
    }

    /// <summary>Babies rooming-in with a mother follow her to her new bed (shared, not a second occupant).</summary>
    private async Task MoveRoomingInBabiesAsync(IpdAdmission mother, int newBedId, CancellationToken ct)
    {
        var babies = await _db.IpdAdmissions.Where(b => b.MotherAdmissionId == mother.Id && b.Kind == AdmissionKind.Newborn && ActiveStatuses.Contains(b.Status)).ToListAsync(ct);
        foreach (var baby in babies)
        {
            if (!await _db.IpdBedAllocations.AnyAsync(x => x.AdmissionId == baby.Id && x.ToUtc == null && x.IsShared, ct)) continue;
            await CloseSharedAsync(baby, ct);
            await AllocateWithinAsync(baby, newBedId, "With mother", true, mother.Id, ct);
        }
    }

    // =====================================================================================
    // Discharge
    // =====================================================================================

    private static void ApplyDischarge(IpdAdmission a, DischargeInput i)
    {
        a.DischargeType = i.Type ?? a.DischargeType;
        a.FinalDiagnosis = T(i.FinalDiagnosis, 1000) ?? a.FinalDiagnosis;
        a.TreatmentSummary = T(i.TreatmentSummary, 4000) ?? a.TreatmentSummary;
        a.Procedures = T(i.Procedures, 2000) ?? a.Procedures;
        a.InvestigationSummary = T(i.InvestigationSummary, 2000) ?? a.InvestigationSummary;
        a.ConditionAtDischarge = T(i.ConditionAtDischarge, 500) ?? a.ConditionAtDischarge;
        a.FollowUp = T(i.FollowUp, 2000) ?? a.FollowUp;
        a.DischargeMedications = T(i.DischargeMedications, 2000) ?? a.DischargeMedications;
        a.ReferredTo = T(i.ReferredTo, 200) ?? a.ReferredTo;
        a.DischargeDoctor = T(i.DischargeDoctor, 150) ?? a.DischargeDoctor;
    }

    public async Task<ServiceResult> RequestDischargeAsync(long id, DischargeInput i, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!a.IsActive) return ServiceResult.Fail("This admission is closed.");
        if (i.Type is { } t && !Enum.IsDefined(t)) return ServiceResult.Fail("Select the discharge type.");
        ApplyDischarge(a, i);
        a.DischargeRequestedUtc = _clock.UtcNow;
        a.DischargeRequestedBy = _ctx.Username;
        Status(a, AdmissionStatus.DischargeRequested, a.DischargeType?.ToString());
        a.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.IpdDischargeRequested, "IpdAdmission", a.AdmissionNumber, $"Discharge of {a.AdmissionNumber} prepared / requested ({a.DischargeType})");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The admission was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DischargeAsync(long id, DischargeInput i, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var a = await LockAdmissionAsync(id, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!a.IsActive) return ServiceResult.Fail("This admission is already closed.");
        ApplyDischarge(a, i);
        var errors = new List<string>();
        if (a.DischargeType is null) errors.Add("Select the discharge type.");
        if (a.ConditionAtDischarge is null) errors.Add("Enter the condition at discharge.");
        if (a.FinalDiagnosis is null && a.DischargeType is not (DischargeType.Lama or DischargeType.Death)) errors.Add("Enter the final diagnosis.");
        if (a.DischargeType is DischargeType.Referred or DischargeType.Transferred && a.ReferredTo is null) errors.Add("Enter the receiving facility.");
        if (await _db.IpdAdmissions.AnyAsync(b => b.MotherAdmissionId == a.Id && b.Kind == AdmissionKind.Newborn && ActiveStatuses.Contains(b.Status), ct))
            errors.Add("A newborn is rooming-in with the mother – discharge or transfer the baby first.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var posted = await PostBedChargesWithinAsync(a, atDischarge: true, ct);
        // Update 10: running equipment stops at discharge (charged up to now).
        var billing = _services.GetRequiredService<IIpdBillingService>();
        await billing.EndAllEquipmentAsync(a.Id, "Stopped at discharge", ct);
        await _db.SaveChangesAsync(ct);
        if ((await _setup.SettingsAsync(ct)).RequireBillClearanceForDischarge)
        {
            // Financial clearance: the balance after the unused advance must be zero, unless an authorised user recorded an override.
            var (due, overridden) = await billing.ClearanceAsync(a.Id, ct);
            if (due > 0 && !overridden)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return ServiceResult.Fail($"The bill has an outstanding balance of {due:N2}. Receive the payment in Financial clearance, or an authorised user must override the clearance with a reason.");
            }
        }
        // The bed is released only now, when the discharge is complete.
        await ReleaseBedAsync(a, ct);
        var now = _clock.UtcNow;
        a.DischargedUtc = now;
        a.DischargeDate = await _clock.TodayAsync();
        a.DischargedBy = _ctx.Username;
        Status(a, a.DischargeType == DischargeType.Referred ? AdmissionStatus.Referred : AdmissionStatus.Discharged, a.DischargeType.ToString());
        a.UpdatedUtc = now;
        if (a.Kind == AdmissionKind.Labour && await _db.LabourRecords.FirstOrDefaultAsync(l => l.AdmissionId == a.Id, ct) is { } lab
            && lab.Status is not (LabourStatus.Referred or LabourStatus.Transferred or LabourStatus.Discharged))
        {
            Event(a.Id, a.PregnancyId, "Labour", "Status", lab.Status.ToString(), LabourStatus.Discharged.ToString(), "Discharged");
            lab.Status = a.DischargeType switch { DischargeType.Referred => LabourStatus.Referred, DischargeType.Transferred => LabourStatus.Transferred, _ => LabourStatus.Discharged };
        }
        _audit.Add(AuditActions.IpdDischarged, "IpdAdmission", a.AdmissionNumber,
            $"{a.AdmissionNumber} discharged ({a.DischargeType}) by {_ctx.Username}{(posted > 0 ? $"; {posted} bed-night charge(s) posted" : "")}" + (a.ReferredTo is null ? "" : $" → {a.ReferredTo}"));
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The admission was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Billing (Phase 3 charges only)
    // =====================================================================================

    private Task<Charge> ChargeAsync(IpdAdmission a, int serviceItemId, int quantity, decimal discount, string? reason, DateOnly date, CancellationToken ct, string? name = null) =>
        ChargeAsync(a, serviceItemId, (decimal)quantity, discount, reason, date, ct, name);

    private async Task<Charge> ChargeAsync(IpdAdmission a, int serviceItemId, decimal quantity, decimal discount, string? reason, DateOnly date, CancellationToken ct, string? name = null)
    {
        var svc = await _db.ServiceItems.AsNoTracking().FirstAsync(s => s.Id == serviceItemId, ct);
        var unit = await _finance.FeeOnAsync(svc.Id, date, ct);
        var fee = decimal.Round(unit * quantity, 2, MidpointRounding.AwayFromZero);
        // Update 10: a service included in a package of this admission is shown on the bill but not charged again.
        var package = await _db.AdmissionPackages.Where(x => x.AdmissionId == a.Id && x.IsActive && x.Package!.Items.Any(i => i.ServiceItemId == serviceItemId))
            .Select(x => x.Package!.Name).FirstOrDefaultAsync(ct);
        if (package is not null && fee > 0) { discount = fee; reason = T($"Included in package {package}", 200); }
        var c = new Charge
        {
            ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", await _clock.TodayAsync(), ct),
            PatientId = a.PatientId, IpdAdmissionId = a.Id, ServiceItemId = svc.Id, ServiceKind = svc.Kind, ServiceName = name ?? (quantity != 1 ? $"{svc.Name} × {quantity:0.##}" : svc.Name),
            Quantity = quantity, UnitPrice = unit,
            DepartmentId = svc.DepartmentId, Fee = fee, Discount = discount, NetAmount = fee - discount, AllowPartialPayment = true,
            DiscountReason = discount > 0 ? reason : null, DoctorShareType = DoctorShareType.HospitalKeepsAll, Status = fee - discount == 0 ? ChargeStatus.Paid : ChargeStatus.Unpaid,
            BusinessDate = await _clock.TodayAsync(), CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        };
        _db.Charges.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<ServiceResult<int>> FinaliseBillAsync(long admissionId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var a = await LockAdmissionAsync(admissionId, ct);
        if (a is null) return ServiceResult<int>.Fail("Admission not found.");
        if (!a.IsActive) return ServiceResult<int>.Fail("This admission is already closed.");
        var n = await PostBedChargesWithinAsync(a, atDischarge: true, ct);
        n += await _services.GetRequiredService<IIpdBillingService>().EndAllEquipmentAsync(a.Id, "Stopped for the final bill", ct);
        _audit.Add(AuditActions.IpdChargePosted, "IpdAdmission", a.AdmissionNumber, $"Final bill prepared for {a.AdmissionNumber}: {n} charge(s) posted");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(n);
    }

    public async Task<ServiceResult<int>> PostBedChargesAsync(long admissionId, bool atDischarge, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var a = await LockAdmissionAsync(admissionId, ct);
        if (a is null) return ServiceResult<int>.Fail("Admission not found.");
        var n = await PostBedChargesWithinAsync(a, atDischarge, ct);
        if (n > 0) _audit.Add(AuditActions.IpdChargePosted, "IpdAdmission", a.AdmissionNumber, $"{n} bed-night charge(s) posted for {a.AdmissionNumber}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(n);
    }

    /// <summary>
    /// One charge per completed night in a bed, at the fee of the ward the patient was in that night (unique per night – never billed twice).
    /// At discharge at least one night is charged when the patient had a bed. Rooming-in babies share the mother's bed and pay no bed charge.
    /// </summary>
    private async Task<int> PostBedChargesWithinAsync(IpdAdmission a, bool atDischarge, CancellationToken ct)
    {
        var allocations = await _db.IpdBedAllocations.AsNoTracking().Where(x => x.AdmissionId == a.Id && !x.IsShared).OrderBy(x => x.FromUtc).ToListAsync(ct);
        if (allocations.Count == 0) return 0;
        var zone = await _clock.ZoneAsync();
        var policy = await _setup.SettingsAsync(ct);
        if (policy.BedChargePolicy != BedChargePolicy.PerNight)
            return await PostPolicyBedChargesAsync(a, allocations, policy, atDischarge, zone, ct);
        var today = await _clock.TodayAsync();
        var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(allocations[0].FromUtc, zone));
        var endExclusive = atDischarge ? (today > start ? today : start.AddDays(1)) : today;
        var done = (await _db.IpdBedCharges.Where(x => x.AdmissionId == a.Id).Select(x => x.Night).ToListAsync(ct)).ToHashSet();
        var wards = await _db.IpdWards.AsNoTracking().ToDictionaryAsync(w => w.Id, ct);
        var posted = 0;
        for (var night = start; night < endExclusive; night = night.AddDays(1))
        {
            if (done.Contains(night)) continue;
            // The ward of that night: the last allocation that began before the end of the day.
            var endOfDay = TimeZoneInfo.ConvertTimeToUtc(night.ToDateTime(new TimeOnly(23, 59)), zone);
            var alloc = allocations.LastOrDefault(x => x.FromUtc <= endOfDay) ?? allocations[0];
            if (!wards.TryGetValue(alloc.WardId, out var ward) || ward.BedChargeServiceItemId is not int svc) continue;
            var c = await ChargeAsync(a, svc, 1, 0, null, night, ct, $"{ward.Name} – bed {night:dd MMM yyyy}");
            _db.IpdBedCharges.Add(new IpdBedCharge { AdmissionId = a.Id, Night = night, WardId = ward.Id, ChargeId = c.Id });
            await _db.SaveChangesAsync(ct);
            posted++;
        }
        return posted;
    }

    /// <summary>
    /// Update 10: one bed charge per billing day (unique per admission and day – never twice, never overlapping). The billing day starts
    /// at the cutoff time (cutoff / half-day policies) or at midnight (hourly). After a transfer the day is charged at the ward where the
    /// patient spent most of it. Only completed billing days are posted, plus the running day at discharge.
    /// </summary>
    private async Task<int> PostPolicyBedChargesAsync(IpdAdmission a, IReadOnlyList<IpdBedAllocation> allocations, IpdSettings s, bool atDischarge, TimeZoneInfo zone, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var days = BedDays(allocations.Select(x => (x.FromUtc, x.ToUtc, x.WardId)).ToList(), s.BedChargePolicy, s.BedChargeCutoff, s.HalfDayHours, now, atDischarge, zone);
        var done = (await _db.IpdBedCharges.Where(x => x.AdmissionId == a.Id).Select(x => x.Night).ToListAsync(ct)).ToHashSet();
        var wards = await _db.IpdWards.AsNoTracking().ToDictionaryAsync(w => w.Id, ct);
        var posted = 0;
        foreach (var d in days)
        {
            if (done.Contains(d.Day) || d.Quantity <= 0) continue;
            if (!wards.TryGetValue(d.WardId, out var ward) || ward.BedChargeServiceItemId is not int svc) continue;
            var label = s.BedChargePolicy == BedChargePolicy.Hourly ? $"{ward.Name} – bed {d.Day:dd MMM yyyy} ({d.Quantity:0.##} h)"
                : d.Quantity == 1 ? $"{ward.Name} – bed day {d.Day:dd MMM yyyy}" : $"{ward.Name} – bed {d.Day:dd MMM yyyy} (½ day)";
            var c = await ChargeAsync(a, svc, d.Quantity, 0, null, d.Day, ct, label);
            _db.IpdBedCharges.Add(new IpdBedCharge { AdmissionId = a.Id, Night = d.Day, WardId = ward.Id, ChargeId = c.Id, Quantity = d.Quantity });
            await _db.SaveChangesAsync(ct);
            posted++;
        }
        return posted;
    }

    /// <summary>Billing days of the bed periods under a charging policy (pure function – also used by the tests).</summary>
    public static IReadOnlyList<(DateOnly Day, int WardId, decimal Quantity)> BedDays(IReadOnlyList<(DateTime FromUtc, DateTime? ToUtc, int WardId)> periods, BedChargePolicy policy,
        TimeOnly cutoff, int halfDayHours, DateTime nowUtc, bool atDischarge, TimeZoneInfo zone)
    {
        var result = new List<(DateOnly, int, decimal)>();
        if (periods.Count == 0) return result;
        var start = periods.Min(p => p.FromUtc);
        var end = periods.Max(p => p.ToUtc ?? nowUtc);
        if (end > nowUtc) end = nowUtc;
        var dayStart = policy == BedChargePolicy.Hourly ? TimeOnly.MinValue : cutoff;
        var local = TimeZoneInfo.ConvertTimeFromUtc(start, zone);
        var day = DateOnly.FromDateTime(local);
        if (TimeOnly.FromDateTime(local) < dayStart) day = day.AddDays(-1);
        for (; ; day = day.AddDays(1))
        {
            var winStart = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(dayStart), zone);
            var winEnd = TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(dayStart), zone);
            if (winStart >= end) break;
            if (winEnd > nowUtc && !atDischarge) break; // the running billing day is charged when it is complete (or at discharge)
            var byWard = periods.Select(p => (p.WardId, Hours: (Min(p.ToUtc ?? nowUtc, winEnd) - Max(p.FromUtc, winStart)).TotalHours))
                .Where(x => x.Hours > 0).GroupBy(x => x.WardId).Select(g => (Ward: g.Key, Hours: g.Sum(x => x.Hours))).ToList();
            if (byWard.Count == 0) continue;
            var hours = byWard.Sum(x => x.Hours);
            var ward = byWard.OrderByDescending(x => x.Hours).First().Ward;
            var qty = policy switch
            {
                BedChargePolicy.Hourly => Math.Max(1m, Math.Ceiling((decimal)hours)),
                BedChargePolicy.HalfDay => hours < halfDayHours ? 0.5m : 1m,
                _ => 1m,
            };
            result.Add((day, ward, qty));
        }
        return result;

        static DateTime Min(DateTime x, DateTime y) => x < y ? x : y;
        static DateTime Max(DateTime x, DateTime y) => x > y ? x : y;
    }

    public async Task<ServiceResult> AddServiceChargeAsync(long admissionId, int serviceItemId, int quantity, decimal discount, string? reason, bool canDiscount, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (a.Status == AdmissionStatus.Cancelled) return ServiceResult.Fail("This admission was cancelled.");
        var svc = await _db.ServiceItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceItemId && s.IsActive, ct);
        if (svc is null) return ServiceResult.Fail("Select an active service.");
        if (svc.Kind == ServiceKind.Pharmacy) return ServiceResult.Fail("Medicines are billed through the Pharmacy patient issue of this admission.");
        if (svc.Kind is ServiceKind.Laboratory or ServiceKind.XRay or ServiceKind.Ultrasound
            || await _db.RadiologyServices.AnyAsync(r => r.ServiceItemId == svc.Id, ct) || await _db.LabTests.AnyAsync(t => t.ServiceItemId == svc.Id, ct))
            return ServiceResult.Fail("Investigations are billed when they are booked in the Laboratory / Radiology module for this admission.");
        if (quantity is < 1 or > 100) return ServiceResult.Fail("Quantity must be between 1 and 100.");
        if (discount != 0 && !canDiscount) return ServiceResult.Fail("You are not allowed to give discounts.");
        var fee = await _finance.FeeOnAsync(svc.Id, await _clock.TodayAsync(), ct) * quantity;
        if (discount < 0 || discount > fee || !FinanceMath.IsMoney(discount)) return ServiceResult.Fail($"The discount must be between 0 and {fee:N2}.");
        if (discount > 0 && T(reason, 200) is null) return ServiceResult.Fail("Enter the reason for the discount.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var c = await ChargeAsync(a, svc.Id, quantity, discount, T(reason, 200), await _clock.TodayAsync(), ct);
        _audit.Add(AuditActions.IpdChargePosted, "IpdAdmission", a.AdmissionNumber, $"{c.ServiceName} {c.NetAmount:N2} added to the IPD bill of {a.AdmissionNumber}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Clinical records (insert-only; corrections are new records)
    // =====================================================================================

    public async Task<ServiceResult<long>> AddRecordAsync(RecordInput i, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(i.Kind)) return ServiceResult<long>.Fail("Unknown form.");
        if (!await _db.Patients.AnyAsync(p => p.Id == i.PatientId, ct)) return ServiceResult<long>.Fail("Patient not found.");
        if (i.AdmissionId is long aid && !await _db.IpdAdmissions.AnyAsync(a => a.Id == aid && a.PatientId == i.PatientId, ct)) return ServiceResult<long>.Fail("The admission does not belong to this patient.");
        if (i.PregnancyId is long pid && !await _db.Pregnancies.AnyAsync(p => p.Id == pid && p.PatientId == i.PatientId, ct)) return ServiceResult<long>.Fail("The pregnancy does not belong to this patient.");
        if (i.SurgeryId is long sid && !await _db.Surgeries.AnyAsync(x => x.Id == sid && x.PatientId == i.PatientId, ct)) return ServiceResult<long>.Fail("The surgery does not belong to this patient.");
        var fields = await _setup.FieldsAsync(i.Kind, activeOnly: true, ct);
        var errors = new List<string>();
        var values = new Dictionary<string, object?>();
        foreach (var f in fields)
        {
            i.Values.TryGetValue(f.Key, out var raw);
            raw = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            if (raw is null)
            {
                // Missing data stays missing – it is never stored as a normal value.
                if (f.IsRequired && i.CorrectsRecordId is null) errors.Add($"{f.Label}: required.");
                continue;
            }
            switch (f.Type)
            {
                case FieldType.Number:
                    if (!decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) || Math.Abs(n) > 1_000_000) { errors.Add($"{f.Label}: enter a number."); continue; }
                    values[f.Key] = n;
                    break;
                case FieldType.Choice:
                    var choices = (f.Choices ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var match = choices.FirstOrDefault(c => c.Equals(raw, StringComparison.OrdinalIgnoreCase));
                    if (match is null) { errors.Add($"{f.Label}: choose one of {string.Join(", ", choices)}."); continue; }
                    values[f.Key] = match;
                    break;
                case FieldType.YesNo:
                    if (raw is not ("Yes" or "No")) { errors.Add($"{f.Label}: choose Yes or No."); continue; }
                    values[f.Key] = raw;
                    break;
                default:
                    var max = f.Type == FieldType.LongText ? 4000 : 300;
                    values[f.Key] = raw.Length > max ? raw[..max] : raw;
                    break;
            }
        }
        var notes = T(i.Notes, 4000);
        if (values.Count == 0 && notes is null && errors.Count == 0) errors.Add("Enter at least one value or a note.");
        ClinicalRecord? original = null;
        if (i.CorrectsRecordId is long cid)
        {
            original = await _db.ClinicalRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == cid, ct);
            if (original is null || original.PatientId != i.PatientId || original.Kind != i.Kind) errors.Add("The record to correct was not found.");
            else if (await _db.ClinicalRecords.AnyAsync(r => r.CorrectsRecordId == cid, ct)) errors.Add("This record was already corrected – correct the latest version.");
            if (T(i.CorrectionReason, 300) is null) errors.Add("Enter the reason for the correction.");
        }
        if (errors.Count > 0) return ServiceResult<long>.Fail(errors.ToArray());
        var now = _clock.UtcNow;
        var recordedUtc = i.RecordedLocal is DateTime l ? await _clock.LocalToUtcAsync(l) : original?.RecordedUtc ?? now;
        if (recordedUtc > now.AddMinutes(5)) return ServiceResult<long>.Fail("The observation time cannot be in the future.");
        var r = new ClinicalRecord
        {
            Kind = i.Kind, PatientId = i.PatientId, AdmissionId = i.AdmissionId ?? original?.AdmissionId, PregnancyId = i.PregnancyId ?? original?.PregnancyId, SurgeryId = i.SurgeryId ?? original?.SurgeryId,
            RecordedUtc = recordedUtc, EnteredUtc = now, UserId = _ctx.UserId, Username = _ctx.Username, ResponsibleName = T(i.ResponsibleName, 150),
            Values = JsonSerializer.Serialize(values), Notes = notes, CorrectsRecordId = i.CorrectsRecordId, CorrectionReason = T(i.CorrectionReason, 300)
        };
        _db.ClinicalRecords.Add(r);
        if (r.AdmissionId is long adm && await _db.IpdAdmissions.FirstOrDefaultAsync(x => x.Id == adm, ct) is { } a
            && a.Status is AdmissionStatus.Admitted && i.Kind is RecordKind.ProgressNote or RecordKind.NursingAssessment)
            Status(a, AdmissionStatus.UnderTreatment, null);
        await _db.SaveChangesAsync(ct);
        _audit.Add(original is null ? AuditActions.IpdRecordAdded : AuditActions.IpdRecordCorrected, "ClinicalRecord", r.Id.ToString(),
            original is null ? $"{i.Kind} recorded for patient {i.PatientId}" : $"{i.Kind} record {original.Id} corrected: {r.CorrectionReason}",
            original is null ? null : new { original.Values, original.Notes }, original is null ? null : new { r.Values, r.Notes });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(r.Id);
    }

    // =====================================================================================
    // Nursing tasks & shift handover
    // =====================================================================================

    public async Task<ServiceResult<long>> AddTaskAsync(long admissionId, NursingTaskType type, string? description, DateTime? dueLocal, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null || !a.IsActive) return ServiceResult<long>.Fail("Tasks can only be added to an active admission.");
        if (!Enum.IsDefined(type)) return ServiceResult<long>.Fail("Select the task type.");
        description = T(description, 300) ?? (type == NursingTaskType.Other ? null : type.ToString());
        if (description is null) return ServiceResult<long>.Fail("Describe the task.");
        var t = new NursingTask
        {
            AdmissionId = admissionId, Type = type, Description = description, DueUtc = dueLocal is DateTime d ? await _clock.LocalToUtcAsync(d) : _clock.UtcNow,
            CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username
        };
        _db.NursingTasks.Add(t);
        _audit.Add(AuditActions.IpdTaskSaved, "IpdAdmission", a.AdmissionNumber, $"Nursing task for {a.AdmissionNumber}: {description}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(t.Id);
    }

    public async Task<ServiceResult> CompleteTaskAsync(long taskId, bool cancel, string? note, CancellationToken ct = default)
    {
        var t = await _db.NursingTasks.FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (t is null) return ServiceResult.Fail("Task not found.");
        if (t.Status != NursingTaskStatus.Pending) return ServiceResult.Fail("This task is already closed.");
        if (cancel && T(note, 300) is null) return ServiceResult.Fail("Enter the reason for cancelling the task.");
        t.Status = cancel ? NursingTaskStatus.Cancelled : NursingTaskStatus.Done;
        t.CompletedUtc = _clock.UtcNow;
        t.CompletedBy = _ctx.Username;
        t.Note = T(note, 300);
        _audit.Add(AuditActions.IpdTaskSaved, "NursingTask", t.Id.ToString(), $"Nursing task {t.Description} {(cancel ? "cancelled" : "done")}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The task was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<long>> HandoverAsync(long admissionId, HandoverInput i, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null || !a.IsActive) return ServiceResult<long>.Fail("A handover needs an active admission.");
        if (T(i.PatientCondition, 1000) is null) return ServiceResult<long>.Fail("Describe the patient's condition.");
        var h = new ShiftHandover
        {
            AdmissionId = a.Id, WardId = a.CurrentWardId, Shift = T(i.Shift, 30), OutgoingUserId = _ctx.UserId, OutgoingUsername = _ctx.Username, CreatedUtc = _clock.UtcNow,
            PatientCondition = T(i.PatientCondition, 1000), Diagnosis = T(i.Diagnosis, 500), PendingInvestigations = T(i.PendingInvestigations, 500), MedicationDue = T(i.MedicationDue, 500),
            IvFluids = T(i.IvFluids, 500), ImportantObservations = T(i.ImportantObservations, 1000), PendingProcedures = T(i.PendingProcedures, 500),
            SpecialCare = T(i.SpecialCare, 500), OutstandingTasks = T(i.OutstandingTasks, 500)
        };
        _db.ShiftHandovers.Add(h);
        _audit.Add(AuditActions.IpdHandoverCreated, "IpdAdmission", a.AdmissionNumber, $"Shift handover ({h.Shift}) for {a.AdmissionNumber} by {_ctx.Username}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(h.Id);
    }

    public async Task<ServiceResult> AcknowledgeHandoverAsync(long handoverId, string? note, CancellationToken ct = default)
    {
        var h = await _db.ShiftHandovers.FirstOrDefaultAsync(x => x.Id == handoverId, ct);
        if (h is null) return ServiceResult.Fail("Handover not found.");
        if (h.AcknowledgedUtc is not null) return ServiceResult.Fail($"Already acknowledged by {h.AcknowledgedByUsername}.");
        if (h.OutgoingUserId == _ctx.UserId) return ServiceResult.Fail("The incoming nurse must acknowledge the handover – not the outgoing nurse.");
        h.AcknowledgedByUserId = _ctx.UserId;
        h.AcknowledgedByUsername = _ctx.Username;
        h.AcknowledgedUtc = _clock.UtcNow;
        h.AcknowledgeNote = T(note, 300);
        _audit.Add(AuditActions.IpdHandoverAcknowledged, "ShiftHandover", h.Id.ToString(), $"Handover {h.Id} from {h.OutgoingUsername} acknowledged by {_ctx.Username}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The handover was acknowledged by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Medication orders & administration record (MAR)
    // =====================================================================================

    public static IReadOnlyList<TimeOnly>? ParseTimes(string? s)
    {
        var list = new List<TimeOnly>();
        foreach (var part in (s ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TimeOnly.TryParseExact(part.Trim(), new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return null;
            list.Add(t);
        }
        return list.Count is > 0 and <= 24 ? list.Distinct().OrderBy(t => t).ToList() : null;
    }

    public async Task<ServiceResult<long>> OrderMedicationAsync(long admissionId, MedicationOrderInput i, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null || !a.IsActive) return ServiceResult<long>.Fail("Medication can only be ordered for an active admission.");
        var errors = new List<string>();
        var med = await _db.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == i.MedicineId && m.IsActive, ct);
        if (med is null) errors.Add("Select the medicine from the pharmacy catalogue.");
        if (T(i.Dose, 60) is null) errors.Add("Enter the dose.");
        if (T(i.Route, 40) is null) errors.Add("Enter the route.");
        if (T(i.Frequency, 60) is null) errors.Add("Enter the frequency.");
        var times = ParseTimes(i.Times);
        if (times is null) errors.Add("Enter the administration times, e.g. 08:00,14:00,20:00.");
        if (i.DurationDays is not (>= 1 and <= 60)) errors.Add("Duration must be 1 to 60 days.");
        if (T(i.Prescriber, 150) is null) errors.Add("Enter the prescribing clinician (as written on the prescription).");
        if (!Enum.IsDefined(i.Source)) errors.Add("Select the source of the order.");
        if (i.StockUnitsPerDose is < 0 or > 50) errors.Add("Ward stock units per dose must be 0 to 50.");
        if (errors.Count > 0) return ServiceResult<long>.Fail(errors.ToArray());
        var now = _clock.UtcNow;
        var startUtc = i.StartLocal is DateTime s ? await _clock.LocalToUtcAsync(s) : now;
        var stopUtc = startUtc.AddDays(i.DurationDays!.Value);
        var o = new MedicationOrder
        {
            AdmissionId = a.Id, PatientId = a.PatientId, MedicineId = med!.Id, MedicineName = med.DisplayName, Dose = T(i.Dose, 60)!, Route = T(i.Route, 40)!,
            Frequency = T(i.Frequency, 60)!, Times = string.Join(",", times!.Select(t => t.ToString("HH:mm"))), DurationDays = i.DurationDays, StartUtc = startUtc, StopUtc = stopUtc,
            Prescriber = T(i.Prescriber, 150)!, Source = i.Source, Instructions = T(i.Instructions, 500), StockUnitsPerDose = i.StockUnitsPerDose, CreatedUtc = now, EnteredBy = _ctx.Username
        };
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.MedicationOrders.Add(o);
        await _db.SaveChangesAsync(ct);
        // Schedule every dose of the order (the nurse then records each one; nothing is ever marked administered automatically).
        var zone = await _clock.ZoneAsync();
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(startUtc, zone);
        for (var day = DateOnly.FromDateTime(startLocal); day <= DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(stopUtc, zone)); day = day.AddDays(1))
            foreach (var t in times!)
            {
                var due = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(t), zone);
                if (due < startUtc.AddMinutes(-1) || due >= stopUtc) continue;
                _db.MedicationDoses.Add(new MedicationDose { OrderId = o.Id, AdmissionId = a.Id, ScheduledUtc = due });
            }
        _audit.Add(AuditActions.IpdMedicationOrdered, "IpdAdmission", a.AdmissionNumber,
            $"Medication order for {a.AdmissionNumber}: {o.MedicineName} {o.Dose} {o.Route} {o.Frequency} for {o.DurationDays} day(s) – {o.Prescriber} ({o.Source}), entered by {_ctx.Username}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<long>.Ok(o.Id);
    }

    public async Task<ServiceResult> StopMedicationAsync(long orderId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for stopping the order.");
        var o = await _db.MedicationOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null) return ServiceResult.Fail("Order not found.");
        if (o.Status != MedicationOrderStatus.Active) return ServiceResult.Fail("This order is already stopped.");
        var now = _clock.UtcNow;
        o.Status = MedicationOrderStatus.Stopped;
        o.StopUtc = now;
        o.StopReason = reason;
        o.StoppedBy = _ctx.Username;
        foreach (var d in await _db.MedicationDoses.Where(d => d.OrderId == o.Id && d.Status == DoseStatus.Scheduled && d.ScheduledUtc > now).ToListAsync(ct))
        {
            d.Status = DoseStatus.Cancelled;
            d.Reason = "Order stopped: " + reason;
            d.RecordedUtc = now;
            d.RecordedBy = _ctx.Username;
        }
        _audit.Add(AuditActions.IpdMedicationStopped, "MedicationOrder", o.Id.ToString(), $"{o.MedicineName} stopped: {reason}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RecordDoseAsync(long doseId, DoseInput i, CancellationToken ct = default)
    {
        if (i.Status is not (DoseStatus.Administered or DoseStatus.Missed or DoseStatus.Withheld)) return ServiceResult.Fail("Choose administered, missed or withheld.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"MARDOSE|{doseId}", ct);
        var d = await _db.MedicationDoses.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == doseId, ct);
        if (d is null) return ServiceResult.Fail("Dose not found.");
        if (d.Status is not (DoseStatus.Scheduled or DoseStatus.Pending)) return ServiceResult.Fail($"This dose is already recorded as {d.Status}.");
        var a = await _db.IpdAdmissions.Include(x => x.Patient).FirstAsync(x => x.Id == d.AdmissionId, ct);
        if (!a.IsActive) return ServiceResult.Fail("The admission is closed.");
        var errors = new List<string>();
        var settings = await _setup.SettingsAsync(ct);
        if (i.Status == DoseStatus.Administered)
        {
            if (settings.RequireMarConfirmations && !(i.ConfirmPatient && i.ConfirmMedicine && i.ConfirmDose && i.ConfirmRoute && i.ConfirmTime))
                errors.Add("Confirm the patient, medicine, dose, route and time before recording the dose.");
            if (T(i.DoseGiven, 60) is null) errors.Add("Enter the dose given.");
            if (T(i.RouteGiven, 40) is null) errors.Add("Enter the route.");
        }
        else if (T(i.Reason, 300) is null) errors.Add("Enter the reason why the dose was not given.");
        if (T(i.Nurse, 150) is null && i.Status == DoseStatus.Administered) i.Nurse = _ctx.Username;
        var now = _clock.UtcNow;
        var givenUtc = i.GivenLocal is DateTime g ? await _clock.LocalToUtcAsync(g) : now;
        if (givenUtc > now.AddMinutes(5)) errors.Add("The time cannot be in the future.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        d.Status = i.Status;
        d.GivenUtc = i.Status == DoseStatus.Administered ? givenUtc : null;
        d.DoseGiven = T(i.DoseGiven, 60);
        d.RouteGiven = T(i.RouteGiven, 40);
        d.Nurse = T(i.Nurse, 150);
        d.Reason = T(i.Reason, 300);
        d.Remarks = T(i.Remarks, 300);
        d.RecordedUtc = now;
        d.RecordedBy = _ctx.Username;
        // Ward stock (Phase 5): only when the ward keeps stock and the order says how many units a dose uses.
        if (i.Status == DoseStatus.Administered && d.Order!.StockUnitsPerDose > 0 && a.CurrentWardId is int wid
            && await _db.IpdWards.AsNoTracking().Where(w => w.Id == wid).Select(w => w.StockLocationId).FirstOrDefaultAsync(ct) is int loc)
        {
            var med = await _db.Medicines.AsNoTracking().FirstAsync(m => m.Id == d.Order.MedicineId, ct);
            var today = await _clock.TodayAsync();
            var (alloc, err) = await StockEngine.AllocateAsync(_db, loc, med, d.Order.StockUnitsPerDose, null, false, today, ct);
            if (err is not null) { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); return ServiceResult.Fail("Ward stock: " + err + " Issue the medicine to the ward (pharmacy transfer) first."); }
            foreach (var x in alloc!)
            {
                if (await StockEngine.PostAsync(_db, new StockMove(InventoryTxnType.WardAdministration, loc, med.Id, x.Batch.Id, -x.Quantity, "MAR", d.Id, a.AdmissionNumber,
                        PatientId: a.PatientId, Note: $"Dose {d.ScheduledUtc:dd MMM HH:mm} UTC"), today, now, _ctx.UserId, _ctx.Username, ct) is null)
                { await tx.RollbackAsync(ct); _db.ChangeTracker.Clear(); return ServiceResult.Fail("Ward stock is not sufficient."); }
            }
            await _db.SaveChangesAsync(ct);
            d.StockTransactionId = await _db.InventoryTransactions.Where(t => t.DocumentType == "MAR" && t.DocumentId == d.Id).OrderBy(t => t.Id).Select(t => (long?)t.Id).FirstOrDefaultAsync(ct);
        }
        _audit.Add(AuditActions.IpdDoseRecorded, "IpdAdmission", a.AdmissionNumber,
            $"{d.Order!.MedicineName} dose of {d.ScheduledUtc:dd MMM HH:mm} UTC for {a.Patient!.PatientNumber}: {i.Status}" + (d.Reason is null ? "" : $" – {d.Reason}"));
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The dose was recorded by someone else. Please reload."); }
        return ServiceResult.Ok();
    }
}
