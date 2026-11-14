using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Finance;

public sealed class NewChargeLine
{
    public int ServiceItemId { get; set; }
    /// <summary>Doctor for a consultation (defaults to the doctor linked to the service).</summary>
    public int? DoctorId { get; set; }
    public long? EncounterId { get; set; }
    public decimal Discount { get; set; }
    public string? DiscountReason { get; set; }
    /// <summary>Amount paid now. Null = full net amount.</summary>
    public decimal? PayNow { get; set; }
}

public sealed class BalancePayment
{
    public long ChargeId { get; set; }
    public decimal PayNow { get; set; }
}

public sealed class CollectInput
{
    public Guid IdempotencyKey { get; set; }
    public long? PatientId { get; set; }
    public string? PayerName { get; set; }
    public int CounterId { get; set; }
    public int PaymentMethodId { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public List<NewChargeLine> Lines { get; set; } = new();
    public List<BalancePayment> Balances { get; set; } = new();
}

public sealed record CollectResult(Receipt? Receipt, bool WasDuplicate, IReadOnlyList<Charge> FreeCharges);

public sealed class ReceiptQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>Restricts to one cashier (always set for users who may only see their own receipts).</summary>
    public int? CashierUserId { get; set; }
    public int? CounterId { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public int? PaymentMethodId { get; set; }
    public long? PatientId { get; set; }
    /// <summary>valid | cancelled | refunded | null</summary>
    public string? Status { get; set; }
    public bool IncludeCancelled { get; set; } = true;
    public string? Q { get; set; }
    public int Take { get; set; } = 500;
}

public sealed record ReceiptListItem(
    long Id, string ReceiptNumber, DateTime CreatedUtc, DateOnly BusinessDate, ReceiptStatus Status,
    long? PatientId, string? PatientNumber, string? PatientName, string? PayerName,
    int CounterId, string CounterName, int CashierUserId, string CashierName, string PaymentMethod, bool IsCash,
    decimal TotalNet, decimal AmountPaid, decimal BalanceAfter, decimal RefundedAmount, string Services, int PrintCount);

public interface IBillingService
{
    Task<ServiceResult<CollectResult>> CollectAsync(CollectInput input, bool canDiscount, CancellationToken ct = default);
    Task<Receipt?> GetReceiptAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ReceiptListItem>> ReceiptsAsync(ReceiptQuery q, CancellationToken ct = default);
    Task<IReadOnlyList<Charge>> OpenChargesAsync(long patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Charge>> ChargesOfEncounterAsync(long encounterId, CancellationToken ct = default);
    Task<ServiceResult<int>> RecordPrintAsync(long receiptId, string format, string method, CancellationToken ct = default);
    Task<int> PrintCountAsync(long receiptId, CancellationToken ct = default);
    Task<ServiceResult> CancelReceiptAsync(long receiptId, string? reason, CancellationToken ct = default);

    Task<ServiceResult<Refund>> RequestRefundAsync(long receiptId, decimal amount, int paymentMethodId, string? reason, CancellationToken ct = default);
    Task<ServiceResult> ApproveRefundAsync(long refundId, string? note, CancellationToken ct = default);
    Task<ServiceResult> RejectRefundAsync(long refundId, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<Refund>> RefundsAsync(RefundStatus? status, long? receiptId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<decimal> RefundableAsync(long receiptId, CancellationToken ct = default);
}

public sealed class BillingService : IBillingService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _setup;

    public BillingService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IFinanceSetupService setup)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _setup = setup;
    }

    private static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    // =====================================================================================
    // Collection
    // =====================================================================================

