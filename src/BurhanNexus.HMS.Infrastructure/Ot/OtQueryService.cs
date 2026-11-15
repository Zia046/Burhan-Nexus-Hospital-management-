using System.Globalization;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ot;

public sealed record SurgeryRow(
    long Id, string Number, long PatientId, string PatientNumber, string PatientName, string AgeGender, string Procedure, string Theatre, int TheatreId,
    DateTime PlannedStartUtc, DateTime PlannedEndUtc, int CleaningMinutes, SurgeryPriority Priority, SurgeryStatus Status, string? Surgeon, string? Anesthetist,
    AnesthesiaType? Anesthesia, DateTime? ActualStartUtc, DateTime? ActualEndUtc, RecoveryStatus? Recovery, string? Department, long? IpdAdmissionId);

public sealed record TheatreLoad(int Id, string Name, OtType Type, bool Available, bool Occupied, int Booked, int BookedMinutes, int UsedMinutes, string? UnavailableReason);

public sealed record OtDashboard(DateOnly From, DateOnly To, int Scheduled, int Completed, int InProgress, int Upcoming, int Cancelled, int Postponed, int Emergency,
    int Waiting, int InRecovery, int TheatresAvailable, int TheatresOccupied, decimal Utilization, IReadOnlyList<SurgeryRow> Rows, IReadOnlyList<TheatreLoad> Theatres);

public sealed record OtReport(string Key, string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string>? Totals = null, bool Financial = false);

public sealed class OtFilter
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int? TheatreId { get; set; }
    public int? SurgeonId { get; set; }
    public int? AnesthetistId { get; set; }
    public int? DepartmentId { get; set; }
    public int? ProcedureId { get; set; }
    public SurgeryStatus? Status { get; set; }
    public string? Q { get; set; }
}

public interface IOtQueryService
{
    Task<IReadOnlyList<SurgeryRow>> SurgeriesAsync(OtFilter f, CancellationToken ct = default);
    Task<OtDashboard> DashboardAsync(OtFilter f, CancellationToken ct = default);
    Task<Surgery?> SurgeryAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<SurgeryTeamMember>> TeamAsync(long id, bool includeRemoved, CancellationToken ct = default);
    Task<IReadOnlyList<RecordView>> RecordsAsync(long surgeryId, RecordKind[] kinds, bool includeSuperseded = true, CancellationToken ct = default);
    Task<IReadOnlyList<OtReport>> ReportAsync(string key, OtFilter f, bool finance, bool isPrivate, CancellationToken ct = default);
    Task<IReadOnlyList<(int Id, string Name)>> SurgeonsAsync(CancellationToken ct = default);
}

public sealed class OtQueryService : IOtQueryService
{
    private readonly HmsDbContext _db;
    private readonly HospitalClock _clock;
    public OtQueryService(HmsDbContext db, HospitalClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public static readonly (string Key, string Title, bool Financial)[] Reports =
    {
        ("daily", "Daily Surgery Report", false), ("monthly", "Monthly Surgery Report", false), ("surgeon", "Surgeon-wise Surgery Report", false),
        ("department", "Department-wise Surgery Report", false), ("theatre", "OT-wise Utilization Report", false), ("procedure", "Procedure-wise Statistics", false),
        ("priority", "Emergency vs Elective Surgery Report", false), ("cancelled", "Cancelled / Postponed Surgery Report", false), ("anesthesia", "Anesthesia-wise Report", false),
        ("complications", "Complication Report", false), ("recovery", "Recovery Room Report", false), ("consumption", "OT Medicine and Consumable Usage", false),
        ("stock", "OT Stock Report", false), ("cssd", "CSSD Sterilization Report", false), ("revenue", "OT Revenue and Charges Report", true),
    };

    private async Task<(DateTime From, DateTime To)> UtcRangeAsync(OtFilter f) =>
        (await _clock.StartOfDayUtcAsync(f.From), await _clock.StartOfDayUtcAsync(f.To.AddDays(1)));

    private IQueryable<Surgery> Filtered(OtFilter f, DateTime from, DateTime to)
    {
        var q = _db.Surgeries.AsNoTracking().Where(s => s.PlannedStartUtc < to && s.PlannedEndUtc >= from);
        if (f.TheatreId is int t) q = q.Where(s => s.TheatreId == t);
        if (f.DepartmentId is int d) q = q.Where(s => s.DepartmentId == d);
        if (f.ProcedureId is int p) q = q.Where(s => s.ProcedureId == p);
        if (f.Status is SurgeryStatus st) q = q.Where(s => s.Status == st);
        if (f.SurgeonId is int sg) q = q.Where(s => _db.SurgeryTeamMembers.Any(m => m.SurgeryId == s.Id && m.RemovedUtc == null && m.DoctorId == sg && (m.Role == OtRole.PrimarySurgeon || m.Role == OtRole.AssistantSurgeon)));
        if (f.AnesthetistId is int an) q = q.Where(s => _db.SurgeryTeamMembers.Any(m => m.SurgeryId == s.Id && m.RemovedUtc == null && m.DoctorId == an && m.Role == OtRole.Anesthetist));
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            q = q.Where(s => s.Number == term || s.Patient!.PatientNumber == term || EF.Functions.ILike(s.Patient!.FullName, "%" + term + "%") || EF.Functions.ILike(s.ProcedureName, "%" + term + "%"));
        }
        return q;
    }

