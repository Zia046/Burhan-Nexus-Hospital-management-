using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ot;

public sealed class CssdBatchInput
{
    public string? Method { get; set; }
    public string? Steriliser { get; set; }
    public string? CycleNumber { get; set; }
    public DateTime? StartLocal { get; set; }
    public string? Parameters { get; set; }
    public string? ResponsibleStaff { get; set; }
    public List<int> SetIds { get; set; } = new();
}

public interface ICssdService
{
    Task<ServiceResult<int>> SaveInstrumentAsync(int? id, string? code, string? name, string? category, bool active, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveSetAsync(int? id, string? code, string? name, string? description, int? theatreId, bool active, IDictionary<int, int> items, CancellationToken ct = default);
    Task<ServiceResult<long>> StartBatchAsync(CssdBatchInput input, CancellationToken ct = default);
    Task<ServiceResult> BatchResultAsync(long batchId, bool passed, string? failReason, DateOnly? expiry, DateTime? endLocal, CancellationToken ct = default);
    Task<ServiceResult> ReleaseAsync(long batchId, CancellationToken ct = default);
    Task<ServiceResult> IssueAsync(int setId, long? surgeryId, string? issuedTo, CancellationToken ct = default);
    Task<ServiceResult> ReturnAsync(long issueId, string? note, CancellationToken ct = default);
    Task<ServiceResult> QuarantineAsync(int setId, string? reason, CancellationToken ct = default);
}

/// <summary>
/// CSSD: every set has a traceable identity, every cycle a batch number. Only a passed cycle can be released (also enforced by a database
/// trigger); only a sterile, unexpired set can be issued; a returned set goes back to "not sterile" and must be reprocessed.
/// </summary>
public sealed class CssdService : ICssdService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly ISurgeryService _surgeries;

