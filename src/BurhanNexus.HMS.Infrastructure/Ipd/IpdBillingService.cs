using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ipd;

// =====================================================================================
// Update 10 – the inpatient account. There is no second ledger: every line is a Phase 3 charge with IpdAdmissionId, every
// payment a Phase 3 receipt; advances, equipment periods and packages only add the inpatient detail.
// =====================================================================================

public enum LedgerGroup { AdmissionFee = 1, WardBed = 2, Pharmacy = 3, Laboratory = 4, Radiology = 5, OperationTheatre = 6, NicuEquipment = 7, Package = 8, Services = 9 }

public sealed record LedgerLine(Charge Charge, LedgerGroup Group);

public sealed record LedgerPayment(long ReceiptId, string ReceiptNumber, DateTime CreatedUtc, string Method, string Cashier, decimal Amount, bool FromAdvance, ReceiptStatus Status);

public sealed record IpdLedger(IpdAdmission Admission, IReadOnlyList<LedgerLine> Lines, IReadOnlyList<LedgerPayment> Payments, IReadOnlyList<Refund> Refunds,
    IReadOnlyList<PatientAdvance> Advances, IReadOnlyList<DischargeClearanceOverride> Overrides, IReadOnlyList<EquipmentUsage> Equipment, IReadOnlyList<IpdBedCharge> BedDays,
    IReadOnlyList<AdmissionPackage> Packages)
{
    public IEnumerable<LedgerLine> Active => Lines.Where(l => l.Charge.Status != ChargeStatus.Cancelled);
    public decimal Gross => Active.Sum(l => l.Charge.Fee);
    public decimal Discounts => Active.Sum(l => l.Charge.Discount);
    public decimal Net => Active.Sum(l => l.Charge.NetAmount);
    public decimal Paid => Active.Sum(l => l.Charge.PaidAmount);
    public decimal PaidFromAdvance => Payments.Where(p => p.FromAdvance && p.Status == ReceiptStatus.Valid).Sum(p => p.Amount);
    public decimal PaidDirect => Paid - PaidFromAdvance;
    public decimal Refunded => Refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount);
    public decimal Outstanding => Net - Paid;
    public decimal AdvanceReceived => Advances.Where(a => a.Status == AdvanceStatus.Valid).Sum(a => a.Amount);
    public decimal AdvanceAvailable => Advances.Sum(a => a.Available);
    public decimal AdvanceRefunded => Advances.Sum(a => a.RefundedAmount);
    /// <summary>What the patient still has to pay after the unused advance is adjusted (negative = advance to return).</summary>
    public decimal BalanceDue => Outstanding - AdvanceAvailable;
    public decimal Total(LedgerGroup g) => Active.Where(l => l.Group == g).Sum(l => l.Charge.NetAmount);
    public string PaymentStatus => Net == 0 && Lines.Count > 0 ? "WAIVED" : Outstanding <= 0 ? "PAID" : Paid > 0 ? "PARTIAL" : "UNPAID";

    public static string Label(LedgerGroup g) => g switch
    {
        LedgerGroup.AdmissionFee => "Admission fee", LedgerGroup.WardBed => "Ward / bed charges", LedgerGroup.Pharmacy => "Pharmacy (medicines issued)",
        LedgerGroup.Laboratory => "Laboratory", LedgerGroup.Radiology => "Radiology & imaging", LedgerGroup.OperationTheatre => "OT / procedures (surgery)",
        LedgerGroup.NicuEquipment => "NICU equipment", LedgerGroup.Package => "Packages", _ => "Other services (nursing, procedures, oxygen …)",
    };
}

public sealed class AdvanceInput
{
    public Guid IdempotencyKey { get; set; }
    public long AdmissionId { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethodId { get; set; }
    public int? CounterId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public sealed class EquipmentStartInput
{
    public Guid IdempotencyKey { get; set; }
    public long AdmissionId { get; set; }
    public int EquipmentId { get; set; }
    public string? Identifier { get; set; }
    /// <summary>Empty = now. Local time of the hospital.</summary>
    public DateTime? StartLocal { get; set; }
    public string? Notes { get; set; }
}

public sealed class PackageInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Excluded { get; set; }
    public int ServiceItemId { get; set; }
    public List<int> IncludedServiceIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public interface IIpdBillingService
{
    Task<IpdLedger?> LedgerAsync(long admissionId, CancellationToken ct = default);
    Task<IReadOnlyList<Charge>> PatientChargesAsync(long patientId, CancellationToken ct = default);

    Task<ServiceResult<PatientAdvance>> ReceiveAdvanceAsync(AdvanceInput input, CancellationToken ct = default);
    Task<ServiceResult<decimal>> ApplyAdvanceAsync(long admissionId, Guid idempotencyKey, CancellationToken ct = default);
    Task<ServiceResult> RefundAdvanceAsync(long advanceId, decimal amount, string? reason, int paymentMethodId, CancellationToken ct = default);

    /// <summary>Remaining balance after advances, and whether an authorised override exists.</summary>
    Task<(decimal Due, bool Overridden)> ClearanceAsync(long admissionId, CancellationToken ct = default);
    Task<ServiceResult> OverrideClearanceAsync(long admissionId, string? reason, CancellationToken ct = default);

    /// <summary>Receives a (partial) payment against the admission account – applied to the oldest open charges first. Returns the receipt.</summary>
    Task<ServiceResult<Receipt?>> ReceivePaymentAsync(long admissionId, Guid idempotencyKey, decimal amount, int paymentMethodId, int? counterId, string? reference, CancellationToken ct = default);
    Task<ServiceResult> PayAdmissionFeeAsync(long admissionId, Guid idempotencyKey, int paymentMethodId, int? counterId, string? reference, CancellationToken ct = default);
    Task<ServiceResult> WaiveAdmissionFeeAsync(long admissionId, string? reason, CancellationToken ct = default);
    /// <summary>Update 11: outstanding amount of one medicine issuance on the admission's account (0 = settled / nothing billed).</summary>
    Task<decimal> MedicineIssueDueAsync(long admissionId, long issueId, CancellationToken ct = default);
    /// <summary>Update 11: "pay now" for an IPD medicine issuance – a Phase 3 receipt against exactly that issuance's charges (partial allowed).
    /// Without it the amount simply stays outstanding on the IPD account and the discharge statement.</summary>
    Task<ServiceResult<string>> PayMedicineIssueAsync(long admissionId, long issueId, Guid idempotencyKey, int paymentMethodId, int? counterId, decimal amount, string? reference, CancellationToken ct = default);

    Task<IReadOnlyList<NicuEquipment>> EquipmentCatalogueAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<EquipmentUsage>> StartEquipmentAsync(EquipmentStartInput input, CancellationToken ct = default);
    Task<ServiceResult<EquipmentUsage>> EndEquipmentAsync(long usageId, DateTime? endLocal, string? reason, CancellationToken ct = default);
    /// <summary>Ends every running equipment period of the admission (discharge, transfer out) – inside the caller's transaction.</summary>
    Task<int> EndAllEquipmentAsync(long admissionId, string reason, CancellationToken ct = default);

    Task<IReadOnlyList<ServicePackage>> PackagesAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SavePackageAsync(int? id, PackageInput input, CancellationToken ct = default);
    Task<ServiceResult> ApplyPackageAsync(long admissionId, int packageId, CancellationToken ct = default);
    Task<ServiceResult> RemovePackageAsync(long admissionPackageId, string? reason, CancellationToken ct = default);
    /// <summary>Name of the active package of the admission that includes the service (null = billed normally).</summary>
    Task<string?> PackageCoverAsync(long admissionId, int serviceItemId, CancellationToken ct = default);
}

public sealed class IpdBillingService : IIpdBillingService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _finance;
    private readonly IBillingService _billing;

    public IpdBillingService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IFinanceSetupService finance, IBillingService billing)
    {
        _db = db; _audit = audit; _ctx = ctx; _clock = clock; _finance = finance; _billing = billing;
    }

