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

public sealed class OpdVisitInput
{
    public long PatientId { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public int? ServiceItemId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? Notes { get; set; }
}

public sealed class EmergencyInput
{
    /// <summary>Existing patient; when null, <see cref="NewPatient"/> is registered in the same transaction.</summary>
    public long? PatientId { get; set; }
    public PatientInput? NewPatient { get; set; }
    /// <summary>Arrival time in hospital local time; null = now.</summary>
    public DateTime? ArrivalLocal { get; set; }
    public int? CategoryId { get; set; }
    public string? BriefComplaint { get; set; }
    public string? BroughtBy { get; set; }
    public string? AttendantName { get; set; }
    public string? AttendantContact { get; set; }
    public string? ReferredBy { get; set; }
    public bool IsMedicoLegal { get; set; }
    public string? Notes { get; set; }
}

public sealed class EmergencyDetailsInput
{
    public int? CategoryId { get; set; }
    public string? BriefComplaint { get; set; }
    public string? BroughtBy { get; set; }
    public string? AttendantName { get; set; }
    public string? AttendantContact { get; set; }
    public string? ReferredBy { get; set; }
    public bool IsMedicoLegal { get; set; }
    public string? Notes { get; set; }
}

public sealed record EncounterListItem(
    long Id, string EncounterNumber, EncounterType Type, DateTime EncounterUtc, int? TokenNumber,
    long PatientId, string PatientNumber, string PatientName, GuardianRelation? GuardianRelation, string? GuardianName,
    Gender Gender, string? AgeText, string? MobileNumber, bool IsUnidentified,
    string? DepartmentName, string? DoctorName, string? CategoryName, EmergencyStatus? EmergencyStatus,
    string? Complaint, int PrintCount, string? CreatedByUsername);

public sealed class EncounterQuery
{
    public EncounterType? Type { get; set; }
    public long? PatientId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public int? CategoryId { get; set; }
    /// <summary>Emergency filter: "active" (registered + under treatment) or a status name.</summary>
    public string? Status { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed record ClinicalCounts(int OpdToday, int EmergencyToday, int EmergencyActive, int RegisteredToday);

public interface IEncounterService
{
    Task<ServiceResult<PatientEncounter>> RegisterOpdAsync(OpdVisitInput input, CancellationToken ct = default);
    Task<ServiceResult<PatientEncounter>> RegisterEmergencyAsync(EmergencyInput input, CancellationToken ct = default);
    Task<PatientEncounter?> GetAsync(long id, CancellationToken ct = default);
    Task<PagedResult<EncounterListItem>> ListAsync(EncounterQuery q, CancellationToken ct = default);
    Task<ServiceResult> UpdateOpdAsync(long id, OpdVisitInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> UpdateEmergencyDetailsAsync(long id, EmergencyDetailsInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> ChangeEmergencyStatusAsync(long id, EmergencyStatus newStatus, string? reason, CancellationToken ct = default);
    IReadOnlyList<EmergencyStatus> AllowedStatusChanges(EmergencyStatus current, IReadOnlySet<string> permissions);
    Task<int> RecordPrintAsync(long encounterId, PrintedDocumentType type, string paper, CancellationToken ct = default);
    Task<ClinicalCounts> CountsAsync(CancellationToken ct = default);
}

public sealed class EncounterService : IEncounterService
{
    /// <summary>Statuses that require the restricted "Close / Admit / Re-open" permission.</summary>
    public static readonly EmergencyStatus[] RestrictedStatuses = { EmergencyStatus.Closed, EmergencyStatus.Admitted };
    /// <summary>Statuses that need a written reason.</summary>
    public static readonly EmergencyStatus[] ReasonRequired = { EmergencyStatus.Referred, EmergencyStatus.Admitted, EmergencyStatus.Closed };

    private readonly HmsDbContext _db;
    private readonly IPatientService _patients;
    private readonly IRegistrationSetupService _setup;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    public EncounterService(HmsDbContext db, IPatientService patients, IRegistrationSetupService setup, IPermissionService permissions,
        IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
        _db = db;
        _patients = patients;
        _setup = setup;
        _permissions = permissions;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    private static string? Clean(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();

    private async Task<HospitalType> HospitalTypeAsync(CancellationToken ct) =>
        await _db.HospitalConfigurations.Select(c => c.HospitalType).FirstOrDefaultAsync(ct);

    private static ServiceResult<PatientEncounter> Fail(params string[] e) => ServiceResult<PatientEncounter>.Fail(e);

    private async Task<string?> PatientBlockReasonAsync(Patient p) => p.Status switch
    {
        PatientStatus.Archived => $"Patient {p.PatientNumber} is archived. Restore the record before registering a new visit.",
        PatientStatus.Merged => $"Registration {p.PatientNumber} was merged into another patient. Use the correct Patient ID.",
        _ => await Task.FromResult<string?>(null)
    };

    private async Task<(long Seq, string Number)> NextEncounterNumberAsync(EncounterType type, DateOnly localDate, CancellationToken ct)
    {
        var settings = await _setup.GetSettingsAsync(ct);
        var seqName = type == EncounterType.Opd ? HmsDbContext.OpdNumberSequence : HmsDbContext.EmergencyNumberSequence;
        var seq = await _db.Database.SqlQueryRaw<long>($"SELECT nextval('{seqName}') AS \"Value\"").SingleAsync(ct);
        return (seq, _setup.FormatEncounterNumber(settings, type, seq, localDate.Year));
    }

    /// <summary>Next daily token for the scope, serialised with a transaction-level advisory lock.</summary>
    private async Task<int> NextTokenAsync(string scope, DateOnly date, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext(@k))",
            new[] { new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = scope + "|" + date.ToString("yyyy-MM-dd") } }, ct);
        var max = await _db.PatientEncounters.Where(e => e.TokenScope == scope && e.EncounterDate == date).MaxAsync(e => (int?)e.TokenNumber, ct);
        return (max ?? 0) + 1;
    }

    // ---------------- OPD ----------------

    public async Task<ServiceResult<PatientEncounter>> RegisterOpdAsync(OpdVisitInput input, CancellationToken ct = default)
    {
        var settings = await _setup.GetSettingsAsync(ct);
        var hospitalType = await HospitalTypeAsync(ct);
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PatientId, ct);
        if (patient is null) return Fail("Patient not found.");
        if (await PatientBlockReasonAsync(patient) is { } block) return Fail(block);

        var errors = await ValidateOpdAsync(input, settings, hospitalType, ct);
        if (errors.Count > 0) return Fail(errors.ToArray());

        var departmentId = input.DepartmentId ?? settings.DefaultOpdDepartmentId;
        Doctor? doctor = input.DoctorId is int did ? await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == did, ct) : null;
        departmentId ??= doctor?.DepartmentId;
        var serviceId = input.ServiceItemId;
        if (serviceId is null && doctor is not null)
            serviceId = await _db.ServiceItems.Where(s => s.DoctorId == doctor.Id && s.IsActive && s.Category == EncounterType.Opd).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        serviceId ??= settings.DefaultOpdServiceItemId;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = _clock.UtcNow;
        var date = await _clock.TodayAsync();
        var (seq, number) = await NextEncounterNumberAsync(EncounterType.Opd, date, ct);
        string? scope = null;
        int? token = null;
        if (settings.OpdTokenEnabled)
        {
            scope = settings.OpdTokenScope switch
            {
                TokenScopeMode.Department => $"OPD:D{departmentId ?? 0}",
                TokenScopeMode.Doctor => $"OPD:R{doctor?.Id ?? 0}",
                _ => "OPD"
            };
            token = await NextTokenAsync(scope, date, ct);
        }
        var repeat = await _db.PatientEncounters.AnyAsync(e => e.PatientId == patient.Id && e.Type == EncounterType.Opd, ct);
        var enc = new PatientEncounter
        {
            EncounterNumber = number, SequenceNumber = seq, Type = EncounterType.Opd, PatientId = patient.Id,
            EncounterUtc = now, EncounterDate = date, DepartmentId = departmentId, DoctorId = doctor?.Id,
            TokenNumber = token, TokenScope = scope, HospitalTypeAtVisit = hospitalType, ServiceItemId = serviceId,
            Notes = Clean(input.Notes, 1000), CreatedUtc = now, UpdatedUtc = now, CreatedByUserId = _ctx.UserId,
            CreatedByUsername = _ctx.Username, Workstation = Clean(_ctx.Workstation, 100),
            Opd = new OpdVisit { IsRepeatVisit = repeat, ChiefComplaint = Clean(input.ChiefComplaint, 300) }
        };
        _db.PatientEncounters.Add(enc);
        _audit.Add(AuditActions.OpdVisitRegistered, "Encounter", number,
            $"OPD visit {number} registered for {patient.PatientNumber} ({patient.FullName})" + (token is null ? "" : $", token {token}")
            + (doctor is null ? "" : $", {doctor.Name}"),
            newValues: new { number, patient.PatientNumber, token, departmentId, Doctor = doctor?.Name, HospitalType = hospitalType.ToString(), repeat });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<PatientEncounter>.Ok(enc);
    }

    private async Task<List<string>> ValidateOpdAsync(OpdVisitInput i, RegistrationSettings s, HospitalType type, CancellationToken ct)
    {
        var e = new List<string>();
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d && x.IsActive, ct)) e.Add("Selected department is not available.");
        if (i.DoctorId is int doc && !await _db.Doctors.AnyAsync(x => x.Id == doc && x.IsActive, ct)) e.Add("Selected doctor is not available.");
        if (i.ServiceItemId is int sid && !await _db.ServiceItems.AnyAsync(x => x.Id == sid && x.IsActive, ct)) e.Add("Selected service is not available.");
        if (type == HospitalType.Private && s.RequireDoctorForPrivateOpd && i.DoctorId is null && await _db.Doctors.AnyAsync(x => x.IsActive, ct))
            e.Add("Please select the doctor for this visit.");
        if ((i.ChiefComplaint?.Trim().Length ?? 0) > 300) e.Add("Complaint must not exceed 300 characters.");
        if ((i.Notes?.Trim().Length ?? 0) > 1000) e.Add("Notes must not exceed 1000 characters.");
        return e;
    }

