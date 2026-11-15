using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Pharmacy;

public sealed class MedicineRequestInput
{
    public Guid IdempotencyKey { get; set; }
    public long AdmissionId { get; set; }
    public MedicineRequestPriority Priority { get; set; } = MedicineRequestPriority.Routine;
    public string? Notes { get; set; }
    public List<MedicineRequestLineInput> Lines { get; set; } = new();
}

public sealed class MedicineRequestLineInput
{
    public int MedicineId { get; set; }
    public int Quantity { get; set; }
    public string? Instructions { get; set; }
}

public enum LineDecision { Approve = 1, Reject = 2, Unavailable = 3 }

public sealed record MedicineRequestRow(long Id, string Number, string PatientNumber, string PatientName, string? AdmissionNumber, string? WardName, string? BedCode,
    MedicineRequestPriority Priority, MedicineRequestStatus Status, int Lines, int OpenLines, DateTime RequestedUtc, string? RequestedBy);

public interface IMedicineRequestService
{
    /// <param name="wards">Wards the nurse works in (null = all wards).</param>
    Task<ServiceResult<DocumentResult>> CreateAsync(MedicineRequestInput input, IReadOnlyList<int>? wards, CancellationToken ct = default);
    Task<ServiceResult> CancelAsync(long requestId, string? reason, IReadOnlyList<int>? wards, CancellationToken ct = default);
    Task<ServiceResult> DecideAsync(long lineId, LineDecision decision, string? note, CancellationToken ct = default);
    /// <summary>Issues the given quantities through the normal patient issue (stock deducted once, against the admission).</summary>
    Task<ServiceResult<DocumentResult>> IssueAsync(long requestId, Guid idempotencyKey, IReadOnlyDictionary<long, int> quantities, PharmacyRights rights, CancellationToken ct = default);
    Task<MedicineRequest?> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineRequestEvent>> EventsAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineRequestRow>> ListAsync(bool openOnly, long? admissionId, IReadOnlyList<int>? wards, int take = 200, CancellationToken ct = default);
}

/// <summary>
/// Update 9 – nursing medicine demand. A nurse requests medicines for an admitted patient; the pharmacy approves, rejects, marks
/// unavailable or issues (fully or partly). Nothing is deducted from stock until the pharmacy issues, and every issue is a normal
/// patient issue linked to the admission, so stock, prices and charges follow the existing pharmacy rules.
/// </summary>
public sealed class MedicineRequestService : IMedicineRequestService
{
    private static readonly AdmissionStatus[] Active =
        { AdmissionStatus.Registered, AdmissionStatus.Admitted, AdmissionStatus.UnderTreatment, AdmissionStatus.Transferred, AdmissionStatus.DischargeRequested };
    private static readonly MedicineRequestLineStatus[] OpenLine = { MedicineRequestLineStatus.Pending, MedicineRequestLineStatus.Approved, MedicineRequestLineStatus.PartlyIssued };
    private static readonly MedicineRequestStatus[] OpenRequest = { MedicineRequestStatus.Pending, MedicineRequestStatus.Approved, MedicineRequestStatus.PartlyIssued };

    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IPharmacyService _pharmacy;

