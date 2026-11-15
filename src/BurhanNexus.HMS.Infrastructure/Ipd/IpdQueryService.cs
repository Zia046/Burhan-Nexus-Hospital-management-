using System.Globalization;
using System.Text.Json;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ipd;

public sealed record AdmissionRow(
    long Id, string AdmissionNumber, AdmissionKind Kind, AdmissionStatus Status, DateTime AdmittedUtc, DateTime? DischargedUtc,
    long PatientId, string PatientNumber, string PatientName, string AgeGender, string? Ward, string? Room, string? Bed, string? Department,
    string? Diagnosis, bool NeedsAttention, string? AttentionNote, string? Allergies, LabourStatus? LabourStatus, long? MotherAdmissionId);

public sealed record WardOccupancy(int WardId, string Ward, WardType Type, int Beds, int Occupied, int Available, int Reserved, int Cleaning, int Unavailable, int Patients);

public sealed record IpdDashboard(
    DateOnly From, DateOnly To, int Admitted, int Occupied, int Available, int Reserved, int Cleaning, int Unavailable,
    int AdmittedInPeriod, int DischargedInPeriod, int AwaitingDischarge, int TransferredInPeriod, int NeedsAttention,
    int Gynae, int Labour, int Nicu, IReadOnlyList<WardOccupancy> Wards, decimal? Billed, decimal? Collected);

public sealed record RecordView(ClinicalRecord Record, IReadOnlyDictionary<string, string> Values, long? SupersededBy)
{
    public bool IsSuperseded => SupersededBy is not null;
    public decimal? Number(string key) => Values.TryGetValue(key, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
}

public sealed record BillLine(long ChargeId, string ChargeNumber, DateOnly Date, DateTime CreatedUtc, string Service, ServiceKind Kind, string Source,
    decimal Fee, decimal Discount, decimal Net, decimal Paid, decimal Refunded, ChargeStatus Status)
{
    public decimal Balance => Status == ChargeStatus.Cancelled ? 0 : Net - Paid;
}

public sealed record TimelineItem(DateTime Utc, string Category, string Title, string? Detail, string? Link, string? By);

public sealed record InvestigationRow(DateTime Utc, string Kind, string Number, string Name, string Status, string Link);

public interface IIpdQueryService
{
    Task<IReadOnlyList<int>?> VisibleWardIdsAsync(int? userId, bool allWards, CancellationToken ct = default);
    Task<IpdDashboard> DashboardAsync(DateOnly from, DateOnly to, int? departmentId, int? wardId, IReadOnlyList<int>? wards, bool finance, CancellationToken ct = default);
    Task<IReadOnlyList<AdmissionRow>> AdmissionsAsync(string scope, string? q, int? wardId, AdmissionKind? kind, IReadOnlyList<int>? wards, DateOnly? from = null, DateOnly? to = null, long? patientId = null, int take = 500, CancellationToken ct = default);
    Task<IpdAdmission?> AdmissionAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<IpdBedAllocation>> AllocationsAsync(long admissionId, CancellationToken ct = default);
    Task<IReadOnlyList<IpdTransfer>> TransfersAsync(long admissionId, CancellationToken ct = default);
    Task<IReadOnlyList<IpdEvent>> EventsAsync(long? admissionId, long? pregnancyId, CancellationToken ct = default);
    Task<IReadOnlyList<RecordView>> RecordsAsync(long patientId, long? admissionId, long? pregnancyId, RecordKind[] kinds, DateTime? fromUtc = null, DateTime? toUtc = null, bool includeSuperseded = true, CancellationToken ct = default);
    Task<IReadOnlyList<BillLine>> BillAsync(long admissionId, CancellationToken ct = default);
    Task<IReadOnlyList<InvestigationRow>> InvestigationsAsync(long admissionId, long patientId, CancellationToken ct = default);
    Task<IReadOnlyList<TimelineItem>> PregnancyTimelineAsync(long pregnancyId, CancellationToken ct = default);
    Task<IReadOnlyList<IpdBedAllocation>> BedHistoryAsync(int bedId, CancellationToken ct = default);
}

public sealed class IpdQueryService : IIpdQueryService
{
    private readonly HmsDbContext _db;
    private readonly HospitalClock _clock;

