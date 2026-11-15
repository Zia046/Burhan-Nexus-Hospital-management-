using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Pharmacy;

/// <summary>A requested medicine line (issue, sale or transfer). Without a batch the system picks FEFO batches.</summary>
public sealed class StockLineInput
{
    public int MedicineId { get; set; }
    public long? BatchId { get; set; }
    public int Quantity { get; set; }
    public string? Instructions { get; set; }
    /// <summary>Private: discount amount on this line (needs "Apply Discount").</summary>
    public decimal Discount { get; set; }
}

public sealed class ReceiveLineInput
{
    public int MedicineId { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public int Quantity { get; set; }
    // Private only
    public decimal? UnitCost { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? Discount { get; set; }
}

public sealed class ReceiveInput
{
    public Guid IdempotencyKey { get; set; }
    public int? SupplierId { get; set; }
    public string? SourceName { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AdditionalCharges { get; set; }
    public string? Notes { get; set; }
    public List<ReceiveLineInput> Lines { get; set; } = new();
}

public sealed class IssueInput
{
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public long? EncounterId { get; set; }
    /// <summary>Phase 7: medicines for an inpatient admission (Government: an active admission is enough).</summary>
    public long? IpdAdmissionId { get; set; }
    public string? Notes { get; set; }
    public string? DiscountReason { get; set; }
    public List<StockLineInput> Lines { get; set; } = new();
}

public sealed class SaleInput
{
    public Guid IdempotencyKey { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    public int CounterId { get; set; }
    public int PaymentMethodId { get; set; }
    public string? PaymentReference { get; set; }
    public string? DiscountReason { get; set; }
    public List<StockLineInput> Lines { get; set; } = new();
}

public sealed class TransferInput
{
    public Guid IdempotencyKey { get; set; }
    public int FromLocationId { get; set; }
    public int ToLocationId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public List<StockLineInput> Lines { get; set; } = new();
}

public sealed class AdjustmentInput
{
    public Guid IdempotencyKey { get; set; }
    public int LocationId { get; set; }
    public long BatchId { get; set; }
    public bool Increase { get; set; }
    public int Quantity { get; set; }
    public StockAdjustmentReason Reason { get; set; }
    public string? Note { get; set; }
}

public sealed class ReturnLineInput
{
    public long ItemId { get; set; }
    public int Quantity { get; set; }
    /// <summary>Undamaged, unopened and not expired: back into stock. Otherwise only recorded.</summary>
    public bool Restock { get; set; }
}

public sealed class ReturnInput
{
    public Guid IdempotencyKey { get; set; }
    public string? Reason { get; set; }
    public int? RefundPaymentMethodId { get; set; }
    public List<ReturnLineInput> Lines { get; set; } = new();
}

public sealed class SupplierReturnInput
{
    public Guid IdempotencyKey { get; set; }
    public int? SupplierId { get; set; }
    public string? SourceName { get; set; }
    public string? ReferenceInvoice { get; set; }
    public string? Reason { get; set; }
    public List<SupplierReturnLineInput> Lines { get; set; } = new();
}

public sealed class SupplierReturnLineInput
{
    public long BatchId { get; set; }
    public int Quantity { get; set; }
}

public sealed class SupplierPaymentInput
{
    public Guid IdempotencyKey { get; set; }
    public int SupplierId { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethodId { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
}

/// <summary>What the signed-in user may do (checked by the page, enforced again here).</summary>
public sealed record PharmacyRights(bool SelectBatch = false, bool Discount = false, bool ManagePurchases = false, bool ManagePrices = false, bool ApproveAdjustments = false);

public sealed record DocumentResult(long Id, string Number, bool WasDuplicate);

public interface IPharmacyService
{
    Task<ServiceResult<DocumentResult>> ReceiveAsync(ReceiveInput input, PharmacyRights rights, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> IssueAsync(IssueInput input, PharmacyRights rights, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> SellAsync(SaleInput input, PharmacyRights rights, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> TransferAsync(TransferInput input, PharmacyRights rights, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> RequestAdjustmentAsync(AdjustmentInput input, PharmacyRights rights, CancellationToken ct = default);
    Task<ServiceResult> DecideAdjustmentAsync(long id, bool approve, string? note, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> ReturnSaleAsync(long saleId, ReturnInput input, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> ReturnIssueAsync(long issueId, ReturnInput input, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> ReturnToSupplierAsync(SupplierReturnInput input, CancellationToken ct = default);
    Task<ServiceResult<DocumentResult>> PaySupplierAsync(SupplierPaymentInput input, CancellationToken ct = default);
}

public sealed class PharmacyService : IPharmacyService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IPharmacyCatalogService _catalog;
    private readonly IBillingService _billing;
    private readonly IFinanceSetupService _finance;

    public PharmacyService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IPharmacyCatalogService catalog,
        IBillingService billing, IFinanceSetupService finance)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _catalog = catalog;
        _billing = billing;
        _finance = finance;
    }

    private static string? T(string? v, int max) => PharmacyCatalogService.T(v, max);
    private static decimal R(decimal v) => FinanceMath.Round(v);

    /// <summary>Runs a stock operation in one transaction: success commits; any failure rolls everything back.</summary>
    private async Task<ServiceResult<DocumentResult>> RunAsync(string uniqueIndex, Func<Task<DocumentResult?>> existing, Func<Task<ServiceResult<DocumentResult>>> work, CancellationToken ct)
    {
        if (await existing() is { } dup)
        {
            await _audit.LogAsync(AuditActions.PharmDuplicateBlocked, "Pharmacy", dup.Number, $"Repeated submission of {dup.Number} ignored (stock was not changed twice)", ct: ct);
            return ServiceResult<DocumentResult>.Ok(dup with { WasDuplicate = true });
        }
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var r = await work();
            if (!r.Succeeded)
            {
                await tx.RollbackAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
                return r;
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return r;
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, uniqueIndex))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return await existing() is { } d ? ServiceResult<DocumentResult>.Ok(d with { WasDuplicate = true }) : ServiceResult<DocumentResult>.Fail("Please try again.");
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private static ServiceResult<DocumentResult> Fail(params string[] e) => ServiceResult<DocumentResult>.Fail(e);
    private static ServiceResult<DocumentResult> Fail(IEnumerable<string> e) => ServiceResult<DocumentResult>.Fail(e.Distinct().ToArray());

    private async Task<Dictionary<int, Medicine>> MedicinesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.Medicines.AsNoTracking().Include(m => m.DosageForm).Where(m => list.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
    }

    private Task<StockLocation> MainAsync(CancellationToken ct) => _catalog.MainLocationAsync(ct);

    /// <summary>A medicine line after FEFO allocation and (Private) pricing; one entry per batch.</summary>
    private sealed record Priced(Medicine Medicine, MedicineBatch Batch, int Quantity, bool FefoOverridden, string? Instructions,
        decimal? UnitPrice, decimal? UnitCost, decimal Discount, decimal? LineTotal);

    /// <summary>Allocates batches for all lines and prices them (Private). Returns errors instead of throwing.</summary>
    private async Task<(List<Priced> Lines, List<string> Errors)> PrepareAsync(int locationId, IReadOnlyList<StockLineInput> input, bool priced,
        PharmacyRights rights, string? discountReason, DateOnly today, CancellationToken ct)
    {
        var errors = new List<string>();
        var lines = input.Where(l => l.MedicineId > 0).ToList();
        if (lines.Count == 0) errors.Add("Add at least one medicine.");
        if (lines.Count > 50) errors.Add("At most 50 medicines per document.");
        var meds = await MedicinesAsync(lines.Select(l => l.MedicineId), ct);
        var result = new List<Priced>();
        // The same medicine twice: quantities are combined per batch by the allocation, so reject ambiguous duplicates.
        if (lines.GroupBy(l => (l.MedicineId, l.BatchId)).Any(g => g.Count() > 1)) errors.Add("The same medicine (and batch) is listed twice. Combine the quantities.");
        foreach (var l in lines)
        {
            if (!meds.TryGetValue(l.MedicineId, out var m)) { errors.Add("Medicine not found."); continue; }
            if (!m.IsActive) { errors.Add($"{m.Name} is inactive."); continue; }
            if (l.Quantity is <= 0 or > 1_000_000) { errors.Add($"Enter a valid quantity for {m.Name}."); continue; }
            if (l.Discount != 0 && !priced) { errors.Add("Discounts are not used here."); continue; }
            var (alloc, error) = await StockEngine.AllocateAsync(_db, locationId, m, l.Quantity, l.BatchId, rights.SelectBatch, today, ct);
            if (error is not null) { errors.Add(error); continue; }
            if (!priced)
            {
                result.AddRange(alloc!.Select(a => new Priced(m, a.Batch, a.Quantity, a.FefoOverridden, T(l.Instructions, 200), null, null, 0, null)));
                continue;
            }
            // Private pricing: the batch price (or the medicine price) as it is today; old documents keep their own prices.
            var parts = new List<(Allocation A, decimal Price, decimal? Cost, decimal Gross)>();
            foreach (var a in alloc!)
            {
                var price = a.Batch.SalePrice ?? m.SalePrice;
                if (price is null) { errors.Add($"No sale price is set for {m.Name}. Ask a user with “Manage Medicine Prices”."); break; }
                parts.Add((a, price.Value, a.Batch.UnitCost ?? m.PurchasePrice, R(price.Value * a.Quantity)));
            }
            if (parts.Count != alloc.Count) continue;
            var gross = parts.Sum(p => p.Gross);
            var discount = l.Discount;
            if (discount != 0)
            {
                if (!rights.Discount) { errors.Add("You are not allowed to give discounts."); continue; }
                if (!m.AllowDiscount) { errors.Add($"Discounts are not allowed on {m.Name}."); continue; }
                var max = R(gross * m.MaxDiscountPercent / 100);
                if (discount < 0 || discount > max || !FinanceMath.IsMoney(discount)) { errors.Add($"Discount on {m.Name} must be between 0 and {max:N2} ({m.MaxDiscountPercent:0.##} %)."); continue; }
                if (T(discountReason, 200) is null) { errors.Add("Enter the reason for the discount."); continue; }
            }
            // Split the discount over the batches by value; the last part takes the rounding remainder.
            var left = discount;
            for (var i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                var d = i == parts.Count - 1 ? left : gross == 0 ? 0 : R(discount * p.Gross / gross);
                left -= d;
                result.Add(new Priced(m, p.A.Batch, p.A.Quantity, p.A.FefoOverridden, T(l.Instructions, 200), p.Price, p.Cost, d, p.Gross - d));
            }
        }
        return (result, errors);
    }

    private async Task<string> NumberAsync(string seq, string prefix, DateOnly today, CancellationToken ct) => await FinanceDb.NextNumberAsync(_db, seq, prefix, today, ct);

    private async Task<string?> PostAsync(StockMove m, DateOnly today, CancellationToken ct)
    {
        var after = await StockEngine.PostAsync(_db, m, today, _clock.UtcNow, _ctx.UserId, _ctx.Username, ct);
        return after is null ? "The stock changed while you were working (another counter used it). Please check the quantities and try again." : null;
    }

    private void AuditFefo(string number, IEnumerable<Priced> lines)
    {
        foreach (var l in lines.Where(x => x.FefoOverridden))
            _audit.Add(AuditActions.PharmFefoOverride, "MedicineBatch", l.Batch.BatchNumber,
                $"{number}: {l.Medicine.Name} batch {l.Batch.BatchNumber} (expiry {l.Batch.ExpiryDate:dd MMM yyyy}) chosen instead of the earliest-expiring batch");
    }

    // =====================================================================================
    // Stock receiving (GRN / purchase)
    // =====================================================================================

    public Task<ServiceResult<DocumentResult>> ReceiveAsync(ReceiveInput input, PharmacyRights rights, CancellationToken ct = default) =>
        RunAsync("UX_StockReceipts_Idempotency",
            async () => await _db.StockReceipts.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (input.IdempotencyKey == Guid.Empty) return Fail("The form has expired. Please reload the page.");
                var isPrivate = await _catalog.IsPrivateAsync(ct);
                var today = await _clock.TodayAsync();
                var errors = new List<string>();
                Supplier? supplier = null;
                if (input.SupplierId is int sid)
                {
                    supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct);
                    if (supplier is null || !supplier.IsActive) errors.Add("Select an active supplier.");
                }
                var source = T(input.SourceName, 150);
                if (supplier is null && source is null) errors.Add("Select the supplier or enter the stock source.");
                var reference = T(input.ReferenceNumber, 60);
                if (reference is not null && supplier is not null && await _db.StockReceipts.AnyAsync(r => r.SupplierId == supplier.Id && r.ReferenceNumber == reference, ct))
                    errors.Add($"Invoice / reference {reference} of {supplier.Name} was already received. Check for a duplicate entry.");
                var received = input.ReceivedDate ?? today;
                if (received > today) errors.Add("The receiving date cannot be in the future.");
                // Prices: Private only, and only for users who manage purchases. Government receiving never stores prices.
                var withPrices = isPrivate && rights.ManagePurchases;
                var anyPrice = input.Lines.Any(l => l.UnitCost is not null and not 0 || l.SalePrice is not null || l.Discount is not null and not 0) || input.DiscountAmount != 0 || input.AdditionalCharges != 0;
                if (anyPrice && !isPrivate) errors.Add("Purchase and sale prices are not used in a Government hospital.");
                else if (anyPrice && !withPrices) errors.Add("You may receive stock, but entering purchase amounts needs the “Manage Purchases” permission.");
                if (input.Lines.Any(l => l.SalePrice is not null) && !rights.ManagePrices) errors.Add("Setting a sale price needs the “Manage Medicine Prices” permission.");
                var lines = input.Lines.Where(l => l.MedicineId > 0).ToList();
                if (lines.Count == 0) errors.Add("Add at least one medicine.");
                if (lines.Count > 100) errors.Add("At most 100 lines per receiving.");
                var meds = await MedicinesAsync(lines.Select(l => l.MedicineId), ct);
                foreach (var l in lines)
                {
                    if (!meds.TryGetValue(l.MedicineId, out var m)) { errors.Add("Medicine not found."); continue; }
                    var bn = T(l.BatchNumber, 50);
                    if (bn is null) errors.Add($"Enter the batch number of {m.Name}.");
                    if (l.ExpiryDate is null) errors.Add($"Enter the expiry date of {m.Name}.");
                    else if (l.ExpiryDate < today) errors.Add($"{m.Name} batch {bn} is already expired ({l.ExpiryDate:MMM yyyy}) and cannot be received.");
                    if (l.ManufactureDate is DateOnly md && (md > today || l.ExpiryDate is DateOnly ed && md >= ed)) errors.Add($"Check the manufacturing date of {m.Name}.");
                    if (l.Quantity is <= 0 or > 10_000_000) errors.Add($"Enter the received quantity of {m.Name}.");
                    if (withPrices)
                    {
                        if (l.UnitCost is null or < 0 || decimal.Round(l.UnitCost.Value, 4) != l.UnitCost) errors.Add($"Enter the purchase price per {m.Unit.ToLowerInvariant()} of {m.Name}.");
                        if (l.SalePrice is < 0 || l.SalePrice is decimal sp && !FinanceMath.IsMoney(sp)) errors.Add($"Check the sale price of {m.Name}.");
                        if (l.Discount is < 0) errors.Add($"Check the discount of {m.Name}.");
                    }
                }
                if (lines.GroupBy(l => (l.MedicineId, T(l.BatchNumber, 50)?.ToUpperInvariant())).Any(g => g.Count() > 1)) errors.Add("The same batch is listed twice. Combine the quantities.");
                if (withPrices && (input.DiscountAmount < 0 || input.AdditionalCharges < 0 || !FinanceMath.IsMoney(input.DiscountAmount) || !FinanceMath.IsMoney(input.AdditionalCharges)))
                    errors.Add("Check the invoice discount and additional charges.");
                if (errors.Count > 0) return Fail(errors);

                var location = await MainAsync(ct);
                var now = _clock.UtcNow;
                var receipt = new StockReceipt
                {
                    Number = await NumberAsync(HmsDbContext.StockReceiptSequence, "GRN", today, ct), IdempotencyKey = input.IdempotencyKey,
                    Mode = isPrivate ? HospitalType.Private : HospitalType.Government, LocationId = location.Id, SupplierId = supplier?.Id,
                    SourceName = supplier is null ? source : null, ReferenceNumber = reference, InvoiceDate = input.InvoiceDate, ReceivedDate = received,
                    Notes = T(input.Notes, 500), BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                };
                foreach (var l in lines)
                {
                    var m = meds[l.MedicineId];
                    var bn = T(l.BatchNumber, 50)!;
                    // One batch row per medicine + batch number; a second delivery of the same batch must have the same expiry.
                    var batch = await _db.MedicineBatches.FirstOrDefaultAsync(b => b.MedicineId == m.Id && b.BatchNumber.ToUpper() == bn.ToUpper(), ct);
                    if (batch is null)
                    {
                        batch = new MedicineBatch
                        {
                            MedicineId = m.Id, BatchNumber = bn, ExpiryDate = l.ExpiryDate!.Value, ManufactureDate = l.ManufactureDate, SupplierId = supplier?.Id,
                            UnitCost = withPrices ? l.UnitCost : isPrivate ? m.PurchasePrice : null,
                            SalePrice = withPrices && l.SalePrice is not null && l.SalePrice != m.SalePrice ? l.SalePrice : null,
                            CreatedUtc = now, CreatedByUsername = _ctx.Username
                        };
                        _db.MedicineBatches.Add(batch);
                        await _db.SaveChangesAsync(ct);
                    }
                    else
                    {
                        if (batch.ExpiryDate != l.ExpiryDate) return Fail($"{m.Name} batch {bn} is already recorded with expiry {batch.ExpiryDate:dd MMM yyyy}. Check the expiry date.");
                        if (withPrices && l.UnitCost is not null && batch.UnitCost != l.UnitCost)
                        {
                            // Weighted cost is not used: the batch keeps the latest purchase price (history in the receiving lines).
                            batch.UnitCost = l.UnitCost;
                        }
                        if (withPrices && l.SalePrice is not null && batch.SalePrice != l.SalePrice) batch.SalePrice = l.SalePrice;
                    }
                    var gross = withPrices ? R(l.UnitCost!.Value * l.Quantity) : (decimal?)null;
                    var item = new StockReceiptItem
                    {
                        MedicineId = m.Id, Batch = batch, MedicineName = m.DisplayName, BatchNumber = batch.BatchNumber, ExpiryDate = batch.ExpiryDate,
                        ManufactureDate = l.ManufactureDate, Quantity = l.Quantity,
                        UnitCost = withPrices ? l.UnitCost : null, SalePrice = withPrices ? l.SalePrice : null,
                        Discount = withPrices ? l.Discount ?? 0 : null, LineTotal = withPrices ? gross - (l.Discount ?? 0) : null
                    };
                    if (item.LineTotal is < 0) return Fail($"The discount on {m.Name} is larger than the line amount.");
                    receipt.Items.Add(item);
                }
                // The receiving document is written once (it is insert-only in the database).
                if (withPrices)
                {
                    receipt.GrossAmount = receipt.Items.Sum(i => i.LineTotal ?? 0);
                    receipt.DiscountAmount = input.DiscountAmount;
                    receipt.AdditionalCharges = input.AdditionalCharges;
                    receipt.NetAmount = receipt.GrossAmount - input.DiscountAmount + input.AdditionalCharges;
                    if (receipt.NetAmount < 0) return Fail("The invoice discount is larger than the invoice amount.");
                }
                _db.StockReceipts.Add(receipt);
                await _db.SaveChangesAsync(ct);
                foreach (var i in receipt.Items)
                    if (await PostAsync(new StockMove(InventoryTxnType.Receipt, location.Id, i.MedicineId, i.BatchId, i.Quantity, "GRN", receipt.Id, receipt.Number,
                            UnitCost: i.UnitCost, Note: supplier?.Name ?? source), today, ct) is { } err) return Fail(err);
                if (withPrices && supplier is not null && receipt.NetAmount > 0)
                    _db.SupplierLedger.Add(new SupplierLedgerEntry
                    {
                        SupplierId = supplier.Id, Type = SupplierEntryType.Purchase, Amount = receipt.NetAmount.Value, StockReceiptId = receipt.Id, Reference = receipt.Number,
                        Description = $"Purchase {receipt.Number}{(reference is null ? "" : $" (invoice {reference})")}", BusinessDate = today, CreatedUtc = now,
                        CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                    });
                _audit.Add(AuditActions.PharmStockReceived, "StockReceipt", receipt.Number,
                    $"{receipt.Number}: {receipt.Items.Count} line(s), {receipt.Items.Sum(i => i.Quantity)} units from {supplier?.Name ?? source}" +
                    (receipt.NetAmount is decimal net ? $", net {net:N2}" : ""),
                    newValues: new { receipt.Number, receipt.ReferenceNumber, Items = receipt.Items.Select(i => new { i.MedicineName, i.BatchNumber, i.ExpiryDate, i.Quantity, i.UnitCost }) });
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(receipt.Id, receipt.Number, false));
            }, ct);

    // =====================================================================================
    // Issue to a registered patient
    // =====================================================================================

    public Task<ServiceResult<DocumentResult>> IssueAsync(IssueInput input, PharmacyRights rights, CancellationToken ct = default) =>
        RunAsync("UX_PatientMedicineIssues_Idempotency",
            async () => await _db.PatientMedicineIssues.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (input.IdempotencyKey == Guid.Empty) return Fail("The form has expired. Please reload the page.");
                var isPrivate = await _catalog.IsPrivateAsync(ct);
                var settings = await _catalog.SettingsAsync(ct);
                var today = await _clock.TodayAsync();
                var errors = new List<string>();
                var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PatientId, ct);
                if (patient is null) return Fail("Select a registered patient.");
                if (patient.Status == PatientStatus.Merged) return Fail($"{patient.PatientNumber} was merged into another registration. Use the correct Patient ID.");
                PatientEncounter? encounter = null;
                if (input.EncounterId is long eid)
                {
                    encounter = await _db.PatientEncounters.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eid, ct);
                    if (encounter is null || encounter.PatientId != patient.Id) errors.Add("The selected visit does not belong to this patient.");
                    else if (encounter.Type is not (EncounterType.Opd or EncounterType.Emergency)) errors.Add("Select an OPD or Emergency visit.");
                }
                if (input.IpdAdmissionId is long aid
                    && !await _db.IpdAdmissions.AnyAsync(a => a.Id == aid && a.PatientId == patient.Id
                        && (a.Status == AdmissionStatus.Registered || a.Status == AdmissionStatus.Admitted || a.Status == AdmissionStatus.UnderTreatment
                            || a.Status == AdmissionStatus.Transferred || a.Status == AdmissionStatus.DischargeRequested), ct))
                    errors.Add("The selected admission is not an active admission of this patient.");
                if (!isPrivate && input.IpdAdmissionId is null)
                {
                    // Government: only for a registered OPD / Emergency visit (or an inpatient admission), never for walk-ins.
                    if (input.EncounterId is null) errors.Add("Select the patient's OPD or Emergency visit. Medicines are only issued against a registered visit.");
                    else if (encounter is not null && encounter.EncounterDate < today.AddDays(-settings.EncounterValidDays))
                        errors.Add($"Visit {encounter.EncounterNumber} of {encounter.EncounterDate:dd MMM yyyy} is older than {settings.EncounterValidDays} day(s). Register a new OPD / Emergency visit.");
                    if (input.Lines.Any(l => l.Discount != 0)) errors.Add("Discounts are not used in a Government hospital.");
                }
                if (errors.Count > 0) return Fail(errors);
                var location = await MainAsync(ct);
                var (lines, lineErrors) = await PrepareAsync(location.Id, input.Lines, isPrivate, rights, input.DiscountReason, today, ct);
                if (lineErrors.Count > 0) return Fail(lineErrors);

                var now = _clock.UtcNow;
                var issue = new PatientMedicineIssue
                {
                    Number = await NumberAsync(HmsDbContext.MedicineIssueSequence, "ISS", today, ct), IdempotencyKey = input.IdempotencyKey,
                    Mode = isPrivate ? HospitalType.Private : HospitalType.Government, PatientId = patient.Id, EncounterId = encounter?.Id, LocationId = location.Id, IpdAdmissionId = input.IpdAdmissionId,
                    Notes = T(input.Notes, 500), BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username,
                    Workstation = T(_ctx.Workstation, 100)
                };
                foreach (var l in lines)
                    issue.Items.Add(new PatientMedicineIssueItem
                    {
                        MedicineId = l.Medicine.Id, BatchId = l.Batch.Id, MedicineName = l.Medicine.DisplayName, BatchNumber = l.Batch.BatchNumber, ExpiryDate = l.Batch.ExpiryDate,
                        Quantity = l.Quantity, Instructions = l.Instructions, FefoOverridden = l.FefoOverridden,
                        UnitPrice = l.UnitPrice, UnitCost = isPrivate ? l.UnitCost : null, Discount = isPrivate ? l.Discount : null, LineTotal = l.LineTotal
                    });
                if (isPrivate)
                {
                    issue.GrossAmount = issue.Items.Sum(i => R(i.UnitPrice!.Value * i.Quantity));
                    issue.DiscountAmount = issue.Items.Sum(i => i.Discount ?? 0);
                    issue.NetAmount = issue.Items.Sum(i => i.LineTotal ?? 0);
                }
                _db.PatientMedicineIssues.Add(issue);
                await _db.SaveChangesAsync(ct);
                foreach (var i in issue.Items)
                    if (await PostAsync(new StockMove(InventoryTxnType.PatientIssue, location.Id, i.MedicineId, i.BatchId, -i.Quantity, "ISS", issue.Id, issue.Number,
                            UnitCost: i.UnitCost, UnitPrice: i.UnitPrice, PatientId: patient.Id), today, ct) is { } err) return Fail(err);

                // Money: Private → one Phase 3 charge per line (paid at the cash counter). Government → optional fixed issue fee.
                if (isPrivate)
                {
                    var svc = await _db.ServiceItems.AsNoTracking().FirstAsync(s => s.Id == settings.MedicineServiceItemId, ct);
                    foreach (var i in issue.Items)
                    {
                        var gross = R(i.UnitPrice!.Value * i.Quantity);
                        var charge = await NewChargeAsync(svc, patient.Id, encounter?.Id, $"{i.MedicineName} × {i.Quantity}", gross, i.Discount ?? 0,
                            i.Discount > 0 ? T(input.DiscountReason, 200) : null, today, now, ct);
                        charge.PharmacyIssueId = issue.Id;
                        charge.IpdAdmissionId = input.IpdAdmissionId;
                        _db.Charges.Add(charge);
                        await _db.SaveChangesAsync(ct);
                        i.ChargeId = charge.Id;
                    }
                }
                else if (settings.GovernmentIssueServiceItemId is int govSvc)
                {
                    var svc = await _db.ServiceItems.AsNoTracking().FirstAsync(s => s.Id == govSvc, ct);
                    var fee = await _finance.FeeOnAsync(svc.Id, today, ct);
                    if (fee > 0)
                    {
                        var charge = await NewChargeAsync(svc, patient.Id, encounter?.Id, svc.Name, fee, 0, null, today, now, ct);
                        charge.PharmacyIssueId = issue.Id;
                        charge.IpdAdmissionId = input.IpdAdmissionId;
                        _db.Charges.Add(charge);
                    }
                }
                AuditFefo(issue.Number, lines);
                _audit.Add(AuditActions.PharmMedicineIssued, "PatientMedicineIssue", issue.Number,
                    $"{issue.Number}: {issue.Items.Count} medicine line(s) issued to {patient.PatientNumber}{(encounter is null ? "" : $" ({encounter.EncounterNumber})")}" +
                    (issue.NetAmount is decimal n ? $", amount {n:N2}" : ""),
                    newValues: new { issue.Number, Items = issue.Items.Select(i => new { i.MedicineName, i.BatchNumber, i.Quantity, i.UnitPrice }) });
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(issue.Id, issue.Number, false));
            }, ct);

    private async Task<Charge> NewChargeAsync(ServiceItem svc, long? patientId, long? encounterId, string name, decimal fee, decimal discount, string? discountReason,
        DateOnly today, DateTime now, CancellationToken ct) => new()
    {
        ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", today, ct),
        PatientId = patientId, EncounterId = encounterId, ServiceItemId = svc.Id, ServiceKind = svc.Kind, ServiceName = name.Length > 150 ? name[..150] : name,
        DepartmentId = svc.DepartmentId, Fee = fee, Discount = discount, NetAmount = fee - discount, AllowPartialPayment = false,
        DiscountReason = discountReason, DoctorShareType = DoctorShareType.HospitalKeepsAll,
        Status = fee - discount == 0 ? ChargeStatus.Paid : ChargeStatus.Unpaid, BusinessDate = today, CreatedUtc = now,
        CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
    };

    // =====================================================================================
    // Private retail sale (walk-in) – stock and the Phase 3 receipt in one transaction
    // =====================================================================================

    public Task<ServiceResult<DocumentResult>> SellAsync(SaleInput input, PharmacyRights rights, CancellationToken ct = default) =>
        RunAsync("UX_PharmacySales_Idempotency",
            async () => await _db.PharmacySales.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (input.IdempotencyKey == Guid.Empty) return Fail("The form has expired. Please reload the page.");
                if (!await _catalog.IsPrivateAsync(ct)) return Fail("Retail sales are only available in a Private hospital.");
                var settings = await _catalog.SettingsAsync(ct);
                if (!await _db.Counters.AnyAsync(c => c.Id == input.CounterId && c.IsActive, ct)) return Fail("Select the cash counter.");
                if (!await _db.PaymentMethods.AnyAsync(m => m.Id == input.PaymentMethodId && m.IsActive, ct)) return Fail("Select the payment method.");
                var today = await _clock.TodayAsync();
                var location = await MainAsync(ct);
                var (lines, errors) = await PrepareAsync(location.Id, input.Lines, true, rights, input.DiscountReason, today, ct);
                if (errors.Count > 0) return Fail(errors);
                var now = _clock.UtcNow;
                var sale = new PharmacySale
                {
                    Number = await NumberAsync(HmsDbContext.PharmacySaleSequence, "SAL", today, ct), IdempotencyKey = input.IdempotencyKey, LocationId = location.Id,
                    CustomerName = T(input.CustomerName, 150), CustomerPhone = T(input.CustomerPhone, 30), Notes = T(input.Notes, 300), CounterId = input.CounterId,
                    BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                };
                foreach (var l in lines)
                    sale.Items.Add(new PharmacySaleItem
                    {
                        MedicineId = l.Medicine.Id, BatchId = l.Batch.Id, MedicineName = l.Medicine.DisplayName, BatchNumber = l.Batch.BatchNumber, ExpiryDate = l.Batch.ExpiryDate,
                        Quantity = l.Quantity, FefoOverridden = l.FefoOverridden, UnitPrice = l.UnitPrice!.Value, UnitCost = l.UnitCost, Discount = l.Discount, LineTotal = l.LineTotal!.Value
                    });
                sale.GrossAmount = sale.Items.Sum(i => R(i.UnitPrice * i.Quantity));
                sale.DiscountAmount = sale.Items.Sum(i => i.Discount);
                sale.NetAmount = sale.Items.Sum(i => i.LineTotal);
                _db.PharmacySales.Add(sale);
                await _db.SaveChangesAsync(ct);
                foreach (var i in sale.Items)
                    if (await PostAsync(new StockMove(InventoryTxnType.Sale, location.Id, i.MedicineId, i.BatchId, -i.Quantity, "SAL", sale.Id, sale.Number,
                            UnitCost: i.UnitCost, UnitPrice: i.UnitPrice), today, ct) is { } err) return Fail(err);

                // Payment through the existing cashier: same counters, receipt numbers, cashier ledger and refunds.
                var collect = new CollectInput
                {
                    IdempotencyKey = input.IdempotencyKey, PayerName = sale.CustomerName ?? "Walk-in customer", CounterId = input.CounterId,
                    PaymentMethodId = input.PaymentMethodId, PaymentReference = input.PaymentReference, Notes = $"Pharmacy sale {sale.Number}"
                };
                foreach (var i in sale.Items)
                    collect.Lines.Add(new NewChargeLine
                    {
                        ServiceItemId = settings.MedicineServiceItemId!.Value, PresetFee = R(i.UnitPrice * i.Quantity), PresetName = $"{i.MedicineName} × {i.Quantity}",
                        Discount = i.Discount, DiscountReason = i.Discount > 0 ? T(input.DiscountReason, 200) : null, PharmacySaleId = sale.Id
                    });
                var paid = await _billing.CollectAsync(collect, canDiscount: true, ct);
                if (!paid.Succeeded) return Fail(paid.Errors);
                var receipt = paid.Value!.Receipt;
                if (receipt is not null)
                {
                    sale.ReceiptId = receipt.Id;
                    var receiptLines = receipt.Lines.ToList();
                    for (var k = 0; k < sale.Items.Count && k < receiptLines.Count; k++) sale.Items[k].ChargeId = receiptLines[k].ChargeId;
                }
                AuditFefo(sale.Number, lines);
                _audit.Add(AuditActions.PharmSale, "PharmacySale", sale.Number,
                    $"{sale.Number}: {sale.Items.Count} line(s), net {sale.NetAmount:N2}{(sale.DiscountAmount > 0 ? $" (discount {sale.DiscountAmount:N2})" : "")}, receipt {receipt?.ReceiptNumber}",
                    newValues: new { sale.Number, sale.CustomerName, Items = sale.Items.Select(i => new { i.MedicineName, i.BatchNumber, i.Quantity, i.UnitPrice, i.Discount }) });
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(sale.Id, sale.Number, false));
            }, ct);

    // =====================================================================================
    // Transfers between locations (pharmacy → Minor OT and other department stores)
    // =====================================================================================

    public Task<ServiceResult<DocumentResult>> TransferAsync(TransferInput input, PharmacyRights rights, CancellationToken ct = default) =>
        RunAsync("UX_StockTransfers_Idempotency",
            async () => await _db.StockTransfers.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (input.IdempotencyKey == Guid.Empty) return Fail("The form has expired. Please reload the page.");
                if (input.FromLocationId == input.ToLocationId) return Fail("Source and destination must be different.");
                var locs = await _db.StockLocations.AsNoTracking().Where(l => l.Id == input.FromLocationId || l.Id == input.ToLocationId).ToDictionaryAsync(l => l.Id, ct);
                if (!locs.TryGetValue(input.FromLocationId, out var from) || !from.IsActive) return Fail("Select the source location.");
                if (!locs.TryGetValue(input.ToLocationId, out var to) || !to.IsActive) return Fail("Select an active destination department.");
                var today = await _clock.TodayAsync();
                var (lines, errors) = await PrepareAsync(from.Id, input.Lines, false, rights, null, today, ct);
                if (errors.Count > 0) return Fail(errors);
                var now = _clock.UtcNow;
                var t = new StockTransfer
                {
                    Number = await NumberAsync(HmsDbContext.StockTransferSequence, "TRF", today, ct), IdempotencyKey = input.IdempotencyKey, FromLocationId = from.Id,
                    ToLocationId = to.Id, Reference = T(input.Reference, 60), Notes = T(input.Notes, 500), BusinessDate = today, CreatedUtc = now,
                    CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                };
                foreach (var l in lines)
                    t.Items.Add(new StockTransferItem
                    {
                        MedicineId = l.Medicine.Id, BatchId = l.Batch.Id, MedicineName = l.Medicine.DisplayName, BatchNumber = l.Batch.BatchNumber, ExpiryDate = l.Batch.ExpiryDate, Quantity = l.Quantity
                    });
                _db.StockTransfers.Add(t);
                await _db.SaveChangesAsync(ct);
                foreach (var i in t.Items)
                {
                    var cost = lines.First(l => l.Batch.Id == i.BatchId).Batch.UnitCost;
                    if (await PostAsync(new StockMove(InventoryTxnType.TransferOut, from.Id, i.MedicineId, i.BatchId, -i.Quantity, "TRF", t.Id, t.Number, UnitCost: cost, CounterpartLocationId: to.Id), today, ct) is { } e1) return Fail(e1);
                    if (await PostAsync(new StockMove(InventoryTxnType.TransferIn, to.Id, i.MedicineId, i.BatchId, i.Quantity, "TRF", t.Id, t.Number, UnitCost: cost, CounterpartLocationId: from.Id), today, ct) is { } e2) return Fail(e2);
                }
                AuditFefo(t.Number, lines);
                _audit.Add(AuditActions.PharmTransfer, "StockTransfer", t.Number,
                    $"{t.Number}: {t.Items.Sum(i => i.Quantity)} units in {t.Items.Count} line(s) from {from.Name} to {to.Name}{(t.Reference is null ? "" : $" (ref {t.Reference})")}",
                    newValues: new { t.Number, From = from.Name, To = to.Name, Items = t.Items.Select(i => new { i.MedicineName, i.BatchNumber, i.Quantity }) });
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(t.Id, t.Number, false));
            }, ct);

    // =====================================================================================
    // Stock adjustments (request → approve)
    // =====================================================================================

    public Task<ServiceResult<DocumentResult>> RequestAdjustmentAsync(AdjustmentInput input, PharmacyRights rights, CancellationToken ct = default) =>
        RunAsync("UX_StockAdjustments_Idempotency",
            async () => await _db.StockAdjustments.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (input.IdempotencyKey == Guid.Empty) return Fail("The form has expired. Please reload the page.");
                var note = T(input.Note, 300);
                if (!Enum.IsDefined(input.Reason)) return Fail("Select the reason.");
                if (note is null) return Fail("Explain the adjustment (mandatory).");
                if (input.Quantity is <= 0 or > 10_000_000) return Fail("Enter the adjustment quantity.");
                var loc = await _db.StockLocations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == input.LocationId, ct);
                var batch = await _db.MedicineBatches.AsNoTracking().Include(b => b.Medicine).FirstOrDefaultAsync(b => b.Id == input.BatchId, ct);
                if (loc is null || batch is null) return Fail("Select the location and batch.");
                var current = await _db.StockBalances.Where(s => s.LocationId == loc.Id && s.BatchId == batch.Id).Select(s => (int?)s.Quantity).FirstOrDefaultAsync(ct) ?? 0;
                if (!input.Increase && input.Quantity > current) return Fail($"Only {current} unit(s) of batch {batch.BatchNumber} are recorded at {loc.Name}.");
                if (input.Increase && input.Reason is StockAdjustmentReason.Expired or StockAdjustmentReason.Damaged or StockAdjustmentReason.BrokenPackaging or StockAdjustmentReason.Lost)
                    return Fail("This reason can only reduce stock.");
                var today = await _clock.TodayAsync();
                var a = new StockAdjustment
                {
                    Number = await NumberAsync(HmsDbContext.StockAdjustmentSequence, "ADJ", today, ct), IdempotencyKey = input.IdempotencyKey, LocationId = loc.Id,
                    MedicineId = batch.MedicineId, BatchId = batch.Id, Increase = input.Increase, Quantity = input.Quantity, QuantityBefore = current, Reason = input.Reason,
                    Note = note, Status = StockAdjustmentStatus.Pending, BusinessDate = today, RequestedUtc = _clock.UtcNow, RequestedByUserId = _ctx.UserId,
                    RequestedByUsername = _ctx.Username
                };
                _db.StockAdjustments.Add(a);
                await _db.SaveChangesAsync(ct);
                _audit.Add(AuditActions.PharmAdjustmentRequested, "StockAdjustment", a.Number,
                    $"{a.Number}: {(a.Increase ? "+" : "−")}{a.Quantity} {batch.Medicine!.Name} batch {batch.BatchNumber} at {loc.Name} – {a.Reason}: {note}");
                // A user who may approve applies the correction at once (recorded as approved by that user).
                if (rights.ApproveAdjustments && await ApplyAdjustmentAsync(a, batch, loc, null, today, ct) is { } err) return Fail(err);
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(a.Id, a.Number, false));
            }, ct);

    private async Task<string?> ApplyAdjustmentAsync(StockAdjustment a, MedicineBatch batch, StockLocation loc, string? note, DateOnly today, CancellationToken ct)
    {
        var err = await PostAsync(new StockMove(a.Increase ? InventoryTxnType.AdjustmentIncrease : InventoryTxnType.AdjustmentDecrease, loc.Id, a.MedicineId, a.BatchId,
            a.Increase ? a.Quantity : -a.Quantity, "ADJ", a.Id, a.Number, UnitCost: batch.UnitCost, Note: $"{a.Reason}: {a.Note}"), today, ct);
        if (err is not null) return $"Batch {batch.BatchNumber} at {loc.Name} no longer has {a.Quantity} unit(s). The adjustment cannot be applied.";
        a.Status = StockAdjustmentStatus.Approved;
        a.DecidedUtc = _clock.UtcNow;
        a.DecidedByUserId = _ctx.UserId;
        a.DecidedByUsername = _ctx.Username;
        a.DecisionNote = T(note, 300);
        a.QuantityAfter = await _db.StockBalances.Where(s => s.LocationId == loc.Id && s.BatchId == batch.Id).Select(s => s.Quantity).FirstAsync(ct);
        _audit.Add(AuditActions.PharmAdjustmentApproved, "StockAdjustment", a.Number, $"{a.Number} approved: {batch.BatchNumber} at {loc.Name} now {a.QuantityAfter}");
        return null;
    }

    public async Task<ServiceResult> DecideAdjustmentAsync(long id, bool approve, string? note, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var a = await _db.StockAdjustments.FromSqlRaw("SELECT a.*, a.xmin FROM \"StockAdjustments\" a WHERE a.\"Id\" = {0} FOR UPDATE", id).FirstOrDefaultAsync(ct);
        if (a is null) return ServiceResult.Fail("Adjustment not found.");
        if (a.Status != StockAdjustmentStatus.Pending) return ServiceResult.Fail($"{a.Number} was already {a.Status.ToString().ToLowerInvariant()}.");
        var today = await _clock.TodayAsync();
        if (!approve)
        {
            if (T(note, 300) is null) return ServiceResult.Fail("Enter the reason for rejecting the adjustment.");
            a.Status = StockAdjustmentStatus.Rejected;
            a.DecidedUtc = _clock.UtcNow;
            a.DecidedByUserId = _ctx.UserId;
            a.DecidedByUsername = _ctx.Username;
            a.DecisionNote = T(note, 300);
            _audit.Add(AuditActions.PharmAdjustmentRejected, "StockAdjustment", a.Number, $"{a.Number} rejected: {a.DecisionNote}");
        }
        else
        {
            var batch = await _db.MedicineBatches.AsNoTracking().FirstAsync(b => b.Id == a.BatchId, ct);
            var loc = await _db.StockLocations.AsNoTracking().FirstAsync(l => l.Id == a.LocationId, ct);
            if (await ApplyAdjustmentAsync(a, batch, loc, note, today, ct) is { } err)
            {
                await tx.RollbackAsync(CancellationToken.None);
                _db.ChangeTracker.Clear();
                return ServiceResult.Fail(err);
            }
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Returns
    // =====================================================================================

    private static string? ValidateReturn(ReturnInput input, out string? reason)
    {
        reason = T(input.Reason, 300);
        if (input.IdempotencyKey == Guid.Empty) return "The form has expired. Please reload the page.";
        if (reason is null) return "Enter the reason for the return.";
        if (!input.Lines.Any(l => l.Quantity > 0)) return "Enter the quantity to return.";
        return null;
    }

    public Task<ServiceResult<DocumentResult>> ReturnSaleAsync(long saleId, ReturnInput input, CancellationToken ct = default) =>
        RunAsync("UX_MedicineReturns_Idempotency",
            async () => await _db.MedicineReturns.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (ValidateReturn(input, out var reason) is { } v) return Fail(v);
                if (!await _catalog.IsPrivateAsync(ct)) return Fail("Retail sales are only available in a Private hospital.");
                var sale = await _db.PharmacySales.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == saleId, ct);
                if (sale is null) return Fail("Sale not found.");
                if (sale.ReceiptId is null) return Fail("This sale has no receipt to refund.");
                if (input.RefundPaymentMethodId is null) return Fail("Select how the money is refunded.");
                var today = await _clock.TodayAsync();
                var ret = new MedicineReturn
                {
                    Number = await NumberAsync(HmsDbContext.MedicineReturnSequence, "RET", today, ct), IdempotencyKey = input.IdempotencyKey,
                    Kind = MedicineReturnKind.SaleReturn, SaleId = sale.Id, LocationId = sale.LocationId, Reason = reason!, BusinessDate = today,
                    CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                };
                foreach (var l in input.Lines.Where(x => x.Quantity > 0))
                {
                    var item = sale.Items.FirstOrDefault(i => i.Id == l.ItemId);
                    if (item is null) return Fail("Sale line not found.");
                    if (l.Quantity > item.Quantity - item.ReturnedQuantity) return Fail($"Only {item.Quantity - item.ReturnedQuantity} of {item.MedicineName} can still be returned.");
                    var restock = l.Restock && !StockEngine.IsExpired(item.ExpiryDate, today);
                    var amount = item.ReturnedQuantity + l.Quantity == item.Quantity
                        ? item.LineTotal - R(item.LineTotal * item.ReturnedQuantity / item.Quantity)
                        : R(item.LineTotal * l.Quantity / item.Quantity);
                    item.ReturnedQuantity += l.Quantity;
                    ret.Items.Add(new MedicineReturnItem
                    {
                        SaleItemId = item.Id, MedicineId = item.MedicineId, BatchId = item.BatchId, MedicineName = item.MedicineName, BatchNumber = item.BatchNumber,
                        Quantity = l.Quantity, Restocked = restock, Amount = amount
                    });
                }
                ret.Amount = ret.Items.Sum(i => i.Amount ?? 0);
                if (ret.Amount > 0)
                {
                    // The refund is requested in the Phase 3 refund queue – the money is paid out only after an authorised user approves it.
                    var refund = await _billing.RequestRefundAsync(sale.ReceiptId.Value, ret.Amount.Value, input.RefundPaymentMethodId.Value, $"Pharmacy return {ret.Number}: {reason}", ct);
                    if (!refund.Succeeded) return Fail(refund.Errors);
                    ret.RefundId = refund.Value!.Id;
                }
                // The return document is insert-only: it is written once, complete.
                _db.MedicineReturns.Add(ret);
                await _db.SaveChangesAsync(ct);
                foreach (var i in ret.Items.Where(x => x.Restocked))
                    if (await PostAsync(new StockMove(InventoryTxnType.SaleReturn, sale.LocationId, i.MedicineId, i.BatchId, i.Quantity, "RET", ret.Id, ret.Number, Note: reason), today, ct) is { } err)
                        return Fail(err);
                _audit.Add(AuditActions.PharmReturn, "MedicineReturn", ret.Number,
                    $"{ret.Number}: return against {sale.Number}, {ret.Items.Sum(i => i.Quantity)} unit(s), refund {ret.Amount:N2} requested; " +
                    $"{ret.Items.Count(i => i.Restocked)} line(s) back to stock, {ret.Items.Count(i => !i.Restocked)} not restocked. {reason}");
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(ret.Id, ret.Number, false));
            }, ct);

    public Task<ServiceResult<DocumentResult>> ReturnIssueAsync(long issueId, ReturnInput input, CancellationToken ct = default) =>
        RunAsync("UX_MedicineReturns_Idempotency",
            async () => await _db.MedicineReturns.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (ValidateReturn(input, out var reason) is { } v) return Fail(v);
                var issue = await _db.PatientMedicineIssues.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == issueId, ct);
                if (issue is null) return Fail("Issue not found.");
                var today = await _clock.TodayAsync();
                var now = _clock.UtcNow;
                var ret = new MedicineReturn
                {
                    Number = await NumberAsync(HmsDbContext.MedicineReturnSequence, "RET", today, ct), IdempotencyKey = input.IdempotencyKey,
                    Kind = MedicineReturnKind.IssueReturn, IssueId = issue.Id, LocationId = issue.LocationId, Reason = reason!, BusinessDate = today,
                    CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                };
                var refundTotal = 0m;
                foreach (var l in input.Lines.Where(x => x.Quantity > 0))
                {
                    var item = issue.Items.FirstOrDefault(i => i.Id == l.ItemId);
                    if (item is null) return Fail("Issue line not found.");
                    if (l.Quantity > item.Quantity - item.ReturnedQuantity) return Fail($"Only {item.Quantity - item.ReturnedQuantity} of {item.MedicineName} can still be returned.");
                    var restock = l.Restock && !StockEngine.IsExpired(item.ExpiryDate, today);
                    decimal? amount = null;
                    if (item.ChargeId is long chargeId && item.LineTotal is decimal total)
                    {
                        amount = item.ReturnedQuantity + l.Quantity == item.Quantity ? total - R(total * item.ReturnedQuantity / item.Quantity) : R(total * l.Quantity / item.Quantity);
                        var charge = await _db.Charges.FromSqlRaw("SELECT c.*, c.xmin FROM \"Charges\" c WHERE c.\"Id\" = {0} FOR UPDATE", chargeId).FirstAsync(ct);
                        if (charge.PaidAmount == 0 && charge.Status != ChargeStatus.Cancelled && charge.NetAmount > 0)
                        {
                            // Not paid yet: cancel the charge and (for a partial return) bill the medicines that were kept.
                            charge.Status = ChargeStatus.Cancelled;
                            var keep = item.Quantity - item.ReturnedQuantity - l.Quantity;
                            if (keep > 0)
                            {
                                var svc = await _db.ServiceItems.AsNoTracking().FirstAsync(s => s.Id == charge.ServiceItemId, ct);
                                var keepNet = charge.NetAmount - amount.Value;
                                var keepDiscount = R((item.Discount ?? 0) * keep / item.Quantity);
                                var replacement = await NewChargeAsync(svc, charge.PatientId, charge.EncounterId, $"{item.MedicineName} × {keep}", keepNet + keepDiscount, keepDiscount,
                                    keepDiscount > 0 ? charge.DiscountReason : null, today, now, ct);
                                replacement.PharmacyIssueId = issue.Id;
                                _db.Charges.Add(replacement);
                                await _db.SaveChangesAsync(ct);
                                item.ChargeId = replacement.Id;
                            }
                        }
                        else if (charge.PaidAmount > 0)
                        {
                            if (input.RefundPaymentMethodId is null) return Fail("Select how the money is refunded.");
                            refundTotal += Math.Min(amount.Value, charge.PaidAmount);
                        }
                    }
                    item.ReturnedQuantity += l.Quantity;
                    ret.Items.Add(new MedicineReturnItem
                    {
                        IssueItemId = item.Id, MedicineId = item.MedicineId, BatchId = item.BatchId, MedicineName = item.MedicineName, BatchNumber = item.BatchNumber,
                        Quantity = l.Quantity, Restocked = restock, Amount = amount
                    });
                }
                ret.Amount = issue.Mode == HospitalType.Private ? ret.Items.Sum(i => i.Amount ?? 0) : null;
                if (refundTotal > 0)
                {
                    // Refund on the receipt that paid the medicines (Phase 3 refund queue, approved by an authorised user).
                    var chargeIds = issue.Items.Where(i => i.ChargeId != null).Select(i => i.ChargeId!.Value).ToList();
                    var receiptId = await _db.ReceiptLines.Where(l => chargeIds.Contains(l.ChargeId) && l.Receipt!.Status == ReceiptStatus.Valid)
                        .OrderByDescending(l => l.ReceiptId).Select(l => (long?)l.ReceiptId).FirstOrDefaultAsync(ct);
                    if (receiptId is null) return Fail("The receipt for these medicines was not found.");
                    var refund = await _billing.RequestRefundAsync(receiptId.Value, refundTotal, input.RefundPaymentMethodId!.Value, $"Pharmacy return {ret.Number}: {reason}", ct);
                    if (!refund.Succeeded) return Fail(refund.Errors);
                    ret.RefundId = refund.Value!.Id;
                }
                _db.MedicineReturns.Add(ret);
                await _db.SaveChangesAsync(ct);
                foreach (var i in ret.Items.Where(x => x.Restocked))
                    if (await PostAsync(new StockMove(InventoryTxnType.IssueReturn, issue.LocationId, i.MedicineId, i.BatchId, i.Quantity, "RET", ret.Id, ret.Number,
                            PatientId: issue.PatientId, Note: reason), today, ct) is { } err) return Fail(err);
                _audit.Add(AuditActions.PharmReturn, "MedicineReturn", ret.Number,
                    $"{ret.Number}: patient return against {issue.Number}, {ret.Items.Sum(i => i.Quantity)} unit(s); {ret.Items.Count(i => i.Restocked)} line(s) back to stock" +
                    (refundTotal > 0 ? $", refund {refundTotal:N2} requested" : "") + $". {reason}");
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(ret.Id, ret.Number, false));
            }, ct);

    public Task<ServiceResult<DocumentResult>> ReturnToSupplierAsync(SupplierReturnInput input, CancellationToken ct = default) =>
        RunAsync("UX_SupplierReturns_Idempotency",
            async () => await _db.SupplierReturns.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (input.IdempotencyKey == Guid.Empty) return Fail("The form has expired. Please reload the page.");
                var reason = T(input.Reason, 300);
                if (reason is null) return Fail("Enter the reason for the supplier return.");
                var isPrivate = await _catalog.IsPrivateAsync(ct);
                Supplier? supplier = null;
                if (input.SupplierId is int sid && (supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct)) is null) return Fail("Supplier not found.");
                var source = T(input.SourceName, 150);
                if (supplier is null && source is null) return Fail("Select the supplier or enter where the stock is returned to.");
                var lines = input.Lines.Where(l => l.Quantity > 0 && l.BatchId > 0).ToList();
                if (lines.Count == 0) return Fail("Add at least one batch to return.");
                var location = await MainAsync(ct);
                var today = await _clock.TodayAsync();
                var r = new SupplierReturn
                {
                    Number = await NumberAsync(HmsDbContext.SupplierReturnSequence, "SRT", today, ct), IdempotencyKey = input.IdempotencyKey,
                    Mode = isPrivate ? HospitalType.Private : HospitalType.Government, SupplierId = supplier?.Id, SourceName = supplier is null ? source : null,
                    LocationId = location.Id, ReferenceInvoice = T(input.ReferenceInvoice, 60), Reason = reason, BusinessDate = today, CreatedUtc = _clock.UtcNow,
                    CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                };
                foreach (var l in lines)
                {
                    var b = await _db.MedicineBatches.AsNoTracking().Include(x => x.Medicine).ThenInclude(m => m!.DosageForm).FirstOrDefaultAsync(x => x.Id == l.BatchId, ct);
                    if (b is null) return Fail("Batch not found.");
                    var cost = isPrivate ? b.UnitCost : null;
                    r.Items.Add(new SupplierReturnItem
                    {
                        MedicineId = b.MedicineId, BatchId = b.Id, MedicineName = b.Medicine!.DisplayName, BatchNumber = b.BatchNumber, ExpiryDate = b.ExpiryDate,
                        Quantity = l.Quantity, UnitCost = cost, Amount = cost is decimal c ? R(c * l.Quantity) : null
                    });
                }
                r.Amount = isPrivate ? r.Items.Sum(i => i.Amount ?? 0) : null;
                _db.SupplierReturns.Add(r);
                await _db.SaveChangesAsync(ct);
                foreach (var i in r.Items)
                    if (await PostAsync(new StockMove(InventoryTxnType.SupplierReturn, location.Id, i.MedicineId, i.BatchId, -i.Quantity, "SRT", r.Id, r.Number, UnitCost: i.UnitCost, Note: reason), today, ct) is not null)
                        return Fail($"Only the stock in the main pharmacy can be returned: {i.MedicineName} batch {i.BatchNumber} does not have {i.Quantity} unit(s) there.");
                if (isPrivate && supplier is not null && r.Amount > 0)
                    _db.SupplierLedger.Add(new SupplierLedgerEntry
                    {
                        SupplierId = supplier.Id, Type = SupplierEntryType.PurchaseReturn, Amount = -r.Amount.Value, SupplierReturnId = r.Id, Reference = r.Number,
                        Description = $"Return {r.Number}{(r.ReferenceInvoice is null ? "" : $" (invoice {r.ReferenceInvoice})")}", BusinessDate = today, CreatedUtc = _clock.UtcNow,
                        CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                    });
                _audit.Add(AuditActions.PharmSupplierReturn, "SupplierReturn", r.Number,
                    $"{r.Number}: {r.Items.Sum(i => i.Quantity)} unit(s) returned to {supplier?.Name ?? source}{(r.Amount is decimal a ? $", credit {a:N2}" : "")}. {reason}");
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(r.Id, r.Number, false));
            }, ct);

    // =====================================================================================
    // Supplier payments (Private)
    // =====================================================================================

    public Task<ServiceResult<DocumentResult>> PaySupplierAsync(SupplierPaymentInput input, CancellationToken ct = default) =>
        RunAsync("UX_SupplierPayments_Idempotency",
            async () => await _db.SupplierPayments.AsNoTracking().Where(r => r.IdempotencyKey == input.IdempotencyKey && input.IdempotencyKey != Guid.Empty)
                .Select(r => new DocumentResult(r.Id, r.Number, true)).FirstOrDefaultAsync(ct),
            async () =>
            {
                if (input.IdempotencyKey == Guid.Empty) return Fail("The form has expired. Please reload the page.");
                if (!await _catalog.IsPrivateAsync(ct)) return Fail("Supplier accounts are only used in a Private hospital.");
                var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == input.SupplierId, ct);
                if (supplier is null) return Fail("Supplier not found.");
                if (input.Amount <= 0 || !FinanceMath.IsMoney(input.Amount)) return Fail("Enter the amount paid.");
                var method = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == input.PaymentMethodId && m.IsActive, ct);
                if (method is null) return Fail("Select the payment method.");
                if (method.RequiresReference && T(input.PaymentReference, 100) is null) return Fail($"Enter the {method.Name} reference.");
                await FinanceDb.LockAsync(_db, $"SUPPLIER|{supplier.Id}", ct);
                var balance = await _db.SupplierLedger.Where(e => e.SupplierId == supplier.Id).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
                if (input.Amount > balance) return Fail($"The payment cannot exceed the outstanding balance of {balance:N2}.");
                var today = await _clock.TodayAsync();
                var p = new SupplierPayment
                {
                    Number = await NumberAsync(HmsDbContext.SupplierPaymentSequence, "SPY", today, ct), IdempotencyKey = input.IdempotencyKey, SupplierId = supplier.Id,
                    Amount = input.Amount, PaymentMethodId = method.Id, PaymentReference = T(input.PaymentReference, 100), Notes = T(input.Notes, 300),
                    BusinessDate = today, PaidUtc = _clock.UtcNow, RecordedByUserId = _ctx.UserId, RecordedByUsername = _ctx.Username
                };
                _db.SupplierPayments.Add(p);
                await _db.SaveChangesAsync(ct);
                _db.SupplierLedger.Add(new SupplierLedgerEntry
                {
                    SupplierId = supplier.Id, Type = SupplierEntryType.Payment, Amount = -p.Amount, SupplierPaymentId = p.Id, Reference = p.Number,
                    Description = $"Payment {p.Number} ({method.Name}{(p.PaymentReference is null ? "" : $" {p.PaymentReference}")})", BusinessDate = today,
                    CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
                });
                _audit.Add(AuditActions.PharmSupplierPayment, "SupplierPayment", p.Number, $"{p.Number}: {p.Amount:N2} paid to {supplier.Name} by {method.Name}");
                return ServiceResult<DocumentResult>.Ok(new DocumentResult(p.Id, p.Number, false));
            }, ct);
}
