using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BurhanNexus.HMS.Infrastructure.Patients;

public sealed class BookAppointmentInput
{
    public long PatientId { get; set; }
    public int DoctorId { get; set; }
    /// <summary>Hospital-local date; null = today.</summary>
    public DateOnly? AppointmentDate { get; set; }
    public TimeOnly? AppointmentTime { get; set; }
    public string? Complaint { get; set; }
    public string? Notes { get; set; }

    // ---- consultation fee (update 9) ----
    /// <summary>Booking-form key: a double click returns the same appointment and never collects the fee twice.</summary>
    public Guid IdempotencyKey { get; set; }
    /// <summary>"Paid" (fee received now) or "FreeByDoctor" (the doctor waived the fee). Required when the doctor has a fee.</summary>
    public string? Payment { get; set; }
    public int CounterId { get; set; }
    public int PaymentMethodId { get; set; }
    public string? PaymentReference { get; set; }
    /// <summary>Free by Doctor: the doctor who waived (default: the appointment's doctor) and the reason.</summary>
    public int? WaiverDoctorId { get; set; }
    public string? WaiverReason { get; set; }
}

/// <summary>"Free by Doctor" recorded after booking.</summary>
public sealed class WaiverInput
{
    public int? WaiverDoctorId { get; set; }
    public string? Reason { get; set; }
    /// <summary>Only when the fee had been collected: the money was actually handed back to the patient (a normal refund is recorded).</summary>
    public bool CashReturned { get; set; }
    public int RefundMethodId { get; set; }
}

public sealed class EditAppointmentInput
{
    public TimeOnly? AppointmentTime { get; set; }
    public string? Complaint { get; set; }
    public string? Notes { get; set; }
}

public sealed record AppointmentListItem(
    long Id, string AppointmentNumber, long EncounterId, int DoctorId, string DoctorName, string? DoctorSpecialty,
    DateOnly AppointmentDate, TimeOnly? AppointmentTime, int TokenNumber, AppointmentStatus Status,
    long PatientId, string PatientNumber, string PatientName, string? GuardianName, Gender Gender, string? AgeText, string? MobileNumber,
    string? Complaint, decimal? ConsultationFee, DateTime BookedUtc, string? BookedByUsername, string? CancelReason, int PrintCount,
    AppointmentPayment PaymentStatus = AppointmentPayment.NotRecorded, decimal? AmountPaid = null, decimal? WaivedAmount = null, string? WaivedByUsername = null);

public sealed class AppointmentQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? DoctorId { get; set; }
    public long? PatientId { get; set; }
    /// <summary>"active" (not cancelled), "cancelled" or null for all.</summary>
    public string? Status { get; set; }
    /// <summary>"paid", "free" (free + free by doctor), "freebydoctor", "notrecorded" or null for all.</summary>
    public string? Payment { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 500;
}

/// <summary>Booked = all appointments that were not cancelled ("seen" no longer exists as a step).</summary>
public sealed record DoctorAppointmentStats(int DoctorId, string DoctorName, string? Specialty, int Booked, int Cancelled,
    int Paid = 0, int Free = 0, int FreeByDoctor = 0, decimal Collected = 0, decimal Waived = 0)
{
    public int Total => Booked + Cancelled;
    public int Active => Booked;
}

/// <summary>One "Free by Doctor" waiver for the waiver report.</summary>
public sealed record WaiverRow(long AppointmentId, string AppointmentNumber, DateOnly Date, int DoctorId, string DoctorName, string? WaiverDoctorName,
    string PatientNumber, string PatientName, decimal OriginalFee, decimal Waived, bool AfterPayment, string? RefundNumber, string? RefundStatus,
    string? Reason, string? WaivedBy, DateTime? WaivedUtc);

public sealed record AppointmentDayCount(DateOnly Date, int DoctorId, AppointmentStatus Status, int Count);

public sealed record DoctorDayLoad(int DoctorId, string DoctorName, string? Specialty, int Issued, int LastToken, decimal? Fee, bool FeeIsFree = false);

/// <summary>Update 18: a consultation on the patient profile with its fee, payment, waiver, discount and refunds.</summary>
public sealed record ConsultationRefundRow(long Id, string Number, decimal Amount, RefundStatus Status, string Reason, string? By, DateTime RequestedUtc);
public sealed record ConsultationRow(long AppointmentId, string? EncounterNumber, DateOnly Date, int Token, string Doctor, AppointmentStatus Status,
    AppointmentPayment Payment, decimal Fee, decimal Collected, decimal Refunded, decimal PendingRefunds, bool ChargeUnpaid, Appointment Appointment, IReadOnlyList<ConsultationRefundRow> Refunds)
{
    public decimal Returnable => Math.Max(0, Collected - Refunded - PendingRefunds);
}

