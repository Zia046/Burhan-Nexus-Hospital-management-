using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ot;

public sealed class BookingInput
{
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public long? IpdAdmissionId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ProcedureId { get; set; }
    public string? ProcedureName { get; set; }
    public string? Diagnosis { get; set; }
    public string? Site { get; set; }
    public int TheatreId { get; set; }
    public DateTime? StartLocal { get; set; }
    public int? DurationMinutes { get; set; }
    public AnesthesiaType? AnesthesiaType { get; set; }
    public string? AnesthesiaDetail { get; set; }
    public SurgeryPriority Priority { get; set; } = SurgeryPriority.Elective;
    public string? EmergencyReason { get; set; }
    public string? ClinicalNotes { get; set; }
    /// <summary>Save as a request (to be confirmed by the OT) instead of a confirmed schedule.</summary>
    public bool AsRequest { get; set; }
    public int? SurgeonDoctorId { get; set; }
    public int? AssistantDoctorId { get; set; }
    public int? AnesthetistDoctorId { get; set; }
    /// <summary>Emergency only: proceed although a team member is booked elsewhere (reason required).</summary>
    public bool OverrideStaffConflict { get; set; }
    public string? OverrideReason { get; set; }
}

public sealed class TeamInput
{
    public OtRole Role { get; set; }
    public int? DoctorId { get; set; }
    public int? UserId { get; set; }
    public bool OverrideConflict { get; set; }
    public string? OverrideReason { get; set; }
}

public interface ISurgeryService
{
    Task<ServiceResult<Surgery>> BookAsync(BookingInput input, CancellationToken ct = default);
    Task<ServiceResult> RescheduleAsync(long id, int theatreId, DateTime startLocal, int durationMinutes, string? reason, CancellationToken ct = default);
    Task<ServiceResult> EditAsync(long id, BookingInput input, CancellationToken ct = default);
    Task<ServiceResult> SetStatusAsync(long id, SurgeryStatus status, string? reason, CancellationToken ct = default);
    Task<ServiceResult> AddTeamMemberAsync(long id, TeamInput input, CancellationToken ct = default);
    Task<ServiceResult> RemoveTeamMemberAsync(long memberId, string? reason, CancellationToken ct = default);
    Task<ServiceResult> TeamTimesAsync(long memberId, DateTime? startLocal, DateTime? endLocal, CancellationToken ct = default);
    /// <summary>Conflicts of a proposed slot (theatre, operating hours and team) – for the booking screen.</summary>
    Task<IReadOnlyList<string>> ConflictsAsync(long? surgeryId, int theatreId, DateTime startUtc, DateTime endUtc, IEnumerable<(int? DoctorId, int? UserId)> team, CancellationToken ct = default);
    void Event(long surgeryId, string category, string evt, string? from, string? to, string? note);
}

/// <summary>
/// Surgery booking, schedule and team. Every change of the schedule runs under one database lock, so two computers can never
/// book the same theatre (or the same surgeon / anesthetist) for overlapping times. Numbers come from a database sequence.
/// </summary>
public sealed class SurgeryService : ISurgeryService
{
    private const string ScheduleLock = "OT-SCHEDULE";
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IPermissionService _perms;