    public async Task<ServiceResult<CollectResult>> CollectAsync(CollectInput input, bool canDiscount, CancellationToken ct = default)
    {
        if (_ctx.UserId is not int userId) return ServiceResult<CollectResult>.Fail("Please sign in again.");
        if (input.IdempotencyKey == Guid.Empty) return ServiceResult<CollectResult>.Fail("The payment form has expired. Please reload the page.");

        // A repeated submission (double click, browser retry) returns the receipt that was already created.
        if (await ExistingAsync(input.IdempotencyKey, ct) is { } existing)
            return ServiceResult<CollectResult>.Ok(new CollectResult(existing, true, Array.Empty<Charge>()));

        var errors = new List<string>();
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var counter = await _db.Counters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.CounterId, ct);
        if (counter is null || !counter.IsActive) errors.Add("Select an active counter.");
        else if (!user.IsSystemAdministrator && !await _db.CounterAssignments.AnyAsync(a => a.CounterId == counter.Id && a.UserId == userId && a.EndedUtc == null, ct))
            errors.Add($"You are not assigned to {counter.Name}. Ask the administrator to assign you to this counter.");
        var method = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == input.PaymentMethodId, ct);
        if (method is null || !method.IsActive) errors.Add("Select a payment method.");
        Patient? patient = null;
        if (input.PatientId is long pid)
        {
            patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (patient is null) errors.Add("Patient not found.");
            else if (patient.Status == PatientStatus.Merged) errors.Add($"{patient.PatientNumber} was merged into another registration. Use the correct Patient ID.");
        }
        var payer = T(input.PayerName, 150);
        if (patient is null && payer is null && input.PatientId is null) errors.Add("Select the patient, or enter the payer's name for a walk-in service.");
        var lines = input.Lines.Where(l => l.ServiceItemId > 0).ToList();
        var balances = input.Balances.Where(b => b.ChargeId > 0 && b.PayNow > 0).ToList();
        if (lines.Count == 0 && balances.Count == 0) errors.Add("Add at least one service or select an outstanding balance to pay.");
        if (lines.Count > 20) errors.Add("At most 20 services per receipt.");
        if (balances.Count > 0 && patient is null) errors.Add("Outstanding balances can only be paid for a registered patient.");
        if (errors.Count > 0) return ServiceResult<CollectResult>.Fail(errors.ToArray());

        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        var settings = await _setup.GetSettingsAsync(ct);

