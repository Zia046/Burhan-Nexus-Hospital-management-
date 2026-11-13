using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Finance;

public sealed class FinanceFilter
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int? DepartmentId { get; set; }
    public int? CounterId { get; set; }
    public int? UserId { get; set; }
    public int? DoctorId { get; set; }
    public int? PaymentMethodId { get; set; }
    /// <summary>valid | cancelled | null</summary>
    public string? Status { get; set; }
}

public sealed record GroupTotal(string Key, string Name, int Count, decimal Amount);
public sealed record DayTotal(DateOnly Date, decimal Amount, int Count);

public sealed record FinanceDashboard(
    FinanceFilter Filter,
    decimal Collected, int ValidReceipts, int CancelledReceipts, decimal CancelledAmount, decimal Refunds, int RefundCount,
    decimal NetCollection, decimal DepositsConfirmed, decimal CashOutstanding, decimal PendingDeposits, int PendingDepositCount, int PendingRefundCount,
    decimal DoctorConsultationCollections, decimal DoctorShareEarned, decimal DoctorsPaid, decimal DoctorsPayableNow, decimal Discounts,
    IReadOnlyList<GroupTotal> ByDepartment, IReadOnlyList<GroupTotal> ByCounter, IReadOnlyList<GroupTotal> ByUser,
    IReadOnlyList<GroupTotal> ByDoctor, IReadOnlyList<GroupTotal> ByMethod, IReadOnlyList<GroupTotal> ByService, IReadOnlyList<DayTotal> ByDay);

/// <summary>One line of the transaction report (receipt line level, so department / doctor filters are exact).</summary>
public sealed record TransactionRow(
    long ReceiptId, string ReceiptNumber, DateTime CreatedUtc, DateOnly BusinessDate, string Status, string? PatientNumber, string? PatientName,
    string Service, string? Department, string? Doctor, string Counter, string Cashier, string Method, string? Reference,
    decimal Fee, decimal Discount, decimal Net, decimal Paid, decimal Balance, decimal Refunded, decimal DoctorShare, string? CancelReason);

public interface IFinanceReportService
{
    Task<FinanceDashboard> DashboardAsync(FinanceFilter f, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionRow>> TransactionsAsync(FinanceFilter f, int take = 5000, CancellationToken ct = default);
}

public sealed class FinanceReportService : IFinanceReportService
{
    private readonly HmsDbContext _db;
    public FinanceReportService(HmsDbContext db) => _db = db;

    private IQueryable<ReceiptLine> Lines(FinanceFilter f, bool validOnly)
    {
        var q = _db.ReceiptLines.AsNoTracking().Where(l => l.Receipt!.BusinessDate >= f.From && l.Receipt.BusinessDate <= f.To);
        if (validOnly) q = q.Where(l => l.Receipt!.Status == ReceiptStatus.Valid);
        if (f.DepartmentId is int d) q = q.Where(l => l.DepartmentId == d);
        if (f.CounterId is int c) q = q.Where(l => l.Receipt!.CounterId == c);
        if (f.UserId is int u) q = q.Where(l => l.Receipt!.CashierUserId == u);
        if (f.DoctorId is int doc) q = q.Where(l => l.DoctorId == doc);
        if (f.PaymentMethodId is int m) q = q.Where(l => l.Receipt!.PaymentMethodId == m);
        return q;
    }