public interface IAppointmentService
{
    /// <summary>Update 18: consultations of a patient (patient profile – Return Fee, Free by Doctor, Discount).</summary>
    Task<IReadOnlyList<ConsultationRow>> PatientConsultationsAsync(long patientId, CancellationToken ct = default);
    /// <summary>Update 18: return a collected consultation fee (full or partial) – a normal refund, never deleting the payment.</summary>
    Task<ServiceResult<Refund>> ReturnFeeAsync(long id, decimal amount, string? reason, int refundMethodId, CancellationToken ct = default);
    /// <summary>Update 18: fixed or percentage discount on the consultation fee (unpaid: the charge is reduced; paid: the discount is refunded).</summary>
    Task<ServiceResult> DiscountAsync(long id, string? type, decimal value, string? reason, int refundMethodId, CancellationToken ct = default);
    Task<ServiceResult<Appointment>> BookAsync(BookAppointmentInput input, bool canCollectFee, bool canMarkFree, CancellationToken ct = default);
    Task<ServiceResult> MarkFreeByDoctorAsync(long id, WaiverInput input, CancellationToken ct = default);
    Task<IReadOnlyList<WaiverRow>> WaiversAsync(DateOnly from, DateOnly to, int? doctorId, CancellationToken ct = default);
    Task<ServiceResult> EditAsync(long id, EditAppointmentInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> CancelAsync(long id, string? reason, CancellationToken ct = default);
    Task<Appointment?> GetAsync(long id, CancellationToken ct = default);
    Task<Appointment?> GetByEncounterAsync(long encounterId, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentListItem>> ListAsync(AppointmentQuery q, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorAppointmentStats>> StatsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<AppointmentDayCount>> DailyCountsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorDayLoad>> DayLoadAsync(DateOnly date, CancellationToken ct = default);
    Task<int> PrintCountAsync(long id, CancellationToken ct = default);
    /// <summary>Records a print or reprint (never changes the token). Returns the copy number.</summary>
    Task<ServiceResult<int>> RecordPrintAsync(long id, string paper, string method, CancellationToken ct = default);
    Task<decimal?> DoctorFeeAsync(int doctorId, CancellationToken ct = default);
}

public sealed class AppointmentService : IAppointmentService
{
    private readonly HmsDbContext _db;
    private readonly IRegistrationSetupService _setup;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    private readonly Finance.IBillingService _billing;

    public AppointmentService(HmsDbContext db, IRegistrationSetupService setup, IAuditService audit, IRequestContext ctx, HospitalClock clock, Finance.IBillingService billing)
    {
        _billing = billing;
        _db = db;
        _setup = setup;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    private static string? Clean(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();

    public static string TokenScope(int doctorId) => $"APPT:D{doctorId}";

    private async Task<ServiceItem?> DoctorServiceAsync(int doctorId, CancellationToken ct) =>
        await _db.ServiceItems.AsNoTracking()
            .Where(s => s.DoctorId == doctorId && s.IsActive && s.Category == EncounterType.Opd)
            .OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

    public async Task<decimal?> DoctorFeeAsync(int doctorId, CancellationToken ct = default) =>
        (await DoctorServiceAsync(doctorId, ct))?.DefaultFee;

    // ---------------- Booking ----------------

    public async Task<ServiceResult<Appointment>> BookAsync(BookAppointmentInput input, bool canCollectFee, bool canMarkFree, CancellationToken ct = default)
    {
        var config = await _db.HospitalConfigurations.AsNoTracking().FirstAsync(ct);
        if (false) // Update 16: consultant (doctor) tokens in every hospital
            return ServiceResult<Appointment>.Fail("Doctor appointments with thermal token slips are used in Private hospital mode. Use OPD registration in Government mode.");
        if (input.IdempotencyKey == Guid.Empty) return ServiceResult<Appointment>.Fail("The booking form has expired. Please reload the page.");
        // A repeated submission (double click, browser retry) returns the appointment that was already booked – the fee is never collected twice.
        if (await ExistingAsync(input.IdempotencyKey, ct) is { } already) return ServiceResult<Appointment>.Ok(already);

        var settings = await _setup.GetSettingsAsync(ct);
        var today = await _clock.TodayAsync();
        var date = input.AppointmentDate ?? today;
        var errors = new List<string>();
        if (date < today) errors.Add("Appointments cannot be booked for a past date.");
        if (date > today.AddDays(Math.Max(0, settings.AppointmentMaxDaysAhead)))
            errors.Add(settings.AppointmentMaxDaysAhead == 0 ? "Appointments can only be booked for today." : $"Appointments can be booked at most {settings.AppointmentMaxDaysAhead} days ahead.");
        if ((input.Complaint?.Length ?? 0) > 300) errors.Add("Complaint must not exceed 300 characters.");
        if ((input.Notes?.Length ?? 0) > 500) errors.Add("Notes must not exceed 500 characters.");
        var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == input.DoctorId, ct);
        if (doctor is null || !doctor.IsActive) errors.Add("Please select an available doctor.");
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PatientId, ct);
        if (patient is null) errors.Add("Patient not found.");
        else if (patient.Status == PatientStatus.Archived) errors.Add($"Patient {patient.PatientNumber} is archived. Restore the record first.");
        else if (patient.Status == PatientStatus.Merged) errors.Add($"Registration {patient.PatientNumber} was merged into another patient. Use the correct Patient ID.");
        if (errors.Count > 0) return ServiceResult<Appointment>.Fail(errors.ToArray());

        var service = await DoctorServiceAsync(doctor!.Id, ct);
        // Update 13: no doctor is free by default – the consultation fee must be set (or the doctor marked FREE) in OPD Setup.
        if (service is null)
            return ServiceResult<Appointment>.Fail($"The consultation fee of {doctor.Name} is not set. Enter it in OPD Setup (or mark the doctor's consultation FREE).");
        if (await Finance.ServiceCatalogue.FeeNotSetErrorAsync(_db, service.Id, today, ct) is not null)
            return ServiceResult<Appointment>.Fail($"The consultation fee of {doctor.Name} is not set. Enter it in OPD Setup (or mark the doctor's consultation FREE).");
        var fee = await Finance.ServiceCatalogue.FeeOnAsync(_db, service.Id, today, ct);

        // ---- how the consultation fee is settled ----
        var payment = fee <= 0 ? AppointmentPayment.Free
            : input.Payment switch { "Paid" => AppointmentPayment.Paid, "FreeByDoctor" => AppointmentPayment.FreeByDoctor, _ => AppointmentPayment.NotRecorded };
        Doctor? waiverDoctor = null;
        var waiverReason = Clean(input.WaiverReason, 300);
        if (payment == AppointmentPayment.NotRecorded)
            errors.Add($"Consultation fee {fee:N2}: record it as received, or as Free by Doctor.");
        if (payment == AppointmentPayment.Paid && !canCollectFee)
            errors.Add("You are not allowed to collect the consultation fee. Ask the administrator for “Collect Consultation Fee”.");
        if (payment == AppointmentPayment.FreeByDoctor)
        {
            if (!canMarkFree) errors.Add("You are not allowed to mark an appointment as Free by Doctor.");
            if (waiverReason is null) errors.Add("Free by Doctor: enter the reason.");
            waiverDoctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == (input.WaiverDoctorId ?? doctor.Id), ct);
            if (waiverDoctor is null) errors.Add("Free by Doctor: select the doctor who waived the fee.");
        }
        if (errors.Count > 0) return ServiceResult<Appointment>.Fail(errors.ToArray());

        var now = _clock.UtcNow;
        var time = settings.AppointmentTimeEnabled ? input.AppointmentTime : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        // 1. Serialise token issue for this doctor and date (other doctors are not blocked).
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext(@k))",
            new[] { new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = $"APPT|{doctor.Id}|{date:yyyy-MM-dd}" } }, ct);
        if (await ExistingAsync(input.IdempotencyKey, ct) is { } again) { await tx.RollbackAsync(ct); return ServiceResult<Appointment>.Ok(again); }
        // 2. Next token = highest token ever issued for this doctor on this date (cancelled included) + 1.
        var last = await _db.Appointments.Where(a => a.DoctorId == doctor.Id && a.AppointmentDate == date).MaxAsync(a => (int?)a.TokenNumber, ct);
        var token = (last ?? 0) + 1;