        // ---- validate and price new services (fee as of today; historical receipts keep their own fee) ----
        var services = await _db.ServiceItems.AsNoTracking().Include(s => s.Counters).Include(s => s.Doctor)
            .Where(s => lines.Select(l => l.ServiceItemId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var doctorIds = lines.Where(l => l.DoctorId != null).Select(l => l.DoctorId!.Value).Concat(services.Values.Where(s => s.DoctorId != null).Select(s => s.DoctorId!.Value)).Distinct().ToList();
        var doctors = await _db.Doctors.AsNoTracking().Where(d => doctorIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var priced = new List<(NewChargeLine Line, ServiceItem Service, Doctor? Doctor, decimal Fee, decimal Discount, decimal Net, decimal Pay)>();
        foreach (var l in lines)
        {
            if (!services.TryGetValue(l.ServiceItemId, out var s) || !s.IsActive) { errors.Add("A selected service is not available."); continue; }
            if (s.Counters.Count > 0 && s.Counters.All(c => c.CounterId != input.CounterId)) { errors.Add($"{s.Name} cannot be collected at {counter!.Name}."); continue; }
            var doctorId = l.DoctorId ?? s.DoctorId;
            Doctor? doctor = null;
            if (doctorId is int did)
            {
                if (!doctors.TryGetValue(did, out doctor)) { errors.Add("Doctor not found."); continue; }
                if (s.DoctorId is int fixedDoc && fixedDoc != did) { errors.Add($"{s.Name} belongs to {s.Doctor!.Name}."); continue; }
            }
            if (s.Kind == ServiceKind.Consultation && doctor is null) { errors.Add($"Select the doctor for {s.Name}."); continue; }
            var fee = await _setup.FeeOnAsync(s.Id, today, ct);
            var discount = l.Discount;
            if (discount != 0 && !canDiscount) { errors.Add("You are not allowed to give discounts."); continue; }
            if (discount < 0 || discount > fee || !FinanceMath.IsMoney(discount)) { errors.Add($"Discount on {s.Name} must be between 0 and {fee:N2}."); continue; }
            if (discount > 0 && T(l.DiscountReason, 200) is null) { errors.Add($"Enter the reason for the discount on {s.Name}."); continue; }
            var net = fee - discount;
            var pay = l.PayNow ?? net;
            if (pay < 0 || pay > net || !FinanceMath.IsMoney(pay)) { errors.Add($"Amount for {s.Name} must be between 0 and {net:N2}."); continue; }
            if (pay < net && !s.AllowPartialPayment) { errors.Add($"{s.Name} must be paid in full ({net:N2})."); continue; }
            if (l.EncounterId is long eid && !await _db.PatientEncounters.AnyAsync(e => e.Id == eid && e.PatientId == input.PatientId, ct))
            { errors.Add("The visit does not belong to this patient."); continue; }
            priced.Add((l, s, doctor, fee, discount, net, pay));
        }
        if (method is { RequiresReference: true } && T(input.PaymentReference, 100) is null && (priced.Sum(p => p.Pay) + balances.Sum(b => b.PayNow)) > 0)
            errors.Add($"Enter the {method.Name} reference (transaction / slip number).");
        if (balances.Any(b => !FinanceMath.IsMoney(b.PayNow))) errors.Add("Use at most 2 decimals.");
        if (errors.Count > 0) return ServiceResult<CollectResult>.Fail(errors.Distinct().ToArray());

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // ---- outstanding balances: lock the charges so two counters cannot over-collect the same balance ----
            var balanceIds = balances.Select(b => b.ChargeId).Distinct().ToList();
            var locked = balanceIds.Count == 0 ? new List<Charge>()
                : await _db.Charges.FromSqlRaw("SELECT c.*, c.xmin FROM \"Charges\" c WHERE c.\"Id\" = ANY({0}) FOR UPDATE", balanceIds.ToArray()).ToListAsync(ct);
            foreach (var b in balances)
            {
                var c = locked.FirstOrDefault(x => x.Id == b.ChargeId);
                if (c is null || c.PatientId != input.PatientId) errors.Add("Outstanding charge not found for this patient.");
                else if (c.Status == ChargeStatus.Cancelled) errors.Add($"{c.ChargeNumber} was cancelled.");
                else if (b.PayNow > c.NetAmount - c.PaidAmount) errors.Add($"{c.ServiceName}: only {c.NetAmount - c.PaidAmount:N2} is outstanding.");
                else if (!c.AllowPartialPayment && b.PayNow < c.NetAmount - c.PaidAmount) errors.Add($"{c.ServiceName} must be paid in full ({c.NetAmount - c.PaidAmount:N2}).");
                else if (balances.Count(x => x.ChargeId == b.ChargeId) > 1) errors.Add("The same balance was selected twice.");
            }
            if (errors.Count > 0)
            {
                await tx.RollbackAsync(ct);
                return ServiceResult<CollectResult>.Fail(errors.Distinct().ToArray());
            }

            var total = priced.Sum(p => p.Pay) + balances.Sum(b => b.PayNow);
            var freeCharges = new List<Charge>();
            var needsReceipt = total > 0 || priced.Any(p => p.Net > 0) || settings.IssueZeroReceiptForFreeServices || balances.Count > 0;

            var newCharges = new List<(Charge Charge, decimal Pay)>();
            foreach (var p in priced)
            {
                var share = p.Doctor is null ? 0 : FinanceMath.DoctorShare(p.Doctor.ShareType, p.Doctor.ShareValue, p.Net);
                var c = new Charge
                {
                    ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", today, ct),
                    PatientId = input.PatientId, EncounterId = p.Line.EncounterId, ServiceItemId = p.Service.Id, ServiceKind = p.Service.Kind,
                    ServiceName = p.Service.Name, DepartmentId = p.Service.DepartmentId ?? p.Doctor?.DepartmentId, DoctorId = p.Doctor?.Id, DoctorName = p.Doctor?.Name,
                    Fee = p.Fee, Discount = p.Discount, NetAmount = p.Net, PaidAmount = 0, AllowPartialPayment = p.Service.AllowPartialPayment,
                    DiscountReason = p.Discount > 0 ? T(p.Line.DiscountReason, 200) : null,
                    DoctorShareType = p.Doctor?.ShareType ?? DoctorShareType.HospitalKeepsAll, DoctorShareValue = p.Doctor?.ShareValue ?? 0, DoctorShareAmount = share,
                    Status = p.Net == 0 ? ChargeStatus.Paid : ChargeStatus.Unpaid, BusinessDate = today, CreatedUtc = now,
                    CreatedByUserId = userId, CreatedByUsername = user.Username
                };
                _db.Charges.Add(c);
                newCharges.Add((c, p.Pay));
                if (p.Net == 0) freeCharges.Add(c);
            }

            Receipt? receipt = null;
            if (needsReceipt)
            {
                receipt = new Receipt
                {
                    ReceiptNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ReceiptNumberSequence, settings.ReceiptPrefix, today, ct),
                    IdempotencyKey = input.IdempotencyKey, PatientId = input.PatientId, PayerName = patient is null ? payer : null,
                    CounterId = counter!.Id, CashierUserId = userId, CashierUsername = user.Username, CashierName = user.FullName,
                    PaymentMethodId = method!.Id, PaymentReference = T(input.PaymentReference, 100), Notes = T(input.Notes, 300),
                    BusinessDate = today, CreatedUtc = now, Workstation = T(_ctx.Workstation, 100), Status = ReceiptStatus.Valid
                };
                _db.Receipts.Add(receipt);
                foreach (var (c, pay) in newCharges) AddLine(receipt, c, pay, 0m);
                foreach (var b in balances)
                {
                    var c = locked.First(x => x.Id == b.ChargeId);
                    var previousShare = await _db.ReceiptLines.Where(l => l.ChargeId == c.Id && l.Receipt!.Status == ReceiptStatus.Valid).SumAsync(l => (decimal?)l.DoctorShare, ct) ?? 0;
                    AddLine(receipt, c, b.PayNow, previousShare);
                }
                receipt.TotalFee = receipt.Lines.Sum(l => l.Fee);
                receipt.TotalDiscount = receipt.Lines.Sum(l => l.Discount);
                receipt.TotalNet = receipt.Lines.Sum(l => l.NetAmount);
                receipt.AmountPaid = receipt.Lines.Sum(l => l.Amount);
                receipt.BalanceAfter = receipt.Lines.Sum(l => l.BalanceAfter);
            }
            await _db.SaveChangesAsync(ct);

            if (receipt is not null)
            {
                foreach (var (c, _) in newCharges) c.OriginReceiptId = receipt.Id;
                if (receipt.AmountPaid > 0)
                    _db.CashierLedger.Add(new CashierLedgerEntry
                    {
                        UserId = userId, CounterId = counter!.Id, EntryType = CashierEntryType.Collection, Amount = receipt.AmountPaid,
                        PaymentMethodId = method!.Id, AffectsCashInHand = method.CountsAsCashInHand, ReceiptId = receipt.Id, Reference = receipt.ReceiptNumber,
                        Description = $"Receipt {receipt.ReceiptNumber} ({method.Name})", BusinessDate = today, CreatedUtc = now,
                        CreatedByUserId = userId, CreatedByUsername = user.Username
                    });
                foreach (var l in receipt.Lines.Where(l => l.DoctorId != null && (l.DoctorShare > 0 || l.Amount > 0)))
                    _db.DoctorLedger.Add(new DoctorLedgerEntry
                    {
                        DoctorId = l.DoctorId!.Value, EntryType = DoctorEntryType.ConsultationShare, Amount = l.DoctorShare, GrossAmount = l.Amount,
                        HospitalShare = l.Amount - l.DoctorShare, ReceiptId = receipt.Id, ReceiptLineId = l.Id, Reference = receipt.ReceiptNumber,
                        Description = $"{l.ServiceName} – receipt {receipt.ReceiptNumber}", BusinessDate = today, CreatedUtc = now,
                        CreatedByUserId = userId, CreatedByUsername = user.Username
                    });
                _audit.Add(AuditActions.PaymentCollected, "Receipt", receipt.ReceiptNumber,
                    $"Receipt {receipt.ReceiptNumber}: {receipt.AmountPaid:N2} {method!.Name} at {counter!.Name}" +
                    (patient is null ? $" from {payer}" : $" for {patient.PatientNumber}") +
                    (receipt.BalanceAfter > 0 ? $", balance {receipt.BalanceAfter:N2}" : ""),
                    newValues: new { receipt.ReceiptNumber, receipt.TotalFee, receipt.TotalDiscount, receipt.AmountPaid, receipt.BalanceAfter, Lines = receipt.Lines.Select(l => new { l.ServiceName, l.Fee, l.Discount, l.Amount, l.DoctorShare }) });
            }
            else
            {
                _audit.Add(AuditActions.PaymentCollected, "Charge", string.Join(",", freeCharges.Select(c => c.ChargeNumber)),
                    $"Free service recorded without receipt: {string.Join(", ", freeCharges.Select(c => c.ServiceName))}");
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<CollectResult>.Ok(new CollectResult(receipt, false, freeCharges));
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_Receipts_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            var dup = await ExistingAsync(input.IdempotencyKey, ct);
            return dup is null ? ServiceResult<CollectResult>.Fail("Please try again.") : ServiceResult<CollectResult>.Ok(new CollectResult(dup, true, Array.Empty<Charge>()));
        }
    }

    private void AddLine(Receipt r, Charge c, decimal pay, decimal previousShare)
    {
        var before = c.PaidAmount;
        c.PaidAmount += pay;
        c.Status = c.NetAmount == 0 || c.PaidAmount == c.NetAmount ? ChargeStatus.Paid : c.PaidAmount > 0 ? ChargeStatus.PartiallyPaid : ChargeStatus.Unpaid;
        // Doctor earns the agreed share of what is actually collected; the last payment absorbs rounding.
        var share = c.DoctorShareAmount == 0 || c.NetAmount == 0 ? 0
            : c.PaidAmount == c.NetAmount ? Math.Max(0, c.DoctorShareAmount - previousShare)
            : Math.Min(pay, FinanceMath.Round(pay * c.DoctorShareAmount / c.NetAmount));
        r.Lines.Add(new ReceiptLine
        {
            Charge = c, ServiceName = c.ServiceName, DoctorName = c.DoctorName, DepartmentId = c.DepartmentId, DoctorId = c.DoctorId,
            Fee = c.Fee, Discount = c.Discount, NetAmount = c.NetAmount, PreviouslyPaid = before, Amount = pay,
            BalanceAfter = c.NetAmount - c.PaidAmount, DoctorShare = Math.Min(share, pay)
        });
    }

    private async Task<Receipt?> ExistingAsync(Guid key, CancellationToken ct)
    {
        var r = await _db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (r is not null)
            await _audit.LogAsync(AuditActions.DuplicatePaymentBlocked, "Receipt", r.ReceiptNumber, $"Repeated submission of receipt {r.ReceiptNumber} ignored (no second payment recorded)", ct: ct);
        return r;
    }

    // =====================================================================================
    // Queries
    // =====================================================================================

    public async Task<Receipt?> GetReceiptAsync(long id, CancellationToken ct = default) =>
        await _db.Receipts.AsNoTracking()
            .Include(r => r.Patient).Include(r => r.Counter).Include(r => r.PaymentMethod)
            .Include(r => r.Lines.OrderBy(l => l.Id)).ThenInclude(l => l.Charge)
            .Include(r => r.Prints.OrderBy(p => p.CopyNumber))
            .AsSplitQuery().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<ReceiptListItem>> ReceiptsAsync(ReceiptQuery q, CancellationToken ct = default)
    {
        var x = _db.Receipts.AsNoTracking().AsQueryable();
        if (q.From is { } f) x = x.Where(r => r.BusinessDate >= f);
        if (q.To is { } t) x = x.Where(r => r.BusinessDate <= t);
        if (q.CashierUserId is int u) x = x.Where(r => r.CashierUserId == u);
        if (q.CounterId is int c) x = x.Where(r => r.CounterId == c);
        if (q.PaymentMethodId is int m) x = x.Where(r => r.PaymentMethodId == m);
        if (q.PatientId is long p) x = x.Where(r => r.PatientId == p);
        if (q.DepartmentId is int d) x = x.Where(r => r.Lines.Any(l => l.DepartmentId == d));
        if (q.DoctorId is int doc) x = x.Where(r => r.Lines.Any(l => l.DoctorId == doc));
        if (!q.IncludeCancelled) x = x.Where(r => r.Status == ReceiptStatus.Valid);
        x = q.Status switch
        {
            "valid" => x.Where(r => r.Status == ReceiptStatus.Valid),
            "cancelled" => x.Where(r => r.Status == ReceiptStatus.Cancelled),
            "refunded" => x.Where(r => r.RefundedAmount > 0),
            "balance" => x.Where(r => r.Status == ReceiptStatus.Valid && r.BalanceAfter > 0),
            _ => x,
        };
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var s = q.Q.Trim();
            var like = "%" + s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            x = x.Where(r => r.ReceiptNumber == s.ToUpper() || EF.Functions.ILike(r.ReceiptNumber, like)
                             || (r.Patient != null && (r.Patient.PatientNumber == s.ToUpper() || EF.Functions.ILike(r.Patient.FullName, like)))
                             || (r.PayerName != null && EF.Functions.ILike(r.PayerName, like)));
        }
        var rows = await x.OrderByDescending(r => r.Id).Take(Math.Clamp(q.Take, 1, 5000))
            .Select(r => new
            {
                r.Id, r.ReceiptNumber, r.CreatedUtc, r.BusinessDate, r.Status, r.PatientId,
                PatientNumber = r.Patient != null ? r.Patient.PatientNumber : null, PatientName = r.Patient != null ? r.Patient.FullName : null,
                r.PayerName, r.CounterId, CounterName = r.Counter!.Name, r.CashierUserId, r.CashierName, Method = r.PaymentMethod!.Name,
                IsCash = r.PaymentMethod.CountsAsCashInHand, r.TotalNet, r.AmountPaid, r.BalanceAfter, r.RefundedAmount,
                Services = r.Lines.OrderBy(l => l.Id).Select(l => l.ServiceName).ToList(), Prints = r.Prints.Count
            }).ToListAsync(ct);
        return rows.Select(r => new ReceiptListItem(r.Id, r.ReceiptNumber, r.CreatedUtc, r.BusinessDate, r.Status, r.PatientId, r.PatientNumber,
            r.PatientName, r.PayerName, r.CounterId, r.CounterName, r.CashierUserId, r.CashierName, r.Method, r.IsCash, r.TotalNet, r.AmountPaid,
            r.BalanceAfter, r.RefundedAmount, string.Join(", ", r.Services), r.Prints)).ToList();
    }

