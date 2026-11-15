using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Radiology;

public sealed record RadiologyPeriodRow(DateOnly Period, int XRay, int Ultrasound, int Completed, int Cancelled);
public sealed record RadiologyCountRow(string Name, string? Modality, int Booked, int Reported, int Pending, int Cancelled);
public sealed record RadiologyRevenueRow(DateOnly Period, int Receipts, decimal XRay, decimal Ultrasound, decimal Refunded)
{
    public decimal Total => XRay + Ultrasound;
}

public sealed record RadiologyStatistics(
    DateOnly From, DateOnly To, string Grouping, RadiologyModality? Modality,
    IReadOnlyList<RadiologyPeriodRow> Activity, IReadOnlyList<RadiologyCountRow> ByService, IReadOnlyList<RadiologyCountRow> ByDepartment,
    IReadOnlyList<RadiologyCountRow> ByDoctor, IReadOnlyList<RadiologyRevenueRow>? Revenue);

public interface IRadiologyReportService
{
    Task<RadiologyStatistics> StatisticsAsync(DateOnly from, DateOnly to, string grouping, RadiologyModality? modality, bool includeFinance, CancellationToken ct = default);
}

/// <summary>Radiology statistics. Counts come from the examinations; money only from Phase 3 receipts (no separate radiology ledger).</summary>
public sealed class RadiologyReportService : IRadiologyReportService
{
    private readonly HmsDbContext _db;
    public RadiologyReportService(HmsDbContext db) => _db = db;

    private static bool Reported(RadiologyExamStatus s) => s is RadiologyExamStatus.AwaitingVerification or RadiologyExamStatus.Verified or RadiologyExamStatus.Released;
    private static bool Open(RadiologyExamStatus s) => !Reported(s) && s != RadiologyExamStatus.Cancelled;

    public async Task<RadiologyStatistics> StatisticsAsync(DateOnly from, DateOnly to, string grouping, RadiologyModality? modality, bool includeFinance, CancellationToken ct = default)
    {
        var exams = await _db.RadiologyExams.AsNoTracking()
            .Where(e => e.ScheduledDate >= from && e.ScheduledDate <= to && (modality == null || e.Modality == modality))
            .Select(e => new
            {
                e.ScheduledDate, e.Modality, e.ServiceName, e.Status, Department = e.Order!.ReferringDepartment != null ? e.Order.ReferringDepartment.Name : null,
                e.Order.ReferralType, Doctor = e.Order.ReferringDoctor != null ? e.Order.ReferringDoctor.Name : null, e.Order.ExternalDoctorName
            }).ToListAsync(ct);
        DateOnly Key(DateOnly d) => grouping switch { "month" => new DateOnly(d.Year, d.Month, 1), "year" => new DateOnly(d.Year, 1, 1), _ => d };

        var activity = exams.GroupBy(e => Key(e.ScheduledDate)).OrderBy(g => g.Key)
            .Select(g => new RadiologyPeriodRow(g.Key,
                g.Count(e => e.Modality == RadiologyModality.XRay && e.Status != RadiologyExamStatus.Cancelled),
                g.Count(e => e.Modality == RadiologyModality.Ultrasound && e.Status != RadiologyExamStatus.Cancelled),
                g.Count(e => Reported(e.Status)), g.Count(e => e.Status == RadiologyExamStatus.Cancelled))).ToList();

        RadiologyCountRow Count(string name, string? mod, IEnumerable<RadiologyExamStatus> s)
        {
            var list = s.ToList();
            return new RadiologyCountRow(name, mod, list.Count(x => x != RadiologyExamStatus.Cancelled), list.Count(Reported), list.Count(Open), list.Count(x => x == RadiologyExamStatus.Cancelled));
        }

        var byService = exams.GroupBy(e => new { e.ServiceName, e.Modality })
            .Select(g => Count(g.Key.ServiceName, RadiologyCatalogService.Label(g.Key.Modality), g.Select(e => e.Status))).OrderByDescending(c => c.Booked).ToList();
        var byDepartment = exams.GroupBy(e => e.Department ?? "Not recorded")
            .Select(g => Count(g.Key, null, g.Select(e => e.Status))).OrderByDescending(c => c.Booked).ToList();
        var byDoctor = exams.Where(e => e.ReferralType != RadiologyReferralType.None)
            .GroupBy(e => e.ReferralType == RadiologyReferralType.HospitalDoctor ? e.Doctor ?? "—" : (e.ExternalDoctorName ?? "—") + " (external)")
            .Select(g => Count(g.Key, null, g.Select(e => e.Status))).OrderByDescending(c => c.Booked).ToList();

        List<RadiologyRevenueRow>? revenue = null;
        if (includeFinance)
        {
            var lines = await _db.ReceiptLines.AsNoTracking()
                .Where(l => l.Charge!.RadiologyOrderId != null && l.Receipt!.Status == ReceiptStatus.Valid && l.Receipt.BusinessDate >= from && l.Receipt.BusinessDate <= to
                            && (modality == null || (modality == RadiologyModality.XRay ? l.Charge.ServiceKind == ServiceKind.XRay : l.Charge.ServiceKind == ServiceKind.Ultrasound)))
                .Select(l => new { l.Receipt!.BusinessDate, l.ReceiptId, l.Charge!.ServiceKind, l.Amount, l.RefundedAmount }).ToListAsync(ct);
            revenue = lines.GroupBy(l => Key(l.BusinessDate)).OrderBy(g => g.Key)
                .Select(g => new RadiologyRevenueRow(g.Key, g.Select(l => l.ReceiptId).Distinct().Count(),
                    g.Where(l => l.ServiceKind == ServiceKind.XRay).Sum(l => l.Amount - l.RefundedAmount),
                    g.Where(l => l.ServiceKind == ServiceKind.Ultrasound).Sum(l => l.Amount - l.RefundedAmount),
                    g.Sum(l => l.RefundedAmount))).ToList();
        }
        return new RadiologyStatistics(from, to, grouping, modality, activity, byService, byDepartment, byDoctor, revenue);
    }
}
