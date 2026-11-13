using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Finance;

public sealed record MethodTotal(string Method, bool IsCash, int Count, decimal Amount);

/// <summary>One cashier's account for a period. All figures come from ledger / receipt rows.</summary>
public sealed record CashierAccount(
    int UserId, string Username, string FullName,
    DateOnly From, DateOnly To,
    decimal OpeningCashBalance,
    decimal GrossCollections, decimal CashCollections, decimal NonCashCollections,
    int ReceiptCount, int CancelledCount, decimal CancelledAmount, decimal Refunds, decimal CashRefunds,
    decimal DepositsSubmitted, decimal DepositsConfirmed, decimal DepositsPending, decimal Adjustments,
    decimal ClosingCashBalance,
    decimal OutstandingNow, decimal PendingDepositsNow, decimal TodayCollection,
    IReadOnlyList<MethodTotal> ByMethod)
{
    /// <summary>Valid collections in the period = collections − cancellations − refunds.</summary>
    public decimal ValidCollections => GrossCollections - CancelledAmount - Refunds;
}

public sealed record CashierBalanceRow(int UserId, string Username, string FullName, decimal Outstanding, decimal PendingDeposits, decimal TodayCollection, string Counters);

public sealed record ClosingFigures(
    ClosingScope Scope, string ScopeName, DateOnly Date, decimal Opening, decimal Cash, decimal NonCash, int Receipts, int Cancelled, decimal CancelledAmount,
    decimal Refunds, decimal DepositsSubmitted, decimal DepositsConfirmed, decimal Adjustments, decimal Closing);

public interface ICashAccountService
{
    Task<CashierAccount> AccountAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<CashierLedgerEntry>> LedgerAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IReadOnlyList<CashierBalanceRow>> BalancesAsync(CancellationToken ct = default);
    Task<decimal> OutstandingAsync(int userId, CancellationToken ct = default);