    public CssdService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, ISurgeryService surgeries)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _surgeries = surgeries;
    }

    private static string? T(string? v, int max) => OtSetupService.T(v, max);

    public async Task<ServiceResult<int>> SaveInstrumentAsync(int? id, string? code, string? name, string? category, bool active, CancellationToken ct = default)
    {
        code = T(code, 30)?.ToUpperInvariant(); name = T(name, 150);
        if (code is null || name is null) return ServiceResult<int>.Fail("Instrument code and name are required.");
        if (await _db.CssdInstruments.AnyAsync(x => x.Id != id && x.Code == code, ct)) return ServiceResult<int>.Fail("Another instrument uses this code.");
        var x = id is null ? _db.CssdInstruments.Add(new CssdInstrument()).Entity : await _db.CssdInstruments.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (x is null) return ServiceResult<int>.Fail("Instrument not found.");
        x.Code = code; x.Name = name; x.Category = T(category, 60); x.IsActive = active;
        _audit.Add(AuditActions.OtCssdSaved, "CssdInstrument", code, $"CSSD instrument {name} ({code}) {(id is null ? "registered" : "updated")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(x.Id);
    }

    public async Task<ServiceResult<int>> SaveSetAsync(int? id, string? code, string? name, string? description, int? theatreId, bool active, IDictionary<int, int> items, CancellationToken ct = default)
    {
        code = T(code, 30)?.ToUpperInvariant(); name = T(name, 150);
        if (code is null || name is null) return ServiceResult<int>.Fail("Set ID and name are required.");
        if (await _db.CssdSets.AnyAsync(x => x.Id != id && x.Code == code, ct)) return ServiceResult<int>.Fail("Another instrument set uses this ID.");
        if (theatreId is int th && !await _db.OtTheatres.AnyAsync(t => t.Id == th, ct)) return ServiceResult<int>.Fail("Select a valid theatre.");
        var wanted = items.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => Math.Min(kv.Value, 999));
        if (wanted.Count > 0 && await _db.CssdInstruments.CountAsync(i => wanted.Keys.Contains(i.Id), ct) != wanted.Count) return ServiceResult<int>.Fail("Unknown instrument in the set.");
        var s = id is null ? _db.CssdSets.Add(new CssdSet { Status = CssdSetStatus.NotSterile }).Entity : await _db.CssdSets.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return ServiceResult<int>.Fail("Instrument set not found.");
        s.Code = code; s.Name = name; s.Description = T(description, 500); s.TheatreId = theatreId; s.IsActive = active; s.UpdatedUtc = _clock.UtcNow;
        foreach (var it in s.Items.ToList())
            if (!wanted.TryGetValue(it.InstrumentId, out var q)) _db.CssdSetItems.Remove(it); else it.Quantity = q;
        foreach (var (instrument, q) in wanted.Where(w => s.Items.All(i => i.InstrumentId != w.Key)))
            s.Items.Add(new CssdSetItem { InstrumentId = instrument, Quantity = q });
        _audit.Add(AuditActions.OtCssdSaved, "CssdSet", code, $"Instrument set {name} ({code}) {(id is null ? "created" : "updated")} – {wanted.Count} instrument line(s)");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult<int>.Fail("The set was changed by someone else. Please reload."); }
        return ServiceResult<int>.Ok(s.Id);
    }

    public async Task<ServiceResult<long>> StartBatchAsync(CssdBatchInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        if (T(i.Method, 60) is null) errors.Add("Enter the sterilization method.");
        if (T(i.ResponsibleStaff, 150) is null) errors.Add("Enter the responsible staff.");
        if (i.SetIds.Count == 0) errors.Add("Select the instrument sets loaded in this cycle.");
        var start = i.StartLocal is DateTime l ? await _clock.LocalToUtcAsync(l) : _clock.UtcNow;
        if (start > _clock.UtcNow.AddMinutes(5)) errors.Add("The start time cannot be in the future.");
        if (errors.Count > 0) return ServiceResult<long>.Fail(errors.ToArray());
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, "CSSD", ct);
        var sets = await _db.CssdSets.Where(s => i.SetIds.Contains(s.Id) && s.IsActive).ToListAsync(ct);
        if (sets.Count != i.SetIds.Distinct().Count()) return ServiceResult<long>.Fail("Select active instrument sets.");
        var busy = sets.Where(s => s.Status is CssdSetStatus.InProcess or CssdSetStatus.Issued).Select(s => $"{s.Code} is {s.Status}").ToList();
        if (busy.Count > 0) return ServiceResult<long>.Fail(busy.ToArray());
        var b = new CssdBatch
        {
            Number = await FinanceDb.NextNumberAsync(_db, HmsDbContext.CssdBatchSequence, "CSB", await _clock.TodayAsync(), ct), Method = T(i.Method, 60)!, Steriliser = T(i.Steriliser, 60),
            CycleNumber = T(i.CycleNumber, 30), StartUtc = start, Parameters = T(i.Parameters, 500), ResponsibleStaff = T(i.ResponsibleStaff, 150), CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username
        };
        foreach (var s in sets) { b.Sets.Add(new CssdBatchSet { SetId = s.Id }); s.Status = CssdSetStatus.InProcess; s.SterileUntil = null; s.UpdatedUtc = _clock.UtcNow; }
        _db.CssdBatches.Add(b);
        _audit.Add(AuditActions.OtCssdBatch, "CssdBatch", b.Number, $"Sterilization cycle {b.Number} ({b.Method}) started with {sets.Count} set(s): {string.Join(", ", sets.Select(s => s.Code))}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<long>.Ok(b.Id);
    }

    public async Task<ServiceResult> BatchResultAsync(long batchId, bool passed, string? failReason, DateOnly? expiry, DateTime? endLocal, CancellationToken ct = default)
    {
        var b = await _db.CssdBatches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == batchId, ct);
        if (b is null) return ServiceResult.Fail("Batch not found.");
        if (b.Status != CssdBatchStatus.InProgress) return ServiceResult.Fail($"The result of {b.Number} is already recorded ({b.Status}).");
        failReason = T(failReason, 300);
        if (!passed && failReason is null) return ServiceResult.Fail("Record why the cycle failed.");
        if (passed && expiry is null) return ServiceResult.Fail("Enter the sterility expiry / reuse-by date.");
        if (passed && expiry < await _clock.TodayAsync()) return ServiceResult.Fail("The expiry date is in the past.");
        var end = endLocal is DateTime l ? await _clock.LocalToUtcAsync(l) : _clock.UtcNow;
        if (end < b.StartUtc || end > _clock.UtcNow.AddMinutes(5)) return ServiceResult.Fail("Enter a valid end time.");
        b.Status = passed ? CssdBatchStatus.Passed : CssdBatchStatus.Failed; b.FailReason = passed ? null : failReason; b.ExpiryDate = passed ? expiry : null;
        b.EndUtc = end; b.ResultUtc = _clock.UtcNow; b.ResultBy = _ctx.Username;
        var setIds = b.Sets.Select(s => s.SetId).ToList();
        // A failed cycle never makes a set sterile – the sets go back for reprocessing.
        if (!passed)
            foreach (var s in await _db.CssdSets.Where(x => setIds.Contains(x.Id)).ToListAsync(ct)) { s.Status = CssdSetStatus.NotSterile; s.UpdatedUtc = _clock.UtcNow; }
        _audit.Add(AuditActions.OtCssdBatch, "CssdBatch", b.Number, passed ? $"Cycle {b.Number} passed – awaiting release (expiry {expiry:dd MMM yyyy})" : $"Cycle {b.Number} FAILED – {failReason}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The batch was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReleaseAsync(long batchId, CancellationToken ct = default)
    {
        var b = await _db.CssdBatches.Include(x => x.Sets).FirstOrDefaultAsync(x => x.Id == batchId, ct);
        if (b is null) return ServiceResult.Fail("Batch not found.");
        if (b.Status != CssdBatchStatus.Passed) return ServiceResult.Fail(b.Status == CssdBatchStatus.Failed ? "A failed cycle can never be released." : "Record a passed result before releasing the batch.");
        b.Status = CssdBatchStatus.Released; b.ReleasedUtc = _clock.UtcNow; b.ReleasedBy = _ctx.Username;
        var setIds = b.Sets.Select(s => s.SetId).ToList();
        foreach (var s in await _db.CssdSets.Where(x => setIds.Contains(x.Id)).ToListAsync(ct))
            if (s.Status == CssdSetStatus.InProcess) { s.Status = CssdSetStatus.Sterile; s.SterileUntil = b.ExpiryDate; s.LastBatchId = b.Id; s.UpdatedUtc = _clock.UtcNow; }
        _audit.Add(AuditActions.OtCssdBatch, "CssdBatch", b.Number, $"Batch {b.Number} released – {setIds.Count} set(s) sterile until {b.ExpiryDate:dd MMM yyyy}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The batch was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> IssueAsync(int setId, long? surgeryId, string? issuedTo, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"CSSDSET|{setId}", ct);
        var s = await _db.CssdSets.FirstOrDefaultAsync(x => x.Id == setId, ct);
        if (s is null) return ServiceResult.Fail("Instrument set not found.");
        var today = await _clock.TodayAsync();
        if (s.Status != CssdSetStatus.Sterile) return ServiceResult.Fail($"{s.Code} is {s.Status} – only a sterile, released set can be issued.");
        if (s.SterileUntil is not DateOnly until || until < today) return ServiceResult.Fail($"{s.Code}: sterility has expired – reprocess the set.");
        Surgery? g = null;
        if (surgeryId is long sid)
        {
            g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid, ct);
            if (g is null || g.Status is SurgeryStatus.Cancelled or SurgeryStatus.Completed or SurgeryStatus.Postponed) return ServiceResult.Fail("Select an open surgery.");
        }
        _db.CssdIssues.Add(new CssdIssue { SetId = s.Id, SurgeryId = surgeryId, BatchId = s.LastBatchId, IssuedUtc = _clock.UtcNow, IssuedBy = _ctx.Username, IssuedTo = T(issuedTo, 150) });
        s.Status = CssdSetStatus.Issued; s.UpdatedUtc = _clock.UtcNow;
        if (g is not null) _surgeries.Event(g.Id, "CSSD", "Set issued", null, $"{s.Code} {s.Name}", null);
        _audit.Add(AuditActions.OtCssdIssue, "CssdSet", s.Code, $"Set {s.Code} issued{(g is null ? "" : $" for surgery {g.Number}")} to {T(issuedTo, 150) ?? "OT"}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReturnAsync(long issueId, string? note, CancellationToken ct = default)
    {
        var i = await _db.CssdIssues.Include(x => x.Set).FirstOrDefaultAsync(x => x.Id == issueId, ct);
        if (i is null || i.ReturnedUtc is not null) return ServiceResult.Fail("Open issue not found.");
        i.ReturnedUtc = _clock.UtcNow; i.ReturnedBy = _ctx.Username; i.ReturnNote = T(note, 300);
        i.Set!.Status = CssdSetStatus.NotSterile; i.Set.SterileUntil = null; i.Set.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.OtCssdIssue, "CssdSet", i.Set.Code, $"Set {i.Set.Code} returned to CSSD for reprocessing {i.ReturnNote}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The set was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> QuarantineAsync(int setId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason.");
        var s = await _db.CssdSets.FirstOrDefaultAsync(x => x.Id == setId, ct);
        if (s is null) return ServiceResult.Fail("Instrument set not found.");
        if (s.Status is CssdSetStatus.Issued or CssdSetStatus.InProcess) return ServiceResult.Fail($"{s.Code} is {s.Status}.");
        s.Status = CssdSetStatus.Quarantined; s.SterileUntil = null; s.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.OtCssdSaved, "CssdSet", s.Code, $"Set {s.Code} quarantined – {reason}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