    public MedicineRequestService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IPharmacyService pharmacy)
    {
        _db = db; _audit = audit; _ctx = ctx; _clock = clock; _pharmacy = pharmacy;
    }

    private static string? T(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > max ? s.Trim()[..max] : s.Trim());

    private MedicineRequestEvent Ev(long requestId, long? lineId, string ev, int? qty = null, string? note = null, long? issueId = null, string? issueNo = null) => new()
    {
        RequestId = requestId, LineId = lineId, Event = ev, Quantity = qty, Note = T(note, 300), PharmacyIssueId = issueId, PharmacyIssueNumber = issueNo,
        CreatedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username
    };

    public async Task<ServiceResult<DocumentResult>> CreateAsync(MedicineRequestInput input, IReadOnlyList<int>? wards, CancellationToken ct = default)
    {
        if (input.IdempotencyKey == Guid.Empty) return ServiceResult<DocumentResult>.Fail("The form has expired. Please reload the page.");
        if (await ExistingAsync(input.IdempotencyKey, ct) is { } dup) return ServiceResult<DocumentResult>.Ok(dup);
        var adm = await _db.IpdAdmissions.Include(a => a.CurrentWard).Include(a => a.CurrentBed).Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == input.AdmissionId, ct);
        if (adm is null) return ServiceResult<DocumentResult>.Fail("Admission not found.");
        if (!Active.Contains(adm.Status)) return ServiceResult<DocumentResult>.Fail("Medicines can only be requested for an active admission.");
        if (wards is not null && adm.CurrentWardId is int w && !wards.Contains(w)) return ServiceResult<DocumentResult>.Fail("This patient is not in your ward.");

        var errors = new List<string>();
        var lines = input.Lines.Where(l => l.MedicineId > 0 || l.Quantity > 0).ToList();
        if (lines.Count == 0) errors.Add("Add at least one medicine.");
        if (lines.GroupBy(l => l.MedicineId).Any(g => g.Count() > 1)) errors.Add("A medicine is listed twice – put the total quantity on one line.");
        var ids = lines.Select(l => l.MedicineId).Distinct().ToList();
        var meds = await _db.Medicines.Include(m => m.DosageForm).Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        foreach (var l in lines)
        {
            if (!meds.TryGetValue(l.MedicineId, out var m) || !m.IsActive) errors.Add("Choose an active medicine on every line.");
            if (l.Quantity is <= 0 or > 10000) errors.Add($"{(meds.TryGetValue(l.MedicineId, out var mm) ? mm.DisplayName : "Medicine")}: enter a quantity between 1 and 10000.");
        }
        if (errors.Count > 0) return ServiceResult<DocumentResult>.Fail(errors.Distinct().ToArray());

        await using var tx = await TxScope.BeginAsync(_db, ct);
        // One lock per admission: two nurses cannot request the same medicine twice at the same moment.
        await FinanceDb.LockAsync(_db, $"MRQ-ADM|{adm.Id}", ct);
        if (await ExistingAsync(input.IdempotencyKey, ct) is { } dup2) { await tx.RollbackAsync(CancellationToken.None); return ServiceResult<DocumentResult>.Ok(dup2); }
        var open = await _db.MedicineRequestLines.AsNoTracking()
            .Where(l => l.Request!.AdmissionId == adm.Id && ids.Contains(l.MedicineId) && OpenLine.Contains(l.Status) && l.IssuedQuantity < l.Quantity)
            .Select(l => new { l.MedicineName, l.Request!.Number }).ToListAsync(ct);
        if (open.Count > 0)
        {
            await tx.RollbackAsync(CancellationToken.None);
            await _audit.LogAsync(AuditActions.MedicineRequestCreated, "IpdAdmission", adm.AdmissionNumber,
                $"Duplicate medicine request blocked: {string.Join(", ", open.Select(o => $"{o.MedicineName} ({o.Number})"))}", success: false, ct: ct);
            return ServiceResult<DocumentResult>.Fail(open.Select(o => $"{o.MedicineName} is already requested for this patient ({o.Number}) and not yet issued.").ToArray());
        }
        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        var name = _ctx.UserId is int uid ? await _db.Users.Where(u => u.Id == uid).Select(u => u.FullName).FirstOrDefaultAsync(ct) : null;
        var req = new MedicineRequest
        {
            Number = await FinanceDb.NextNumberAsync(_db, HmsDbContext.MedicineRequestSequence, "MRQ", today, ct), IdempotencyKey = input.IdempotencyKey,
            AdmissionId = adm.Id, PatientId = adm.PatientId, WardId = adm.CurrentWardId, WardName = adm.CurrentWard?.Name, BedCode = adm.CurrentBed?.Code,
            Priority = Enum.IsDefined(input.Priority) ? input.Priority : MedicineRequestPriority.Routine, Notes = T(input.Notes, 500),
            RequestedUtc = now, UpdatedUtc = now, RequestedByUserId = _ctx.UserId, RequestedByUsername = _ctx.Username, RequestedByName = name ?? _ctx.Username
        };
        foreach (var l in lines)
            req.Lines.Add(new MedicineRequestLine { MedicineId = l.MedicineId, MedicineName = meds[l.MedicineId].DisplayName, Quantity = l.Quantity, Instructions = T(l.Instructions, 300) });
        _db.MedicineRequests.Add(req);
        try
        {
            await _db.SaveChangesAsync(ct);
            _db.MedicineRequestEvents.Add(Ev(req.Id, null, "Created", req.Lines.Sum(x => x.Quantity), $"{req.Lines.Count} medicine(s){(req.Priority == MedicineRequestPriority.Urgent ? ", URGENT" : "")}"));
            _audit.Add(AuditActions.MedicineRequestCreated, "MedicineRequest", req.Number,
                $"{req.Number}: {req.Lines.Count} medicine(s) requested for {adm.Patient?.PatientNumber} ({adm.AdmissionNumber}, {req.WardName} {req.BedCode})",
                newValues: new { req.Number, Lines = req.Lines.Select(x => new { x.MedicineName, x.Quantity }) });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_MedicineRequests_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return await ExistingAsync(input.IdempotencyKey, ct) is { } d ? ServiceResult<DocumentResult>.Ok(d) : ServiceResult<DocumentResult>.Fail("Please try again.");
        }
        return ServiceResult<DocumentResult>.Ok(new DocumentResult(req.Id, req.Number, false));
    }

    private Task<DocumentResult?> ExistingAsync(Guid key, CancellationToken ct) =>
        _db.MedicineRequests.AsNoTracking().Where(r => r.IdempotencyKey == key).Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct);

    public async Task<ServiceResult> CancelAsync(long requestId, string? reason, IReadOnlyList<int>? wards, CancellationToken ct = default)
    {
        if (T(reason, 300) is not { } why) return ServiceResult.Fail("Enter the reason for cancelling.");
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"MRQ|{requestId}", ct);
        var req = await _db.MedicineRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null) return ServiceResult.Fail("Request not found.");
        if (wards is not null && req.WardId is int w && !wards.Contains(w)) return ServiceResult.Fail("This request is not from your ward.");
        if (!OpenRequest.Contains(req.Status)) return ServiceResult.Fail("This request is already closed.");
        var n = 0;
        foreach (var l in req.Lines.Where(l => OpenLine.Contains(l.Status)))
        {
            // Lines already partly issued keep the issued quantity; only the remainder is cancelled.
            l.Status = l.IssuedQuantity > 0 ? MedicineRequestLineStatus.Issued : MedicineRequestLineStatus.Cancelled;
            if (l.IssuedQuantity > 0) { l.Quantity = l.IssuedQuantity; }
            l.DecisionNote = why; l.DecidedUtc = _clock.UtcNow; l.DecidedBy = _ctx.Username;
            n++;
        }
        Recalc(req);
        if (req.Status is not (MedicineRequestStatus.Completed)) req.Status = MedicineRequestStatus.Cancelled;
        req.UpdatedUtc = _clock.UtcNow;
        _db.MedicineRequestEvents.Add(Ev(req.Id, null, "Cancelled", null, why));
        _audit.Add(AuditActions.MedicineRequestCancelled, "MedicineRequest", req.Number, $"{req.Number} cancelled ({n} open line(s)): {why}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DecideAsync(long lineId, LineDecision decision, string? note, CancellationToken ct = default)
    {
        var requestId = await _db.MedicineRequestLines.Where(l => l.Id == lineId).Select(l => (long?)l.RequestId).FirstOrDefaultAsync(ct);
        if (requestId is null) return ServiceResult.Fail("Request line not found.");
        if (decision != LineDecision.Approve && T(note, 300) is null) return ServiceResult.Fail("Enter the reason.");
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"MRQ|{requestId}", ct);
        var req = await _db.MedicineRequests.Include(r => r.Lines).FirstAsync(r => r.Id == requestId, ct);
        var line = req.Lines.First(l => l.Id == lineId);
        if (!OpenLine.Contains(line.Status)) return ServiceResult.Fail($"{line.MedicineName}: this line is already {line.Status.ToString().ToLowerInvariant()}.");
        if (decision == LineDecision.Approve && line.Status != MedicineRequestLineStatus.Pending) return ServiceResult.Fail($"{line.MedicineName} is already approved.");
        switch (decision)
        {
            case LineDecision.Approve: line.Status = MedicineRequestLineStatus.Approved; break;
            case LineDecision.Reject: line.Status = line.IssuedQuantity > 0 ? MedicineRequestLineStatus.Issued : MedicineRequestLineStatus.Rejected; break;
            case LineDecision.Unavailable: line.Status = line.IssuedQuantity > 0 ? MedicineRequestLineStatus.Issued : MedicineRequestLineStatus.Unavailable; break;
            default: return ServiceResult.Fail("Choose a decision.");
        }
        if (decision != LineDecision.Approve && line.IssuedQuantity > 0) line.Quantity = line.IssuedQuantity; // remainder closed
        line.DecisionNote = T(note, 300); line.DecidedUtc = _clock.UtcNow; line.DecidedBy = _ctx.Username;
        Recalc(req);
        req.UpdatedUtc = _clock.UtcNow;
        var ev = decision switch { LineDecision.Approve => "Approved", LineDecision.Reject => "Rejected", _ => "Unavailable" };
        _db.MedicineRequestEvents.Add(Ev(req.Id, line.Id, ev, line.Quantity, note));
        _audit.Add(decision == LineDecision.Approve ? AuditActions.MedicineRequestApproved : AuditActions.MedicineRequestRejected, "MedicineRequest", req.Number,
            $"{req.Number}: {line.MedicineName} × {line.Quantity} {ev.ToLowerInvariant()}{(T(note, 300) is { } n ? $" – {n}" : "")}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<DocumentResult>> IssueAsync(long requestId, Guid idempotencyKey, IReadOnlyDictionary<long, int> quantities, PharmacyRights rights, CancellationToken ct = default)
    {
        if (idempotencyKey == Guid.Empty) return ServiceResult<DocumentResult>.Fail("The form has expired. Please reload the page.");
        var wanted = quantities.Where(q => q.Value != 0).ToDictionary(q => q.Key, q => q.Value);
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"MRQ|{requestId}", ct);
        // A repeated submission (double click, refresh) finds the issue already made and changes nothing.
        var done = await _db.MedicineRequestEvents.AsNoTracking().Where(e => e.RequestId == requestId && e.Event == "Issued" && e.PharmacyIssueId != null)
            .Join(_db.PatientMedicineIssues.Where(i => i.IdempotencyKey == idempotencyKey), e => e.PharmacyIssueId, i => i.Id, (e, i) => new DocumentResult(i.Id, i.Number, true))
            .FirstOrDefaultAsync(ct);
        if (done is not null)
        {
            await tx.RollbackAsync(CancellationToken.None);
            await _audit.LogAsync(AuditActions.PharmDuplicateBlocked, "MedicineRequest", done.Number, "Repeated issue of a ward request ignored (stock was not changed twice)", ct: ct);
            return ServiceResult<DocumentResult>.Ok(done);
        }
        var req = await _db.MedicineRequests.Include(r => r.Lines).Include(r => r.Admission).FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (req is null) return ServiceResult<DocumentResult>.Fail("Request not found.");
        if (!OpenRequest.Contains(req.Status)) return ServiceResult<DocumentResult>.Fail($"{req.Number} is {req.Status.ToString().ToLowerInvariant()} – nothing can be issued.");
        if (req.Admission is null || !Active.Contains(req.Admission.Status)) return ServiceResult<DocumentResult>.Fail("The patient is no longer admitted. Medicines cannot be issued against this request.");
        var errors = new List<string>();
        var issue = new List<(MedicineRequestLine Line, int Qty)>();
        foreach (var (lineId, qty) in wanted)
        {
            var line = req.Lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null) { errors.Add("A line does not belong to this request."); continue; }
            if (!OpenLine.Contains(line.Status)) { errors.Add($"{line.MedicineName}: line is {line.Status.ToString().ToLowerInvariant()}."); continue; }
            if (qty < 0 || qty > line.Remaining) { errors.Add($"{line.MedicineName}: issue between 1 and {line.Remaining}."); continue; }
            issue.Add((line, qty));
        }
        if (issue.Count == 0 && errors.Count == 0) errors.Add("Enter the quantity to issue.");
        if (errors.Count > 0) { await tx.RollbackAsync(CancellationToken.None); return ServiceResult<DocumentResult>.Fail(errors.ToArray()); }

        var pharmacyInput = new IssueInput
        {
            IdempotencyKey = idempotencyKey, PatientId = req.PatientId, IpdAdmissionId = req.AdmissionId, Notes = $"Ward request {req.Number}",
            Lines = issue.Select(x => new StockLineInput { MedicineId = x.Line.MedicineId, Quantity = x.Qty, Instructions = x.Line.Instructions }).ToList()
        };
        ServiceResult<DocumentResult> r;
        try { r = await _pharmacy.IssueAsync(pharmacyInput, rights with { SelectBatch = false, Discount = false }, ct); }
        catch { await tx.RollbackAsync(CancellationToken.None); _db.ChangeTracker.Clear(); throw; }
        if (!r.Succeeded || r.Value is null)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return r;
        }
        foreach (var (line, qty) in issue)
        {
            line.IssuedQuantity += qty;
            line.Status = line.IssuedQuantity >= line.Quantity ? MedicineRequestLineStatus.Issued : MedicineRequestLineStatus.PartlyIssued;
            _db.MedicineRequestEvents.Add(Ev(req.Id, line.Id, "Issued", qty, line.MedicineName, r.Value.Id, r.Value.Number));
        }
        Recalc(req);
        req.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.MedicineRequestIssued, "MedicineRequest", req.Number,
            $"{req.Number}: issued as {r.Value.Number} – {string.Join(", ", issue.Select(x => $"{x.Line.MedicineName} × {x.Qty}"))} (status {req.Status})");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<DocumentResult>.Fail("The request was changed by someone else. Please reload.");
        }
        return r;
    }

    /// <summary>Request status from its lines.</summary>
    private static void Recalc(MedicineRequest req)
    {
        var lines = req.Lines;
        if (lines.All(l => !OpenLine.Contains(l.Status)))
            req.Status = lines.Any(l => l.IssuedQuantity > 0) ? MedicineRequestStatus.Completed
                : lines.All(l => l.Status == MedicineRequestLineStatus.Cancelled) ? MedicineRequestStatus.Cancelled : MedicineRequestStatus.Rejected;
        else if (lines.Any(l => l.IssuedQuantity > 0)) req.Status = MedicineRequestStatus.PartlyIssued;
        else if (lines.Any(l => l.Status == MedicineRequestLineStatus.Approved)) req.Status = MedicineRequestStatus.Approved;
        else req.Status = MedicineRequestStatus.Pending;
    }

    public Task<MedicineRequest?> GetAsync(long id, CancellationToken ct = default) =>
        _db.MedicineRequests.AsNoTracking().Include(r => r.Lines.OrderBy(l => l.Id)).Include(r => r.Patient).Include(r => r.Admission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<MedicineRequestEvent>> EventsAsync(long id, CancellationToken ct = default) =>
        await _db.MedicineRequestEvents.AsNoTracking().Where(e => e.RequestId == id).OrderBy(e => e.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<MedicineRequestRow>> ListAsync(bool openOnly, long? admissionId, IReadOnlyList<int>? wards, int take = 200, CancellationToken ct = default)
    {
        var q = _db.MedicineRequests.AsNoTracking().AsQueryable();
        if (openOnly) q = q.Where(r => OpenRequest.Contains(r.Status));
        if (admissionId is long a) q = q.Where(r => r.AdmissionId == a);
        if (wards is not null) q = q.Where(r => r.WardId == null || wards.Contains(r.WardId.Value));
        return await q.OrderByDescending(r => r.Priority == MedicineRequestPriority.Urgent && openOnly).ThenByDescending(r => r.Id).Take(take)
            .Select(r => new MedicineRequestRow(r.Id, r.Number, r.Patient!.PatientNumber, r.Patient.FullName, r.Admission!.AdmissionNumber, r.WardName, r.BedCode,
                r.Priority, r.Status, r.Lines.Count, r.Lines.Count(l => OpenLine.Contains(l.Status)), r.RequestedUtc, r.RequestedByName))
            .ToListAsync(ct);
    }
}