        var seq = await _db.Database.SqlQueryRaw<long>($"SELECT nextval('{HmsDbContext.OpdNumberSequence}') AS \"Value\"").SingleAsync(ct);
        var number = _setup.FormatEncounterNumber(settings, EncounterType.Opd, seq, date.Year);
        var encounterUtc = date == today ? now
            : await _clock.LocalToUtcAsync(date.ToDateTime(time ?? new TimeOnly(9, 0)));
        var repeat = await _db.PatientEncounters.AnyAsync(e => e.PatientId == patient!.Id && e.Type == EncounterType.Opd, ct);

        var encounter = new PatientEncounter
        {
            EncounterNumber = number, SequenceNumber = seq, Type = EncounterType.Opd, PatientId = patient!.Id,
            EncounterUtc = encounterUtc, EncounterDate = date, DepartmentId = doctor.DepartmentId, DoctorId = doctor.Id,
            TokenNumber = token, TokenScope = TokenScope(doctor.Id), HospitalTypeAtVisit = HospitalType.Private,
            ServiceItemId = service?.Id ?? settings.DefaultOpdServiceItemId, Notes = Clean(input.Notes, 500),
            CreatedUtc = now, UpdatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username,
            Workstation = Clean(_ctx.Workstation, 100),
            Opd = new OpdVisit { IsRepeatVisit = repeat, ChiefComplaint = Clean(input.Complaint, 300) }
        };
        var appt = new Appointment
        {
            PatientId = patient.Id, DoctorId = doctor.Id, Encounter = encounter, AppointmentDate = date, AppointmentTime = time,
            TokenNumber = token, Status = AppointmentStatus.Booked, Complaint = Clean(input.Complaint, 300), Notes = Clean(input.Notes, 500),
            ServiceItemId = service?.Id, ConsultationFee = service is null ? null : fee, IdempotencyKey = input.IdempotencyKey, PaymentStatus = payment,
            BookedUtc = now, BookedByUserId = _ctx.UserId, BookedByUsername = _ctx.Username, Workstation = Clean(_ctx.Workstation, 100),
            UpdatedUtc = now
        };
        if (payment == AppointmentPayment.FreeByDoctor)
        {
            appt.WaiverDoctorId = waiverDoctor!.Id; appt.WaiverDoctorName = waiverDoctor.Name; appt.WaiverReason = waiverReason; appt.WaivedAmount = fee;
            appt.WaivedUtc = now; appt.WaivedByUserId = _ctx.UserId; appt.WaivedByUsername = _ctx.Username;
        }
        _db.Appointments.Add(appt);
        try
        {
            await _db.SaveChangesAsync(ct);
            // 3. Fee received now: a normal Phase 3 charge + receipt in the same transaction (the token slip is the payment proof – no separate receipt print).
            if (payment == AppointmentPayment.Paid)
            {
                var collect = await _billing.CollectAsync(new Finance.CollectInput
                {
                    IdempotencyKey = input.IdempotencyKey, PatientId = patient.Id, CounterId = await CounterAsync(input.CounterId, ct), PaymentMethodId = await MethodAsync(input.PaymentMethodId, ct),
                    PaymentReference = input.PaymentReference, Notes = $"Consultation fee – appointment {number}, token {token}",
                    Lines = { new Finance.NewChargeLine { ServiceItemId = service!.Id, DoctorId = doctor.Id, EncounterId = encounter.Id } },
                }, canDiscount: false, ct);
                if (!collect.Succeeded || collect.Value!.Receipt is not { } receipt)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    _db.ChangeTracker.Clear();
                    return ServiceResult<Appointment>.Fail(collect.Succeeded ? new[] { "The fee could not be recorded. Please try again." } : collect.Errors.Prepend("The appointment was not booked – the fee could not be recorded:").ToArray());
                }
                var line = await _db.ReceiptLines.AsNoTracking().Where(l => l.ReceiptId == receipt.Id).OrderBy(l => l.Id).FirstAsync(ct);
                appt.ChargeId = line.ChargeId;
                appt.ReceiptId = receipt.Id;
                appt.AmountPaid = receipt.AmountPaid;
                appt.PaymentMethodName = await _db.PaymentMethods.Where(m => m.Id == receipt.PaymentMethodId).Select(m => m.Name).FirstAsync(ct);
            }
            _audit.Add(AuditActions.AppointmentBooked, "Appointment", number,
                $"Appointment {number}: {patient.PatientNumber} ({patient.FullName}) with {doctor.Name} on {date:dd/MM/yyyy} – token {token}, fee {FeeText(appt)}",
                newValues: new { number, patient.PatientNumber, Doctor = doctor.Name, DoctorId = doctor.Id, Date = date, Time = time, Token = token, Fee = appt.ConsultationFee, Payment = payment.ToString(), appt.AmountPaid, appt.PaymentMethodName });
            if (payment == AppointmentPayment.FreeByDoctor)
                _audit.Add(AuditActions.AppointmentFreeByDoctor, "Appointment", number,
                    $"Appointment {number}: consultation fee {fee:N2} waived by {waiverDoctor!.Name} (Free by Doctor) at booking – {waiverReason}",
                    newValues: new { Fee = fee, WaivedBy = waiverDoctor.Name, Reason = waiverReason, AfterPayment = false });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Only possible if the lock was bypassed; the unique constraints still guarantee no duplicate token / booking.
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            if (await ExistingAsync(input.IdempotencyKey, ct) is { } dup) return ServiceResult<Appointment>.Ok(dup);
            return ServiceResult<Appointment>.Fail("Another computer booked the same token at this moment. Please try again.");
        }
        appt.Doctor = doctor;
        appt.Patient = patient;
        return ServiceResult<Appointment>.Ok(appt);
    }

    public static string FeeText(Appointment a) => a.PaymentStatus switch
    {
        AppointmentPayment.Paid => $"{a.AmountPaid:N2} PAID ({a.PaymentMethodName})",
        AppointmentPayment.FreeByDoctor => $"{a.ConsultationFee:N2} FREE BY DOCTOR",
        AppointmentPayment.Free => "FREE",
        _ => a.ConsultationFee is decimal f ? $"{f:N2} (not recorded at booking)" : "—",
    };

    private async Task<Appointment?> ExistingAsync(Guid key, CancellationToken ct)
    {
        var a = await _db.Appointments.AsNoTracking().Include(x => x.Encounter).FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (a is not null)
            await _audit.LogAsync(AuditActions.AppointmentDuplicateBlocked, "Appointment", a.Encounter?.EncounterNumber,
                $"Repeated submission of appointment {a.Encounter?.EncounterNumber} ignored (no second token, no second fee)", ct: ct);
        return a;
    }

    /// <summary>The booking user's counter: the one chosen, else the only counter assigned to the user (any active counter for the system administrator).</summary>
    private async Task<int> CounterAsync(int chosen, CancellationToken ct)
    {
        if (chosen > 0) return chosen;
        var mine = await _db.CounterAssignments.Where(a => a.UserId == _ctx.UserId && a.EndedUtc == null && a.Counter!.IsActive).Select(a => a.CounterId).Distinct().ToListAsync(ct);
        if (mine.Count == 1) return mine[0];
        var admin = await _db.Users.AnyAsync(u => u.Id == _ctx.UserId && u.IsSystemAdministrator, ct);
        return admin ? await _db.Counters.Where(c => c.IsActive).OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync(ct) : 0;
    }

    private async Task<int> MethodAsync(int chosen, CancellationToken ct) =>
        chosen > 0 ? chosen : await _db.PaymentMethods.Where(m => m.IsActive && m.CountsAsCashInHand).OrderBy(m => m.Id).Select(m => m.Id).FirstOrDefaultAsync(ct);

    // ---------------- Changes ----------------

    public async Task<ServiceResult> EditAsync(long id, EditAppointmentInput input, uint rowVersion, CancellationToken ct = default)
    {
        var a = await _db.Appointments.Include(x => x.Encounter).ThenInclude(e => e!.Opd).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Appointment not found.");
        if (a.Status == AppointmentStatus.Cancelled) return ServiceResult.Fail("A cancelled appointment cannot be edited.");
        if ((input.Complaint?.Length ?? 0) > 300 || (input.Notes?.Length ?? 0) > 500) return ServiceResult.Fail("Complaint or notes are too long.");
        var settings = await _setup.GetSettingsAsync(ct);
        var old = new { a.AppointmentTime, a.Complaint, a.Notes, a.TokenNumber };
        _db.Entry(a).Property(x => x.RowVersion).OriginalValue = rowVersion;
        if (settings.AppointmentTimeEnabled) a.AppointmentTime = input.AppointmentTime;
        a.Complaint = Clean(input.Complaint, 300);
        a.Notes = Clean(input.Notes, 500);
        a.UpdatedUtc = _clock.UtcNow;
        if (a.Encounter?.Opd is not null) a.Encounter.Opd.ChiefComplaint = a.Complaint;
        if (a.Encounter is not null) { a.Encounter.Notes = a.Notes; a.Encounter.UpdatedUtc = a.UpdatedUtc; }
        _audit.Add(AuditActions.AppointmentUpdated, "Appointment", a.Encounter?.EncounterNumber,
            $"Appointment {a.Encounter?.EncounterNumber} edited (token {a.TokenNumber} unchanged)", old,
            new { a.AppointmentTime, a.Complaint, a.Notes, a.TokenNumber });
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> CancelAsync(long id, string? reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3) return ServiceResult.Fail("Please give a reason for cancelling.");
        var a = await _db.Appointments.Include(x => x.Encounter).Include(x => x.Doctor).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Appointment not found.");
        if (a.Status == AppointmentStatus.Cancelled) return ServiceResult.Fail("The appointment is already cancelled.");
        var old = a.Status;
        var now = _clock.UtcNow;
        a.Status = AppointmentStatus.Cancelled;
        a.CancelledUtc = now;
        a.CancelledByUserId = _ctx.UserId;
        a.CancelledByUsername = _ctx.Username;
        a.CancelReason = Clean(reason, 500);
        a.UpdatedUtc = now;
        _audit.Add(AuditActions.AppointmentCancelled, "Appointment", a.Encounter?.EncounterNumber,
            $"Appointment {a.Encounter?.EncounterNumber} with {a.Doctor?.Name} cancelled – token {a.TokenNumber} stays used: {a.CancelReason}",
            new { Status = old.ToString() }, new { Status = "Cancelled", a.CancelReason, a.TokenNumber });
        return await SaveAsync(ct);
    }

    // ---------------- Free by Doctor (consultation-fee waiver) ----------------

    public async Task<ServiceResult> MarkFreeByDoctorAsync(long id, WaiverInput input, CancellationToken ct = default)
    {
        var reason = Clean(input.Reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for the fee waiver.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext(@k))",
            new[] { new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = $"APPT-FEE|{id}" } }, ct);
        var a = await _db.Appointments.Include(x => x.Encounter).Include(x => x.Doctor).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Appointment not found.");
        if (a.Status == AppointmentStatus.Cancelled) return ServiceResult.Fail("The appointment is cancelled.");
        if (a.PaymentStatus == AppointmentPayment.FreeByDoctor) return ServiceResult.Fail("This appointment is already marked as Free by Doctor.");
        if (a.PaymentStatus == AppointmentPayment.Free) return ServiceResult.Fail("This consultation has no fee.");
        var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == (input.WaiverDoctorId ?? a.DoctorId), ct);
        if (doctor is null) return ServiceResult.Fail("Select the doctor who waived the fee.");

        // What was actually collected for this consultation (booking receipt, or – for older appointments – the cashier).
        var lines = await _db.ReceiptLines.AsNoTracking()
            .Where(l => l.Receipt!.Status == ReceiptStatus.Valid && l.Charge!.Status != ChargeStatus.Cancelled
                        && (l.ChargeId == a.ChargeId || (l.Charge.EncounterId == a.EncounterId && l.Charge.DoctorId == a.DoctorId)))
            .Select(l => new { l.Id, l.ReceiptId, l.Amount, l.RefundedAmount, l.DoctorShare }).ToListAsync(ct);
        var collected = lines.Sum(l => l.Amount - l.RefundedAmount);
        var lineIds = lines.Select(l => l.Id).ToList();
        var reversed = await _db.DoctorLedger.Where(d => d.ReceiptLineId != null && lineIds.Contains(d.ReceiptLineId.Value) && d.EntryType == DoctorEntryType.ShareReversal)
            .SumAsync(d => (decimal?)-d.Amount, ct) ?? 0;
        var share = Math.Max(0, lines.Sum(l => l.DoctorShare) - reversed);
        if (input.CashReturned && collected <= 0) return ServiceResult.Fail("No fee was collected for this appointment, so nothing can be returned.");

        var now = _clock.UtcNow;
        var today = await _clock.TodayAsync();
        var number = a.Encounter?.EncounterNumber ?? a.Id.ToString();
        var old = new { Payment = a.PaymentStatus.ToString(), a.AmountPaid };
        a.PaymentStatus = AppointmentPayment.FreeByDoctor;
        a.WaiverDoctorId = doctor.Id; a.WaiverDoctorName = doctor.Name; a.WaiverReason = reason;
        a.WaivedAmount = collected > 0 ? collected : a.ConsultationFee ?? 0;
        a.WaivedAfterPayment = collected > 0;
        a.WaivedUtc = now; a.WaivedByUserId = _ctx.UserId; a.WaivedByUsername = _ctx.Username; a.UpdatedUtc = now;
        if (collected > 0)
        {
            // Not a cash refund: the doctor's earnings for this consultation are reduced by a fee-waiver adjustment (never by editing old rows).
            _db.DoctorLedger.Add(new DoctorLedgerEntry
            {
                DoctorId = a.DoctorId, EntryType = DoctorEntryType.FeeWaiver, Amount = -share, GrossAmount = -collected, HospitalShare = -(collected - share),
                ReceiptId = lines[0].ReceiptId, AppointmentId = a.Id, Reference = number,
                Description = $"Free by Doctor – appointment {number} (token {a.TokenNumber}), waived by {doctor.Name}: {reason}",
                BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
            });
        }
        await _db.SaveChangesAsync(ct);
        string? refundText = null;
        if (input.CashReturned)
        {
            // Money actually handed back to the patient: a normal Phase 3 refund (approved / paid out at the cashier as usual).
            var method = input.RefundMethodId > 0 ? input.RefundMethodId
                : await _db.PaymentMethods.Where(m => m.IsActive && m.CountsAsCashInHand).OrderBy(m => m.Id).Select(m => m.Id).FirstOrDefaultAsync(ct);
            var receiptId = lines.GroupBy(l => l.ReceiptId).OrderByDescending(g => g.Sum(l => l.Amount - l.RefundedAmount)).First().Key;
            var amount = lines.Where(l => l.ReceiptId == receiptId).Sum(l => l.Amount - l.RefundedAmount);
            var refund = await _billing.RequestRefundAsync(receiptId, amount, method, $"Free by Doctor ({doctor.Name}) – {reason}", ct);
            if (!refund.Succeeded)
            {
                await tx.RollbackAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
                return ServiceResult.Fail(refund.Errors.Prepend("The waiver was not saved – the refund could not be recorded:").ToArray());
            }
            var r = await _db.Refunds.FirstAsync(x => x.Id == refund.Value!.Id, ct);
            r.WaiverAppointmentId = a.Id;
            a.WaiverRefundId = r.Id;
            refundText = $"; cash refund {r.RefundNumber} of {amount:N2} requested";
        }
        _audit.Add(AuditActions.AppointmentFreeByDoctor, "Appointment", number,
            $"Appointment {number}: consultation fee {(collected > 0 ? $"{collected:N2} (already collected)" : $"{a.ConsultationFee:N2} (not collected)")} waived by {doctor.Name} – Free by Doctor: {reason}" +
            (collected > 0 ? $"; doctor share {share:N2} adjusted" : "") + (refundText ?? (collected > 0 ? "; no cash returned" : "")),
            old, new { Payment = "FreeByDoctor", Waived = a.WaivedAmount, WaivedBy = doctor.Name, Reason = reason, AfterPayment = collected > 0, CashReturned = input.CashReturned });
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("This waiver was just recorded by someone else.");
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The appointment was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    private async Task<(List<(long Id, long ReceiptId, decimal Amount, decimal Refunded)> Lines, decimal Collected)> CollectedAsync(Appointment a, CancellationToken ct)
    {
        var lines = await _db.ReceiptLines.AsNoTracking()
            .Where(l => l.Receipt!.Status == ReceiptStatus.Valid && l.Charge!.Status != ChargeStatus.Cancelled
                        && (l.ChargeId == a.ChargeId || (l.Charge.EncounterId == a.EncounterId && l.Charge.DoctorId == a.DoctorId)))
            .Select(l => new { l.Id, l.ReceiptId, l.Amount, l.RefundedAmount }).ToListAsync(ct);
        var list = lines.Select(l => (l.Id, l.ReceiptId, l.Amount, l.RefundedAmount)).ToList();
        return (list, list.Sum(l => l.Amount));
    }

    public async Task<IReadOnlyList<ConsultationRow>> PatientConsultationsAsync(long patientId, CancellationToken ct = default)
    {
        var appts = await _db.Appointments.AsNoTracking().Include(a => a.Doctor).Include(a => a.Encounter)
            .Where(a => a.PatientId == patientId).OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.Id).Take(50).ToListAsync(ct);
        var ids = appts.Select(a => a.Id).ToList();
        var refunds = await _db.Refunds.AsNoTracking()
            .Where(r => (r.ConsultationAppointmentId != null && ids.Contains(r.ConsultationAppointmentId.Value)) || (r.WaiverAppointmentId != null && ids.Contains(r.WaiverAppointmentId.Value)))
            .ToListAsync(ct);
        var rows = new List<ConsultationRow>();
        foreach (var a in appts)
        {
            var (lines, collected) = await CollectedAsync(a, ct);
            var receiptIds = lines.Select(l => l.ReceiptId).Distinct().ToList();
            var mine = refunds.Where(r => r.ConsultationAppointmentId == a.Id || r.WaiverAppointmentId == a.Id).OrderBy(r => r.Id).ToList();
            var completed = lines.Sum(l => l.Refunded);
            var pending = mine.Where(r => r.Status == RefundStatus.Pending).Sum(r => r.Amount);
            var unpaid = a.ChargeId is long cid && await _db.Charges.AnyAsync(c => c.Id == cid && c.PaidAmount == 0 && c.Status == ChargeStatus.Unpaid, ct);
            rows.Add(new ConsultationRow(a.Id, a.Encounter?.EncounterNumber, a.AppointmentDate, a.TokenNumber, a.Doctor?.Name ?? "", a.Status, a.PaymentStatus,
                a.ConsultationFee ?? 0, collected, completed, pending, unpaid, a,
                mine.Select(r => new ConsultationRefundRow(r.Id, r.RefundNumber, r.Amount, r.Status, r.Reason, r.RequestedByUsername, r.RequestedUtc)).ToList()));
        }
        return rows;
    }

    private async Task<int> RefundMethodAsync(int requested, CancellationToken ct) => requested > 0 ? requested
        : await _db.PaymentMethods.Where(m => m.IsActive && m.CountsAsCashInHand).OrderBy(m => m.Id).Select(m => m.Id).FirstOrDefaultAsync(ct);

    public async Task<ServiceResult<Refund>> ReturnFeeAsync(long id, decimal amount, string? reason, int refundMethodId, CancellationToken ct = default)
    {
        reason = Clean(reason, 250);
        if (reason is null) return ServiceResult<Refund>.Fail("Enter the reason for returning the fee.");
        if (amount <= 0 || !Finance.FinanceMath.IsMoney(amount)) return ServiceResult<Refund>.Fail("Enter the amount to return.");
        var a = await _db.Appointments.AsNoTracking().Include(x => x.Doctor).Include(x => x.Encounter).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult<Refund>.Fail("Appointment not found.");
        var (lines, collected) = await CollectedAsync(a, ct);
        if (collected <= 0) return ServiceResult<Refund>.Fail("No consultation fee was collected for this appointment, so nothing can be returned.");
        // The receipt that holds the consultation fee; the billing service refuses more than is still refundable (pending refunds included),
        // so a fee can never be returned twice – not even from two computers at the same moment (receipt lock).
        var receiptId = lines.GroupBy(l => l.ReceiptId).OrderByDescending(g => g.Sum(l => l.Amount - l.Refunded)).First().Key;
        var pendingOnReceipt = await _db.Refunds.Where(r => r.ReceiptId == receiptId && r.Status == RefundStatus.Pending).SumAsync(r => (decimal?)r.Amount, ct) ?? 0;
        var left = lines.Where(l => l.ReceiptId == receiptId).Sum(l => l.Amount - l.Refunded) - pendingOnReceipt;
        if (amount > left) return ServiceResult<Refund>.Fail(left <= 0 ? "The consultation fee was already returned (or a return is waiting for approval)." : $"At most {left:N2} can be returned.");
        var r = await _billing.RequestRefundAsync(receiptId, amount, await RefundMethodAsync(refundMethodId, ct),
            $"Consultation fee returned ({a.Doctor?.Name}) – {reason}", ct);
        if (!r.Succeeded) return r;
        var refund = await _db.Refunds.FirstAsync(x => x.Id == r.Value!.Id, ct);
        refund.ConsultationAppointmentId = a.Id;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditActions.ConsultationFeeReturned, "Appointment", a.Encounter?.EncounterNumber ?? a.Id.ToString(),
            $"Consultation fee {amount:N2} of {collected:N2} to be returned ({refund.RefundNumber}, {a.Doctor?.Name}): {reason}", ct: ct);
        return ServiceResult<Refund>.Ok(refund);
    }

    public async Task<ServiceResult> DiscountAsync(long id, string? type, decimal value, string? reason, int refundMethodId, CancellationToken ct = default)
    {
        reason = Clean(reason, 250);
        if (reason is null) return ServiceResult.Fail("Enter the reason for the discount.");
        var percent = type == "Percent";
        if (!percent && type != "Fixed") return ServiceResult.Fail("Select the discount type (fixed amount or percentage).");
        if (value <= 0 || (percent && value > 100) || !Finance.FinanceMath.IsMoney(value)) return ServiceResult.Fail(percent ? "Enter a percentage between 0 and 100." : "Enter the discount amount.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext(@k))",
            new[] { new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = $"APPT-FEE|{id}" } }, ct);
        var a = await _db.Appointments.Include(x => x.Doctor).Include(x => x.Encounter).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Appointment not found.");
        if (a.Status == AppointmentStatus.Cancelled) return ServiceResult.Fail("The appointment is cancelled.");
        if (a.PaymentStatus is AppointmentPayment.FreeByDoctor or AppointmentPayment.Free) return ServiceResult.Fail("This consultation is free – no discount possible.");
        if (a.DiscountAmount is not null) return ServiceResult.Fail("A discount was already given on this consultation.");
        var original = a.ConsultationFee ?? 0;
        if (original <= 0) return ServiceResult.Fail("This consultation has no fee.");
        var amount = percent ? Math.Round(original * value / 100m, 2, MidpointRounding.AwayFromZero) : value;
        if (amount <= 0 || amount > original) return ServiceResult.Fail($"The discount must be more than 0 and at most the fee of {original:N2}.");
        var (lines, collected) = await CollectedAsync(a, ct);
        var now = _clock.UtcNow;
        string how;
        if (collected <= 0)
        {
            // Not paid yet: the unpaid consultation charge is reduced (the original fee stays on the charge and the appointment).
            var charge = a.ChargeId is long cid ? await _db.Charges.FirstOrDefaultAsync(c => c.Id == cid, ct) : null;
            if (charge is null || charge.Status != ChargeStatus.Unpaid || charge.PaidAmount != 0)
                return ServiceResult.Fail("The consultation fee is not open for a discount (no unpaid consultation charge).");
            if (amount > charge.NetAmount) return ServiceResult.Fail($"The discount cannot be more than the open amount of {charge.NetAmount:N2}.");
            charge.Discount += amount;
            charge.NetAmount -= amount;
            charge.DiscountReason = Clean($"Consultation discount – {reason}", 200);
            how = $"unpaid charge {charge.ChargeNumber} reduced to {charge.NetAmount:N2}";
        }
        else
        {
            var receiptId = lines.GroupBy(l => l.ReceiptId).OrderByDescending(g => g.Sum(l => l.Amount - l.Refunded)).First().Key;
            var r = await _billing.RequestRefundAsync(receiptId, amount, await RefundMethodAsync(refundMethodId, ct), $"Consultation discount ({(percent ? $"{value:0.##}%" : "fixed")}) – {reason}", ct);
            if (!r.Succeeded)
            {
                await tx.RollbackAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
                return ServiceResult.Fail(r.Errors.Prepend("The discount was not saved – the refund could not be recorded:").ToArray());
            }
            var refund = await _db.Refunds.FirstAsync(x => x.Id == r.Value!.Id, ct);
            refund.ConsultationAppointmentId = a.Id;
            a.DiscountRefundId = refund.Id;
            how = $"already paid – refund {refund.RefundNumber} of {amount:N2} requested";
        }
        a.DiscountType = percent ? "Percent" : "Fixed";
        a.DiscountValue = value;
        a.DiscountAmount = amount;
        a.DiscountOriginalFee = original;
        a.DiscountReason = reason;
        a.DiscountedUtc = now;
        a.DiscountedByUsername = _ctx.Username;
        a.UpdatedUtc = now;
        _audit.Add(AuditActions.ConsultationDiscount, "Appointment", a.Encounter?.EncounterNumber ?? a.Id.ToString(),
            $"Consultation discount: original {original:N2}, {(percent ? $"{value:0.##}%" : "fixed")} = {amount:N2}, final {original - amount:N2} ({how}): {reason}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The appointment was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<WaiverRow>> WaiversAsync(DateOnly from, DateOnly to, int? doctorId, CancellationToken ct = default)
    {
        var q = _db.Appointments.AsNoTracking().Where(a => a.PaymentStatus == AppointmentPayment.FreeByDoctor && a.AppointmentDate >= from && a.AppointmentDate <= to);
        if (doctorId is int d) q = q.Where(a => a.DoctorId == d);
        var rows = await q.OrderBy(a => a.AppointmentDate).ThenBy(a => a.Id).Select(a => new
        {
            a.Id, a.Encounter!.EncounterNumber, a.AppointmentDate, a.DoctorId, Doctor = a.Doctor!.Name, a.WaiverDoctorName, a.Patient!.PatientNumber, a.Patient.FullName,
            a.ConsultationFee, a.WaivedAmount, a.WaivedAfterPayment, a.WaiverRefundId, a.WaiverReason, a.WaivedByUsername, a.WaivedUtc
        }).ToListAsync(ct);
        var refundIds = rows.Where(r => r.WaiverRefundId != null).Select(r => r.WaiverRefundId!.Value).ToList();
        var refunds = await _db.Refunds.AsNoTracking().Where(r => refundIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => new { r.RefundNumber, r.Status }, ct);
        return rows.Select(r => new WaiverRow(r.Id, r.EncounterNumber, r.AppointmentDate, r.DoctorId, r.Doctor, r.WaiverDoctorName, r.PatientNumber, r.FullName,
            r.ConsultationFee ?? 0, r.WaivedAmount ?? 0, r.WaivedAfterPayment,
            r.WaiverRefundId is long x && refunds.TryGetValue(x, out var rf) ? rf.RefundNumber : null,
            r.WaiverRefundId is long y && refunds.TryGetValue(y, out var rs) ? rs.Status.ToString() : null,
            r.WaiverReason, r.WaivedByUsername, r.WaivedUtc)).ToList();
    }

    // ---------------- Queries ----------------

    public Task<Appointment?> GetAsync(long id, CancellationToken ct = default) =>
        _db.Appointments.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Doctor).ThenInclude(d => d!.Department)
            .Include(a => a.Encounter).ThenInclude(e => e!.PrintedDocuments)
            .Include(a => a.ServiceItem).Include(a => a.Receipt)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Appointment?> GetByEncounterAsync(long encounterId, CancellationToken ct = default)
    {
        var id = await _db.Appointments.Where(a => a.EncounterId == encounterId).Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);
        return id is null ? null : await GetAsync(id.Value, ct);
    }

    public async Task<IReadOnlyList<AppointmentListItem>> ListAsync(AppointmentQuery q, CancellationToken ct = default)
    {
        var query = _db.Appointments.AsNoTracking().AsQueryable();
        if (q.From is { } f) query = query.Where(a => a.AppointmentDate >= f);
        if (q.To is { } t) query = query.Where(a => a.AppointmentDate <= t);
        if (q.DoctorId is { } d) query = query.Where(a => a.DoctorId == d);
        if (q.PatientId is { } p) query = query.Where(a => a.PatientId == p);
        query = q.Status switch
        {
            "active" => query.Where(a => a.Status != AppointmentStatus.Cancelled),
            "cancelled" => query.Where(a => a.Status == AppointmentStatus.Cancelled),
            _ => query
        };
        query = q.Payment switch
        {
            "paid" => query.Where(a => a.PaymentStatus == AppointmentPayment.Paid),
            "free" => query.Where(a => a.PaymentStatus == AppointmentPayment.Free || a.PaymentStatus == AppointmentPayment.FreeByDoctor),
            "freebydoctor" => query.Where(a => a.PaymentStatus == AppointmentPayment.FreeByDoctor),
            "notrecorded" => query.Where(a => a.PaymentStatus == AppointmentPayment.NotRecorded),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var text = q.Q.Trim();
            var upper = text.ToUpperInvariant();
            var like = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var digits = PatientService.Digits(text);
            query = query.Where(a => a.Patient!.PatientNumber.ToUpper() == upper || a.Encounter!.EncounterNumber.ToUpper() == upper
                                     || EF.Functions.ILike(a.Patient!.FullName, like)
                                     || (digits.Length >= 4 && a.Patient!.MobileDigits != null && a.Patient.MobileDigits.Contains(digits)));
        }
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 10, 2000);
        var rows = await query.OrderBy(a => a.AppointmentDate).ThenBy(a => a.Doctor!.Name).ThenBy(a => a.TokenNumber)
            .Skip((page - 1) * size).Take(size)
            .Select(a => new
            {
                a.Id, a.Encounter!.EncounterNumber, a.EncounterId, a.DoctorId, Doctor = a.Doctor!.Name, a.Doctor.Specialty,
                a.AppointmentDate, a.AppointmentTime, a.TokenNumber, a.Status, a.PatientId, a.Patient!.PatientNumber, a.Patient.FullName,
                a.Patient.GuardianName, a.Patient.Gender, a.Patient.DateOfBirth, a.Patient.AgeAtRegistration, a.Patient.AgeUnitAtRegistration,
                a.Patient.MobileNumber, a.Complaint, a.ConsultationFee, a.BookedUtc, a.BookedByUsername, a.CancelReason,
                a.PaymentStatus, a.AmountPaid, a.WaivedAmount, a.WaivedByUsername,
                Prints = a.Encounter.PrintedDocuments.Count(p => p.DocumentType == PrintedDocumentType.AppointmentSlip)
            }).ToListAsync(ct);
        return rows.Select(r =>
        {
            var age = r.DateOfBirth is { } dob ? AgeCalculator.FromDateOfBirth(dob, r.AppointmentDate)
                : r.AgeAtRegistration is { } x && r.AgeUnitAtRegistration is { } u ? new PatientAge(x, u) : (PatientAge?)null;
            return new AppointmentListItem(r.Id, r.EncounterNumber, r.EncounterId, r.DoctorId, r.Doctor, r.Specialty, r.AppointmentDate, r.AppointmentTime,
                r.TokenNumber, r.Status, r.PatientId, r.PatientNumber, r.FullName, r.GuardianName, r.Gender, age?.ToString(), r.MobileNumber,
                r.Complaint, r.ConsultationFee, r.BookedUtc, r.BookedByUsername, r.CancelReason, r.Prints, r.PaymentStatus, r.AmountPaid, r.WaivedAmount, r.WaivedByUsername);
        }).ToList();
    }

    public async Task<IReadOnlyList<DoctorAppointmentStats>> StatsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= from && a.AppointmentDate <= to)
            .GroupBy(a => new { a.DoctorId, a.Status, a.PaymentStatus })
            .Select(g => new { g.Key.DoctorId, g.Key.Status, g.Key.PaymentStatus, Count = g.Count(), Paid = g.Sum(a => a.AmountPaid ?? 0), Waived = g.Sum(a => a.WaivedAmount ?? 0) })
            .ToListAsync(ct);
        var doctors = await _db.Doctors.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        return doctors
            .Select(d =>
            {
                var mine = rows.Where(r => r.DoctorId == d.Id).ToList();
                var active = mine.Where(r => r.Status != AppointmentStatus.Cancelled).ToList();
                return new DoctorAppointmentStats(d.Id, d.Name, d.Specialty, active.Sum(r => r.Count),
                    mine.Where(r => r.Status == AppointmentStatus.Cancelled).Sum(r => r.Count),
                    active.Where(r => r.PaymentStatus == AppointmentPayment.Paid).Sum(r => r.Count),
                    active.Where(r => r.PaymentStatus == AppointmentPayment.Free).Sum(r => r.Count),
                    mine.Where(r => r.PaymentStatus == AppointmentPayment.FreeByDoctor).Sum(r => r.Count),
                    mine.Sum(r => r.Paid), mine.Sum(r => r.Waived));
            })
            .Where(s => s.Total > 0 || doctors.First(d => d.Id == s.DoctorId).IsActive)
            .ToList();
    }

    public async Task<IReadOnlyList<AppointmentDayCount>> DailyCountsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= from && a.AppointmentDate <= to)
            .GroupBy(a => new { a.AppointmentDate, a.DoctorId, a.Status })
            .Select(g => new { g.Key.AppointmentDate, g.Key.DoctorId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => new AppointmentDayCount(r.AppointmentDate, r.DoctorId, r.Status, r.Count))
            .OrderBy(r => r.Date).ToList();
    }

    public async Task<IReadOnlyList<DoctorDayLoad>> DayLoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var counts = await _db.Appointments.AsNoTracking().Where(a => a.AppointmentDate == date)
            .GroupBy(a => a.DoctorId).Select(g => new { DoctorId = g.Key, Issued = g.Count(), Last = g.Max(a => a.TokenNumber) }).ToListAsync(ct);
        var doctors = await _db.Doctors.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(ct);
        var fees = await _db.ServiceItems.AsNoTracking().Where(s => s.DoctorId != null && s.IsActive && s.Category == EncounterType.Opd)
            .GroupBy(s => s.DoctorId!.Value).Select(g => new { DoctorId = g.Key, Fee = g.OrderBy(s => s.Id).First().DefaultFee, IsFree = g.OrderBy(s => s.Id).First().IsFree }).ToListAsync(ct);
        return doctors.Select(d =>
        {
            var c = counts.FirstOrDefault(x => x.DoctorId == d.Id);
            var f = fees.FirstOrDefault(x => x.DoctorId == d.Id);
            return new DoctorDayLoad(d.Id, d.Name, d.Specialty, c?.Issued ?? 0, c?.Last ?? 0, f?.Fee, f?.IsFree ?? false);
        }).ToList();
    }

    public async Task<int> PrintCountAsync(long id, CancellationToken ct = default)
    {
        var encId = await _db.Appointments.Where(a => a.Id == id).Select(a => (long?)a.EncounterId).FirstOrDefaultAsync(ct);
        return encId is null ? 0 : await _db.PrintedDocuments.CountAsync(p => p.EncounterId == encId && p.DocumentType == PrintedDocumentType.AppointmentSlip, ct);
    }

    public async Task<ServiceResult<int>> RecordPrintAsync(long id, string paper, string method, CancellationToken ct = default)
    {
        var a = await _db.Appointments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.EncounterId, x.Encounter!.EncounterNumber, x.TokenNumber, x.DoctorId, x.Status }).FirstOrDefaultAsync(ct);
        if (a is null) return ServiceResult<int>.Fail("Appointment not found.");
        var count = await _db.PrintedDocuments.CountAsync(p => p.EncounterId == a.EncounterId && p.DocumentType == PrintedDocumentType.AppointmentSlip, ct) + 1;
        _db.PrintedDocuments.Add(new PrintedDocument
        {
            EncounterId = a.EncounterId, DocumentType = PrintedDocumentType.AppointmentSlip, PaperSize = paper, PrintNumber = count,
            PrintedUtc = _clock.UtcNow, PrintedByUserId = _ctx.UserId, PrintedByUsername = _ctx.Username, Workstation = Clean(_ctx.Workstation, 100)
        });
        _audit.Add(count == 1 ? AuditActions.AppointmentSlipPrinted : AuditActions.AppointmentSlipReprinted, "Appointment", a.EncounterNumber,
            $"Appointment slip {a.EncounterNumber} {(count == 1 ? "printed" : $"reprinted (copy {count})")} – token {a.TokenNumber}, {paper}, {method}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(count);
    }

    private async Task<ServiceResult> SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("This appointment was changed on another computer. Please reload and try again.");
        }
    }
}
