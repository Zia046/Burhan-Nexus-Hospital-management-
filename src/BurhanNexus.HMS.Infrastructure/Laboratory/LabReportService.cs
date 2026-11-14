using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Laboratory;

public sealed record LabCount(string Name, int Booked, int Completed, int Pending, int Cancelled);
public sealed record LabDay(DateOnly Date, int Orders, int Booked, int Completed, int Cancelled);
public sealed record LabListRow(DateOnly Date, string OrderNumber, long OrderId, string PatientNumber, string PatientName, string Test, string Detail, string? By);
public sealed record LabVerifierRow(string User, int Verified, int SentBack, int Withdrawn);
public sealed record LabCollectionRow(DateOnly Date, int Receipts, decimal Amount, decimal Refunded);

public sealed record LabStatistics(
    DateOnly From, DateOnly To, string Grouping,
    IReadOnlyList<LabDay> Activity, IReadOnlyList<LabCount> ByTest, IReadOnlyList<LabCount> ByCategory, IReadOnlyList<LabCount> ByDoctor,
    IReadOnlyList<LabListRow> Cancelled, IReadOnlyList<LabListRow> SampleRejections, IReadOnlyList<LabVerifierRow> Verification,
    IReadOnlyList<LabCollectionRow>? Collection);

public interface ILabReportService
{
    Task<LabStatistics> StatisticsAsync(DateOnly from, DateOnly to, string grouping, bool includeFinance, CancellationToken ct = default);
}

public sealed class LabReportService : ILabReportService
{
    private readonly HmsDbContext _db;
    public LabReportService(HmsDbContext db) => _db = db;

    private static bool Done(LabOrderItemStatus s) => s is LabOrderItemStatus.AwaitingVerification or LabOrderItemStatus.Verified or LabOrderItemStatus.Released;
    private static bool Open(LabOrderItemStatus s) => LabService.OpenStatuses.Contains(s);

    public async Task<LabStatistics> StatisticsAsync(DateOnly from, DateOnly to, string grouping, bool includeFinance, CancellationToken ct = default)
    {
        var items = await _db.LabOrderItems.AsNoTracking().Where(i => i.Order!.BusinessDate >= from && i.Order.BusinessDate <= to)
            .Select(i => new
            {
                i.Id, i.LabOrderId, i.Order!.OrderNumber, i.Order.BusinessDate, i.TestName, i.CategoryName, i.Status, i.CancelReason, i.CancelledByUsername,
                i.Order.Patient!.PatientNumber, PatientName = i.Order.Patient.FullName, i.Order.ReferralType,
                Doctor = i.Order.ReferringDoctor != null ? i.Order.ReferringDoctor.Name : null, i.Order.ExternalDoctorName
            }).ToListAsync(ct);
        DateOnly Key(DateOnly d) => grouping switch { "month" => new DateOnly(d.Year, d.Month, 1), "year" => new DateOnly(d.Year, 1, 1), _ => d };

        var activity = items.GroupBy(i => Key(i.BusinessDate)).OrderBy(g => g.Key)
            .Select(g => new LabDay(g.Key, g.Select(i => i.LabOrderId).Distinct().Count(), g.Count(i => i.Status != LabOrderItemStatus.Cancelled),
                g.Count(i => Done(i.Status)), g.Count(i => i.Status == LabOrderItemStatus.Cancelled))).ToList();

        var byTest = items.GroupBy(i => i.TestName).Select(g => new LabCount(g.Key, g.Count(i => i.Status != LabOrderItemStatus.Cancelled), g.Count(i => Done(i.Status)),
            g.Count(i => Open(i.Status)), g.Count(i => i.Status == LabOrderItemStatus.Cancelled))).OrderByDescending(c => c.Booked).ToList();
        var byCategory = items.GroupBy(i => i.CategoryName).Select(g => new LabCount(g.Key, g.Count(i => i.Status != LabOrderItemStatus.Cancelled), g.Count(i => Done(i.Status)),
            g.Count(i => Open(i.Status)), g.Count(i => i.Status == LabOrderItemStatus.Cancelled))).OrderByDescending(c => c.Booked).ToList();
        var byDoctor = items.Where(i => i.ReferralType != LabReferralType.None)
            .GroupBy(i => i.ReferralType == LabReferralType.HospitalDoctor ? i.Doctor ?? "—" : (i.ExternalDoctorName ?? "—") + " (external)")
            .Select(g => new LabCount(g.Key, g.Count(i => i.Status != LabOrderItemStatus.Cancelled), g.Count(i => Done(i.Status)),
                g.Count(i => Open(i.Status)), g.Count(i => i.Status == LabOrderItemStatus.Cancelled))).OrderByDescending(c => c.Booked).ToList();
        var cancelled = items.Where(i => i.Status == LabOrderItemStatus.Cancelled)
            .Select(i => new LabListRow(i.BusinessDate, i.OrderNumber, i.LabOrderId, i.PatientNumber, i.PatientName, i.TestName, i.CancelReason ?? "", i.CancelledByUsername)).ToList();

        var rejections = await _db.LabSamples.AsNoTracking().Where(s => s.Status == LabSampleStatus.Rejected && s.RejectedUtc != null && s.BusinessDate >= from && s.BusinessDate <= to)
            .Select(s => new { s.BusinessDate, s.Order!.OrderNumber, s.LabOrderId, s.Order.Patient!.PatientNumber, s.Order.Patient.FullName, s.SampleNumber, s.SampleTypeName, s.RejectReason, s.RejectedByUsername })
            .ToListAsync(ct);
        var events = await _db.LabItemEvents.AsNoTracking()
            .Where(e => e.BusinessDate >= from && e.BusinessDate <= to
                        && (e.EventType == LabEventType.Verified || e.EventType == LabEventType.VerificationRejected || e.EventType == LabEventType.ResultWithdrawn))
            .Select(e => new { e.EventType, e.Username }).ToListAsync(ct);
        var verifiers = events.GroupBy(e => e.Username ?? "—")
            .Select(g => new LabVerifierRow(g.Key, g.Count(e => e.EventType == LabEventType.Verified), g.Count(e => e.EventType == LabEventType.VerificationRejected),
                g.Count(e => e.EventType == LabEventType.ResultWithdrawn))).OrderByDescending(v => v.Verified).ToList();

        List<LabCollectionRow>? collection = null;
        if (includeFinance)
        {
            // Laboratory collection straight from the Phase 3 receipts (no separate laboratory ledger).
            var lines = await _db.ReceiptLines.AsNoTracking()
                .Where(l => l.Charge!.LabOrderId != null && l.Receipt!.Status == ReceiptStatus.Valid && l.Receipt.BusinessDate >= from && l.Receipt.BusinessDate <= to)
                .Select(l => new { l.Receipt!.BusinessDate, l.ReceiptId, l.Amount, l.RefundedAmount }).ToListAsync(ct);
            collection = lines.GroupBy(l => Key(l.BusinessDate)).OrderBy(g => g.Key)
                .Select(g => new LabCollectionRow(g.Key, g.Select(l => l.ReceiptId).Distinct().Count(), g.Sum(l => l.Amount), g.Sum(l => l.RefundedAmount))).ToList();
        }
        return new LabStatistics(from, to, grouping, activity, byTest, byCategory, byDoctor, cancelled,
            rejections.Select(r => new LabListRow(r.BusinessDate, r.OrderNumber, r.LabOrderId, r.PatientNumber, r.FullName, $"{r.SampleNumber} ({r.SampleTypeName})", r.RejectReason ?? "", r.RejectedByUsername)).ToList(),
            verifiers, collection);
    }
}