    public async Task<IReadOnlyList<SurgeryRow>> SurgeriesAsync(OtFilter f, CancellationToken ct = default)
    {
        var (from, to) = await UtcRangeAsync(f);
        var list = await Filtered(f, from, to).Include(s => s.Patient).Include(s => s.Theatre).Include(s => s.Department)
            .OrderBy(s => s.PlannedStartUtc).Take(1000).ToListAsync(ct);
        return await RowsAsync(list, ct);
    }

    private async Task<List<SurgeryRow>> RowsAsync(List<Surgery> list, CancellationToken ct)
    {
        var ids = list.Select(s => s.Id).ToList();
        var team = await _db.SurgeryTeamMembers.AsNoTracking().Where(m => ids.Contains(m.SurgeryId) && m.RemovedUtc == null).ToListAsync(ct);
        var rec = await _db.RecoveryEpisodes.AsNoTracking().Where(r => ids.Contains(r.SurgeryId)).ToDictionaryAsync(r => r.SurgeryId, r => r.Status, ct);
        var today = await _clock.TodayAsync();
        return list.Select(s => new SurgeryRow(s.Id, s.Number, s.PatientId, s.Patient!.PatientNumber, s.Patient.FullName,
            $"{AgeCalculator.Of(s.Patient, today)?.ToString() ?? "—"} / {s.Patient.Gender.ToString()[0]}", s.ProcedureName, s.Theatre!.Name, s.TheatreId,
            s.PlannedStartUtc, s.PlannedEndUtc, s.CleaningMinutes, s.Priority, s.Status,
            team.FirstOrDefault(m => m.SurgeryId == s.Id && m.Role == OtRole.PrimarySurgeon)?.Name, team.FirstOrDefault(m => m.SurgeryId == s.Id && m.Role == OtRole.Anesthetist)?.Name,
            s.AnesthesiaType, s.ActualStartUtc, s.ActualEndUtc, rec.TryGetValue(s.Id, out var r) ? r : null, s.Department?.Name, s.IpdAdmissionId)).ToList();
    }