    public async Task<FinanceDashboard> DashboardAsync(FinanceFilter f, CancellationToken ct = default)
    {
        var lines = await Lines(f, validOnly: false).Select(l => new
        {
            l.ReceiptId, l.Receipt!.Status, l.Receipt.BusinessDate, l.Amount, l.Discount, l.RefundedAmount, l.DoctorShare, l.DoctorId, l.DepartmentId,
            l.ServiceName, l.Receipt.CounterId, CounterName = l.Receipt.Counter!.Name, l.Receipt.CashierUserId, l.Receipt.CashierName,
            l.Receipt.PaymentMethodId, Method = l.Receipt.PaymentMethod!.Name, l.DoctorName
        }).ToListAsync(ct);
        var valid = lines.Where(l => l.Status == ReceiptStatus.Valid).ToList();
        var cancelled = lines.Where(l => l.Status == ReceiptStatus.Cancelled).ToList();
        if (f.Status == "cancelled") valid = new();
        if (f.Status == "valid") cancelled = new();
        var departments = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        // Refunds completed in the period on receipts matching the filters.
        var refunds = await _db.Refunds.AsNoTracking().Where(r => r.Status == RefundStatus.Completed && r.BusinessDate >= f.From && r.BusinessDate <= f.To)
            .Where(r => f.CounterId == null || r.Receipt!.CounterId == f.CounterId)
            .Where(r => f.UserId == null || r.Receipt!.CashierUserId == f.UserId)
            .Where(r => f.PaymentMethodId == null || r.Receipt!.PaymentMethodId == f.PaymentMethodId)
            .Where(r => f.DepartmentId == null || r.Receipt!.Lines.Any(l => l.DepartmentId == f.DepartmentId))
            .Where(r => f.DoctorId == null || r.Receipt!.Lines.Any(l => l.DoctorId == f.DoctorId))
            .Select(r => r.Amount).ToListAsync(ct);

        var ledger = _db.CashierLedger.AsNoTracking();
        if (f.UserId is int u) ledger = ledger.Where(e => e.UserId == u);
        if (f.CounterId is int c) ledger = ledger.Where(e => e.CounterId == c);
        var depositsConfirmed = -(await ledger.Where(e => e.EntryType == CashierEntryType.DepositConfirmed && e.BusinessDate >= f.From && e.BusinessDate <= f.To)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0);
        var outstanding = await ledger.Where(e => e.AffectsCashInHand).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var pendingDeposits = await _db.CashDeposits.AsNoTracking().Where(d => d.Status == DepositStatus.Pending)
            .Where(d => f.UserId == null || d.UserId == f.UserId).Select(d => d.Amount).ToListAsync(ct);
        var pendingRefunds = await _db.Refunds.CountAsync(r => r.Status == RefundStatus.Pending, ct);

        var doctorLedger = _db.DoctorLedger.AsNoTracking();
        if (f.DoctorId is int dd) doctorLedger = doctorLedger.Where(e => e.DoctorId == dd);
        var docPeriod = await doctorLedger.Where(e => e.BusinessDate >= f.From && e.BusinessDate <= f.To)
            .GroupBy(e => e.EntryType).Select(g => new { g.Key, Amount = g.Sum(e => e.Amount) }).ToListAsync(ct);
        var docPayable = await doctorLedger.GroupBy(e => e.DoctorId).Select(g => g.Sum(e => e.Amount)).ToListAsync(ct);

        static IReadOnlyList<GroupTotal> Group<T>(IEnumerable<T> rows, Func<T, string> key, Func<T, string> name, Func<T, decimal> amount, Func<T, long> receipt) =>
            rows.GroupBy(key).Select(g => new GroupTotal(g.Key, name(g.First()), g.Select(receipt).Distinct().Count(), g.Sum(amount)))
                .OrderByDescending(g => g.Amount).ToList();

        var net = (decimal x, decimal r) => x - r;
        return new FinanceDashboard(f,
            valid.Sum(l => l.Amount), valid.Select(l => l.ReceiptId).Distinct().Count(),
            cancelled.Select(l => l.ReceiptId).Distinct().Count(), cancelled.Sum(l => l.Amount),
            refunds.Sum(), refunds.Count,
            valid.Sum(l => l.Amount) - refunds.Sum(),
            depositsConfirmed, outstanding, pendingDeposits.Sum(), pendingDeposits.Count, pendingRefunds,
            valid.Where(l => l.DoctorId != null).Sum(l => l.Amount - l.RefundedAmount),
            docPeriod.Where(x => x.Key is DoctorEntryType.ConsultationShare or DoctorEntryType.ShareReversal).Sum(x => x.Amount),
            -docPeriod.Where(x => x.Key == DoctorEntryType.Settlement).Sum(x => x.Amount),
            docPayable.Where(x => x > 0).Sum(),
            valid.Sum(l => l.Discount),
            Group(valid, l => l.DepartmentId?.ToString() ?? "-", l => l.DepartmentId is int di && departments.TryGetValue(di, out var dn) ? dn : "No department", l => net(l.Amount, l.RefundedAmount), l => l.ReceiptId),
            Group(valid, l => l.CounterId.ToString(), l => l.CounterName, l => net(l.Amount, l.RefundedAmount), l => l.ReceiptId),
            Group(valid, l => l.CashierUserId.ToString(), l => l.CashierName, l => net(l.Amount, l.RefundedAmount), l => l.ReceiptId),
            Group(valid.Where(l => l.DoctorId != null), l => l.DoctorId!.Value.ToString(), l => l.DoctorName ?? "—", l => net(l.Amount, l.RefundedAmount), l => l.ReceiptId),
            Group(valid, l => l.PaymentMethodId.ToString(), l => l.Method, l => net(l.Amount, l.RefundedAmount), l => l.ReceiptId),
            Group(valid, l => l.ServiceName, l => l.ServiceName, l => net(l.Amount, l.RefundedAmount), l => l.ReceiptId),
            valid.GroupBy(l => l.BusinessDate).OrderBy(g => g.Key).Select(g => new DayTotal(g.Key, g.Sum(l => l.Amount - l.RefundedAmount), g.Select(l => l.ReceiptId).Distinct().Count())).ToList());
    }