    public SurgeryService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IPermissionService perms)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _perms = perms;
    }

    private static string? T(string? v, int max) => OtSetupService.T(v, max);

    public static readonly SurgeryStatus[] Holding = { SurgeryStatus.Requested, SurgeryStatus.Scheduled, SurgeryStatus.PreOp, SurgeryStatus.Ready, SurgeryStatus.InProgress };

    public static string Label(SurgeryStatus s) => s switch
    {
        SurgeryStatus.PreOp => "Pre-operative preparation",
        SurgeryStatus.Ready => "Ready for surgery",
        SurgeryStatus.InProgress => "In progress",
        _ => s.ToString(),
    };

    public void Event(long surgeryId, string category, string evt, string? from, string? to, string? note) =>
        _db.SurgeryEvents.Add(new SurgeryEvent
        {
            SurgeryId = surgeryId, Category = category, Event = evt, FromValue = from is null ? null : from[..Math.Min(200, from.Length)],
            ToValue = to is null ? null : to[..Math.Min(200, to.Length)], Note = note is null ? null : note[..Math.Min(500, note.Length)],
            CreatedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username
        });

    // ===================================================================== conflicts
    public async Task<IReadOnlyList<string>> ConflictsAsync(long? surgeryId, int theatreId, DateTime startUtc, DateTime endUtc, IEnumerable<(int? DoctorId, int? UserId)> team, CancellationToken ct = default)
    {
        var list = new List<string>();
        var th = await _db.OtTheatres.AsNoTracking().FirstOrDefaultAsync(t => t.Id == theatreId, ct);
        if (th is null) { list.Add("Select an operation theatre."); return list; }
        var cleaning = th.CleaningMinutes;
        var others = await _db.Surgeries.AsNoTracking().Include(s => s.Patient)
            .Where(s => s.Id != surgeryId && s.TheatreId == theatreId && Holding.Contains(s.Status)
                        && s.PlannedStartUtc < endUtc.AddMinutes(cleaning) && startUtc < s.PlannedEndUtc.AddMinutes(s.CleaningMinutes))
            .OrderBy(s => s.PlannedStartUtc).ToListAsync(ct);
        foreach (var o in others)
            list.Add($"Theatre {th.Name} is booked for {o.Number} ({o.ProcedureName}) {o.PlannedStartUtc:HH:mm}–{o.PlannedEndUtc:HH:mm} UTC plus {o.CleaningMinutes} min preparation / cleaning.");
        list.AddRange(await StaffConflictsAsync(surgeryId, startUtc, endUtc, team, ct));
        return list;
    }

    private async Task<List<string>> StaffConflictsAsync(long? surgeryId, DateTime startUtc, DateTime endUtc, IEnumerable<(int? DoctorId, int? UserId)> team, CancellationToken ct)
    {
        var list = new List<string>();
        var doctors = team.Where(t => t.DoctorId is not null).Select(t => t.DoctorId!.Value).Distinct().ToList();
        var users = team.Where(t => t.UserId is not null).Select(t => t.UserId!.Value).Distinct().ToList();
        if (doctors.Count == 0 && users.Count == 0) return list;
        var busy = await (from m in _db.SurgeryTeamMembers.AsNoTracking()
                          join s in _db.Surgeries.AsNoTracking() on m.SurgeryId equals s.Id
                          where m.RemovedUtc == null && s.Id != surgeryId && Holding.Contains(s.Status)
                                && s.PlannedStartUtc < endUtc && startUtc < s.PlannedEndUtc
                                && ((m.DoctorId != null && doctors.Contains(m.DoctorId.Value)) || (m.UserId != null && users.Contains(m.UserId.Value)))
                          select new { m.Name, m.Role, s.Number, s.PlannedStartUtc, s.PlannedEndUtc }).ToListAsync(ct);
        foreach (var b in busy.DistinctBy(x => (x.Name, x.Number)))
            list.Add($"{b.Name} is already booked as {b.Role} for {b.Number} ({b.PlannedStartUtc:dd MMM HH:mm}–{b.PlannedEndUtc:HH:mm} UTC).");
        return list;
    }

    private async Task<string?> HoursProblemAsync(OtTheatre th, DateTime startUtc, DateTime endUtc)
    {
        if (th.OpensAt is not { } o || th.ClosesAt is not { } c) return null;
        var zone = await _clock.ZoneAsync();
        var ls = TimeZoneInfo.ConvertTimeFromUtc(startUtc, zone);
        var le = TimeZoneInfo.ConvertTimeFromUtc(endUtc, zone);
        if (ls.Date != le.Date || TimeOnly.FromDateTime(ls) < o || TimeOnly.FromDateTime(le) > c)
            return $"{th.Name} operates {o:HH:mm}–{c:HH:mm}. Book within the operating hours (urgent / emergency surgery may be booked outside them).";
        return null;
    }

    // ===================================================================== booking
    public async Task<ServiceResult<Surgery>> BookAsync(BookingInput i, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<Surgery>.Fail("The form has expired. Please reload the page.");
        var existing = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.IdempotencyKey == i.IdempotencyKey, ct);
        if (existing is not null)
        {
            _audit.Add(AuditActions.OtSurgeryDuplicateBlocked, "Surgery", existing.Number, $"Repeated submit of booking {existing.Number} ignored");
            await _db.SaveChangesAsync(ct);
            return ServiceResult<Surgery>.Ok(existing);
        }
        var errors = new List<string>();
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == i.PatientId, ct);
        if (patient is null) return ServiceResult<Surgery>.Fail("Select a registered patient.");
        if (patient.Status != PatientStatus.Active) errors.Add($"{patient.PatientNumber} is {patient.Status.ToString().ToLowerInvariant()} – surgery cannot be booked.");
        if (!Enum.IsDefined(i.Priority)) errors.Add("Select the surgery priority.");
        var emergency = i.Priority == SurgeryPriority.Emergency;
        if (emergency && T(i.EmergencyReason, 500) is null) errors.Add("Emergency surgery: record the reason for the emergency.");
        OtProcedure? proc = null;
        if (i.ProcedureId is int pid)
        {
            proc = await _db.OtProcedures.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid && p.IsActive, ct);
            if (proc is null) errors.Add("Select an active procedure.");
        }
        var procName = T(i.ProcedureName, 200) ?? proc?.Name;
        if (procName is null) errors.Add("Select the procedure (or enter its name).");
        if (i.AnesthesiaType is { } at && !Enum.IsDefined(at)) errors.Add("Unknown anesthesia type.");
        if (i.AnesthesiaType is { } at2 && proc?.AnesthesiaTypes is { Length: > 0 } allowed && !allowed.Split(',').Contains(at2.ToString()))
            errors.Add($"{proc.Name} is configured for {allowed.Replace(",", ", ")} anesthesia.");
        var duration = i.DurationMinutes ?? proc?.DurationMinutes ?? 60;
        if (duration is < 5 or > 1440) errors.Add("Estimated duration must be 5 to 1440 minutes.");
        var th = await _db.OtTheatres.AsNoTracking().FirstOrDefaultAsync(t => t.Id == i.TheatreId, ct);
        if (th is null || !th.IsActive) errors.Add("Select an active operation theatre.");
        else if (!th.IsAvailable) errors.Add($"{th.Name} is temporarily unavailable: {th.UnavailableReason}.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) errors.Add("Select a valid department.");
        var now = _clock.UtcNow;
        DateTime startUtc;
        if (i.StartLocal is DateTime s) startUtc = await _clock.LocalToUtcAsync(s);
        else if (emergency) startUtc = now;
        else { errors.Add("Enter the planned date and start time."); startUtc = now; }
        if (!emergency && startUtc < now.AddMinutes(-5)) errors.Add("The planned start time is in the past.");
        long? admissionId = i.IpdAdmissionId;
        if (admissionId is long aid && !await _db.IpdAdmissions.AnyAsync(a => a.Id == aid && a.PatientId == patient.Id && IpdService.ActiveStatuses.Contains(a.Status), ct))
            errors.Add("The IPD admission does not belong to this patient or is closed.");
        admissionId ??= await _db.IpdAdmissions.Where(a => a.PatientId == patient.Id && IpdService.ActiveStatuses.Contains(a.Status)).Select(a => (long?)a.Id).FirstOrDefaultAsync(ct);
        var team = new List<(OtRole Role, Doctor Doc)>();
        foreach (var (role, docId) in new[] { (OtRole.PrimarySurgeon, i.SurgeonDoctorId), (OtRole.AssistantSurgeon, i.AssistantDoctorId), (OtRole.Anesthetist, i.AnesthetistDoctorId) })
        {
            if (docId is not int di) continue;
            var doc = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == di && x.IsActive, ct);
            if (doc is null) errors.Add($"Select an active doctor as {role}."); else team.Add((role, doc));
        }
        if (team.Count == 0 && !emergency && !i.AsRequest) errors.Add("Select the surgeon.");
        if (errors.Count > 0) return ServiceResult<Surgery>.Fail(errors.Distinct().ToArray());

        var endUtc = startUtc.AddMinutes(duration);
        if (i.Priority == SurgeryPriority.Elective && await HoursProblemAsync(th!, startUtc, endUtc) is { } hours) return ServiceResult<Surgery>.Fail(hours);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, ScheduleLock, ct);
        var conflicts = await ConflictsAsync(null, th!.Id, startUtc, endUtc, Array.Empty<(int?, int?)>(), ct);
        if (conflicts.Count > 0) return ServiceResult<Surgery>.Fail(conflicts.Prepend("The operation theatre is not free at this time:").ToArray());
        var staff = await StaffConflictsAsync(null, startUtc, endUtc, team.Select(t => ((int?)t.Doc.Id, (int?)null)), ct);
        if (staff.Count > 0)
        {
            if (!(emergency && i.OverrideStaffConflict && T(i.OverrideReason, 300) is not null))
                return ServiceResult<Surgery>.Fail(staff.Prepend(emergency ? "Team schedule conflict – for an emergency, tick “proceed” and record the reason:" : "Team schedule conflict:").ToArray());
        }
        var today = await _clock.TodayAsync();
        var g = new Surgery
        {
            Number = await FinanceDb.NextNumberAsync(_db, HmsDbContext.SurgerySequence, "SRG", today, ct), IdempotencyKey = i.IdempotencyKey, PatientId = patient.Id,
            IpdAdmissionId = admissionId, DepartmentId = i.DepartmentId ?? proc?.DepartmentId, ProcedureId = proc?.Id, ProcedureName = procName!, Diagnosis = T(i.Diagnosis, 500),
            Site = T(i.Site, 150), TheatreId = th.Id, PlannedStartUtc = startUtc, DurationMinutes = duration, PlannedEndUtc = endUtc, CleaningMinutes = th.CleaningMinutes,
            PlannedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startUtc, await _clock.ZoneAsync())),
            AnesthesiaType = i.AnesthesiaType, AnesthesiaDetail = T(i.AnesthesiaDetail, 100), Priority = i.Priority, EmergencyReason = emergency ? T(i.EmergencyReason, 500) : T(i.EmergencyReason, 500),
            ClinicalNotes = T(i.ClinicalNotes, 2000), Status = i.AsRequest && !emergency ? SurgeryStatus.Requested : SurgeryStatus.Scheduled,
            CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username, UpdatedUtc = now
        };
        _db.Surgeries.Add(g);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_Surgeries_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<Surgery>.Ok(await _db.Surgeries.AsNoTracking().FirstAsync(x => x.IdempotencyKey == i.IdempotencyKey, ct));
        }
        foreach (var (role, doc) in team)
            _db.SurgeryTeamMembers.Add(new SurgeryTeamMember { SurgeryId = g.Id, Role = role, DoctorId = doc.Id, Name = doc.Name, AddedUtc = now, AddedBy = _ctx.Username });
        Event(g.Id, "Booking", "Booked", null, Label(g.Status), $"{g.ProcedureName} in {th.Name} {g.PlannedStartUtc:dd MMM HH:mm} UTC for {duration} min ({g.Priority})");
        if (staff.Count > 0) Event(g.Id, "Booking", "Team conflict accepted", null, null, $"{i.OverrideReason} – {string.Join(" ", staff)}");
        _audit.Add(emergency ? AuditActions.OtEmergencyBooked : AuditActions.OtSurgeryBooked, "Surgery", g.Number,
            $"{g.Priority} surgery {g.Number} booked for {patient.PatientNumber}: {g.ProcedureName}, {th.Name}, {g.PlannedStartUtc:dd MMM yyyy HH:mm} UTC" + (emergency ? $" – emergency reason: {g.EmergencyReason}" : ""));
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<Surgery>.Ok(g);
    }

    private async Task<Surgery?> LockSurgeryAsync(long id, CancellationToken ct)
    {
        await FinanceDb.LockAsync(_db, ScheduleLock, ct);
        return await _db.Surgeries.Include(s => s.Theatre).Include(s => s.Patient).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    private async Task<List<(int? DoctorId, int? UserId)>> TeamOfAsync(long id, CancellationToken ct) =>
        (await _db.SurgeryTeamMembers.AsNoTracking().Where(m => m.SurgeryId == id && m.RemovedUtc == null).Select(m => new { m.DoctorId, m.UserId }).ToListAsync(ct))
        .Select(m => (m.DoctorId, m.UserId)).ToList();

    public async Task<ServiceResult> RescheduleAsync(long id, int theatreId, DateTime startLocal, int durationMinutes, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for rescheduling.");
        if (durationMinutes is < 5 or > 1440) return ServiceResult.Fail("Estimated duration must be 5 to 1440 minutes.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var g = await LockSurgeryAsync(id, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is SurgeryStatus.InProgress or SurgeryStatus.Completed or SurgeryStatus.Cancelled) return ServiceResult.Fail($"A surgery that is {Label(g.Status).ToLowerInvariant()} cannot be rescheduled.");
        var th = await _db.OtTheatres.AsNoTracking().FirstOrDefaultAsync(t => t.Id == theatreId && t.IsActive, ct);
        if (th is null) return ServiceResult.Fail("Select an active operation theatre.");
        if (!th.IsAvailable) return ServiceResult.Fail($"{th.Name} is temporarily unavailable: {th.UnavailableReason}.");
        var startUtc = await _clock.LocalToUtcAsync(startLocal);
        if (startUtc < _clock.UtcNow.AddMinutes(-5)) return ServiceResult.Fail("The new start time is in the past.");
        var endUtc = startUtc.AddMinutes(durationMinutes);
        if (g.Priority == SurgeryPriority.Elective && await HoursProblemAsync(th, startUtc, endUtc) is { } hours) return ServiceResult.Fail(hours);
        var conflicts = await ConflictsAsync(g.Id, th.Id, startUtc, endUtc, await TeamOfAsync(g.Id, ct), ct);
        if (conflicts.Count > 0) return ServiceResult.Fail(conflicts.Prepend("The new time is not free:").ToArray());
        var from = $"{g.Theatre!.Name} {g.PlannedStartUtc:dd MMM HH:mm} UTC ({g.DurationMinutes} min)";
        g.TheatreId = th.Id; g.PlannedStartUtc = startUtc; g.DurationMinutes = durationMinutes; g.PlannedEndUtc = endUtc; g.CleaningMinutes = th.CleaningMinutes;
        g.PlannedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startUtc, await _clock.ZoneAsync()));
        if (g.Status == SurgeryStatus.Postponed) g.Status = SurgeryStatus.Scheduled;
        g.UpdatedUtc = _clock.UtcNow;
        var to = $"{th.Name} {startUtc:dd MMM HH:mm} UTC ({durationMinutes} min)";
        Event(g.Id, "Schedule", "Rescheduled", from, to, reason);
        _audit.Add(AuditActions.OtSurgeryRescheduled, "Surgery", g.Number, $"Surgery {g.Number} rescheduled: {from} → {to} – {reason}", new { from }, new { to });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The surgery was changed by someone else. Please reload."); }
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> EditAsync(long id, BookingInput i, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is SurgeryStatus.Completed or SurgeryStatus.Cancelled) return ServiceResult.Fail("A completed or cancelled surgery cannot be edited.");
        if (i.AnesthesiaType is { } at && !Enum.IsDefined(at)) return ServiceResult.Fail("Unknown anesthesia type.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) return ServiceResult.Fail("Select a valid department.");
        var name = T(i.ProcedureName, 200);
        if (name is null) return ServiceResult.Fail("The procedure name is required.");
        var old = new { g.ProcedureName, g.Diagnosis, g.Site, g.AnesthesiaType, g.AnesthesiaDetail, g.ClinicalNotes, g.DepartmentId };
        g.ProcedureName = name; g.Diagnosis = T(i.Diagnosis, 500); g.Site = T(i.Site, 150); g.AnesthesiaType = i.AnesthesiaType; g.AnesthesiaDetail = T(i.AnesthesiaDetail, 100);
        g.ClinicalNotes = T(i.ClinicalNotes, 2000); g.DepartmentId = i.DepartmentId; g.UpdatedUtc = _clock.UtcNow;
        Event(g.Id, "Booking", "Details changed", null, null, null);
        _audit.Add(AuditActions.OtSurgeryStatusChanged, "Surgery", g.Number, $"Booking details of {g.Number} changed", old,
            new { g.ProcedureName, g.Diagnosis, g.Site, g.AnesthesiaType, g.AnesthesiaDetail, g.ClinicalNotes, g.DepartmentId });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The surgery was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    /// <summary>Workflow statuses set by hand: confirm a request, pre-operative preparation, ready (needs the pre-op checklist), postpone, cancel.</summary>
    public async Task<ServiceResult> SetStatusAsync(long id, SurgeryStatus status, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var g = await LockSurgeryAsync(id, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status == status) return ServiceResult.Ok();
        if (status is SurgeryStatus.InProgress or SurgeryStatus.Completed) return ServiceResult.Fail("Start and end the surgery from the intra-operative section.");
        if (g.Status is SurgeryStatus.Completed or SurgeryStatus.Cancelled) return ServiceResult.Fail($"This surgery is {g.Status.ToString().ToLowerInvariant()}.");
        if (g.Status == SurgeryStatus.InProgress) return ServiceResult.Fail("The surgery is in progress – end it from the intra-operative section.");
        if (status is SurgeryStatus.Cancelled or SurgeryStatus.Postponed && reason is null) return ServiceResult.Fail($"Enter the reason for the {(status == SurgeryStatus.Cancelled ? "cancellation" : "postponement")}.");
        if (g.Status == SurgeryStatus.Postponed && status is not (SurgeryStatus.Cancelled)) return ServiceResult.Fail("A postponed surgery gets a new date by rescheduling it.");
        if (status == SurgeryStatus.Ready && !await _db.SurgeryChecklistStages.AnyAsync(x => x.SurgeryId == id && x.Stage == ChecklistStage.PreOp, ct))
            return ServiceResult.Fail("Complete the pre-operative checklist first (or record an authorised emergency exception).");
        var from = g.Status;
        g.Status = status;
        g.StatusReason = reason ?? g.StatusReason;
        g.UpdatedUtc = _clock.UtcNow;
        Event(g.Id, "Status", "Status", Label(from), Label(status), reason);
        _audit.Add(status switch { SurgeryStatus.Cancelled => AuditActions.OtSurgeryCancelled, SurgeryStatus.Postponed => AuditActions.OtSurgeryPostponed, _ => AuditActions.OtSurgeryStatusChanged },
            "Surgery", g.Number, $"Surgery {g.Number}: {Label(from)} → {Label(status)}" + (reason is null ? "" : $" – {reason}"));
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The surgery was changed by someone else. Please reload."); }
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // ===================================================================== team
    public async Task<ServiceResult> AddTeamMemberAsync(long id, TeamInput i, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(i.Role)) return ServiceResult.Fail("Select the role.");
        if (i.DoctorId is null == i.UserId is null) return ServiceResult.Fail("Select either a doctor or an HMS user.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var g = await LockSurgeryAsync(id, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status is SurgeryStatus.InProgress or SurgeryStatus.Completed or SurgeryStatus.Cancelled)
            return ServiceResult.Fail("The team can only be changed before the surgery starts.");
        string name;
        if (i.DoctorId is int di)
        {
            var doc = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == di && x.IsActive, ct);
            if (doc is null) return ServiceResult.Fail("Select an active doctor.");
            name = doc.Name;
        }
        else
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == i.UserId, ct);
            if (u is null || !u.IsActive) return ServiceResult.Fail("Select an active HMS user.");
            // Only staff authorised for OT work can be put on a surgical team.
            var p = await _perms.GetEffectivePermissionsAsync(u.Id, ct);
            if (!p.Any(x => x.StartsWith("Ot.", StringComparison.Ordinal))) return ServiceResult.Fail($"{u.FullName} has no Operation Theatre permission and cannot be assigned to a surgical team.");
            name = u.FullName;
        }
        if (await _db.SurgeryTeamMembers.AnyAsync(m => m.SurgeryId == id && m.RemovedUtc == null && m.Role == i.Role && m.DoctorId == i.DoctorId && m.UserId == i.UserId, ct))
            return ServiceResult.Fail($"{name} is already on the team as {i.Role}.");
        var conflicts = await StaffConflictsAsync(id, g.PlannedStartUtc, g.PlannedEndUtc, new[] { (i.DoctorId, i.UserId) }, ct);
        if (conflicts.Count > 0 && !(g.Priority == SurgeryPriority.Emergency && i.OverrideConflict && T(i.OverrideReason, 300) is not null))
            return ServiceResult.Fail(conflicts.Prepend("Schedule conflict:").ToArray());
        _db.SurgeryTeamMembers.Add(new SurgeryTeamMember { SurgeryId = id, Role = i.Role, DoctorId = i.DoctorId, UserId = i.UserId, Name = name, AddedUtc = _clock.UtcNow, AddedBy = _ctx.Username });
        Event(id, "Team", "Added", null, $"{i.Role}: {name}", conflicts.Count > 0 ? $"Conflict accepted (emergency): {i.OverrideReason}" : null);
        _audit.Add(AuditActions.OtTeamChanged, "Surgery", g.Number, $"{name} added as {i.Role} to surgery {g.Number}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveTeamMemberAsync(long memberId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for removing the team member.");
        var m = await _db.SurgeryTeamMembers.FirstOrDefaultAsync(x => x.Id == memberId, ct);
        if (m is null || m.RemovedUtc is not null) return ServiceResult.Fail("Team member not found.");
        var g = await _db.Surgeries.AsNoTracking().FirstAsync(s => s.Id == m.SurgeryId, ct);
        if (g.Status is SurgeryStatus.InProgress or SurgeryStatus.Completed or SurgeryStatus.Cancelled) return ServiceResult.Fail("The team can only be changed before the surgery starts.");
        m.RemovedUtc = _clock.UtcNow; m.RemovedBy = _ctx.Username; m.RemovedReason = reason;
        Event(g.Id, "Team", "Removed", $"{m.Role}: {m.Name}", null, reason);
        _audit.Add(AuditActions.OtTeamChanged, "Surgery", g.Number, $"{m.Name} removed as {m.Role} from surgery {g.Number} – {reason}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The team was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> TeamTimesAsync(long memberId, DateTime? startLocal, DateTime? endLocal, CancellationToken ct = default)
    {
        var m = await _db.SurgeryTeamMembers.FirstOrDefaultAsync(x => x.Id == memberId && x.RemovedUtc == null, ct);
        if (m is null) return ServiceResult.Fail("Team member not found.");
        var s = startLocal is DateTime a ? await _clock.LocalToUtcAsync(a) : (DateTime?)null;
        var e = endLocal is DateTime b ? await _clock.LocalToUtcAsync(b) : (DateTime?)null;
        if (s is not null && e is not null && e < s) return ServiceResult.Fail("End time must be after the start time.");
        if ((s ?? e) > _clock.UtcNow.AddMinutes(5) || e > _clock.UtcNow.AddMinutes(5)) return ServiceResult.Fail("Times cannot be in the future.");
        var old = new { m.StartUtc, m.EndUtc };
        m.StartUtc = s; m.EndUtc = e;
        var g = await _db.Surgeries.AsNoTracking().FirstAsync(x => x.Id == m.SurgeryId, ct);
        Event(g.Id, "Team", "Participation times", null, $"{m.Name}: {s:HH:mm}–{e:HH:mm} UTC", null);
        _audit.Add(AuditActions.OtTeamChanged, "Surgery", g.Number, $"Participation times of {m.Name} ({m.Role}) recorded for {g.Number}", old, new { m.StartUtc, m.EndUtc });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The team was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }
}
