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
    string? Complaint, decimal? ConsultationFee, DateTime BookedUtc, string? BookedByUsername, string? CancelReason, int PrintCount);

public sealed class AppointmentQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? DoctorId { get; set; }
    public long? PatientId { get; set; }
    /// <summary>"active" (booked + seen), "cancelled", "seen", "booked" or null for all.</summary>
    public string? Status { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 500;
}

public sealed record DoctorAppointmentStats(int DoctorId, string DoctorName, string? Specialty, int Booked, int Seen, int Cancelled)
{
    public int Total => Booked + Seen + Cancelled;
    public int Active => Booked + Seen;
}

public sealed record AppointmentDayCount(DateOnly Date, int DoctorId, AppointmentStatus Status, int Count);

public sealed record DoctorDayLoad(int DoctorId, string DoctorName, string? Specialty, int Issued, int LastToken, decimal? Fee);

public interface IAppointmentService
{
    Task<ServiceResult<Appointment>> BookAsync(BookAppointmentInput input, CancellationToken ct = default);
    Task<ServiceResult> EditAsync(long id, EditAppointmentInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> CancelAsync(long id, string? reason, CancellationToken ct = default);
    Task<ServiceResult> MarkSeenAsync(long id, CancellationToken ct = default);
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

    public AppointmentService(HmsDbContext db, IRegistrationSetupService setup, IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
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

    public async Task<ServiceResult<Appointment>> BookAsync(BookAppointmentInput input, CancellationToken ct = default)
    {
        var config = await _db.HospitalConfigurations.AsNoTracking().FirstAsync(ct);
        if (config.HospitalType != HospitalType.Private)
            return ServiceResult<Appointment>.Fail("Doctor appointments with thermal token slips are used in Private hospital mode. Use OPD registration in Government mode.");

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
        var now = _clock.UtcNow;
        var time = settings.AppointmentTimeEnabled ? input.AppointmentTime : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        // 1. Serialise token issue for this doctor and date (other doctors are not blocked).
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext(@k))",
            new[] { new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = $"APPT|{doctor.Id}|{date:yyyy-MM-dd}" } }, ct);
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
            ServiceItemId = service?.Id, ConsultationFee = service?.DefaultFee,
            BookedUtc = now, BookedByUserId = _ctx.UserId, BookedByUsername = _ctx.Username, Workstation = Clean(_ctx.Workstation, 100),
            UpdatedUtc = now
        };
        _db.Appointments.Add(appt);
        _audit.Add(AuditActions.AppointmentBooked, "Appointment", number,
            $"Appointment {number}: {patient.PatientNumber} ({patient.FullName}) with {doctor.Name} on {date:dd/MM/yyyy} – token {token}",
            newValues: new { number, patient.PatientNumber, Doctor = doctor.Name, DoctorId = doctor.Id, Date = date, Time = time, Token = token, Fee = appt.ConsultationFee });
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Only possible if the lock was bypassed; the unique constraint still guarantees no duplicate token.
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<Appointment>.Fail("Another computer booked the same token at this moment. Please try again.");
        }
        appt.Doctor = doctor;
        appt.Patient = patient;
        return ServiceResult<Appointment>.Ok(appt);
    }

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

    public async Task<ServiceResult> MarkSeenAsync(long id, CancellationToken ct = default)
    {
        var a = await _db.Appointments.Include(x => x.Encounter).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return ServiceResult.Fail("Appointment not found.");
        if (a.Status != AppointmentStatus.Booked) return ServiceResult.Fail("Only booked appointments can be marked as seen.");
        a.Status = AppointmentStatus.Seen;
        a.SeenUtc = _clock.UtcNow;
        a.UpdatedUtc = a.SeenUtc.Value;
        _audit.Add(AuditActions.AppointmentSeen, "Appointment", a.Encounter?.EncounterNumber, $"Appointment {a.Encounter?.EncounterNumber} marked as seen (token {a.TokenNumber})");
        return await SaveAsync(ct);
    }

    // ---------------- Queries ----------------

    public Task<Appointment?> GetAsync(long id, CancellationToken ct = default) =>
        _db.Appointments.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Doctor).ThenInclude(d => d!.Department)
            .Include(a => a.Encounter).ThenInclude(e => e!.PrintedDocuments)
            .Include(a => a.ServiceItem)
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
            "seen" => query.Where(a => a.Status == AppointmentStatus.Seen),
            "booked" => query.Where(a => a.Status == AppointmentStatus.Booked),
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
                Prints = a.Encounter.PrintedDocuments.Count(p => p.DocumentType == PrintedDocumentType.AppointmentSlip)
            }).ToListAsync(ct);
        return rows.Select(r =>
        {
            var age = r.DateOfBirth is { } dob ? AgeCalculator.FromDateOfBirth(dob, r.AppointmentDate)
                : r.AgeAtRegistration is { } x && r.AgeUnitAtRegistration is { } u ? new PatientAge(x, u) : (PatientAge?)null;
            return new AppointmentListItem(r.Id, r.EncounterNumber, r.EncounterId, r.DoctorId, r.Doctor, r.Specialty, r.AppointmentDate, r.AppointmentTime,
                r.TokenNumber, r.Status, r.PatientId, r.PatientNumber, r.FullName, r.GuardianName, r.Gender, age?.ToString(), r.MobileNumber,
                r.Complaint, r.ConsultationFee, r.BookedUtc, r.BookedByUsername, r.CancelReason, r.Prints);
        }).ToList();
    }

    public async Task<IReadOnlyList<DoctorAppointmentStats>> StatsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= from && a.AppointmentDate <= to)
            .GroupBy(a => new { a.DoctorId, a.Status })
            .Select(g => new { g.Key.DoctorId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var doctors = await _db.Doctors.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        return doctors
            .Select(d => new DoctorAppointmentStats(d.Id, d.Name, d.Specialty,
                rows.Where(r => r.DoctorId == d.Id && r.Status == AppointmentStatus.Booked).Sum(r => r.Count),
                rows.Where(r => r.DoctorId == d.Id && r.Status == AppointmentStatus.Seen).Sum(r => r.Count),
                rows.Where(r => r.DoctorId == d.Id && r.Status == AppointmentStatus.Cancelled).Sum(r => r.Count)))
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
            .GroupBy(s => s.DoctorId!.Value).Select(g => new { DoctorId = g.Key, Fee = g.OrderBy(s => s.Id).First().DefaultFee }).ToListAsync(ct);
        return doctors.Select(d =>
        {
            var c = counts.FirstOrDefault(x => x.DoctorId == d.Id);
            return new DoctorDayLoad(d.Id, d.Name, d.Specialty, c?.Issued ?? 0, c?.Last ?? 0, fees.FirstOrDefault(f => f.DoctorId == d.Id)?.Fee);
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