    public async Task<IReadOnlyList<TransactionRow>> TransactionsAsync(FinanceFilter f, int take = 5000, CancellationToken ct = default)
    {
        var q = Lines(f, validOnly: false);
        if (f.Status == "valid") q = q.Where(l => l.Receipt!.Status == ReceiptStatus.Valid);
        if (f.Status == "cancelled") q = q.Where(l => l.Receipt!.Status == ReceiptStatus.Cancelled);
        if (f.Status == "refunded") q = q.Where(l => l.RefundedAmount > 0);
        var departments = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var rows = await q.OrderBy(l => l.ReceiptId).ThenBy(l => l.Id).Take(Math.Clamp(take, 1, 50_000)).Select(l => new
        {
            l.ReceiptId, l.Receipt!.ReceiptNumber, l.Receipt.CreatedUtc, l.Receipt.BusinessDate, l.Receipt.Status,
            PatientNumber = l.Receipt.Patient != null ? l.Receipt.Patient.PatientNumber : null,
            PatientName = l.Receipt.Patient != null ? l.Receipt.Patient.FullName : l.Receipt.PayerName,
            l.ServiceName, l.DepartmentId, l.DoctorName, Counter = l.Receipt.Counter!.Name, l.Receipt.CashierName, Method = l.Receipt.PaymentMethod!.Name,
            l.Receipt.PaymentReference, l.Fee, l.Discount, l.NetAmount, l.Amount, l.BalanceAfter, l.RefundedAmount, l.DoctorShare, l.Receipt.CancelReason
        }).ToListAsync(ct);
        return rows.Select(r => new TransactionRow(r.ReceiptId, r.ReceiptNumber, r.CreatedUtc, r.BusinessDate, r.Status.ToString(), r.PatientNumber, r.PatientName,
            r.ServiceName, r.DepartmentId is int d && departments.TryGetValue(d, out var dn) ? dn : null, r.DoctorName, r.Counter, r.CashierName, r.Method,
            r.PaymentReference, r.Fee, r.Discount, r.NetAmount, r.Amount, r.BalanceAfter, r.RefundedAmount, r.DoctorShare, r.CancelReason)).ToList();
    }
}