    public IpdQueryService(HmsDbContext db, HospitalClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Wards a user may work in: wards of the user's departments (and wards without a department). Null = all wards.</summary>
    public async Task<IReadOnlyList<int>?> VisibleWardIdsAsync(int? userId, bool allWards, CancellationToken ct = default)
    {
        if (allWards) return null;
        var depts = userId is int uid ? await _db.UserDepartments.Where(d => d.UserId == uid).Select(d => d.DepartmentId).ToListAsync(ct) : new List<int>();
        return await _db.IpdWards.Where(w => w.DepartmentId == null || depts.Contains(w.DepartmentId.Value)).Select(w => w.Id).ToListAsync(ct);
    }

    public async Task<IpdDashboard> DashboardAsync(DateOnly from, DateOnly to, int? departmentId, int? wardId, IReadOnlyList<int>? wards, bool finance, CancellationToken ct = default)
    {
        var beds = _db.IpdBeds.AsNoTracking().Where(b => b.IsActive && b.Ward!.IsActive);
        if (wardId is int w) beds = beds.Where(b => b.WardId == w);
        if (departmentId is int d) beds = beds.Where(b => b.Ward!.DepartmentId == d);
        if (wards is not null) beds = beds.Where(b => wards.Contains(b.WardId));
        var bedRows = await beds.Select(b => new { b.WardId, Ward = b.Ward!.Name, b.Ward.Type, b.Ward.SortOrder, b.Status }).ToListAsync(ct);
        var adm = _db.IpdAdmissions.AsNoTracking().AsQueryable();
        if (wardId is int w2) adm = adm.Where(a => a.CurrentWardId == w2);
        if (departmentId is int d2) adm = adm.Where(a => a.DepartmentId == d2);
        if (wards is not null) adm = adm.Where(a => a.CurrentWardId == null || wards.Contains(a.CurrentWardId.Value));
        var active = adm.Where(a => IpdService.ActiveStatuses.Contains(a.Status));
        var current = await active.Select(a => new { a.CurrentWardId, a.Kind, a.Status, a.NeedsAttention }).ToListAsync(ct);
        var zone = await _clock.ZoneAsync();
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var admittedIn = await adm.CountAsync(a => a.AdmissionDate >= from && a.AdmissionDate <= to && a.Status != AdmissionStatus.Cancelled, ct);
        var dischargedIn = await adm.CountAsync(a => a.DischargeDate >= from && a.DischargeDate <= to, ct);
        var transfers = _db.IpdTransfers.AsNoTracking().Where(t => t.TransferUtc >= fromUtc && t.TransferUtc < toUtc);
        if (wards is not null) transfers = transfers.Where(t => t.ToWardId == null || wards.Contains(t.ToWardId.Value));
        var transferredIn = await transfers.Select(t => t.AdmissionId).Distinct().CountAsync(ct);
        var wardRows = bedRows.GroupBy(b => new { b.WardId, b.Ward, b.Type, b.SortOrder }).OrderBy(g => g.Key.SortOrder).ThenBy(g => g.Key.Ward)
            .Select(g => new WardOccupancy(g.Key.WardId, g.Key.Ward, g.Key.Type, g.Count(), g.Count(b => b.Status == BedStatus.Occupied), g.Count(b => b.Status == BedStatus.Available),
                g.Count(b => b.Status == BedStatus.Reserved), g.Count(b => b.Status == BedStatus.CleaningRequired),
                g.Count(b => b.Status is BedStatus.UnderMaintenance or BedStatus.TemporarilyUnavailable), current.Count(c => c.CurrentWardId == g.Key.WardId))).ToList();
        decimal? billed = null, collected = null;
        if (finance)
        {
            var charges = _db.Charges.AsNoTracking().Where(c => c.IpdAdmissionId != null && c.Status != ChargeStatus.Cancelled && c.BusinessDate >= from && c.BusinessDate <= to);
            billed = await charges.SumAsync(c => (decimal?)c.NetAmount, ct) ?? 0;
            collected = await _db.ReceiptLines.Where(l => l.Charge!.IpdAdmissionId != null && l.Receipt!.Status == ReceiptStatus.Valid && l.Receipt.BusinessDate >= from && l.Receipt.BusinessDate <= to)
                .SumAsync(l => (decimal?)(l.Amount - l.RefundedAmount), ct) ?? 0;
        }
        return new IpdDashboard(from, to, current.Count, bedRows.Count(b => b.Status == BedStatus.Occupied), bedRows.Count(b => b.Status == BedStatus.Available),
            bedRows.Count(b => b.Status == BedStatus.Reserved), bedRows.Count(b => b.Status == BedStatus.CleaningRequired),
            bedRows.Count(b => b.Status is BedStatus.UnderMaintenance or BedStatus.TemporarilyUnavailable),
            admittedIn, dischargedIn, current.Count(c => c.Status == AdmissionStatus.DischargeRequested), transferredIn, current.Count(c => c.NeedsAttention),
            current.Count(c => c.Kind == AdmissionKind.Gynae), current.Count(c => c.Kind == AdmissionKind.Labour), current.Count(c => c.Kind == AdmissionKind.Nicu),
            wardRows, billed, collected);
    }

    public async Task<IReadOnlyList<AdmissionRow>> AdmissionsAsync(string scope, string? q, int? wardId, AdmissionKind? kind, IReadOnlyList<int>? wards,
        DateOnly? from = null, DateOnly? to = null, long? patientId = null, int take = 500, CancellationToken ct = default)
    {
        var x = _db.IpdAdmissions.AsNoTracking().AsQueryable();
        x = scope switch
        {
            "active" => x.Where(a => IpdService.ActiveStatuses.Contains(a.Status)),
            "discharge" => x.Where(a => a.Status == AdmissionStatus.DischargeRequested),
            "attention" => x.Where(a => IpdService.ActiveStatuses.Contains(a.Status) && a.NeedsAttention),
            "discharged" => x.Where(a => a.Status == AdmissionStatus.Discharged || a.Status == AdmissionStatus.Referred),
            "admitted" => x.Where(a => a.Status != AdmissionStatus.Cancelled),
            _ => x,
        };
        if (from is { } f) x = scope is "discharged" ? x.Where(a => a.DischargeDate >= f) : x.Where(a => a.AdmissionDate >= f);
        if (to is { } t) x = scope is "discharged" ? x.Where(a => a.DischargeDate <= t) : x.Where(a => a.AdmissionDate <= t);
        if (wardId is int w) x = x.Where(a => a.CurrentWardId == w);
        if (kind is { } k) x = x.Where(a => a.Kind == k);
        if (patientId is long pid) x = x.Where(a => a.PatientId == pid);
        if (wards is not null) x = x.Where(a => a.CurrentWardId == null || wards.Contains(a.CurrentWardId.Value));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            var like = "%" + s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var digits = new string(s.Where(char.IsDigit).ToArray());
            x = x.Where(a => a.AdmissionNumber == s.ToUpper() || a.Patient!.PatientNumber == s.ToUpper() || EF.Functions.ILike(a.Patient.FullName, like)
                             || (digits.Length >= 4 && a.Patient.MobileNumber != null && a.Patient.MobileNumber.Contains(digits))
                             || (digits.Length >= 5 && a.Patient.CnicDigits != null && a.Patient.CnicDigits.Contains(digits)));
        }
        var rows = await x.OrderByDescending(a => a.NeedsAttention).ThenByDescending(a => a.AdmittedUtc).Take(take).Select(a => new
        {
            a.Id, a.AdmissionNumber, a.Kind, a.Status, a.AdmittedUtc, a.DischargedUtc, a.PatientId, a.Patient!.PatientNumber, a.Patient.FullName, a.Patient.Gender, a.Patient.DateOfBirth,
            Ward = a.CurrentWard != null ? a.CurrentWard.Name : null, Room = a.CurrentBed != null ? a.CurrentBed.Room!.Name : null, Bed = a.CurrentBed != null ? a.CurrentBed.Code : null,
            Department = a.Department != null ? a.Department.Name : null, a.AdmissionDiagnosis, a.NeedsAttention, a.AttentionNote, a.Allergies, a.MotherAdmissionId,
            Labour = _db.LabourRecords.Where(l => l.AdmissionId == a.Id).Select(l => (LabourStatus?)l.Status).FirstOrDefault()
        }).ToListAsync(ct);
        var today = await _clock.TodayAsync();
        return rows.Select(r =>
        {
            var age = r.DateOfBirth is { } dob ? AgeCalculator.FromDateOfBirth(dob, today).Short : "-";
            var g = r.Gender switch { Gender.Male => "M", Gender.Female => "F", Gender.Other => "O", _ => "-" };
            return new AdmissionRow(r.Id, r.AdmissionNumber, r.Kind, r.Status, r.AdmittedUtc, r.DischargedUtc, r.PatientId, r.PatientNumber, r.FullName, $"{age} / {g}",
                r.Ward, r.Room, r.Bed, r.Department, r.AdmissionDiagnosis, r.NeedsAttention, r.AttentionNote, r.Allergies, r.Labour, r.MotherAdmissionId);
        }).ToList();
    }