    public async Task<IReadOnlyList<Charge>> OpenChargesAsync(long patientId, CancellationToken ct = default) =>
        await _db.Charges.AsNoTracking().Where(c => c.PatientId == patientId && (c.Status == ChargeStatus.Unpaid || c.Status == ChargeStatus.PartiallyPaid))
            .OrderBy(c => c.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Charge>> ChargesOfEncounterAsync(long encounterId, CancellationToken ct = default) =>
        await _db.Charges.AsNoTracking().Where(c => c.EncounterId == encounterId).OrderBy(c => c.Id).ToListAsync(ct);

    // =====================================================================================
    // Printing
    // =====================================================================================

    public Task<int> PrintCountAsync(long receiptId, CancellationToken ct = default) => _db.ReceiptPrints.CountAsync(p => p.ReceiptId == receiptId, ct);

    public async Task<ServiceResult<int>> RecordPrintAsync(long receiptId, string format, string method, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"RCPPRINT|{receiptId}", ct);
        var r = await _db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return ServiceResult<int>.Fail("Receipt not found.");
        var copy = await _db.ReceiptPrints.CountAsync(p => p.ReceiptId == receiptId, ct) + 1;
        _db.ReceiptPrints.Add(new ReceiptPrint
        {
            ReceiptId = receiptId, CopyNumber = copy, Format = format, Method = T(method, 40) ?? "browser", PrintedUtc = _clock.UtcNow,
            PrintedByUserId = _ctx.UserId, PrintedByUsername = _ctx.Username, Workstation = T(_ctx.Workstation, 100)
        });
        _audit.Add(copy == 1 ? AuditActions.ReceiptPrinted : AuditActions.ReceiptReprinted, "Receipt", r.ReceiptNumber,
            $"Receipt {r.ReceiptNumber} {(copy == 1 ? "printed" : $"reprinted – copy {copy}")} ({format}, {method})");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(copy);
    }