    private static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    private static readonly AdmissionStatus[] Open =
        { AdmissionStatus.Registered, AdmissionStatus.Admitted, AdmissionStatus.UnderTreatment, AdmissionStatus.Transferred, AdmissionStatus.DischargeRequested };

    // =====================================================================================
    // Ledger
    // =====================================================================================

    public async Task<IpdLedger?> LedgerAsync(long admissionId, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().Include(x => x.Patient).Include(x => x.CurrentWard).Include(x => x.CurrentBed).FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return null;
        var charges = await _db.Charges.AsNoTracking().Include(c => c.Department).Include(c => c.ServiceItem).Where(c => c.IpdAdmissionId == admissionId).OrderBy(c => c.CreatedUtc).ThenBy(c => c.Id).ToListAsync(ct);
        var ids = charges.Select(c => c.Id).ToList();
        var bed = await _db.IpdBedCharges.AsNoTracking().Where(b => b.AdmissionId == admissionId).OrderBy(b => b.Night).ToListAsync(ct);
        var equipment = await _db.EquipmentUsages.AsNoTracking().Where(u => u.AdmissionId == admissionId).OrderBy(u => u.StartUtc).ToListAsync(ct);
        var packages = await _db.AdmissionPackages.AsNoTracking().Include(p => p.Package).Where(p => p.AdmissionId == admissionId).OrderBy(p => p.AppliedUtc).ToListAsync(ct);
        var admissionService = (await _db.IpdSettings.AsNoTracking().FirstOrDefaultAsync(ct))?.AdmissionServiceItemId;
        var bedIds = bed.Select(b => b.ChargeId).ToHashSet();
        var eqIds = equipment.Where(e => e.ChargeId != null).Select(e => e.ChargeId!.Value).ToHashSet();
        var pkgIds = packages.Where(p => p.ChargeId != null).Select(p => p.ChargeId!.Value).ToHashSet();
        LedgerGroup Group(Charge c) =>
            c.Id == a.AdmissionFeeChargeId || (a.AdmissionFeeChargeId is null && c.ServiceItemId == admissionService) ? LedgerGroup.AdmissionFee
            : bedIds.Contains(c.Id) ? LedgerGroup.WardBed
            : eqIds.Contains(c.Id) ? LedgerGroup.NicuEquipment
            : pkgIds.Contains(c.Id) ? LedgerGroup.Package
            : c.PharmacyIssueId != null || c.ServiceKind == ServiceKind.Pharmacy ? LedgerGroup.Pharmacy
            : c.LabOrderId != null || c.ServiceKind == ServiceKind.Laboratory ? LedgerGroup.Laboratory
            : c.RadiologyOrderId != null || c.ServiceKind is ServiceKind.XRay or ServiceKind.Ultrasound ? LedgerGroup.Radiology
            : c.SurgeryId != null ? LedgerGroup.OperationTheatre
            : LedgerGroup.Services;
        var lines = charges.Select(c => new LedgerLine(c, Group(c))).ToList();
        var payments = await _db.ReceiptLines.AsNoTracking().Where(l => ids.Contains(l.ChargeId))
            .GroupBy(l => new { l.ReceiptId, l.Receipt!.ReceiptNumber, l.Receipt.CreatedUtc, Method = l.Receipt.PaymentMethod!.Name, Code = l.Receipt.PaymentMethod.Code, l.Receipt.CashierName, l.Receipt.Status })
            .Select(g => new LedgerPayment(g.Key.ReceiptId, g.Key.ReceiptNumber, g.Key.CreatedUtc, g.Key.Method, g.Key.CashierName, g.Sum(x => x.Amount), g.Key.Code == BillingService.AdvanceMethodCode, g.Key.Status))
            .ToListAsync(ct);
        var refunds = await _db.Refunds.AsNoTracking().Where(r => _db.ReceiptLines.Any(l => l.ReceiptId == r.ReceiptId && ids.Contains(l.ChargeId))).OrderBy(r => r.Id).ToListAsync(ct);
        var advances = await _db.PatientAdvances.AsNoTracking().Include(x => x.PaymentMethod).Where(x => x.AdmissionId == admissionId).OrderBy(x => x.Id).ToListAsync(ct);
        var overrides = await _db.DischargeClearanceOverrides.AsNoTracking().Where(o => o.AdmissionId == admissionId).OrderBy(o => o.Id).ToListAsync(ct);
        return new IpdLedger(a, lines, payments.OrderBy(p => p.CreatedUtc).ToList(), refunds, advances, overrides, equipment, bed, packages);
    }