    public async Task<IpdAdmission?> AdmissionAsync(long id, CancellationToken ct = default) =>
        await _db.IpdAdmissions.AsNoTracking().Include(a => a.Patient).Include(a => a.Department).Include(a => a.AdmittingDoctor).Include(a => a.SourceEncounter)
            .Include(a => a.CurrentWard).Include(a => a.CurrentBed).ThenInclude(b => b!.Room).AsSplitQuery().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<IpdBedAllocation>> AllocationsAsync(long admissionId, CancellationToken ct = default) =>
        await _db.IpdBedAllocations.AsNoTracking().Include(x => x.Bed).ThenInclude(b => b!.Room).Include(x => x.Bed).ThenInclude(b => b!.Ward)
            .Where(x => x.AdmissionId == admissionId).OrderBy(x => x.FromUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<IpdBedAllocation>> BedHistoryAsync(int bedId, CancellationToken ct = default) =>
        await _db.IpdBedAllocations.AsNoTracking().Where(x => x.BedId == bedId).OrderByDescending(x => x.FromUtc).Take(200).ToListAsync(ct);

    public async Task<IReadOnlyList<IpdTransfer>> TransfersAsync(long admissionId, CancellationToken ct = default) =>
        await _db.IpdTransfers.AsNoTracking().Where(t => t.AdmissionId == admissionId).OrderBy(t => t.TransferUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<IpdEvent>> EventsAsync(long? admissionId, long? pregnancyId, CancellationToken ct = default) =>
        await _db.IpdEvents.AsNoTracking().Where(e => (admissionId != null && e.AdmissionId == admissionId) || (pregnancyId != null && e.PregnancyId == pregnancyId))
            .OrderByDescending(e => e.Id).Take(500).ToListAsync(ct);

    public static IReadOnlyDictionary<string, string> Parse(string json)
    {
        var d = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        foreach (var p in doc.RootElement.EnumerateObject())
            d[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetDecimal().ToString(CultureInfo.InvariantCulture) : p.Value.ToString();
        return d;
    }

    public async Task<IReadOnlyList<RecordView>> RecordsAsync(long patientId, long? admissionId, long? pregnancyId, RecordKind[] kinds, DateTime? fromUtc = null, DateTime? toUtc = null,
        bool includeSuperseded = true, CancellationToken ct = default)
    {
        var x = _db.ClinicalRecords.AsNoTracking().Where(r => r.PatientId == patientId && kinds.Contains(r.Kind));
        if (admissionId is long a) x = x.Where(r => r.AdmissionId == a);
        if (pregnancyId is long p) x = x.Where(r => r.PregnancyId == p);
        if (fromUtc is DateTime f) x = x.Where(r => r.RecordedUtc >= f);
        if (toUtc is DateTime t) x = x.Where(r => r.RecordedUtc < t);
        var list = await x.OrderBy(r => r.RecordedUtc).ThenBy(r => r.Id).Take(2000).ToListAsync(ct);
        var ids = list.Select(r => r.Id).ToList();
        var corrections = await _db.ClinicalRecords.AsNoTracking().Where(r => r.CorrectsRecordId != null && ids.Contains(r.CorrectsRecordId.Value))
            .Select(r => new { Original = r.CorrectsRecordId!.Value, r.Id }).ToDictionaryAsync(r => r.Original, r => r.Id, ct);
        return list.Select(r => new RecordView(r, Parse(r.Values), corrections.TryGetValue(r.Id, out var by) ? by : null))
            .Where(v => includeSuperseded || !v.IsSuperseded).ToList();
    }

    public async Task<IReadOnlyList<BillLine>> BillAsync(long admissionId, CancellationToken ct = default) =>
        (await _db.Charges.AsNoTracking().Where(c => c.IpdAdmissionId == admissionId).OrderBy(c => c.CreatedUtc).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.ChargeNumber, c.BusinessDate, c.CreatedUtc, c.ServiceName, c.ServiceKind, c.LabOrderId, c.RadiologyOrderId, c.PharmacyIssueId,
                c.Fee, c.Discount, c.NetAmount, c.PaidAmount, c.RefundedAmount, c.Status, Bed = _db.IpdBedCharges.Any(b => b.ChargeId == c.Id) })
            .ToListAsync(ct))
        .Select(c => new BillLine(c.Id, c.ChargeNumber, c.BusinessDate, c.CreatedUtc, c.ServiceName, c.ServiceKind,
            c.Bed ? "Bed" : c.LabOrderId != null ? "Laboratory" : c.RadiologyOrderId != null ? "Radiology" : c.PharmacyIssueId != null ? "Medicines" : "Service",
            c.Fee, c.Discount, c.NetAmount, c.PaidAmount, c.RefundedAmount, c.Status)).ToList();

    public async Task<IReadOnlyList<InvestigationRow>> InvestigationsAsync(long admissionId, long patientId, CancellationToken ct = default)
    {
        var lab = await _db.LabOrders.AsNoTracking().Where(o => o.IpdAdmissionId == admissionId)
            .Select(o => new { o.Id, o.OrderNumber, o.BookedUtc, Items = o.Items.Select(i => new { i.TestName, i.Status }).ToList() }).ToListAsync(ct);
        var rad = await _db.RadiologyExams.AsNoTracking().Where(e => e.Order!.IpdAdmissionId == admissionId)
            .Select(e => new { e.Id, e.Order!.OrderNumber, e.Order.BookedUtc, e.ServiceName, e.Status }).ToListAsync(ct);
        return lab.SelectMany(o => o.Items.Select(i => new InvestigationRow(o.BookedUtc, "Laboratory", o.OrderNumber, i.TestName, Laboratory.LabService.Label(i.Status), $"/Lab/Order/{o.Id}")))
            .Concat(rad.Select(e => new InvestigationRow(e.BookedUtc, "Radiology", e.OrderNumber, e.ServiceName, Radiology.RadiologyWorkflowService.Label(e.Status), $"/Radiology/Exam/{e.Id}")))
            .OrderByDescending(r => r.Utc).ToList();
    }

    /// <summary>Everything that happened during one pregnancy, in time order (registration, ANC, ultrasound / lab, admissions, labour, delivery, postnatal).</summary>
    public async Task<IReadOnlyList<TimelineItem>> PregnancyTimelineAsync(long pregnancyId, CancellationToken ct = default)
    {
        var g = await _db.Pregnancies.AsNoTracking().FirstAsync(p => p.Id == pregnancyId, ct);
        var items = new List<TimelineItem>();
        var start = g.CreatedUtc.AddDays(-1);
        var end = g.Status == PregnancyStatus.Active ? DateTime.UtcNow.AddDays(1) : g.UpdatedUtc.AddDays(60);
        foreach (var e in await EventsAsync(null, pregnancyId, ct)) items.Add(new TimelineItem(e.CreatedUtc, e.Category, e.Event + (e.ToValue is null ? "" : $": {e.ToValue}"), e.Note, null, e.Username));
        foreach (var r in await RecordsAsync(g.PatientId, null, pregnancyId, new[] { RecordKind.AncVisit }, includeSuperseded: false, ct: ct))
            items.Add(new TimelineItem(r.Record.RecordedUtc, "ANC", "ANC visit" + (r.Values.TryGetValue("ga", out var ga) ? $" ({ga})" : ""),
                string.Join(" · ", new[] { "bp_sys", "fhr", "assessment" }.Where(k => r.Values.ContainsKey(k)).Select(k => $"{k}: {r.Values[k]}")), null, r.Record.Username));
        var admissions = await _db.IpdAdmissions.AsNoTracking().Where(a => a.PregnancyId == pregnancyId || (a.PatientId == g.PatientId && a.AdmittedUtc >= start && a.AdmittedUtc <= end)).ToListAsync(ct);
        foreach (var a in admissions)
        {
            items.Add(new TimelineItem(a.AdmittedUtc, "Admission", $"{a.Kind} admission {a.AdmissionNumber}", a.AdmissionDiagnosis, $"/Ipd/Admission/{a.Id}", a.CreatedByUsername));
            foreach (var r in await RecordsAsync(g.PatientId, a.Id, null, new[] { RecordKind.PostnatalAssessment }, includeSuperseded: false, ct: ct))
                items.Add(new TimelineItem(r.Record.RecordedUtc, "Postnatal", "Postnatal assessment", r.Record.Notes, $"/Ipd/Admission/{a.Id}?tab=postnatal", r.Record.Username));
        }
        foreach (var d in await _db.DeliveryRecords.AsNoTracking().Include(d => d.Newborns).ThenInclude(n => n.NewbornPatient).Where(d => d.PregnancyId == pregnancyId).ToListAsync(ct))
            items.Add(new TimelineItem(d.DeliveredUtc, "Delivery", $"{MaternityService.Label(d.Type)} – {d.Number}",
                string.Join(", ", d.Newborns.Select(n => $"{n.Sex} {n.BirthWeightGrams} g{(n.NewbornPatient is null ? "" : " " + n.NewbornPatient.PatientNumber)}")), $"/Ipd/Delivery/{d.Id}", d.CreatedBy));
        foreach (var e in await _db.RadiologyExams.AsNoTracking().Where(e => e.Order!.PatientId == g.PatientId && e.Modality == RadiologyModality.Ultrasound && e.ScheduledUtc >= start && e.ScheduledUtc <= end)
                     .Select(e => new { e.Id, e.ScheduledUtc, e.ServiceName, e.Status, e.Impression }).ToListAsync(ct))
            items.Add(new TimelineItem(e.ScheduledUtc, "Ultrasound", e.ServiceName, (e.Status is RadiologyExamStatus.Verified or RadiologyExamStatus.Released ? e.Impression : Radiology.RadiologyWorkflowService.Label(e.Status)), $"/Radiology/Exam/{e.Id}", null));
        foreach (var o in await _db.LabOrders.AsNoTracking().Where(o => o.PatientId == g.PatientId && o.BookedUtc >= start && o.BookedUtc <= end)
                     .Select(o => new { o.Id, o.BookedUtc, o.OrderNumber, Tests = o.Items.Select(i => i.TestName).ToList() }).ToListAsync(ct))
            items.Add(new TimelineItem(o.BookedUtc, "Laboratory", o.OrderNumber, string.Join(", ", o.Tests), $"/Lab/Order/{o.Id}", null));
        return items.OrderBy(i => i.Utc).ToList();
    }
}