    public async Task<ServiceResult> UpdateOpdAsync(long id, OpdVisitInput input, uint rowVersion, CancellationToken ct = default)
    {
        var enc = await _db.PatientEncounters.Include(e => e.Opd).FirstOrDefaultAsync(e => e.Id == id && e.Type == EncounterType.Opd, ct);
        if (enc?.Opd is null) return ServiceResult.Fail("OPD visit not found.");
        var settings = await _setup.GetSettingsAsync(ct);
        var errors = await ValidateOpdAsync(input, settings, enc.HospitalTypeAtVisit, ct);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var old = new { enc.DepartmentId, enc.DoctorId, enc.ServiceItemId, enc.Opd.ChiefComplaint, enc.Notes };
        _db.Entry(enc).Property(x => x.RowVersion).OriginalValue = rowVersion;
        enc.DepartmentId = input.DepartmentId;
        enc.DoctorId = input.DoctorId;
        if (input.ServiceItemId is not null) enc.ServiceItemId = input.ServiceItemId;
        enc.Opd.ChiefComplaint = Clean(input.ChiefComplaint, 300);
        enc.Notes = Clean(input.Notes, 1000);
        enc.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.OpdVisitUpdated, "Encounter", enc.EncounterNumber, $"OPD visit {enc.EncounterNumber} corrected", old,
            new { enc.DepartmentId, enc.DoctorId, enc.ServiceItemId, enc.Opd.ChiefComplaint, enc.Notes });
        return await SaveAsync(ct);
    }

    // ---------------- Emergency ----------------

    public async Task<ServiceResult<PatientEncounter>> RegisterEmergencyAsync(EmergencyInput input, CancellationToken ct = default)
    {
        var errors = await ValidateEmergencyAsync(input.CategoryId, input.AttendantContact, input.BriefComplaint, input.BroughtBy,
            input.AttendantName, input.ReferredBy, input.Notes, ct);
        var nowUtc = _clock.UtcNow;
        var arrivalUtc = nowUtc;
        if (input.ArrivalLocal is { } local)
        {
            arrivalUtc = await _clock.LocalToUtcAsync(local);
            if (arrivalUtc > nowUtc.AddMinutes(5)) errors.Add("Arrival time cannot be in the future.");
            if (arrivalUtc < nowUtc.AddHours(-48)) errors.Add("Arrival time cannot be more than 48 hours ago.");
        }
        if (input.PatientId is null && input.NewPatient is null) errors.Add("Select an existing patient or enter the new patient's details.");
        if (input.NewPatient is not null && input.PatientId is null)
            errors.AddRange(_patients.Validate(input.NewPatient, minimal: true, await _clock.TodayAsync()));
        if (errors.Count > 0) return Fail(errors.Distinct().ToArray());

        var hospitalType = await HospitalTypeAsync(ct);
        var settings = await _setup.GetSettingsAsync(ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        Patient patient;
        if (input.PatientId is long pid)
        {
            var existing = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (existing is null) return Fail("Patient not found.");
            if (await PatientBlockReasonAsync(existing) is { } block) return Fail(block);
            patient = existing;
        }
        else
        {
            var created = await _patients.CreateWithinTransactionAsync(input.NewPatient!, minimal: true, ct);
            if (!created.Succeeded) return Fail(created.Errors.ToArray());
            patient = created.Value!;
        }

        var date = await _clock.LocalDateOfAsync(arrivalUtc);
        var (seq, number) = await NextEncounterNumberAsync(EncounterType.Emergency, date, ct);
        var emergencyDept = await _db.Departments.Where(d => d.Name == "Emergency" && d.IsActive).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        var enc = new PatientEncounter
        {
            EncounterNumber = number, SequenceNumber = seq, Type = EncounterType.Emergency, PatientId = patient.Id,
            EncounterUtc = arrivalUtc, EncounterDate = date, DepartmentId = emergencyDept, HospitalTypeAtVisit = hospitalType,
            ServiceItemId = settings.DefaultEmergencyServiceItemId, Notes = Clean(input.Notes, 1000),
            CreatedUtc = nowUtc, UpdatedUtc = nowUtc, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username,
            Workstation = Clean(_ctx.Workstation, 100),
            Emergency = new EmergencyVisit
            {
                CategoryId = input.CategoryId, BriefComplaint = Clean(input.BriefComplaint, 500), BroughtBy = Clean(input.BroughtBy, 150),
                AttendantName = Clean(input.AttendantName, 150), AttendantContact = PatientService.FormatPhone(input.AttendantContact),
                ReferredBy = Clean(input.ReferredBy, 200), IsMedicoLegal = input.IsMedicoLegal,
                Status = EmergencyStatus.Registered, StatusChangedUtc = nowUtc
            }
        };
        enc.Emergency.StatusHistory.Add(new EmergencyStatusHistory
        {
            ToStatus = EmergencyStatus.Registered, ChangedUtc = nowUtc, ChangedByUserId = _ctx.UserId, ChangedByUsername = _ctx.Username,
            Reason = "Emergency registration"
        });
        _db.PatientEncounters.Add(enc);
        _audit.Add(AuditActions.EmergencyRegistered, "Encounter", number,
            $"Emergency {number} registered for {patient.PatientNumber} ({patient.FullName})" + (patient.IsUnidentified ? " – unidentified patient" : ""),
            newValues: new { number, patient.PatientNumber, input.CategoryId, input.BriefComplaint, input.IsMedicoLegal, ArrivalUtc = arrivalUtc });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<PatientEncounter>.Ok(enc);
    }

    private async Task<List<string>> ValidateEmergencyAsync(int? categoryId, string? attendantContact, string? complaint, string? broughtBy,
        string? attendant, string? referredBy, string? notes, CancellationToken ct)
    {
        var e = new List<string>();
        if (categoryId is int c && !await _db.EmergencyCategories.AnyAsync(x => x.Id == c && x.IsActive, ct)) e.Add("Selected emergency category is not available.");
        if (!string.IsNullOrWhiteSpace(attendantContact) && (PatientService.Digits(attendantContact).Length is < 7 or > 15 || !Validation.IsValidPhone(attendantContact)))
            e.Add("Attendant contact number is not valid.");
        if ((complaint?.Length ?? 0) > 500) e.Add("Brief complaint must not exceed 500 characters.");
        if ((broughtBy?.Length ?? 0) > 150 || (attendant?.Length ?? 0) > 150 || (referredBy?.Length ?? 0) > 200) e.Add("Attendant / referral details are too long.");
        if ((notes?.Length ?? 0) > 1000) e.Add("Notes must not exceed 1000 characters.");
        return e;
    }

    public async Task<ServiceResult> UpdateEmergencyDetailsAsync(long id, EmergencyDetailsInput i, uint rowVersion, CancellationToken ct = default)
    {
        var enc = await _db.PatientEncounters.Include(e => e.Emergency).FirstOrDefaultAsync(e => e.Id == id && e.Type == EncounterType.Emergency, ct);
        if (enc?.Emergency is null) return ServiceResult.Fail("Emergency visit not found.");
        var errors = await ValidateEmergencyAsync(i.CategoryId, i.AttendantContact, i.BriefComplaint, i.BroughtBy, i.AttendantName, i.ReferredBy, i.Notes, ct);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var v = enc.Emergency;
        var old = new { v.CategoryId, v.BriefComplaint, v.BroughtBy, v.AttendantName, v.AttendantContact, v.ReferredBy, v.IsMedicoLegal, enc.Notes };
        _db.Entry(enc).Property(x => x.RowVersion).OriginalValue = rowVersion;
        v.CategoryId = i.CategoryId;
        v.BriefComplaint = Clean(i.BriefComplaint, 500);
        v.BroughtBy = Clean(i.BroughtBy, 150);
        v.AttendantName = Clean(i.AttendantName, 150);
        v.AttendantContact = PatientService.FormatPhone(i.AttendantContact);
        v.ReferredBy = Clean(i.ReferredBy, 200);
        v.IsMedicoLegal = i.IsMedicoLegal;
        enc.Notes = Clean(i.Notes, 1000);
        enc.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.EmergencyUpdated, "Encounter", enc.EncounterNumber, $"Emergency {enc.EncounterNumber} details updated", old,
            new { v.CategoryId, v.BriefComplaint, v.BroughtBy, v.AttendantName, v.AttendantContact, v.ReferredBy, v.IsMedicoLegal, enc.Notes });
        return await SaveAsync(ct);
    }

    public IReadOnlyList<EmergencyStatus> AllowedStatusChanges(EmergencyStatus current, IReadOnlySet<string> perms)
    {
        var canBasic = perms.Contains(Perm.EmergencyStatus);
        var canRestricted = perms.Contains(Perm.EmergencyClose);
        if (current == EmergencyStatus.Closed)
            return canRestricted ? new[] { EmergencyStatus.UnderTreatment } : Array.Empty<EmergencyStatus>(); // re-open
        var list = new List<EmergencyStatus>();
        foreach (var s in Enum.GetValues<EmergencyStatus>())
        {
            if (s == current || s == EmergencyStatus.Registered) continue;
            if (RestrictedStatuses.Contains(s) ? canRestricted : canBasic) list.Add(s);
        }
        return list;
    }

    public async Task<ServiceResult> ChangeEmergencyStatusAsync(long id, EmergencyStatus newStatus, string? reason, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(newStatus)) return ServiceResult.Fail("Unknown status.");
        if (_ctx.UserId is not int actor) return ServiceResult.Fail("You must be logged in.");
        var enc = await _db.PatientEncounters.Include(e => e.Emergency).FirstOrDefaultAsync(e => e.Id == id && e.Type == EncounterType.Emergency, ct);
        if (enc?.Emergency is null) return ServiceResult.Fail("Emergency visit not found.");
        var v = enc.Emergency;
        var perms = await _permissions.GetEffectivePermissionsAsync(actor, ct);
        if (!AllowedStatusChanges(v.Status, perms).Contains(newStatus))
            return ServiceResult.Fail(v.Status == newStatus ? "The visit already has this status."
                : $"You are not allowed to change the status from {Label(v.Status)} to {Label(newStatus)}.");
        var reopening = v.Status == EmergencyStatus.Closed;
        if ((ReasonRequired.Contains(newStatus) || reopening) && (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3))
            return ServiceResult.Fail($"Please give a reason for '{Label(newStatus)}'" + (reopening ? " (re-opening a closed visit)" : "") + ".");
        if ((reason?.Length ?? 0) > 500) return ServiceResult.Fail("Reason must not exceed 500 characters.");

        var from = v.Status;
        var now = _clock.UtcNow;
        v.Status = newStatus;
        v.StatusChangedUtc = now;
        enc.UpdatedUtc = now;
        _db.EmergencyStatusHistory.Add(new EmergencyStatusHistory
        {
            EncounterId = enc.Id, FromStatus = from, ToStatus = newStatus, ChangedUtc = now, ChangedByUserId = actor,
            ChangedByUsername = _ctx.Username, Reason = Clean(reason, 500)
        });
        _audit.Add(AuditActions.EmergencyStatusChanged, "Encounter", enc.EncounterNumber,
            $"Emergency {enc.EncounterNumber}: {Label(from)} → {Label(newStatus)}" + (string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason.Trim()})"),
            new { Status = from.ToString() }, new { Status = newStatus.ToString(), Reason = Clean(reason, 500) });
        return await SaveAsync(ct);
    }

    public static string Label(EmergencyStatus s) => s switch
    {
        EmergencyStatus.UnderTreatment => "Under Treatment",
        EmergencyStatus.Admitted => "Admitted",
        _ => s.ToString()
    };

    // ---------------- Queries ----------------

    public Task<PatientEncounter?> GetAsync(long id, CancellationToken ct = default) =>
        _db.PatientEncounters.AsNoTracking()
            .Include(e => e.Patient)
            .Include(e => e.Department)
            .Include(e => e.Doctor)
            .Include(e => e.ServiceItem)
            .Include(e => e.Opd)
            .Include(e => e.Emergency).ThenInclude(v => v!.Category)
            .Include(e => e.Emergency).ThenInclude(v => v!.StatusHistory)
            .Include(e => e.PrintedDocuments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<PagedResult<EncounterListItem>> ListAsync(EncounterQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 5, 200);
        var query = _db.PatientEncounters.AsNoTracking().AsQueryable();
        if (q.Type is { } t) query = query.Where(e => e.Type == t);
        if (q.PatientId is { } pid) query = query.Where(e => e.PatientId == pid);
        if (q.From is { } from) query = query.Where(e => e.EncounterDate >= from);
        if (q.To is { } to) query = query.Where(e => e.EncounterDate <= to);
        if (q.DepartmentId is { } d) query = query.Where(e => e.DepartmentId == d);
        if (q.DoctorId is { } doc) query = query.Where(e => e.DoctorId == doc);
        if (q.CategoryId is { } cat) query = query.Where(e => e.Emergency != null && e.Emergency.CategoryId == cat);
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            if (q.Status == "active")
                query = query.Where(e => e.Emergency != null && (e.Emergency.Status == EmergencyStatus.Registered || e.Emergency.Status == EmergencyStatus.UnderTreatment));
            else if (Enum.TryParse<EmergencyStatus>(q.Status, true, out var st))
                query = query.Where(e => e.Emergency != null && e.Emergency.Status == st);
        }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var text = q.Q.Trim();
            var upper = text.ToUpperInvariant();
            var like = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var digits = PatientService.Digits(text);
            query = query.Where(e => e.EncounterNumber.ToUpper() == upper || e.Patient!.PatientNumber.ToUpper() == upper
                                     || EF.Functions.ILike(e.Patient!.FullName, like)
                                     || (digits.Length >= 4 && e.Patient!.MobileDigits != null && e.Patient.MobileDigits.Contains(digits)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.EncounterUtc).ThenByDescending(e => e.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(e => new
            {
                e.Id, e.EncounterNumber, e.Type, e.EncounterUtc, e.TokenNumber, e.PatientId,
                e.Patient!.PatientNumber, e.Patient.FullName, e.Patient.GuardianRelation, e.Patient.GuardianName, e.Patient.Gender,
                e.Patient.DateOfBirth, e.Patient.AgeAtRegistration, e.Patient.AgeUnitAtRegistration, e.Patient.MobileNumber, e.Patient.IsUnidentified,
                Dept = e.Department != null ? e.Department.Name : null,
                Doc = e.Doctor != null ? e.Doctor.Name : null,
                Cat = e.Emergency != null && e.Emergency.Category != null ? e.Emergency.Category.Name : null,
                Status = e.Emergency != null ? (EmergencyStatus?)e.Emergency.Status : null,
                Complaint = e.Emergency != null ? e.Emergency.BriefComplaint : e.Opd != null ? e.Opd.ChiefComplaint : null,
                Prints = e.PrintedDocuments.Count, e.CreatedByUsername
            }).ToListAsync(ct);

        var items = new List<EncounterListItem>();
        foreach (var r in rows)
        {
            var onDate = await _clock.LocalDateOfAsync(r.EncounterUtc);
            var age = r.DateOfBirth is { } dob ? AgeCalculator.FromDateOfBirth(dob, onDate)
                : r.AgeAtRegistration is { } a && r.AgeUnitAtRegistration is { } u ? new PatientAge(a, u) : (PatientAge?)null;
            items.Add(new EncounterListItem(r.Id, r.EncounterNumber, r.Type, r.EncounterUtc, r.TokenNumber, r.PatientId, r.PatientNumber,
                r.FullName, r.GuardianRelation, r.GuardianName, r.Gender, age?.ToString(), r.MobileNumber, r.IsUnidentified,
                r.Dept, r.Doc, r.Cat, r.Status, r.Complaint, r.Prints, r.CreatedByUsername));
        }
        return new PagedResult<EncounterListItem>(items, total, page, size);
    }

    public async Task<int> RecordPrintAsync(long encounterId, PrintedDocumentType type, string paper, CancellationToken ct = default)
    {
        var enc = await _db.PatientEncounters.AsNoTracking().Where(e => e.Id == encounterId).Select(e => new { e.EncounterNumber }).FirstOrDefaultAsync(ct);
        if (enc is null) return 0;
        var count = await _db.PrintedDocuments.CountAsync(p => p.EncounterId == encounterId && p.DocumentType == type, ct) + 1;
        _db.PrintedDocuments.Add(new PrintedDocument
        {
            EncounterId = encounterId, DocumentType = type, PaperSize = paper, PrintNumber = count, PrintedUtc = _clock.UtcNow,
            PrintedByUserId = _ctx.UserId, PrintedByUsername = _ctx.Username, Workstation = Clean(_ctx.Workstation, 100)
        });
        _audit.Add(AuditActions.DocumentPrinted, "Encounter", enc.EncounterNumber,
            $"{type} for {enc.EncounterNumber} opened for printing ({paper})" + (count > 1 ? $" – copy {count}" : ""));
        await _db.SaveChangesAsync(ct);
        return count;
    }

    public async Task<ClinicalCounts> CountsAsync(CancellationToken ct = default)
    {
        var today = await _clock.TodayAsync();
        var opd = await _db.PatientEncounters.CountAsync(e => e.Type == EncounterType.Opd && e.EncounterDate == today, ct);
        var er = await _db.PatientEncounters.CountAsync(e => e.Type == EncounterType.Emergency && e.EncounterDate == today, ct);
        var active = await _db.EmergencyVisits.CountAsync(v => v.Status == EmergencyStatus.Registered || v.Status == EmergencyStatus.UnderTreatment, ct);
        var start = await _clock.StartOfDayUtcAsync(today);
        var reg = await _db.Patients.CountAsync(p => p.RegisteredUtc >= start && p.Status != PatientStatus.Merged, ct);
        return new ClinicalCounts(opd, er, active, reg);
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
            return ServiceResult.Fail("This visit was changed on another computer. Please reload and try again.");
        }
    }
}