    // =====================================================================================
    // Cancellation (receipt stays, number never reused, ledgers get reversal entries)
    // =====================================================================================

    public async Task<ServiceResult> CancelReceiptAsync(long receiptId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for cancelling the receipt.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"RECEIPT|{receiptId}", ct);
        var r = await _db.Receipts.Include(x => x.Lines).Include(x => x.PaymentMethod).FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return ServiceResult.Fail("Receipt not found.");
        if (r.Status == ReceiptStatus.Cancelled) return ServiceResult.Fail($"Receipt {r.ReceiptNumber} is already cancelled.");
        if (await _db.Refunds.AnyAsync(x => x.ReceiptId == r.Id && x.Status != RefundStatus.Rejected, ct))
            return ServiceResult.Fail("This receipt has a refund. A refunded receipt cannot be cancelled – refund the remaining amount instead.");
        var chargeIds = r.Lines.Select(l => l.ChargeId).ToList();
        var charges = await _db.Charges.FromSqlRaw("SELECT c.*, c.xmin FROM \"Charges\" c WHERE c.\"Id\" = ANY({0}) FOR UPDATE", chargeIds.ToArray()).ToListAsync(ct);
        var now = _clock.UtcNow;
        var today = await _clock.TodayAsync();
        foreach (var l in r.Lines)
        {
            var c = charges.First(x => x.Id == l.ChargeId);
            c.PaidAmount -= l.Amount;
            var otherPayments = await _db.ReceiptLines.AnyAsync(x => x.ChargeId == c.Id && x.ReceiptId != r.Id && x.Receipt!.Status == ReceiptStatus.Valid, ct);
            c.Status = c.OriginReceiptId == r.Id && !otherPayments ? ChargeStatus.Cancelled
                : c.PaidAmount == 0 ? ChargeStatus.Unpaid : c.PaidAmount == c.NetAmount ? ChargeStatus.Paid : ChargeStatus.PartiallyPaid;
            if (l.DoctorId is int did && l.DoctorShare > 0)
                _db.DoctorLedger.Add(new DoctorLedgerEntry
                {
                    DoctorId = did, EntryType = DoctorEntryType.ShareReversal, Amount = -l.DoctorShare, GrossAmount = -l.Amount,
                    HospitalShare = -(l.Amount - l.DoctorShare), ReceiptId = r.Id, ReceiptLineId = l.Id, Reference = r.ReceiptNumber,
                    Description = $"Receipt {r.ReceiptNumber} cancelled", BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                });
        }
        if (r.AmountPaid > 0)
            _db.CashierLedger.Add(new CashierLedgerEntry
            {
                UserId = r.CashierUserId, CounterId = r.CounterId, EntryType = CashierEntryType.ReceiptCancellation, Amount = -r.AmountPaid,
                PaymentMethodId = r.PaymentMethodId, AffectsCashInHand = r.PaymentMethod!.CountsAsCashInHand, ReceiptId = r.Id, Reference = r.ReceiptNumber,
                Description = $"Receipt {r.ReceiptNumber} cancelled: {reason}", BusinessDate = today, CreatedUtc = now,
                CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
            });
        r.Status = ReceiptStatus.Cancelled;
        r.CancelledUtc = now;
        r.CancelledByUserId = _ctx.UserId;
        r.CancelledByUsername = _ctx.Username;
        r.CancelReason = reason;
        _audit.Add(AuditActions.ReceiptCancelled, "Receipt", r.ReceiptNumber, $"Receipt {r.ReceiptNumber} ({r.AmountPaid:N2}) cancelled: {reason}",
            new { Status = "Valid" }, new { Status = "Cancelled", Reason = reason });
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("The receipt was changed by someone else. Please reload.");
        }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Refunds (separate from cancellation; the receipt stays valid)
    // =====================================================================================

    public async Task<decimal> RefundableAsync(long receiptId, CancellationToken ct = default)
    {
        var r = await _db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null || r.Status != ReceiptStatus.Valid) return 0;
        var pending = await _db.Refunds.Where(x => x.ReceiptId == receiptId && x.Status == RefundStatus.Pending).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        return Math.Max(0, r.AmountPaid - r.RefundedAmount - pending);
    }

    public async Task<ServiceResult<Refund>> RequestRefundAsync(long receiptId, decimal amount, int paymentMethodId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult<Refund>.Fail("Enter the reason for the refund.");
        if (amount <= 0 || !FinanceMath.IsMoney(amount)) return ServiceResult<Refund>.Fail("Enter a valid refund amount.");
        var method = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == paymentMethodId && m.IsActive, ct);
        if (method is null) return ServiceResult<Refund>.Fail("Select the refund payment method.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"RECEIPT|{receiptId}", ct);
        var r = await _db.Receipts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == receiptId, ct);
        if (r is null) return ServiceResult<Refund>.Fail("Receipt not found.");
        if (r.Status != ReceiptStatus.Valid) return ServiceResult<Refund>.Fail("A cancelled receipt cannot be refunded.");
        var refundable = await RefundableAsync(receiptId, ct);
        if (amount > refundable) return ServiceResult<Refund>.Fail($"The refund cannot exceed the refundable amount of {refundable:N2}.");
        var today = await _clock.TodayAsync();
        var refund = new Refund
        {
            RefundNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.RefundNumberSequence, "RFD", today, ct),
            ReceiptId = receiptId, Amount = amount, PaymentMethodId = method.Id, Reason = reason, Status = RefundStatus.Pending,
            RequestedUtc = _clock.UtcNow, RequestedByUserId = _ctx.UserId, RequestedByUsername = _ctx.Username
        };
        _db.Refunds.Add(refund);
        _audit.Add(AuditActions.RefundRequested, "Refund", refund.RefundNumber, $"Refund {refund.RefundNumber} of {amount:N2} requested on {r.ReceiptNumber}: {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<Refund>.Ok(refund);
    }

    public async Task<ServiceResult> ApproveRefundAsync(long refundId, string? note, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var receiptId = await _db.Refunds.Where(x => x.Id == refundId).Select(x => (long?)x.ReceiptId).FirstOrDefaultAsync(ct);
        if (receiptId is null) return ServiceResult.Fail("Refund not found.");
        await FinanceDb.LockAsync(_db, $"RECEIPT|{receiptId}", ct);
        var refund = await _db.Refunds.Include(x => x.PaymentMethod).FirstAsync(x => x.Id == refundId, ct);
        if (refund.Status != RefundStatus.Pending) return ServiceResult.Fail($"Refund {refund.RefundNumber} was already {refund.Status.ToString().ToLowerInvariant()}.");
        var r = await _db.Receipts.Include(x => x.Lines.OrderBy(l => l.Id)).FirstAsync(x => x.Id == receiptId, ct);
        if (r.Status != ReceiptStatus.Valid) return ServiceResult.Fail("The receipt was cancelled; the refund cannot be completed.");
        if (refund.Amount > r.AmountPaid - r.RefundedAmount) return ServiceResult.Fail("The refund exceeds the amount still refundable on this receipt.");

        var now = _clock.UtcNow;
        var today = await _clock.TodayAsync();
        var charges = await _db.Charges.FromSqlRaw("SELECT c.*, c.xmin FROM \"Charges\" c WHERE c.\"Id\" = ANY({0}) FOR UPDATE", r.Lines.Select(l => l.ChargeId).ToArray()).ToListAsync(ct);
        // Allocate the refund across the receipt lines (in order) and reverse the doctor's share proportionally.
        var remaining = refund.Amount;
        foreach (var l in r.Lines)
        {
            if (remaining <= 0) break;
            var part = Math.Min(remaining, l.Amount - l.RefundedAmount);
            if (part <= 0) continue;
            var shareBack = l.Amount == 0 ? 0 : Math.Min(l.DoctorShare, FinanceMath.Round(part * l.DoctorShare / l.Amount));
            if (l.RefundedAmount + part == l.Amount)
            {
                var alreadyReversed = await _db.DoctorLedger.Where(d => d.ReceiptLineId == l.Id && d.EntryType == DoctorEntryType.ShareReversal).SumAsync(d => (decimal?)-d.Amount, ct) ?? 0;
                shareBack = Math.Max(0, l.DoctorShare - alreadyReversed);
            }
            l.RefundedAmount += part;
            charges.First(c => c.Id == l.ChargeId).RefundedAmount += part;
            if (l.DoctorId is int did && shareBack > 0)
                _db.DoctorLedger.Add(new DoctorLedgerEntry
                {
                    DoctorId = did, EntryType = DoctorEntryType.ShareReversal, Amount = -shareBack, GrossAmount = -part, HospitalShare = -(part - shareBack),
                    ReceiptId = r.Id, ReceiptLineId = l.Id, RefundId = refund.Id, Reference = refund.RefundNumber,
                    Description = $"Refund {refund.RefundNumber} on {r.ReceiptNumber}", BusinessDate = today, CreatedUtc = now,
                    CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                });
            remaining -= part;
        }
        r.RefundedAmount += refund.Amount;
        _db.CashierLedger.Add(new CashierLedgerEntry
        {
            UserId = r.CashierUserId, CounterId = r.CounterId, EntryType = CashierEntryType.Refund, Amount = -refund.Amount,
            PaymentMethodId = refund.PaymentMethodId, AffectsCashInHand = refund.PaymentMethod!.CountsAsCashInHand, ReceiptId = r.Id, RefundId = refund.Id,
            Reference = refund.RefundNumber, Description = $"Refund {refund.RefundNumber} on {r.ReceiptNumber} ({refund.PaymentMethod.Name})",
            BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        });
        refund.Status = RefundStatus.Completed;
        refund.DecidedUtc = now;
        refund.DecidedByUserId = _ctx.UserId;
        refund.DecidedByUsername = _ctx.Username;
        refund.DecisionNote = T(note, 300);
        refund.BusinessDate = today;
        _audit.Add(AuditActions.RefundCompleted, "Refund", refund.RefundNumber,
            $"Refund {refund.RefundNumber}: {refund.Amount:N2} ({refund.PaymentMethod.Name}) returned on {r.ReceiptNumber}. Reason: {refund.Reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RejectRefundAsync(long refundId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for rejecting the refund.");
        var refund = await _db.Refunds.FirstOrDefaultAsync(x => x.Id == refundId, ct);
        if (refund is null) return ServiceResult.Fail("Refund not found.");
        if (refund.Status != RefundStatus.Pending) return ServiceResult.Fail("Only pending refunds can be rejected.");
        refund.Status = RefundStatus.Rejected;
        refund.DecidedUtc = _clock.UtcNow;
        refund.DecidedByUserId = _ctx.UserId;
        refund.DecidedByUsername = _ctx.Username;
        refund.DecisionNote = reason;
        _audit.Add(AuditActions.RefundRejected, "Refund", refund.RefundNumber, $"Refund {refund.RefundNumber} rejected: {reason}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The refund was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<Refund>> RefundsAsync(RefundStatus? status, long? receiptId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.Refunds.AsNoTracking().Include(r => r.Receipt).ThenInclude(r => r!.Patient).Include(r => r.PaymentMethod).AsQueryable();
        if (status is { } s) q = q.Where(r => r.Status == s);
        if (receiptId is long id) q = q.Where(r => r.ReceiptId == id);
        if (from is { } f) q = q.Where(r => (r.BusinessDate ?? DateOnly.MinValue) >= f || (r.Status == RefundStatus.Pending));
        if (to is { } t) q = q.Where(r => r.BusinessDate == null || r.BusinessDate <= t);
        return await q.OrderByDescending(r => r.Id).Take(1000).ToListAsync(ct);
    }
}