    public async Task<IReadOnlyList<Charge>> PatientChargesAsync(long patientId, CancellationToken ct = default) =>
        await _db.Charges.AsNoTracking().Include(c => c.Department).Where(c => c.PatientId == patientId).OrderByDescending(c => c.CreatedUtc).ThenByDescending(c => c.Id).Take(1000).ToListAsync(ct);

    // =====================================================================================
    // Advance deposits
    // =====================================================================================

    private async Task<int?> CounterAsync(int? chosen, CancellationToken ct)
    {
        if (_ctx.UserId is not int uid) return null;
        var isAdmin = await _db.Users.AnyAsync(u => u.Id == uid && u.IsSystemAdministrator, ct);
        if (chosen is int c)
            return isAdmin || await _db.CounterAssignments.AnyAsync(x => x.CounterId == c && x.UserId == uid && x.EndedUtc == null, ct) ? c : null;
        var mine = await _db.CounterAssignments.Where(x => x.UserId == uid && x.EndedUtc == null && x.Counter!.IsActive).Select(x => (int?)x.CounterId).FirstOrDefaultAsync(ct);
        if (mine is not null) return mine;
        return isAdmin ? await _db.Counters.Where(x => x.IsActive).OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) : null;
    }

    public async Task<ServiceResult<PatientAdvance>> ReceiveAdvanceAsync(AdvanceInput i, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<PatientAdvance>.Fail("The form has expired. Please reload the page.");
        if (await _db.PatientAdvances.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == i.IdempotencyKey, ct) is { } dup) return ServiceResult<PatientAdvance>.Ok(dup);
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == i.AdmissionId, ct);
        if (a is null) return ServiceResult<PatientAdvance>.Fail("Admission not found.");
        if (a.Status == AdmissionStatus.Cancelled) return ServiceResult<PatientAdvance>.Fail("This admission was cancelled.");
        if (i.Amount <= 0 || i.Amount > 10_000_000 || !FinanceMath.IsMoney(i.Amount)) return ServiceResult<PatientAdvance>.Fail("Enter a valid amount (more than 0, 2 decimals).");
        var method = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == i.PaymentMethodId && m.IsActive, ct);
        if (method is null) return ServiceResult<PatientAdvance>.Fail("Select a payment method.");
        if (method.RequiresReference && T(i.Reference, 100) is null) return ServiceResult<PatientAdvance>.Fail($"Enter the {method.Name} reference.");
        var counter = await CounterAsync(i.CounterId, ct);
        if (counter is null) return ServiceResult<PatientAdvance>.Fail("You are not assigned to a cash counter. Ask the administrator to assign you (Finance → Counters).");
        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"ADV|{a.PatientId}", ct);
        if (await _db.PatientAdvances.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == i.IdempotencyKey, ct) is { } dup2) return ServiceResult<PatientAdvance>.Ok(dup2);
        var adv = new PatientAdvance
        {
            Number = await FinanceDb.NextNumberAsync(_db, HmsDbContext.AdvanceSequence, "ADV", today, ct), IdempotencyKey = i.IdempotencyKey, PatientId = a.PatientId,
            AdmissionId = a.Id, Amount = i.Amount, PaymentMethodId = method.Id, CounterId = counter.Value, CashierUserId = _ctx.UserId!.Value, CashierUsername = _ctx.Username ?? "",
            Reference = T(i.Reference, 100), Notes = T(i.Notes, 300), BusinessDate = today, CreatedUtc = now
        };
        _db.PatientAdvances.Add(adv);
        await _db.SaveChangesAsync(ct);
        _db.PatientAdvanceEntries.Add(new PatientAdvanceEntry { AdvanceId = adv.Id, EntryType = AdvanceEntryType.Received, Amount = adv.Amount, Reference = adv.Number, Note = adv.Notes, CreatedUtc = now, UserId = _ctx.UserId, Username = _ctx.Username });
        _db.CashierLedger.Add(new CashierLedgerEntry
        {
            UserId = adv.CashierUserId, CounterId = adv.CounterId, EntryType = CashierEntryType.AdvanceReceived, Amount = adv.Amount, PaymentMethodId = method.Id,
            AffectsCashInHand = method.CountsAsCashInHand, AdvanceId = adv.Id, Reference = adv.Number, Description = $"Advance {adv.Number} for {a.AdmissionNumber} ({method.Name})",
            BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        });
        _audit.Add(AuditActions.IpdAdvanceReceived, "IpdAdmission", a.AdmissionNumber, $"Advance {adv.Number}: {adv.Amount:N2} {method.Name} received for {a.AdmissionNumber}",
            newValues: new { adv.Number, adv.Amount, Method = method.Name, adv.Reference });
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_PatientAdvances_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<PatientAdvance>.Ok(await _db.PatientAdvances.AsNoTracking().FirstAsync(x => x.IdempotencyKey == i.IdempotencyKey, ct));
        }
        return ServiceResult<PatientAdvance>.Ok(adv);
    }

    public async Task<ServiceResult<decimal>> ApplyAdvanceAsync(long admissionId, Guid key, CancellationToken ct = default)
    {
        if (key == Guid.Empty) return ServiceResult<decimal>.Fail("The form has expired. Please reload the page.");
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult<decimal>.Fail("Admission not found.");
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"ADV|{a.PatientId}", ct);
        // A repeated click finds the receipt of the first one.
        if (await _db.Receipts.AsNoTracking().AnyAsync(r => r.IdempotencyKey == key, ct)) return ServiceResult<decimal>.Ok(0);
        var advances = await _db.PatientAdvances.Where(x => x.AdmissionId == admissionId && x.Status == AdvanceStatus.Valid).OrderBy(x => x.Id).ToListAsync(ct);
        var available = advances.Sum(x => x.Available);
        if (available <= 0) return ServiceResult<decimal>.Fail("There is no unused advance on this admission.");
        var open = await _db.Charges.AsNoTracking().Where(c => c.IpdAdmissionId == admissionId && c.Status != ChargeStatus.Cancelled && c.NetAmount > c.PaidAmount)
            .OrderBy(c => c.CreatedUtc).ThenBy(c => c.Id).ToListAsync(ct);
        var method = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Code == BillingService.AdvanceMethodCode, ct);
        if (method is null) return ServiceResult<decimal>.Fail("The advance payment method is missing – restart the server once to create it.");
        var input = new CollectInput
        {
            IdempotencyKey = key, PatientId = a.PatientId, CounterId = advances.First(x => x.Available > 0).CounterId, PaymentMethodId = method.Id,
            Notes = $"Adjusted from advance deposit(s) – {a.AdmissionNumber}", AdvanceAdjustment = true
        };
        var left = available;
        foreach (var c in open)
        {
            if (left <= 0) break;
            var due = c.NetAmount - c.PaidAmount;
            if (due > left && !c.AllowPartialPayment) continue;
            var pay = Math.Min(left, due);
            input.Balances.Add(new BalancePayment { ChargeId = c.Id, PayNow = pay });
            left -= pay;
        }
        if (input.Balances.Count == 0) return ServiceResult<decimal>.Fail("There is no outstanding charge to adjust the advance against.");
        var r = await _billing.CollectAsync(input, canDiscount: false, ct);
        if (!r.Succeeded || r.Value?.Receipt is not { } receipt) { await tx.RollbackAsync(CancellationToken.None); _db.ChangeTracker.Clear(); return ServiceResult<decimal>.Fail(r.Errors.ToArray()); }
        var applied = receipt.AmountPaid;
        var rest = applied;
        var now = _clock.UtcNow;
        foreach (var adv in advances)
        {
            if (rest <= 0) break;
            var use = Math.Min(rest, adv.Available);
            if (use <= 0) continue;
            adv.AppliedAmount += use;
            rest -= use;
            _db.PatientAdvanceEntries.Add(new PatientAdvanceEntry { AdvanceId = adv.Id, EntryType = AdvanceEntryType.Applied, Amount = use, ReceiptId = receipt.Id, Reference = receipt.ReceiptNumber, CreatedUtc = now, UserId = _ctx.UserId, Username = _ctx.Username });
        }
        _audit.Add(AuditActions.IpdAdvanceApplied, "IpdAdmission", a.AdmissionNumber, $"{applied:N2} of the advance adjusted against the bill of {a.AdmissionNumber} (receipt {receipt.ReceiptNumber})");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<decimal>.Fail("The advance was changed by someone else. Please reload.");
        }
        return ServiceResult<decimal>.Ok(applied);
    }

    public async Task<ServiceResult> RefundAdvanceAsync(long advanceId, decimal amount, string? reason, int paymentMethodId, CancellationToken ct = default)
    {
        if (T(reason, 300) is not { } why) return ServiceResult.Fail("Enter the reason for returning the advance.");
        if (amount <= 0 || !FinanceMath.IsMoney(amount)) return ServiceResult.Fail("Enter a valid amount.");
        var method = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == paymentMethodId && m.IsActive, ct);
        if (method is null) return ServiceResult.Fail("Select how the money is returned.");
        var pid = await _db.PatientAdvances.Where(x => x.Id == advanceId).Select(x => (long?)x.PatientId).FirstOrDefaultAsync(ct);
        if (pid is null) return ServiceResult.Fail("Advance not found.");
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"ADV|{pid}", ct);
        var adv = await _db.PatientAdvances.FirstAsync(x => x.Id == advanceId, ct);
        if (amount > adv.Available) return ServiceResult.Fail($"Only {adv.Available:N2} of {adv.Number} is unused.");
        var counter = await CounterAsync(null, ct);
        if (counter is null) return ServiceResult.Fail("You are not assigned to a cash counter.");
        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        adv.RefundedAmount += amount;
        _db.PatientAdvanceEntries.Add(new PatientAdvanceEntry { AdvanceId = adv.Id, EntryType = AdvanceEntryType.Refunded, Amount = amount, Reference = adv.Number, Note = why, CreatedUtc = now, UserId = _ctx.UserId, Username = _ctx.Username });
        _db.CashierLedger.Add(new CashierLedgerEntry
        {
            UserId = _ctx.UserId!.Value, CounterId = counter, EntryType = CashierEntryType.AdvanceRefunded, Amount = -amount, PaymentMethodId = method.Id,
            AffectsCashInHand = method.CountsAsCashInHand, AdvanceId = adv.Id, Reference = adv.Number, Description = $"Advance {adv.Number} returned ({method.Name}): {why}",
            BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        });
        _audit.Add(AuditActions.IpdAdvanceRefunded, "PatientAdvance", adv.Number, $"{amount:N2} of advance {adv.Number} returned to the patient ({method.Name}): {why}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The advance was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Financial clearance
    // =====================================================================================

    public async Task<(decimal Due, bool Overridden)> ClearanceAsync(long admissionId, CancellationToken ct = default)
    {
        var outstanding = await _db.Charges.Where(c => c.IpdAdmissionId == admissionId && c.Status != ChargeStatus.Cancelled).SumAsync(c => (decimal?)(c.NetAmount - c.PaidAmount), ct) ?? 0;
        var advance = await _db.PatientAdvances.Where(x => x.AdmissionId == admissionId && x.Status == AdvanceStatus.Valid).SumAsync(x => (decimal?)(x.Amount - x.AppliedAmount - x.RefundedAmount), ct) ?? 0;
        var overridden = await _db.DischargeClearanceOverrides.AnyAsync(o => o.AdmissionId == admissionId, ct);
        return (outstanding - advance, overridden);
    }

    public async Task<ServiceResult> OverrideClearanceAsync(long admissionId, string? reason, CancellationToken ct = default)
    {
        if (T(reason, 300) is not { } why) return ServiceResult.Fail("Enter the reason for allowing discharge with a balance.");
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!Open.Contains(a.Status)) return ServiceResult.Fail("This admission is already closed.");
        var (due, _) = await ClearanceAsync(admissionId, ct);
        if (due <= 0) return ServiceResult.Fail("There is no outstanding balance – no override is needed.");
        _db.DischargeClearanceOverrides.Add(new DischargeClearanceOverride { AdmissionId = a.Id, Outstanding = due, Reason = why, CreatedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username });
        _audit.Add(AuditActions.IpdClearanceOverride, "IpdAdmission", a.AdmissionNumber, $"Financial clearance overridden for {a.AdmissionNumber} with {due:N2} outstanding: {why}",
            newValues: new { Outstanding = due, Reason = why });
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Admission fee
    // =====================================================================================

    private async Task<Charge?> AdmissionFeeChargeAsync(IpdAdmission a, bool track, CancellationToken ct)
    {
        var q = track ? _db.Charges : _db.Charges.AsNoTracking();
        if (a.AdmissionFeeChargeId is long id) return await q.FirstOrDefaultAsync(c => c.Id == id, ct);
        var svc = (await _db.IpdSettings.AsNoTracking().FirstOrDefaultAsync(ct))?.AdmissionServiceItemId;
        return svc is null ? null : await q.Where(c => c.IpdAdmissionId == a.Id && c.ServiceItemId == svc && c.Status != ChargeStatus.Cancelled).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
    }

    public async Task<ServiceResult<Receipt?>> ReceivePaymentAsync(long admissionId, Guid key, decimal amount, int paymentMethodId, int? counterId, string? reference, CancellationToken ct = default)
    {
        if (key == Guid.Empty) return ServiceResult<Receipt?>.Fail("The form has expired. Please reload the page.");
        if (await _db.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == key, ct) is { } dup) return ServiceResult<Receipt?>.Ok(dup);
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult<Receipt?>.Fail("Admission not found.");
        if (amount <= 0 || !FinanceMath.IsMoney(amount)) return ServiceResult<Receipt?>.Fail("Enter the amount received.");
        var counter = await CounterAsync(counterId, ct);
        if (counter is null) return ServiceResult<Receipt?>.Fail("You are not assigned to a cash counter. Ask the administrator to assign you (Finance → Counters).");
        var open = await _db.Charges.AsNoTracking().Where(c => c.IpdAdmissionId == admissionId && c.Status != ChargeStatus.Cancelled && c.NetAmount > c.PaidAmount)
            .OrderBy(c => c.CreatedUtc).ThenBy(c => c.Id).ToListAsync(ct);
        var due = open.Sum(c => c.NetAmount - c.PaidAmount);
        if (due <= 0) return ServiceResult<Receipt?>.Fail("Nothing is outstanding on this admission.");
        if (amount > due) return ServiceResult<Receipt?>.Fail($"Only {due:N2} is outstanding. Take the extra amount as an advance deposit instead.");
        var input = new CollectInput { IdempotencyKey = key, PatientId = a.PatientId, CounterId = counter.Value, PaymentMethodId = paymentMethodId, PaymentReference = T(reference, 100), Notes = $"Inpatient payment – {a.AdmissionNumber}" };
        var left = amount;
        foreach (var c in open)
        {
            if (left <= 0) break;
            var d = c.NetAmount - c.PaidAmount;
            if (d > left && !c.AllowPartialPayment) continue;
            var pay = Math.Min(left, d);
            input.Balances.Add(new BalancePayment { ChargeId = c.Id, PayNow = pay });
            left -= pay;
        }
        if (left > 0) return ServiceResult<Receipt?>.Fail("Some charges must be paid in full – enter a larger amount or pay them separately.");
        var r = await _billing.CollectAsync(input, canDiscount: false, ct);
        return r.Succeeded ? ServiceResult<Receipt?>.Ok(r.Value!.Receipt) : ServiceResult<Receipt?>.Fail(r.Errors.ToArray());
    }

    public async Task<ServiceResult> PayAdmissionFeeAsync(long admissionId, Guid key, int paymentMethodId, int? counterId, string? reference, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        var c = await AdmissionFeeChargeAsync(a, false, ct);
        if (c is null) return ServiceResult.Fail("No admission fee is charged for this admission.");
        if (c.NetAmount - c.PaidAmount <= 0) return ServiceResult.Fail("The admission fee is already settled.");
        var counter = await CounterAsync(counterId, ct);
        if (counter is null) return ServiceResult.Fail("You are not assigned to a cash counter. Ask the administrator to assign you (Finance → Counters).");
        var r = await _billing.CollectAsync(new CollectInput
        {
            IdempotencyKey = key, PatientId = a.PatientId, CounterId = counter.Value, PaymentMethodId = paymentMethodId, PaymentReference = T(reference, 100),
            Notes = $"Admission fee – {a.AdmissionNumber}", Balances = { new BalancePayment { ChargeId = c.Id, PayNow = c.NetAmount - c.PaidAmount } }
        }, canDiscount: false, ct);
        if (!r.Succeeded) return ServiceResult.Fail(r.Errors.ToArray());
        if (!r.Value!.WasDuplicate)
            await _audit.LogAsync(AuditActions.IpdAdmissionFeePaid, "IpdAdmission", a.AdmissionNumber, $"Admission fee {c.NetAmount:N2} of {a.AdmissionNumber} paid (receipt {r.Value.Receipt?.ReceiptNumber})", ct: ct);
        return ServiceResult.Ok();
    }

    public async Task<decimal> MedicineIssueDueAsync(long admissionId, long issueId, CancellationToken ct = default) =>
        await _db.Charges.AsNoTracking().Where(c => c.PharmacyIssueId == issueId && c.IpdAdmissionId == admissionId && c.Status != ChargeStatus.Cancelled)
            .SumAsync(c => (decimal?)(c.NetAmount - c.PaidAmount), ct) ?? 0m;

    public async Task<ServiceResult<string>> PayMedicineIssueAsync(long admissionId, long issueId, Guid key, int paymentMethodId, int? counterId, decimal amount, string? reference, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult<string>.Fail("Admission not found.");
        var issue = await _db.PatientMedicineIssues.AsNoTracking().FirstOrDefaultAsync(i => i.Id == issueId && i.IpdAdmissionId == admissionId, ct);
        if (issue is null) return ServiceResult<string>.Fail("The medicine issuance does not belong to this admission.");
        var charges = await _db.Charges.AsNoTracking().Where(c => c.PharmacyIssueId == issueId && c.IpdAdmissionId == admissionId && c.Status != ChargeStatus.Cancelled)
            .OrderBy(c => c.Id).ToListAsync(ct);
        var due = charges.Sum(c => c.NetAmount - c.PaidAmount);
        if (due <= 0) return ServiceResult<string>.Fail($"Nothing is outstanding on {issue.Number}.");
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0) return ServiceResult<string>.Fail("Enter the amount received.");
        if (amount > due) return ServiceResult<string>.Fail($"The amount is more than the outstanding {due:N2} of {issue.Number}.");
        var counter = await CounterAsync(counterId, ct);
        if (counter is null) return ServiceResult<string>.Fail("You are not assigned to a cash counter. Ask the administrator to assign you (Finance → Counters).");
        var input = new CollectInput
        {
            IdempotencyKey = key, PatientId = a.PatientId, CounterId = counter.Value, PaymentMethodId = paymentMethodId, PaymentReference = T(reference, 100),
            Notes = $"Medicines {issue.Number} – {a.AdmissionNumber}"
        };
        var left = amount;
        foreach (var c in charges)
        {
            var open = c.NetAmount - c.PaidAmount;
            if (open <= 0 || left <= 0) continue;
            var pay = Math.Min(open, left);
            input.Balances.Add(new BalancePayment { ChargeId = c.Id, PayNow = pay });
            left -= pay;
        }
        var r = await _billing.CollectAsync(input, canDiscount: false, ct);
        if (!r.Succeeded) return ServiceResult<string>.Fail(r.Errors.ToArray());
        if (!r.Value!.WasDuplicate)
            await _audit.LogAsync(AuditActions.IpdMedicinePaid, "PatientMedicineIssue", issue.Number,
                $"{amount:N2} of {due:N2} received for medicines {issue.Number} ({a.AdmissionNumber}), receipt {r.Value.Receipt?.ReceiptNumber}", ct: ct);
        return ServiceResult<string>.Ok(r.Value.Receipt?.ReceiptNumber ?? "");
    }

    public async Task<ServiceResult> WaiveAdmissionFeeAsync(long admissionId, string? reason, CancellationToken ct = default)
    {
        if (T(reason, 200) is not { } why) return ServiceResult.Fail("Enter the reason for the free admission.");
        await using var tx = await TxScope.BeginAsync(_db, ct);
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        var c0 = await AdmissionFeeChargeAsync(a, false, ct);
        if (c0 is null) return ServiceResult.Fail("No admission fee is charged for this admission.");
        var c = await _db.Charges.FromSqlRaw("SELECT c.*, c.xmin FROM \"Charges\" c WHERE c.\"Id\" = {0} FOR UPDATE", c0.Id).FirstAsync(ct);
        var due = c.NetAmount - c.PaidAmount;
        if (due <= 0) return ServiceResult.Fail("The admission fee is already settled – a paid fee is returned through a refund.");
        var old = new { c.Fee, c.Discount, c.NetAmount, c.Status };
        c.Discount += due;
        c.NetAmount = c.Fee - c.Discount;
        c.DiscountReason = T($"Free admission: {why}", 200);
        c.Status = c.NetAmount <= c.PaidAmount ? ChargeStatus.Paid : c.PaidAmount > 0 ? ChargeStatus.PartiallyPaid : ChargeStatus.Unpaid;
        _audit.Add(AuditActions.IpdAdmissionFeeWaived, "IpdAdmission", a.AdmissionNumber, $"Admission fee of {a.AdmissionNumber} waived ({due:N2}): {why}", old, new { c.Fee, c.Discount, c.NetAmount, c.Status });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // NICU equipment
    // =====================================================================================

    public async Task<IReadOnlyList<NicuEquipment>> EquipmentCatalogueAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.NicuEquipment.AsNoTracking().Include(e => e.ServiceItem).Where(e => !activeOnly || e.IsActive).OrderBy(e => e.SortOrder).ThenBy(e => e.Name).ToListAsync(ct);

    private async Task<DateTime?> ToUtcAsync(DateTime? local)
    {
        if (local is null) return null;
        var zone = await _clock.ZoneAsync();
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified), zone);
    }

    public async Task<ServiceResult<EquipmentUsage>> StartEquipmentAsync(EquipmentStartInput i, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<EquipmentUsage>.Fail("The form has expired. Please reload the page.");
        if (await _db.EquipmentUsages.AsNoTracking().FirstOrDefaultAsync(u => u.IdempotencyKey == i.IdempotencyKey, ct) is { } dup) return ServiceResult<EquipmentUsage>.Ok(dup);
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == i.AdmissionId, ct);
        if (a is null) return ServiceResult<EquipmentUsage>.Fail("Admission not found.");
        if (!Open.Contains(a.Status)) return ServiceResult<EquipmentUsage>.Fail("Equipment can only be started for an active admission.");
        var eq = await _db.NicuEquipment.AsNoTracking().Include(e => e.ServiceItem).FirstOrDefaultAsync(e => e.Id == i.EquipmentId && e.IsActive, ct);
        if (eq is null) return ServiceResult<EquipmentUsage>.Fail("Select the equipment.");
        var now = _clock.UtcNow;
        var start = await ToUtcAsync(i.StartLocal) ?? now;
        if (start > now.AddMinutes(5)) return ServiceResult<EquipmentUsage>.Fail("The start time cannot be in the future.");
        if (start < a.AdmittedUtc.AddMinutes(-5)) return ServiceResult<EquipmentUsage>.Fail("The start time is before the admission.");
        var identifier = T(i.Identifier, 50);
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"EQUIP|{eq.Id}", ct);
        if (await _db.EquipmentUsages.AnyAsync(u => u.AdmissionId == a.Id && u.EquipmentId == eq.Id && u.Status == EquipmentUsageStatus.Active, ct))
            return ServiceResult<EquipmentUsage>.Fail($"{eq.Name} is already running on this patient – stop it (or use Replace) before starting another period.");
        if (identifier is not null && await _db.EquipmentUsages.Include(u => u.Admission).Where(u => u.EquipmentId == eq.Id && u.Identifier == identifier && u.Status == EquipmentUsageStatus.Active)
                .Select(u => u.Admission!.AdmissionNumber).FirstOrDefaultAsync(ct) is { } other)
            return ServiceResult<EquipmentUsage>.Fail($"{eq.Name} {identifier} is in use on {other}. Stop it there first (transfer).");
        // No overlap with an earlier period of the same equipment type on this patient.
        if (await _db.EquipmentUsages.AnyAsync(u => u.AdmissionId == a.Id && u.EquipmentId == eq.Id && u.Status == EquipmentUsageStatus.Ended && u.EndUtc > start, ct))
            return ServiceResult<EquipmentUsage>.Fail($"The start time overlaps an earlier {eq.Name} period of this patient.");
        var unit = await _finance.FeeOnAsync(eq.ServiceItemId, await _clock.LocalDateOfAsync(start), ct);
        var u = new EquipmentUsage
        {
            IdempotencyKey = i.IdempotencyKey, AdmissionId = a.Id, PatientId = a.PatientId, EquipmentId = eq.Id, EquipmentName = eq.Name, Identifier = identifier,
            StartUtc = start, ChargeUnit = eq.ServiceItem!.ChargeUnit, UnitPrice = unit, Notes = T(i.Notes, 300),
            StartedByUserId = _ctx.UserId, StartedByUsername = _ctx.Username
        };
        _db.EquipmentUsages.Add(u);
        _audit.Add(AuditActions.IpdEquipmentStarted, "IpdAdmission", a.AdmissionNumber, $"{eq.Name}{(identifier is null ? "" : " " + identifier)} started on {a.AdmissionNumber} at {unit:N2} {eq.ServiceItem.ChargeUnit}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_EquipmentUsages_ActivePerAdmission") || FinanceDb.IsUniqueViolation(ex, "UX_EquipmentUsages_ActiveDevice"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<EquipmentUsage>.Fail("This equipment was just started by someone else. Please reload.");
        }
        return ServiceResult<EquipmentUsage>.Ok(u);
    }

    /// <summary>Quantity charged for a period: hours (per hour), started days (per day) or one session / item.</summary>
    public static decimal UsageQuantity(ChargeUnit unit, DateTime start, DateTime end)
    {
        var hours = Math.Max(0, (end - start).TotalHours);
        return unit switch
        {
            ChargeUnit.PerHour => Math.Max(1, (decimal)Math.Ceiling(hours)),
            ChargeUnit.PerDay => Math.Max(1, (decimal)Math.Ceiling(hours / 24 - 1e-9)),
            _ => 1,
        };
    }

    public async Task<ServiceResult<EquipmentUsage>> EndEquipmentAsync(long usageId, DateTime? endLocal, string? reason, CancellationToken ct = default)
    {
        var end = await ToUtcAsync(endLocal) ?? _clock.UtcNow;
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"EQUSE|{usageId}", ct);
        var u = await _db.EquipmentUsages.FirstOrDefaultAsync(x => x.Id == usageId, ct);
        if (u is null) return ServiceResult<EquipmentUsage>.Fail("Equipment period not found.");
        if (u.Status != EquipmentUsageStatus.Active)
        {
            await _audit.LogAsync(AuditActions.IpdDuplicateChargeBlocked, "EquipmentUsage", u.Id.ToString(), $"Second stop of {u.EquipmentName} refused (already {u.Status}) – not charged twice", ct: ct);
            return ServiceResult<EquipmentUsage>.Fail($"{u.EquipmentName} was already stopped – it is not charged twice.");
        }
        if (end < u.StartUtc) return ServiceResult<EquipmentUsage>.Fail("The stop time is before the start time.");
        if (end > _clock.UtcNow.AddMinutes(5)) return ServiceResult<EquipmentUsage>.Fail("The stop time cannot be in the future.");
        await EndWithinAsync(u, end, T(reason, 300), ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<EquipmentUsage>.Ok(u);
    }

    private async Task EndWithinAsync(EquipmentUsage u, DateTime end, string? reason, CancellationToken ct)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().FirstAsync(x => x.Id == u.AdmissionId, ct);
        var eq = await _db.NicuEquipment.AsNoTracking().Include(e => e.ServiceItem).FirstAsync(e => e.Id == u.EquipmentId, ct);
        u.EndUtc = end;
        u.Status = EquipmentUsageStatus.Ended;
        u.EndReason = reason;
        u.EndedByUsername = _ctx.Username;
        u.Quantity = UsageQuantity(u.ChargeUnit, u.StartUtc, end);
        var fee = decimal.Round(u.UnitPrice * u.Quantity.Value, 2, MidpointRounding.AwayFromZero);
        var package = await PackageCoverAsync(a.Id, eq.ServiceItemId, ct);
        var unitText = u.ChargeUnit switch { ChargeUnit.PerHour => "h", ChargeUnit.PerDay => "day(s)", _ => "×" };
        var today = await _clock.TodayAsync();
        var c = new Charge
        {
            ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", today, ct),
            PatientId = a.PatientId, IpdAdmissionId = a.Id, ServiceItemId = eq.ServiceItemId, ServiceKind = eq.ServiceItem!.Kind, DepartmentId = eq.ServiceItem.DepartmentId,
            ServiceName = $"{eq.Name}{(u.Identifier is null ? "" : " " + u.Identifier)} – {u.Quantity:0.##} {unitText}", Quantity = u.Quantity.Value, UnitPrice = u.UnitPrice,
            Fee = fee, Discount = package is null ? 0 : fee, NetAmount = package is null ? fee : 0, AllowPartialPayment = true,
            DiscountReason = package is null ? null : $"Included in package {package}"[..Math.Min(200, $"Included in package {package}".Length)],
            DoctorShareType = DoctorShareType.HospitalKeepsAll, Status = package is null && fee > 0 ? ChargeStatus.Unpaid : ChargeStatus.Paid,
            BusinessDate = today, CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        };
        _db.Charges.Add(c);
        await _db.SaveChangesAsync(ct);
        u.ChargeId = c.Id;
        _audit.Add(AuditActions.IpdEquipmentEnded, "IpdAdmission", a.AdmissionNumber,
            $"{u.EquipmentName}{(u.Identifier is null ? "" : " " + u.Identifier)} stopped on {a.AdmissionNumber}: {u.Quantity:0.##} {unitText} × {u.UnitPrice:N2} = {fee:N2}" +
            (package is null ? "" : $" (included in {package} – not billed)") + (reason is null ? "" : $" – {reason}"));
    }

    public async Task<int> EndAllEquipmentAsync(long admissionId, string reason, CancellationToken ct = default)
    {
        var running = await _db.EquipmentUsages.Where(u => u.AdmissionId == admissionId && u.Status == EquipmentUsageStatus.Active).ToListAsync(ct);
        var now = _clock.UtcNow;
        foreach (var u in running) await EndWithinAsync(u, now, reason, ct);
        if (running.Count > 0) await _db.SaveChangesAsync(ct);
        return running.Count;
    }

    /// <summary>NICU equipment types of the standard Fees &amp; Services catalogue (added once each).</summary>
    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        var existing = (await db.NicuEquipment.Select(e => e.ServiceItemId).ToListAsync(ct)).ToHashSet();
        var sort = await db.NicuEquipment.MaxAsync(e => (int?)e.SortOrder, ct) ?? 0;
        foreach (var (code, name, _) in ServiceCatalogue.NicuEquipmentServices)
        {
            var svc = await db.ServiceItems.Where(s => s.Code == code).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
            if (svc is null || existing.Contains(svc.Value) || await db.NicuEquipment.AnyAsync(e => e.Code == code, ct)) continue;
            db.NicuEquipment.Add(new NicuEquipment { Code = code, Name = name, ServiceItemId = svc.Value, SortOrder = sort += 10, UpdatedUtc = now });
        }
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================================
    // Packages
    // =====================================================================================

    public async Task<IReadOnlyList<ServicePackage>> PackagesAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.ServicePackages.AsNoTracking().Include(p => p.ServiceItem).Include(p => p.Items).ThenInclude(i => i.ServiceItem)
            .Where(p => !activeOnly || p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SavePackageAsync(int? id, PackageInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code, 30)?.ToUpperInvariant();
        var name = T(i.Name, 150);
        if (code is null) errors.Add("Package code is required.");
        if (name is null) errors.Add("Package name is required.");
        if (!await _db.ServiceItems.AnyAsync(s => s.Id == i.ServiceItemId && s.IsActive, ct)) errors.Add("Select the service that carries the package price (Fees & Services).");
        var included = i.IncludedServiceIds.Where(x => x > 0 && x != i.ServiceItemId).Distinct().ToList();
        if (included.Count == 0) errors.Add("Select at least one included service.");
        if (await _db.ServiceItems.CountAsync(s => included.Contains(s.Id), ct) != included.Count) errors.Add("Unknown included service.");
        if (code is not null && await _db.ServicePackages.AnyAsync(p => p.Id != id && p.Code == code, ct)) errors.Add("Another package already uses this code.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var now = _clock.UtcNow;
        ServicePackage p;
        if (id is null) { p = new ServicePackage { CreatedUtc = now }; _db.ServicePackages.Add(p); }
        else p = await _db.ServicePackages.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        p.Code = code!; p.Name = name!; p.Description = T(i.Description, 500); p.Excluded = T(i.Excluded, 500); p.ServiceItemId = i.ServiceItemId; p.IsActive = i.IsActive; p.UpdatedUtc = now;
        p.Items.RemoveAll(x => !included.Contains(x.ServiceItemId));
        foreach (var s in included.Where(s => p.Items.All(x => x.ServiceItemId != s))) p.Items.Add(new ServicePackageItem { ServiceItemId = s });
        _audit.Add(AuditActions.IpdPackageSaved, "ServicePackage", p.Code, $"Package {p.Name} {(id is null ? "created" : "updated")} – {included.Count} included service(s)");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(p.Id);
    }

    public async Task<ServiceResult> ApplyPackageAsync(long admissionId, int packageId, CancellationToken ct = default)
    {
        var a = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == admissionId, ct);
        if (a is null) return ServiceResult.Fail("Admission not found.");
        if (!Open.Contains(a.Status)) return ServiceResult.Fail("Packages can only be applied to an active admission.");
        var p = await _db.ServicePackages.AsNoTracking().Include(x => x.ServiceItem).FirstOrDefaultAsync(x => x.Id == packageId && x.IsActive, ct);
        if (p is null) return ServiceResult.Fail("Select an active package.");
        await using var tx = await TxScope.BeginAsync(_db, ct);
        await FinanceDb.LockAsync(_db, $"PKG|{a.Id}", ct);
        if (await _db.AdmissionPackages.AnyAsync(x => x.AdmissionId == a.Id && x.PackageId == p.Id && x.IsActive, ct))
            return ServiceResult.Fail($"{p.Name} is already applied to this admission – it is not charged twice.");
        var today = await _clock.TodayAsync();
        var fee = await _finance.FeeOnAsync(p.ServiceItemId, today, ct);
        var c = new Charge
        {
            ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", today, ct), PatientId = a.PatientId, IpdAdmissionId = a.Id,
            ServiceItemId = p.ServiceItemId, ServiceKind = p.ServiceItem!.Kind, DepartmentId = p.ServiceItem.DepartmentId, ServiceName = $"Package: {p.Name}", Quantity = 1, UnitPrice = fee,
            Fee = fee, NetAmount = fee, AllowPartialPayment = true, DoctorShareType = DoctorShareType.HospitalKeepsAll, Status = fee == 0 ? ChargeStatus.Paid : ChargeStatus.Unpaid,
            BusinessDate = today, CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        };
        _db.Charges.Add(c);
        await _db.SaveChangesAsync(ct);
        _db.AdmissionPackages.Add(new AdmissionPackage { AdmissionId = a.Id, PackageId = p.Id, ChargeId = c.Id, AppliedUtc = _clock.UtcNow, AppliedBy = _ctx.Username });
        _audit.Add(AuditActions.IpdPackageApplied, "IpdAdmission", a.AdmissionNumber, $"Package {p.Name} ({fee:N2}) applied to {a.AdmissionNumber} – its included services are not billed separately");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemovePackageAsync(long admissionPackageId, string? reason, CancellationToken ct = default)
    {
        if (T(reason, 300) is not { } why) return ServiceResult.Fail("Enter the reason.");
        var ap = await _db.AdmissionPackages.Include(x => x.Package).FirstOrDefaultAsync(x => x.Id == admissionPackageId, ct);
        if (ap is null || !ap.IsActive) return ServiceResult.Fail("Package not found.");
        ap.IsActive = false; ap.RemovedUtc = _clock.UtcNow; ap.RemovedBy = _ctx.Username; ap.RemovedReason = why;
        _audit.Add(AuditActions.IpdPackageApplied, "AdmissionPackage", ap.Id.ToString(), $"Package {ap.Package!.Name} removed from the admission (later services are billed normally): {why}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<string?> PackageCoverAsync(long admissionId, int serviceItemId, CancellationToken ct = default) =>
        await _db.AdmissionPackages.Where(x => x.AdmissionId == admissionId && x.IsActive && x.Package!.Items.Any(i => i.ServiceItemId == serviceItemId))
            .Select(x => x.Package!.Name).FirstOrDefaultAsync(ct);
}