    Task<ServiceResult<CashDeposit>> SubmitDepositAsync(decimal amount, string? notes, CancellationToken ct = default);
    Task<ServiceResult> ConfirmDepositAsync(long depositId, decimal? receivedAmount, string? remarks, CancellationToken ct = default);
    Task<ServiceResult> RejectDepositAsync(long depositId, string? remarks, CancellationToken ct = default);
    Task<IReadOnlyList<CashDeposit>> DepositsAsync(int? userId, DepositStatus? status, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<ServiceResult<FinancialAdjustment>> AdjustAsync(AdjustmentTarget target, int id, decimal amount, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<FinancialAdjustment>> AdjustmentsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<ClosingFigures> ClosingFiguresAsync(ClosingScope scope, int? id, DateOnly date, CancellationToken ct = default);
    Task<ServiceResult<DailyCashClosing>> CloseDayAsync(ClosingScope scope, int? id, DateOnly date, string? notes, CancellationToken ct = default);
    Task<IReadOnlyList<DailyCashClosing>> ClosingsAsync(ClosingScope? scope, int? userId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

public sealed class CashAccountService : ICashAccountService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    public CashAccountService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    private static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    // ---------------- balances ----------------

    public async Task<decimal> OutstandingAsync(int userId, CancellationToken ct = default) =>
        await _db.CashierLedger.Where(e => e.UserId == userId && e.AffectsCashInHand).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

    public async Task<CashierAccount> AccountAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var today = await _clock.TodayAsync();
        var opening = await _db.CashierLedger.Where(e => e.UserId == userId && e.AffectsCashInHand && e.BusinessDate < from).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var entries = await _db.CashierLedger.AsNoTracking().Where(e => e.UserId == userId && e.BusinessDate >= from && e.BusinessDate <= to).ToListAsync(ct);
        var methods = await _db.PaymentMethods.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
        var collections = entries.Where(e => e.EntryType == CashierEntryType.Collection).ToList();
        var cancels = entries.Where(e => e.EntryType == CashierEntryType.ReceiptCancellation).ToList();
        var refunds = entries.Where(e => e.EntryType == CashierEntryType.Refund).ToList();
        var deposits = await _db.CashDeposits.AsNoTracking().Where(d => d.UserId == userId).ToListAsync(ct);
        var receipts = await _db.Receipts.AsNoTracking().Where(r => r.CashierUserId == userId && r.BusinessDate >= from && r.BusinessDate <= to)
            .Select(r => new { r.Status, r.AmountPaid }).ToListAsync(ct);
        var byMethod = collections.GroupBy(e => e.PaymentMethodId ?? 0)
            .Select(g => new MethodTotal(methods.TryGetValue(g.Key, out var m) ? m.Name : "—", g.First().AffectsCashInHand, g.Count(),
                g.Sum(e => e.Amount) + cancels.Where(c => c.PaymentMethodId == g.Key).Sum(c => c.Amount)))
            .OrderBy(m => m.Method).ToList();
        var outstanding = await OutstandingAsync(userId, ct);
        var todayCollection = await _db.CashierLedger.Where(e => e.UserId == userId && e.BusinessDate == today
                                  && (e.EntryType == CashierEntryType.Collection || e.EntryType == CashierEntryType.ReceiptCancellation || e.EntryType == CashierEntryType.Refund))
                              .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        return new CashierAccount(
            userId, user.Username, user.FullName, from, to, opening,
            collections.Sum(e => e.Amount),
            collections.Where(e => e.AffectsCashInHand).Sum(e => e.Amount),
            collections.Where(e => !e.AffectsCashInHand).Sum(e => e.Amount),
            receipts.Count, receipts.Count(r => r.Status == ReceiptStatus.Cancelled), -cancels.Sum(e => e.Amount),
            -refunds.Sum(e => e.Amount), -refunds.Where(e => e.AffectsCashInHand).Sum(e => e.Amount),
            deposits.Where(d => d.BusinessDate >= from && d.BusinessDate <= to).Sum(d => d.Amount),
            -entries.Where(e => e.EntryType == CashierEntryType.DepositConfirmed).Sum(e => e.Amount),
            deposits.Where(d => d.Status == DepositStatus.Pending && d.BusinessDate >= from && d.BusinessDate <= to).Sum(d => d.Amount),
            entries.Where(e => e.EntryType == CashierEntryType.Adjustment).Sum(e => e.Amount),
            opening + entries.Where(e => e.AffectsCashInHand).Sum(e => e.Amount),
            outstanding, deposits.Where(d => d.Status == DepositStatus.Pending).Sum(d => d.Amount), todayCollection, byMethod);
    }

    public async Task<IReadOnlyList<CashierLedgerEntry>> LedgerAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await _db.CashierLedger.AsNoTracking().Where(e => e.UserId == userId && e.BusinessDate >= from && e.BusinessDate <= to)
            .OrderBy(e => e.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<CashierBalanceRow>> BalancesAsync(CancellationToken ct = default)
    {
        var today = await _clock.TodayAsync();
        var ledger = await _db.CashierLedger.GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Outstanding = g.Where(e => e.AffectsCashInHand).Sum(e => e.Amount),
                Today = g.Where(e => e.BusinessDate == today && (e.EntryType == CashierEntryType.Collection || e.EntryType == CashierEntryType.ReceiptCancellation || e.EntryType == CashierEntryType.Refund)).Sum(e => e.Amount)
            }).ToListAsync(ct);
        var pending = await _db.CashDeposits.Where(d => d.Status == DepositStatus.Pending).GroupBy(d => d.UserId)
            .Select(g => new { g.Key, Amount = g.Sum(d => d.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Amount, ct);
        var assigned = await _db.CounterAssignments.AsNoTracking().Where(a => a.EndedUtc == null).Select(a => new { a.UserId, a.Counter!.Name }).ToListAsync(ct);
        var userIds = ledger.Select(l => l.UserId).Union(assigned.Select(a => a.UserId)).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        return userIds.Select(id =>
        {
            var l = ledger.FirstOrDefault(x => x.UserId == id);
            var u = users[id];
            return new CashierBalanceRow(id, u.Username, u.FullName, l?.Outstanding ?? 0, pending.GetValueOrDefault(id), l?.Today ?? 0,
                string.Join(", ", assigned.Where(a => a.UserId == id).Select(a => a.Name).OrderBy(n => n)));
        }).OrderBy(r => r.FullName).ToList();
    }

    // ---------------- deposits ----------------

    public async Task<ServiceResult<CashDeposit>> SubmitDepositAsync(decimal amount, string? notes, CancellationToken ct = default)
    {
        if (_ctx.UserId is not int userId) return ServiceResult<CashDeposit>.Fail("Please sign in again.");
        if (amount <= 0 || !FinanceMath.IsMoney(amount)) return ServiceResult<CashDeposit>.Fail("Enter a valid deposit amount.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"CASHIER|{userId}", ct);
        var outstanding = await OutstandingAsync(userId, ct);
        var pending = await _db.CashDeposits.Where(d => d.UserId == userId && d.Status == DepositStatus.Pending).SumAsync(d => (decimal?)d.Amount, ct) ?? 0;
        var available = outstanding - pending;
        if (amount > available)
            return ServiceResult<CashDeposit>.Fail(available <= 0
                ? "You have no outstanding cash to deposit (pending deposits are already waiting for confirmation)."
                : $"You can deposit at most {available:N2} (outstanding {outstanding:N2}, already pending {pending:N2}).");
        var today = await _clock.TodayAsync();
        var counterId = await _db.CounterAssignments.Where(a => a.UserId == userId && a.EndedUtc == null).OrderBy(a => a.Id).Select(a => (int?)a.CounterId).FirstOrDefaultAsync(ct);
        var d = new CashDeposit
        {
            DepositNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.DepositNumberSequence, "DEP", today, ct),
            UserId = userId, Username = _ctx.Username ?? "", CounterId = counterId, Amount = amount, Notes = T(notes, 300),
            Status = DepositStatus.Pending, SubmittedUtc = _clock.UtcNow, BusinessDate = today
        };
        _db.CashDeposits.Add(d);
        _audit.Add(AuditActions.DepositSubmitted, "CashDeposit", d.DepositNumber, $"Deposit {d.DepositNumber} of {amount:N2} submitted by {d.Username} (outstanding {outstanding:N2})");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<CashDeposit>.Ok(d);
    }

    public async Task<ServiceResult> ConfirmDepositAsync(long depositId, decimal? receivedAmount, string? remarks, CancellationToken ct = default)
    {
        remarks = T(remarks, 300);
        if (remarks is null) return ServiceResult.Fail("Enter remarks for the confirmation (e.g. 'cash counted and received').");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var d = await _db.CashDeposits.FirstOrDefaultAsync(x => x.Id == depositId, ct);
        if (d is null) return ServiceResult.Fail("Deposit not found.");
        if (d.UserId == _ctx.UserId) return ServiceResult.Fail("You cannot confirm your own deposit. Another authorised person must confirm it.");
        await FinanceDb.LockAsync(_db, $"CASHIER|{d.UserId}", ct);
        await _db.Entry(d).ReloadAsync(ct);
        if (d.Status != DepositStatus.Pending) return ServiceResult.Fail($"Deposit {d.DepositNumber} was already {d.Status.ToString().ToLowerInvariant()}.");
        var received = receivedAmount ?? d.Amount;
        if (received <= 0 || received > d.Amount || !FinanceMath.IsMoney(received))
            return ServiceResult.Fail($"Received amount must be more than 0 and at most {d.Amount:N2}. If no cash was received, reject the deposit.");
        var now = _clock.UtcNow;
        var today = await _clock.TodayAsync();
        d.Status = DepositStatus.Confirmed;
        d.ReceivedAmount = received;
        d.ReviewedUtc = now;
        d.ReviewedByUserId = _ctx.UserId;
        d.ReviewedByUsername = _ctx.Username;
        d.ReviewRemarks = remarks;
        _db.CashierLedger.Add(new CashierLedgerEntry
        {
            UserId = d.UserId, CounterId = d.CounterId, EntryType = CashierEntryType.DepositConfirmed, Amount = -received, AffectsCashInHand = true,
            DepositId = d.Id, Reference = d.DepositNumber, Description = $"Deposit {d.DepositNumber} received by {_ctx.Username}" + (received < d.Amount ? $" (declared {d.Amount:N2})" : ""),
            BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        });
        _audit.Add(AuditActions.DepositConfirmed, "CashDeposit", d.DepositNumber,
            $"Deposit {d.DepositNumber} of {d.Username}: {received:N2} received (declared {d.Amount:N2}). {remarks}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The deposit was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RejectDepositAsync(long depositId, string? remarks, CancellationToken ct = default)
    {
        remarks = T(remarks, 300);
        if (remarks is null) return ServiceResult.Fail("Enter the reason for rejecting the deposit.");
        var d = await _db.CashDeposits.FirstOrDefaultAsync(x => x.Id == depositId, ct);
        if (d is null) return ServiceResult.Fail("Deposit not found.");
        if (d.UserId == _ctx.UserId) return ServiceResult.Fail("You cannot review your own deposit.");
        if (d.Status != DepositStatus.Pending) return ServiceResult.Fail($"Deposit {d.DepositNumber} was already {d.Status.ToString().ToLowerInvariant()}.");
        d.Status = DepositStatus.Rejected;
        d.ReviewedUtc = _clock.UtcNow;
        d.ReviewedByUserId = _ctx.UserId;
        d.ReviewedByUsername = _ctx.Username;
        d.ReviewRemarks = remarks;
        _audit.Add(AuditActions.DepositRejected, "CashDeposit", d.DepositNumber, $"Deposit {d.DepositNumber} of {d.Username} ({d.Amount:N2}) rejected: {remarks}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The deposit was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<CashDeposit>> DepositsAsync(int? userId, DepositStatus? status, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.CashDeposits.AsNoTracking().Include(d => d.Counter).AsQueryable();
        if (userId is int u) q = q.Where(d => d.UserId == u);
        if (status is { } s) q = q.Where(d => d.Status == s);
        if (from is { } f) q = q.Where(d => d.BusinessDate >= f);
        if (to is { } t) q = q.Where(d => d.BusinessDate <= t);
        return await q.OrderByDescending(d => d.Id).Take(1000).ToListAsync(ct);
    }

    // ---------------- adjustments ----------------

    public async Task<ServiceResult<FinancialAdjustment>> AdjustAsync(AdjustmentTarget target, int id, decimal amount, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult<FinancialAdjustment>.Fail("Enter the reason for the adjustment.");
        if (amount == 0 || !FinanceMath.IsMoney(amount) || Math.Abs(amount) > 10_000_000) return ServiceResult<FinancialAdjustment>.Fail("Enter a valid adjustment amount (positive or negative, not zero).");
        if (target == AdjustmentTarget.Cashier && id == _ctx.UserId) return ServiceResult<FinancialAdjustment>.Fail("You cannot adjust your own account.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        string name;
        if (target == AdjustmentTarget.Cashier)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (u is null) return ServiceResult<FinancialAdjustment>.Fail("User not found.");
            name = u.Username;
            await FinanceDb.LockAsync(_db, $"CASHIER|{id}", ct);
        }
        else
        {
            var d = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (d is null) return ServiceResult<FinancialAdjustment>.Fail("Doctor not found.");
            name = d.Name;
            await FinanceDb.LockAsync(_db, $"DOCTOR|{id}", ct);
        }
        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        var a = new FinancialAdjustment
        {
            AdjustmentNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.AdjustmentNumberSequence, "ADJ", today, ct),
            Target = target, UserId = target == AdjustmentTarget.Cashier ? id : null, DoctorId = target == AdjustmentTarget.Doctor ? id : null,
            Amount = amount, Reason = reason, BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        };
        _db.FinancialAdjustments.Add(a);
        await _db.SaveChangesAsync(ct);
        if (target == AdjustmentTarget.Cashier)
            _db.CashierLedger.Add(new CashierLedgerEntry
            {
                UserId = id, EntryType = CashierEntryType.Adjustment, Amount = amount, AffectsCashInHand = true, AdjustmentId = a.Id,
                Reference = a.AdjustmentNumber, Description = $"Adjustment {a.AdjustmentNumber}: {reason}", BusinessDate = today, CreatedUtc = now,
                CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
            });
        else
            _db.DoctorLedger.Add(new DoctorLedgerEntry
            {
                DoctorId = id, EntryType = DoctorEntryType.Adjustment, Amount = amount, AdjustmentId = a.Id, Reference = a.AdjustmentNumber,
                Description = $"Adjustment {a.AdjustmentNumber}: {reason}", BusinessDate = today, CreatedUtc = now,
                CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
            });
        _audit.Add(AuditActions.AdjustmentRecorded, "FinancialAdjustment", a.AdjustmentNumber,
            $"Adjustment {a.AdjustmentNumber} on {target.ToString().ToLowerInvariant()} {name}: {amount:+#,0.00;-#,0.00}. Reason: {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<FinancialAdjustment>.Ok(a);
    }

    public async Task<IReadOnlyList<FinancialAdjustment>> AdjustmentsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.FinancialAdjustments.AsNoTracking().AsQueryable();
        if (from is { } f) q = q.Where(a => a.BusinessDate >= f);
        if (to is { } t) q = q.Where(a => a.BusinessDate <= t);
        return await q.OrderByDescending(a => a.Id).Take(500).ToListAsync(ct);
    }

    // ---------------- daily closing ----------------

    public async Task<ClosingFigures> ClosingFiguresAsync(ClosingScope scope, int? id, DateOnly date, CancellationToken ct = default)
    {
        var ledger = _db.CashierLedger.AsNoTracking().AsQueryable();
        var deposits = _db.CashDeposits.AsNoTracking().AsQueryable();
        var receipts = _db.Receipts.AsNoTracking().AsQueryable();
        string name;
        switch (scope)
        {
            case ClosingScope.Cashier:
                var u = await _db.Users.AsNoTracking().FirstAsync(x => x.Id == id, ct);
                name = $"{u.FullName} ({u.Username})";
                ledger = ledger.Where(e => e.UserId == id);
                deposits = deposits.Where(d => d.UserId == id);
                receipts = receipts.Where(r => r.CashierUserId == id);
                break;
            case ClosingScope.Counter:
                var c = await _db.Counters.AsNoTracking().FirstAsync(x => x.Id == id, ct);
                name = c.Name;
                ledger = ledger.Where(e => e.CounterId == id);
                deposits = deposits.Where(d => d.CounterId == id);
                receipts = receipts.Where(r => r.CounterId == id);
                break;
            default:
                name = "Entire hospital";
                break;
        }
        var opening = await ledger.Where(e => e.AffectsCashInHand && e.BusinessDate < date).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var day = await ledger.Where(e => e.BusinessDate == date).ToListAsync(ct);
        var dayReceipts = await receipts.Where(r => r.BusinessDate == date).CountAsync(ct);
        var cancelled = day.Where(e => e.EntryType == CashierEntryType.ReceiptCancellation).ToList();
        var submitted = await deposits.Where(d => d.BusinessDate == date).SumAsync(d => (decimal?)d.Amount, ct) ?? 0;
        return new ClosingFigures(scope, name, date, opening,
            day.Where(e => e.EntryType == CashierEntryType.Collection && e.AffectsCashInHand).Sum(e => e.Amount),
            day.Where(e => e.EntryType == CashierEntryType.Collection && !e.AffectsCashInHand).Sum(e => e.Amount),
            dayReceipts, cancelled.Count, -cancelled.Sum(e => e.Amount),
            -day.Where(e => e.EntryType == CashierEntryType.Refund).Sum(e => e.Amount),
            submitted,
            -day.Where(e => e.EntryType == CashierEntryType.DepositConfirmed).Sum(e => e.Amount),
            day.Where(e => e.EntryType == CashierEntryType.Adjustment).Sum(e => e.Amount),
            opening + day.Where(e => e.AffectsCashInHand).Sum(e => e.Amount));
    }

    public async Task<ServiceResult<DailyCashClosing>> CloseDayAsync(ClosingScope scope, int? id, DateOnly date, string? notes, CancellationToken ct = default)
    {
        var today = await _clock.TodayAsync();
        if (date > today) return ServiceResult<DailyCashClosing>.Fail("A future date cannot be closed.");
        if (scope != ClosingScope.Hospital && id is null) return ServiceResult<DailyCashClosing>.Fail("Select the cashier or counter.");
        var f = await ClosingFiguresAsync(scope, scope == ClosingScope.Hospital ? null : id, date, ct);
        var closing = new DailyCashClosing
        {
            BusinessDate = date, Scope = scope, UserId = scope == ClosingScope.Cashier ? id : null, CounterId = scope == ClosingScope.Counter ? id : null,
            ScopeName = f.ScopeName[..Math.Min(150, f.ScopeName.Length)], OpeningBalance = f.Opening, CashCollections = f.Cash, NonCashCollections = f.NonCash,
            ReceiptCount = f.Receipts, CancelledCount = f.Cancelled, CancelledAmount = f.CancelledAmount, Refunds = f.Refunds,
            DepositsSubmitted = f.DepositsSubmitted, DepositsConfirmed = f.DepositsConfirmed, Adjustments = f.Adjustments, ClosingBalance = f.Closing,
            Notes = T(notes, 300), ClosedUtc = _clock.UtcNow, ClosedByUserId = _ctx.UserId, ClosedByUsername = _ctx.Username
        };
        _db.DailyCashClosings.Add(closing);
        _audit.Add(AuditActions.DailyClosingCreated, "DailyCashClosing", $"{date:yyyy-MM-dd}/{scope}/{id}",
            $"Daily closing {date:yyyy-MM-dd} – {f.ScopeName}: cash {f.Cash:N2}, non-cash {f.NonCash:N2}, closing balance {f.Closing:N2}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<DailyCashClosing>.Ok(closing);
    }

    public async Task<IReadOnlyList<DailyCashClosing>> ClosingsAsync(ClosingScope? scope, int? userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.DailyCashClosings.AsNoTracking().AsQueryable();
        if (scope is { } s) q = q.Where(c => c.Scope == s);
        if (userId is int u) q = q.Where(c => c.UserId == u);
        if (from is { } f) q = q.Where(c => c.BusinessDate >= f);
        if (to is { } t) q = q.Where(c => c.BusinessDate <= t);
        return await q.OrderByDescending(c => c.BusinessDate).ThenByDescending(c => c.Id).Take(500).ToListAsync(ct);
    }
}