    public async Task<OtDashboard> DashboardAsync(OtFilter f, CancellationToken ct = default)
    {
        var rows = await SurgeriesAsync(f, ct);
        var now = _clock.UtcNow;
        var theatres = await _db.OtTheatres.AsNoTracking().Where(t => t.IsActive && (f.TheatreId == null || t.Id == f.TheatreId)).OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct);
        var inRecovery = await _db.RecoveryEpisodes.AsNoTracking().CountAsync(r => r.Status == RecoveryStatus.Awaiting || r.Status == RecoveryStatus.InRecovery
                                                                                  || r.Status == RecoveryStatus.UnderObservation || r.Status == RecoveryStatus.ReadyForTransfer, ct);
        var inProgressTheatres = await _db.Surgeries.AsNoTracking().Where(s => s.Status == SurgeryStatus.InProgress).Select(s => s.TheatreId).ToListAsync(ct);
        var days = Math.Max(1, f.To.DayNumber - f.From.DayNumber + 1);
        var loads = theatres.Select(t =>
        {
            var mine = rows.Where(r => r.TheatreId == t.Id && r.Status != SurgeryStatus.Cancelled && r.Status != SurgeryStatus.Postponed).ToList();
            var used = mine.Where(r => r.ActualStartUtc is not null).Sum(r => (int)((r.ActualEndUtc ?? now) - r.ActualStartUtc!.Value).TotalMinutes);
            return new TheatreLoad(t.Id, t.Name, t.Type, t.IsAvailable, inProgressTheatres.Contains(t.Id), mine.Count, mine.Sum(r => (int)(r.PlannedEndUtc - r.PlannedStartUtc).TotalMinutes), used, t.UnavailableReason);
        }).ToList();
        var capacity = theatres.Sum(t => t.OpensAt is { } o && t.ClosesAt is { } c ? (c - o).TotalMinutes : 24 * 60) * days;
        var util = capacity <= 0 ? 0 : Math.Round((decimal)(loads.Sum(l => l.BookedMinutes) / capacity) * 100, 1);
        return new OtDashboard(f.From, f.To, rows.Count(r => r.Status is not (SurgeryStatus.Cancelled or SurgeryStatus.Postponed)), rows.Count(r => r.Status == SurgeryStatus.Completed),
            rows.Count(r => r.Status == SurgeryStatus.InProgress), rows.Count(r => r.Status is SurgeryStatus.Requested or SurgeryStatus.Scheduled or SurgeryStatus.PreOp or SurgeryStatus.Ready && r.PlannedStartUtc >= now),
            rows.Count(r => r.Status == SurgeryStatus.Cancelled), rows.Count(r => r.Status == SurgeryStatus.Postponed), rows.Count(r => r.Priority == SurgeryPriority.Emergency),
            rows.Count(r => r.Status is SurgeryStatus.PreOp or SurgeryStatus.Ready || r.Status == SurgeryStatus.Scheduled && r.PlannedStartUtc < now), inRecovery,
            loads.Count(l => l.Available && !l.Occupied), loads.Count(l => l.Occupied), util, rows, loads);
    }

    public async Task<Surgery?> SurgeryAsync(long id, CancellationToken ct = default) =>
        await _db.Surgeries.AsNoTracking().Include(s => s.Patient).Include(s => s.Theatre).Include(s => s.Procedure).Include(s => s.Department).Include(s => s.IpdAdmission)
            .AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SurgeryTeamMember>> TeamAsync(long id, bool includeRemoved, CancellationToken ct = default) =>
        await _db.SurgeryTeamMembers.AsNoTracking().Where(m => m.SurgeryId == id && (includeRemoved || m.RemovedUtc == null)).OrderBy(m => m.Role).ThenBy(m => m.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<RecordView>> RecordsAsync(long surgeryId, RecordKind[] kinds, bool includeSuperseded = true, CancellationToken ct = default)
    {
        var list = await _db.ClinicalRecords.AsNoTracking().Where(r => r.SurgeryId == surgeryId && kinds.Contains(r.Kind)).OrderBy(r => r.RecordedUtc).ThenBy(r => r.Id).Take(2000).ToListAsync(ct);
        var ids = list.Select(r => r.Id).ToList();
        var corrections = await _db.ClinicalRecords.AsNoTracking().Where(r => r.CorrectsRecordId != null && ids.Contains(r.CorrectsRecordId.Value))
            .Select(r => new { Original = r.CorrectsRecordId!.Value, r.Id }).ToDictionaryAsync(r => r.Original, r => r.Id, ct);
        return list.Select(r => new RecordView(r, IpdQueryService.Parse(r.Values), corrections.TryGetValue(r.Id, out var by) ? by : null))
            .Where(v => includeSuperseded || !v.IsSuperseded).ToList();
    }

    public async Task<IReadOnlyList<(int Id, string Name)>> SurgeonsAsync(CancellationToken ct = default) =>
        (await _db.Doctors.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new { d.Id, d.Name }).ToListAsync(ct)).Select(d => (d.Id, d.Name)).ToList();

    // ===================================================================== reports
    private static string N(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);
    private static IReadOnlyList<string> R(params object?[] cells) => cells.Select(c => c?.ToString() ?? "—").ToList();

    public async Task<IReadOnlyList<OtReport>> ReportAsync(string key, OtFilter f, bool finance, bool isPrivate, CancellationToken ct = default)
    {
        var def = Reports.FirstOrDefault(r => r.Key == key);
        if (def.Key is null) return Array.Empty<OtReport>();
        if (def.Financial && !finance) return Array.Empty<OtReport>();
        var (fromUtc, toUtc) = await UtcRangeAsync(f);
        var zone = await _clock.ZoneAsync();
        string L(DateTime? u) => u is DateTime x ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x, DateTimeKind.Utc), zone).ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) : "—";
        var rows = key is "consumption" or "stock" or "cssd" or "revenue" ? new List<SurgeryRow>() : (await SurgeriesAsync(f, ct)).ToList();
        var done = rows.Where(r => r.Status != SurgeryStatus.Cancelled && r.Status != SurgeryStatus.Postponed).ToList();
        int Mins(SurgeryRow r) => r.ActualStartUtc is DateTime s && r.ActualEndUtc is DateTime e ? (int)(e - s).TotalMinutes : 0;
        OtReport Group(string col, Func<SurgeryRow, string> by) =>
            new(key, def.Title, new[] { col, "Surgeries", "Completed", "Emergency", "Cancelled / postponed", "Operating minutes" },
                rows.GroupBy(by).OrderByDescending(g => g.Count()).Select(g => R(g.Key, g.Count(), g.Count(x => x.Status == SurgeryStatus.Completed),
                    g.Count(x => x.Priority == SurgeryPriority.Emergency), g.Count(x => x.Status is SurgeryStatus.Cancelled or SurgeryStatus.Postponed), g.Sum(Mins))).ToList(),
                R("Total", rows.Count, rows.Count(x => x.Status == SurgeryStatus.Completed), rows.Count(x => x.Priority == SurgeryPriority.Emergency),
                    rows.Count(x => x.Status is SurgeryStatus.Cancelled or SurgeryStatus.Postponed), rows.Sum(Mins)));

        switch (key)
        {
            case "daily":
                return new[] { new OtReport(key, def.Title, new[] { "Surgery", "Planned", "Patient", "Procedure", "Theatre", "Surgeon", "Anesthesia", "Priority", "Status", "Actual start–end" },
                    rows.Select(r => R(r.Number, L(r.PlannedStartUtc), $"{r.PatientName} ({r.PatientNumber})", r.Procedure, r.Theatre, r.Surgeon, r.Anesthesia, r.Priority, r.Status,
                        r.ActualStartUtc is null ? "—" : $"{L(r.ActualStartUtc)} – {L(r.ActualEndUtc)}")).ToList(), R("Total", rows.Count)) };
            case "monthly":
                return new[] { Group("Month", r => TimeZoneInfo.ConvertTimeFromUtc(r.PlannedStartUtc, zone).ToString("yyyy-MM", CultureInfo.InvariantCulture)) };
            case "surgeon": return new[] { Group("Surgeon", r => r.Surgeon ?? "(not assigned)") };
            case "department": return new[] { Group("Department", r => r.Department ?? "(none)") };
            case "procedure": return new[] { Group("Procedure", r => r.Procedure) };
            case "priority": return new[] { Group("Priority", r => r.Priority.ToString()) };
            case "anesthesia": return new[] { Group("Anesthesia type", r => r.Anesthesia?.ToString() ?? "(not recorded)") };
            case "theatre":
            {
                var d = await DashboardAsync(f, ct);
                return new[] { new OtReport(key, def.Title, new[] { "Theatre", "Type", "Surgeries", "Booked minutes", "Actual operating minutes", "Status" },
                    d.Theatres.Select(t => R(t.Name, t.Type, t.Booked, t.BookedMinutes, t.UsedMinutes, t.Available ? (t.Occupied ? "In use" : "Available") : $"Unavailable – {t.UnavailableReason}")).ToList(),
                    R("Booked utilization", $"{d.Utilization}%")) };
            }
            case "cancelled":
            {
                var ids = rows.Where(r => r.Status is SurgeryStatus.Cancelled or SurgeryStatus.Postponed).Select(r => r.Id).ToList();
                var reasons = await _db.Surgeries.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.StatusReason, ct);
                return new[] { new OtReport(key, def.Title, new[] { "Surgery", "Planned", "Patient", "Procedure", "Status", "Reason" },
                    rows.Where(r => ids.Contains(r.Id)).Select(r => R(r.Number, L(r.PlannedStartUtc), r.PatientName, r.Procedure, r.Status, reasons.GetValueOrDefault(r.Id))).ToList(), R("Total", ids.Count)) };
            }
            case "complications":
            {
                var ids = done.Select(r => r.Id).ToList();
                var notes = await _db.OperativeNotes.AsNoTracking().Where(n => ids.Contains(n.SurgeryId) && n.Complications != null).OrderBy(n => n.Version).ToListAsync(ct);
                var latest = notes.GroupBy(n => n.SurgeryId).ToDictionary(g => g.Key, g => g.Last());
                var pacu = await _db.ClinicalRecords.AsNoTracking().Where(r => r.SurgeryId != null && ids.Contains(r.SurgeryId.Value) && r.Kind == RecordKind.PacuObservation && r.Values.Contains("complications"))
                    .Select(r => new { r.SurgeryId, r.Values }).ToListAsync(ct);
                var list = done.Where(r => latest.ContainsKey(r.Id) || pacu.Any(p => p.SurgeryId == r.Id))
                    .Select(r => R(r.Number, L(r.PlannedStartUtc), r.PatientName, r.Procedure, r.Surgeon, latest.TryGetValue(r.Id, out var n) ? n.Complications : "—",
                        string.Join("; ", pacu.Where(p => p.SurgeryId == r.Id).Select(p => IpdQueryService.Parse(p.Values).GetValueOrDefault("complications"))))).ToList();
                return new[] { new OtReport(key, def.Title, new[] { "Surgery", "Date", "Patient", "Procedure", "Surgeon", "Intra-operative (as documented)", "Recovery (as documented)" }, list, R("Total", list.Count)) };
            }
            case "recovery":
            {
                var ids = done.Select(r => r.Id).ToList();
                var rec = await _db.RecoveryEpisodes.AsNoTracking().Where(r => ids.Contains(r.SurgeryId)).ToListAsync(ct);
                return new[] { new OtReport(key, def.Title, new[] { "Surgery", "Patient", "Arrival", "Nurse", "Status", "Transfer / discharge", "Destination", "Decided by" },
                    rec.Select(r => { var s = done.First(x => x.Id == r.SurgeryId); return R(s.Number, s.PatientName, L(r.ArrivalUtc), r.Nurse, r.Status, L(r.TransferUtc), r.Destination, r.DecisionBy); }).ToList(),
                    R("Total", rec.Count)) };
            }
            case "consumption":
            {
                var q = from c in _db.OtConsumptions.AsNoTracking()
                        join s in _db.Surgeries.AsNoTracking() on c.SurgeryId equals s.Id
                        where c.CreatedUtc >= fromUtc && c.CreatedUtc < toUtc && (f.TheatreId == null || s.TheatreId == f.TheatreId)
                        select new { c.MedicineName, c.Kind, c.Quantity };
                var list = await q.ToListAsync(ct);
                return new[] { new OtReport(key, def.Title, new[] { "Medicine / consumable", "Used", "Returned", "Net used" },
                    list.GroupBy(x => x.MedicineName).OrderBy(g => g.Key).Select(g => R(g.Key, g.Where(x => x.Kind == OtConsumptionKind.Use).Sum(x => x.Quantity),
                        g.Where(x => x.Kind == OtConsumptionKind.Return).Sum(x => x.Quantity),
                        g.Sum(x => x.Kind == OtConsumptionKind.Use ? x.Quantity : -x.Quantity))).ToList()) };
            }
            case "stock":
            {
                var locs = await _db.OtTheatres.AsNoTracking().Where(t => t.StockLocationId != null && (f.TheatreId == null || t.Id == f.TheatreId)).Select(t => t.StockLocationId!.Value).Distinct().ToListAsync(ct);
                var today = await _clock.TodayAsync();
                var bal = await _db.StockBalances.AsNoTracking().Include(b => b.Batch).ThenInclude(b => b!.Medicine).Where(b => locs.Contains(b.LocationId) && b.Quantity > 0)
                    .OrderBy(b => b.Batch!.Medicine!.Name).ThenBy(b => b.Batch!.ExpiryDate).ToListAsync(ct);
                var names = await _db.StockLocations.AsNoTracking().Where(l => locs.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name, ct);
                return new[] { new OtReport(key, def.Title, new[] { "OT stock location", "Medicine / consumable", "Batch", "Expiry", "Balance" },
                    bal.Select(b => R(names[b.LocationId], b.Batch!.Medicine!.Name, b.Batch.BatchNumber, b.Batch.ExpiryDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + (b.Batch.ExpiryDate < today ? " (EXPIRED)" : ""), b.Quantity)).ToList()) };
            }
            case "cssd":
            {
                var batches = await _db.CssdBatches.AsNoTracking().Include(b => b.Sets).ThenInclude(s => s.Set).Where(b => b.StartUtc >= fromUtc && b.StartUtc < toUtc).OrderBy(b => b.StartUtc).ToListAsync(ct);
                return new[] { new OtReport(key, def.Title, new[] { "Batch", "Started", "Method", "Steriliser / cycle", "Sets", "Result", "Expiry", "Released by" },
                    batches.Select(b => R(b.Number, L(b.StartUtc), b.Method, $"{b.Steriliser} {b.CycleNumber}", string.Join(", ", b.Sets.Select(s => s.Set!.Code)),
                        b.Status + (b.FailReason is null ? "" : $" – {b.FailReason}"), b.ExpiryDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), b.ReleasedBy)).ToList(),
                    R("Cycles", batches.Count, "Failed", batches.Count(b => b.Status == CssdBatchStatus.Failed))) };
            }
            case "revenue":
            {
                var q = from sc in _db.SurgeryCharges.AsNoTracking()
                        join c in _db.Charges.AsNoTracking() on sc.ChargeId equals c.Id
                        join s in _db.Surgeries.AsNoTracking() on sc.SurgeryId equals s.Id
                        where c.BusinessDate >= f.From && c.BusinessDate <= f.To && (f.TheatreId == null || s.TheatreId == f.TheatreId)
                        select new { sc.Kind, c.NetAmount, c.PaidAmount, c.Status };
                var list = (await q.ToListAsync(ct)).Where(x => x.Status != ChargeStatus.Cancelled).ToList();
                return new[] { new OtReport(key, def.Title, new[] { "Charge type", "Charges", "Amount", "Paid", "Outstanding" },
                    list.GroupBy(x => x.Kind).OrderBy(g => g.Key).Select(g => R(g.Key, g.Count(), N(g.Sum(x => x.NetAmount)), N(g.Sum(x => x.PaidAmount)), N(g.Sum(x => x.NetAmount - x.PaidAmount)))).ToList(),
                    R("Total", list.Count, N(list.Sum(x => x.NetAmount)), N(list.Sum(x => x.PaidAmount)), N(list.Sum(x => x.NetAmount - x.PaidAmount))), Financial: true) };
            }
        }
        return Array.Empty<OtReport>();
    }
}
