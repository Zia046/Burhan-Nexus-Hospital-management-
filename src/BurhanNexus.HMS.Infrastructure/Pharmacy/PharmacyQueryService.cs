using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Pharmacy;

public sealed record BatchPick(long Id, string BatchNumber, DateOnly Expiry, int Available, decimal? SalePrice, bool NearExpiry);

/// <summary>A medicine for the picker: issuable batches at a location, earliest expiry (FEFO) first.</summary>
public sealed record MedicinePick(int Id, string Code, string Name, string? GenericName, string Unit, int Available, decimal? SalePrice,
    bool AllowDiscount, decimal MaxDiscountPercent, IReadOnlyList<BatchPick> Batches, decimal? PurchasePrice = null);

public enum ExpiryState { Ok, NearExpiry, Expired }

public sealed record BatchStockRow(long BatchId, int MedicineId, string MedicineCode, string MedicineName, string? GenericName, string? Category, string Unit,
    string BatchNumber, DateOnly Expiry, ExpiryState State, int LocationId, string LocationName, int Quantity, decimal? UnitCost, decimal? SalePrice, string? Supplier);

public sealed record MedicineStockRow(int MedicineId, string Code, string Name, string? GenericName, string? Category, string Unit, bool IsActive,
    int MinimumStock, int MainStock, int DepartmentStock, int Usable, int Expired, DateOnly? NextExpiry, decimal? Value)
{
    public bool IsLow => IsActive && MinimumStock > 0 && Usable <= MinimumStock;
}

public sealed record PharmacyDashboard(
    DateOnly From, DateOnly To, int ActiveMedicines, int TotalUnits, int LowStock, int NearExpiry, int Expired,
    int Issues, int IssueUnits, int Sales, int Receipts, int Transfers, int PendingAdjustments,
    decimal? SalesAmount, decimal? PurchaseAmount, decimal? GrossProfit, decimal? StockValue,
    IReadOnlyList<DocumentRow> RecentReceipts, IReadOnlyList<DocumentRow> RecentTransfers, IReadOnlyList<DocumentRow> RecentIssues,
    IReadOnlyList<MedicineStockRow> LowStockList, IReadOnlyList<BatchStockRow> ExpiryList);

public sealed record DocumentRow(string Type, long Id, string Number, DateTime CreatedUtc, string Party, string Detail, int Units, decimal? Amount, string? By);

public sealed record MovementRow(long Id, DateTime CreatedUtc, DateOnly BusinessDate, InventoryTxnType Type, string Location, string MedicineName, string BatchNumber,
    DateOnly Expiry, int Quantity, int BalanceAfter, string DocumentType, long DocumentId, string DocumentNumber, string? Note, string? By);

public sealed record PatientMedicineRow(long IssueId, string IssueNumber, DateTime CreatedUtc, DateOnly BusinessDate, string? EncounterNumber, EncounterType? EncounterType,
    string MedicineName, string BatchNumber, DateOnly Expiry, int Quantity, int Returned, string? Instructions, string? IssuedBy, decimal? LineTotal);

public sealed class PharmacyReportFilter
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int? LocationId { get; set; }
    public int? MedicineId { get; set; }
    public int? CategoryId { get; set; }
    public string? Batch { get; set; }
    public int? SupplierId { get; set; }
}

/// <summary>A report as a plain table (rendered on screen, exported to Excel/CSV and printed).</summary>
public sealed record PharmacyReport(string Key, string Title, IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows, object?[]? Totals = null, string? Note = null);

public sealed record ReportDefinition(string Key, string Group, string Title, bool Financial, string Permission);

public interface IPharmacyQueryService
{
    Task<IReadOnlyList<MedicinePick>> PickAsync(string? q, int locationId, bool includePrices, IReadOnlyList<int>? ids = null, int take = 20, CancellationToken ct = default);
    Task<IReadOnlyList<MedicineStockRow>> MedicineStockAsync(string? q, int? categoryId, bool includeValue, bool activeOnly, CancellationToken ct = default);
    Task<IReadOnlyList<BatchStockRow>> BatchStockAsync(int? locationId, int? medicineId, string? expiry, bool includePrices, bool onlyInStock = true, CancellationToken ct = default);
    Task<PharmacyDashboard> DashboardAsync(DateOnly from, DateOnly to, bool includeFinance, CancellationToken ct = default);
    Task<IReadOnlyList<MovementRow>> MovementsAsync(int? medicineId, long? batchId, int? locationId, DateOnly? from, DateOnly? to, int take = 500, CancellationToken ct = default);
    Task<IReadOnlyList<PatientMedicineRow>> PatientHistoryAsync(long patientId, bool includePrices, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentRow>> DocumentsAsync(string type, DateOnly from, DateOnly to, string? q, bool includePrices, CancellationToken ct = default);
    Task<decimal> SupplierBalanceAsync(int supplierId, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierLedgerEntry>> SupplierLedgerAsync(int supplierId, CancellationToken ct = default);
    Task<PharmacyReport> ReportAsync(string key, PharmacyReportFilter f, bool isPrivate, CancellationToken ct = default);
}

public sealed class PharmacyQueryService : IPharmacyQueryService
{
    private readonly HmsDbContext _db;
    private readonly HospitalClock _clock;
    private readonly IPharmacyCatalogService _catalog;

    public PharmacyQueryService(HmsDbContext db, HospitalClock clock, IPharmacyCatalogService catalog)
    {
        _db = db;
        _clock = clock;
        _catalog = catalog;
    }

    public static readonly IReadOnlyList<ReportDefinition> Reports = new ReportDefinition[]
    {
        new("stock", "Inventory", "Current stock", false, Core.Security.Perm.PharmViewStockReports),
        new("batches", "Inventory", "Batch-wise stock", false, Core.Security.Perm.PharmViewStockReports),
        new("low-stock", "Inventory", "Low-stock medicines", false, Core.Security.Perm.PharmViewLowStockReports),
        new("near-expiry", "Inventory", "Near-expiry medicines", false, Core.Security.Perm.PharmViewExpiryReports),
        new("expired", "Inventory", "Expired medicines", false, Core.Security.Perm.PharmViewExpiryReports),
        new("receiving", "Inventory", "Stock receiving history", false, Core.Security.Perm.PharmViewStockReports),
        new("issuance", "Inventory", "Stock issuance history", false, Core.Security.Perm.PharmViewStockReports),
        new("transfers", "Inventory", "Stock transfer history", false, Core.Security.Perm.PharmViewStockReports),
        new("adjustments", "Inventory", "Stock adjustment history", false, Core.Security.Perm.PharmViewStockReports),
        new("supplier-returns", "Inventory", "Supplier returns", false, Core.Security.Perm.PharmViewStockReports),
        new("physical-difference", "Inventory", "Physical stock difference", false, Core.Security.Perm.PharmViewStockReports),
        new("patient-issues", "Patients", "Patient-wise medicine issuance", false, Core.Security.Perm.PharmViewPatientHistory),
        new("opd-issues", "Patients", "OPD medicine issuance", false, Core.Security.Perm.PharmViewPatientHistory),
        new("emergency-issues", "Patients", "Emergency medicine issuance", false, Core.Security.Perm.PharmViewPatientHistory),
        new("daily-issues", "Patients", "Date-wise patient issuance", false, Core.Security.Perm.PharmViewPatientHistory),
        new("medicine-issues", "Patients", "Medicine-wise patient issuance", false, Core.Security.Perm.PharmViewPatientHistory),
        // Update 17: medicine issue modes. Free issues reduce stock but are never revenue; subsidy and discount are shown separately.
        new("free-issues", "Issue modes", "Free medicine issue report", false, Core.Security.Perm.PharmViewPatientHistory),
        new("paid-sales", "Issue modes", "Paid medicine sales report", false, Core.Security.Perm.PharmViewFinancialReports),
        new("subsidy-issues", "Issue modes", "Partial payment / subsidy report", false, Core.Security.Perm.PharmViewFinancialReports),
        new("discounts", "Issue modes", "Discount report", false, Core.Security.Perm.PharmViewFinancialReports),
        new("returns-all", "Issue modes", "Medicine return report", false, Core.Security.Perm.PharmViewStockReports),
        new("user-issues", "Issue modes", "User-wise medicine issue report", false, Core.Security.Perm.PharmViewPatientHistory),
        new("pharmacy-daily", "Issue modes", "Daily pharmacy summary", false, Core.Security.Perm.PharmViewFinancialReports),
        new("pharmacy-monthly", "Issue modes", "Monthly pharmacy summary", false, Core.Security.Perm.PharmViewFinancialReports),
        new("department-stock", "Departments", "Department-wise stock (incl. Minor OT)", false, Core.Security.Perm.PharmViewDepartmentStock),
        new("ot-transfers", "Departments", "Pharmacy → Minor OT transfers", false, Core.Security.Perm.PharmViewDepartmentStock),
        new("department-transfers", "Departments", "Department-wise stock transfers", false, Core.Security.Perm.PharmViewDepartmentStock),
        new("sales-daily", "Financial", "Daily retail sales", true, Core.Security.Perm.PharmViewFinancialReports),
        new("sales-monthly", "Financial", "Monthly sales", true, Core.Security.Perm.PharmViewFinancialReports),
        new("sales-medicine", "Financial", "Medicine-wise sales", true, Core.Security.Perm.PharmViewFinancialReports),
        new("purchases", "Financial", "Purchase report", true, Core.Security.Perm.PharmViewFinancialReports),
        new("supplier-balances", "Financial", "Supplier balances", true, Core.Security.Perm.PharmViewFinancialReports),
        new("sales-returns", "Financial", "Sales returns", true, Core.Security.Perm.PharmViewFinancialReports),
        new("purchase-returns", "Financial", "Purchase returns", true, Core.Security.Perm.PharmViewFinancialReports),
        new("gross-profit", "Financial", "Gross profit", true, Core.Security.Perm.PharmViewFinancialReports),
        new("valuation", "Financial", "Stock valuation", true, Core.Security.Perm.PharmViewFinancialReports),
    };

    private async Task<(DateOnly Today, int NearDays)> TodayAsync(CancellationToken ct) =>
        (await _clock.TodayAsync(), (await _catalog.SettingsAsync(ct)).NearExpiryDays);

    private static ExpiryState State(DateOnly expiry, DateOnly today, int nearDays) =>
        expiry < today ? ExpiryState.Expired : expiry <= today.AddDays(nearDays) ? ExpiryState.NearExpiry : ExpiryState.Ok;

    private static string Like(string term) => "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    public async Task<IReadOnlyList<MedicinePick>> PickAsync(string? q, int locationId, bool includePrices, IReadOnlyList<int>? ids = null, int take = 20, CancellationToken ct = default)
    {
        var (today, near) = await TodayAsync(ct);
        var query = _db.Medicines.AsNoTracking().Include(m => m.DosageForm).Where(m => m.IsActive);
        if (ids is { Count: > 0 }) query = query.Where(m => ids.Contains(m.Id));
        else if (PharmacyCatalogService.T(q, 100) is { } term)
        {
            var like = Like(term);
            query = query.Where(m => EF.Functions.ILike(m.Name, like) || EF.Functions.ILike(m.GenericName ?? "", like) || EF.Functions.ILike(m.BrandName ?? "", like) || EF.Functions.ILike(m.Code, like));
        }
        var meds = await query.OrderBy(m => m.Name).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        var medIds = meds.Select(m => m.Id).ToList();
        var stock = await _db.StockBalances.AsNoTracking().Include(s => s.Batch)
            .Where(s => s.LocationId == locationId && medIds.Contains(s.MedicineId) && s.Quantity > 0 && s.Batch!.ExpiryDate >= today)
            .OrderBy(s => s.Batch!.ExpiryDate).ThenBy(s => s.BatchId).ToListAsync(ct);
        return meds.Select(m =>
        {
            var batches = stock.Where(s => s.MedicineId == m.Id)
                .Select(s => new BatchPick(s.BatchId, s.Batch!.BatchNumber, s.Batch.ExpiryDate, s.Quantity, includePrices ? s.Batch.SalePrice ?? m.SalePrice : null,
                    State(s.Batch.ExpiryDate, today, near) == ExpiryState.NearExpiry)).ToList();
            return new MedicinePick(m.Id, m.Code, m.DisplayName, m.GenericName, m.Unit, batches.Sum(b => b.Available), includePrices ? m.SalePrice : null,
                includePrices && m.AllowDiscount, includePrices ? m.MaxDiscountPercent : 0, batches, includePrices ? m.PurchasePrice : null);
        }).ToList();
    }

    public async Task<IReadOnlyList<MedicineStockRow>> MedicineStockAsync(string? q, int? categoryId, bool includeValue, bool activeOnly, CancellationToken ct = default)
    {
        var (today, _) = await TodayAsync(ct);
        var meds = await _catalog.MedicinesAsync(q, activeOnly, categoryId, 2000, ct);
        var ids = meds.Select(m => m.Id).ToList();
        var balances = await _db.StockBalances.AsNoTracking().Include(s => s.Batch).Include(s => s.Location)
            .Where(s => ids.Contains(s.MedicineId) && s.Quantity > 0).ToListAsync(ct);
        return meds.Select(m =>
        {
            var b = balances.Where(x => x.MedicineId == m.Id).ToList();
            var main = b.Where(x => x.Location!.Kind == StockLocationKind.MainPharmacy).ToList();
            return new MedicineStockRow(m.Id, m.Code, m.DisplayName, m.GenericName, m.Category?.Name, m.Unit, m.IsActive, m.MinimumStock,
                main.Sum(x => x.Quantity), b.Where(x => x.Location!.Kind != StockLocationKind.MainPharmacy).Sum(x => x.Quantity),
                main.Where(x => x.Batch!.ExpiryDate >= today).Sum(x => x.Quantity), b.Where(x => x.Batch!.ExpiryDate < today).Sum(x => x.Quantity),
                main.Where(x => x.Batch!.ExpiryDate >= today).Select(x => (DateOnly?)x.Batch!.ExpiryDate).Min(),
                includeValue ? b.Sum(x => x.Quantity * (x.Batch!.UnitCost ?? m.PurchasePrice ?? 0)) : null);
        }).ToList();
    }

    public async Task<IReadOnlyList<BatchStockRow>> BatchStockAsync(int? locationId, int? medicineId, string? expiry, bool includePrices, bool onlyInStock = true, CancellationToken ct = default)
    {
        var (today, near) = await TodayAsync(ct);
        var nearLimit = today.AddDays(near);
        var q = _db.StockBalances.AsNoTracking().Include(s => s.Location).Include(s => s.Batch).ThenInclude(b => b!.Supplier)
            .Include(s => s.Medicine).ThenInclude(m => m!.DosageForm).Include(s => s.Medicine).ThenInclude(m => m!.Category)
            .Where(s => (!onlyInStock || s.Quantity > 0) && (locationId == null || s.LocationId == locationId) && (medicineId == null || s.MedicineId == medicineId));
        q = expiry switch
        {
            "expired" => q.Where(s => s.Batch!.ExpiryDate < today),
            "near" => q.Where(s => s.Batch!.ExpiryDate >= today && s.Batch.ExpiryDate <= nearLimit),
            "usable" => q.Where(s => s.Batch!.ExpiryDate >= today),
            _ => q
        };
        var rows = await q.OrderBy(s => s.Batch!.ExpiryDate).ThenBy(s => s.Medicine!.Name).Take(5000).ToListAsync(ct);
        return rows.Select(s => new BatchStockRow(s.BatchId, s.MedicineId, s.Medicine!.Code, s.Medicine.DisplayName, s.Medicine.GenericName, s.Medicine.Category?.Name, s.Medicine.Unit,
            s.Batch!.BatchNumber, s.Batch.ExpiryDate, State(s.Batch.ExpiryDate, today, near), s.LocationId, s.Location!.Name, s.Quantity,
            includePrices ? s.Batch.UnitCost ?? s.Medicine.PurchasePrice : null, includePrices ? s.Batch.SalePrice ?? s.Medicine.SalePrice : null, s.Batch.Supplier?.Name)).ToList();
    }

    public async Task<PharmacyDashboard> DashboardAsync(DateOnly from, DateOnly to, bool includeFinance, CancellationToken ct = default)
    {
        var (today, near) = await TodayAsync(ct);
        var isPrivate = await _catalog.IsPrivateAsync(ct);
        includeFinance &= isPrivate;
        var stock = await MedicineStockAsync(null, null, includeFinance, true, ct);
        var batches = await BatchStockAsync(null, null, null, false, true, ct);
        var nearList = batches.Where(b => b.State != ExpiryState.Ok).OrderBy(b => b.Expiry).Take(15).ToList();
        var issues = await _db.PatientMedicineIssues.AsNoTracking().Where(i => i.BusinessDate >= from && i.BusinessDate <= to).CountAsync(ct);
        var issueUnits = await _db.PatientMedicineIssueItems.AsNoTracking().Where(i => i.Issue!.BusinessDate >= from && i.Issue.BusinessDate <= to).SumAsync(i => (int?)i.Quantity, ct) ?? 0;
        var sales = await _db.PharmacySales.AsNoTracking().Where(s => s.BusinessDate >= from && s.BusinessDate <= to).ToListAsync(ct);
        var receipts = await _db.StockReceipts.AsNoTracking().Where(r => r.BusinessDate >= from && r.BusinessDate <= to).ToListAsync(ct);
        var transfers = await _db.StockTransfers.AsNoTracking().Where(r => r.BusinessDate >= from && r.BusinessDate <= to).CountAsync(ct);
        var pending = await _db.StockAdjustments.CountAsync(a => a.Status == StockAdjustmentStatus.Pending, ct);
        decimal? profit = null;
        if (includeFinance)
        {
            var sold = await _db.PharmacySaleItems.AsNoTracking().Where(i => i.Sale!.BusinessDate >= from && i.Sale.BusinessDate <= to)
                .Select(i => new { i.Quantity, i.ReturnedQuantity, i.LineTotal, i.UnitCost }).ToListAsync(ct);
            var issued = await _db.PatientMedicineIssueItems.AsNoTracking().Where(i => i.Issue!.BusinessDate >= from && i.Issue.BusinessDate <= to && i.LineTotal != null)
                .Select(i => new { i.Quantity, i.ReturnedQuantity, LineTotal = i.LineTotal!.Value, i.UnitCost }).ToListAsync(ct);
            profit = sold.Sum(i => Profit(i.Quantity, i.ReturnedQuantity, i.LineTotal, i.UnitCost)) + issued.Sum(i => Profit(i.Quantity, i.ReturnedQuantity, i.LineTotal, i.UnitCost));
        }
        var recentReceipts = await DocumentsAsync("receipts", today.AddDays(-30), today, null, includeFinance, ct);
        var recentTransfers = await DocumentsAsync("transfers", today.AddDays(-30), today, null, false, ct);
        var recentIssues = await DocumentsAsync("issues", today, today, null, false, ct);
        return new PharmacyDashboard(from, to, stock.Count, stock.Sum(s => s.MainStock + s.DepartmentStock), stock.Count(s => s.IsLow),
            batches.Count(b => b.State == ExpiryState.NearExpiry), batches.Count(b => b.State == ExpiryState.Expired),
            issues, issueUnits, sales.Count, receipts.Count, transfers, pending,
            includeFinance ? sales.Sum(s => s.NetAmount) : null, includeFinance ? receipts.Sum(r => r.NetAmount ?? 0) : null, profit,
            includeFinance ? stock.Sum(s => s.Value ?? 0) : null,
            recentReceipts.Take(6).ToList(), recentTransfers.Take(6).ToList(), recentIssues.Take(8).ToList(),
            stock.Where(s => s.IsLow).OrderBy(s => s.Usable).Take(15).ToList(), nearList);
    }

    /// <summary>Profit of a sold / issued line after returns: net revenue − cost of the units kept by the customer.</summary>
    private static decimal Profit(int qty, int returned, decimal lineTotal, decimal? unitCost)
    {
        if (qty == 0) return 0;
        var kept = qty - returned;
        var revenue = FinanceMath.Round(lineTotal * kept / qty);
        return revenue - FinanceMath.Round((unitCost ?? 0) * kept);
    }

    public async Task<IReadOnlyList<MovementRow>> MovementsAsync(int? medicineId, long? batchId, int? locationId, DateOnly? from, DateOnly? to, int take = 500, CancellationToken ct = default)
    {
        var q = _db.InventoryTransactions.AsNoTracking()
            .Where(t => (medicineId == null || t.MedicineId == medicineId) && (batchId == null || t.BatchId == batchId) && (locationId == null || t.LocationId == locationId)
                        && (from == null || t.BusinessDate >= from) && (to == null || t.BusinessDate <= to));
        return await q.OrderByDescending(t => t.Id).Take(Math.Clamp(take, 1, 5000))
            .Select(t => new MovementRow(t.Id, t.CreatedUtc, t.BusinessDate, t.Type, t.Location!.Name, t.Medicine!.Name + (t.Medicine.Strength == null ? "" : " " + t.Medicine.Strength),
                t.Batch!.BatchNumber, t.Batch.ExpiryDate, t.Quantity, t.BalanceAfter, t.DocumentType, t.DocumentId, t.DocumentNumber, t.Note, t.Username))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PatientMedicineRow>> PatientHistoryAsync(long patientId, bool includePrices, CancellationToken ct = default) =>
        await _db.PatientMedicineIssueItems.AsNoTracking().Where(i => i.Issue!.PatientId == patientId)
            .OrderByDescending(i => i.IssueId).ThenBy(i => i.Id)
            .Select(i => new PatientMedicineRow(i.IssueId, i.Issue!.Number, i.Issue.CreatedUtc, i.Issue.BusinessDate, i.Issue.Encounter!.EncounterNumber,
                i.Issue.Encounter != null ? i.Issue.Encounter.Type : (EncounterType?)null, i.MedicineName, i.BatchNumber, i.ExpiryDate, i.Quantity, i.ReturnedQuantity, i.Instructions,
                i.Issue.CreatedByUsername, includePrices ? i.LineTotal : null))
            .Take(1000).ToListAsync(ct);

    public async Task<IReadOnlyList<DocumentRow>> DocumentsAsync(string type, DateOnly from, DateOnly to, string? q, bool includePrices, CancellationToken ct = default)
    {
        var term = PharmacyCatalogService.T(q, 60);
        var like = term is null ? null : Like(term);
        switch (type)
        {
            case "receipts":
                return await _db.StockReceipts.AsNoTracking()
                    .Where(r => (like != null || r.BusinessDate >= from && r.BusinessDate <= to)
                                && (like == null || EF.Functions.ILike(r.Number, like) || EF.Functions.ILike(r.ReferenceNumber ?? "", like) || EF.Functions.ILike(r.Supplier!.Name, like) || EF.Functions.ILike(r.SourceName ?? "", like)))
                    .OrderByDescending(r => r.Id).Take(500)
                    .Select(r => new DocumentRow("GRN", r.Id, r.Number, r.CreatedUtc, r.Supplier != null ? r.Supplier.Name : r.SourceName ?? "",
                        r.ReferenceNumber ?? "", r.Items.Sum(i => i.Quantity), includePrices ? r.NetAmount : null, r.CreatedByUsername)).ToListAsync(ct);
            case "issues":
                return await _db.PatientMedicineIssues.AsNoTracking()
                    .Where(r => (like != null || r.BusinessDate >= from && r.BusinessDate <= to)
                                && (like == null || EF.Functions.ILike(r.Number, like) || EF.Functions.ILike(r.Patient!.PatientNumber, like) || EF.Functions.ILike(r.Patient.FullName, like)))
                    .OrderByDescending(r => r.Id).Take(500)
                    .Select(r => new DocumentRow("ISS", r.Id, r.Number, r.CreatedUtc, r.Patient!.FullName + " (" + r.Patient.PatientNumber + ")",
                        r.Encounter != null ? r.Encounter.EncounterNumber : "", r.Items.Sum(i => i.Quantity), includePrices ? r.NetAmount : null, r.CreatedByUsername)).ToListAsync(ct);
            case "sales":
                return await _db.PharmacySales.AsNoTracking()
                    .Where(r => (like != null || r.BusinessDate >= from && r.BusinessDate <= to)
                                && (like == null || EF.Functions.ILike(r.Number, like) || EF.Functions.ILike(r.CustomerName ?? "", like) || EF.Functions.ILike(r.CustomerPhone ?? "", like) || EF.Functions.ILike(r.Receipt!.ReceiptNumber, like)))
                    .OrderByDescending(r => r.Id).Take(500)
                    .Select(r => new DocumentRow("SAL", r.Id, r.Number, r.CreatedUtc, r.CustomerName ?? "Walk-in customer", r.Receipt != null ? r.Receipt.ReceiptNumber : "",
                        r.Items.Sum(i => i.Quantity), includePrices ? r.NetAmount : null, r.CreatedByUsername)).ToListAsync(ct);
            case "transfers":
                return await _db.StockTransfers.AsNoTracking()
                    .Where(r => (like != null || r.BusinessDate >= from && r.BusinessDate <= to)
                                && (like == null || EF.Functions.ILike(r.Number, like) || EF.Functions.ILike(r.Reference ?? "", like) || EF.Functions.ILike(r.ToLocation!.Name, like)))
                    .OrderByDescending(r => r.Id).Take(500)
                    .Select(r => new DocumentRow("TRF", r.Id, r.Number, r.CreatedUtc, r.FromLocation!.Name + " → " + r.ToLocation!.Name, r.Reference ?? "",
                        r.Items.Sum(i => i.Quantity), null, r.CreatedByUsername)).ToListAsync(ct);
            case "adjustments":
                return await _db.StockAdjustments.AsNoTracking()
                    .Where(r => (like != null || r.BusinessDate >= from && r.BusinessDate <= to)
                                && (like == null || EF.Functions.ILike(r.Number, like) || EF.Functions.ILike(r.Medicine!.Name, like) || EF.Functions.ILike(r.Batch!.BatchNumber, like)))
                    .OrderByDescending(r => r.Id).Take(500)
                    .Select(r => new DocumentRow("ADJ", r.Id, r.Number, r.RequestedUtc, r.Medicine!.Name + " · " + r.Batch!.BatchNumber + " · " + r.Location!.Name,
                        (r.Increase ? "+" : "−") + r.Quantity + " · " + r.Reason + " · " + r.Status, r.Increase ? r.Quantity : -r.Quantity, null, r.RequestedByUsername)).ToListAsync(ct);
            case "returns":
                return await _db.MedicineReturns.AsNoTracking()
                    .Where(r => (like != null || r.BusinessDate >= from && r.BusinessDate <= to) && (like == null || EF.Functions.ILike(r.Number, like)))
                    .OrderByDescending(r => r.Id).Take(500)
                    .Select(r => new DocumentRow("RET", r.Id, r.Number, r.CreatedUtc, r.Kind == MedicineReturnKind.SaleReturn ? "Sale " + r.Sale!.Number : "Issue " + r.Issue!.Number,
                        r.Reason, r.Items.Sum(i => i.Quantity), includePrices ? r.Amount : null, r.CreatedByUsername)).ToListAsync(ct);
            case "supplier-returns":
                return await _db.SupplierReturns.AsNoTracking()
                    .Where(r => (like != null || r.BusinessDate >= from && r.BusinessDate <= to) && (like == null || EF.Functions.ILike(r.Number, like)))
                    .OrderByDescending(r => r.Id).Take(500)
                    .Select(r => new DocumentRow("SRT", r.Id, r.Number, r.CreatedUtc, r.Supplier != null ? r.Supplier.Name : r.SourceName ?? "", r.Reason,
                        r.Items.Sum(i => i.Quantity), includePrices ? r.Amount : null, r.CreatedByUsername)).ToListAsync(ct);
            default:
                return Array.Empty<DocumentRow>();
        }
    }

    public async Task<decimal> SupplierBalanceAsync(int supplierId, CancellationToken ct = default) =>
        await _db.SupplierLedger.Where(e => e.SupplierId == supplierId).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

    public async Task<IReadOnlyList<SupplierLedgerEntry>> SupplierLedgerAsync(int supplierId, CancellationToken ct = default) =>
        await _db.SupplierLedger.AsNoTracking().Where(e => e.SupplierId == supplierId).OrderBy(e => e.Id).ToListAsync(ct);

    // =====================================================================================
    // Reports
    // =====================================================================================

    public async Task<PharmacyReport> ReportAsync(string key, PharmacyReportFilter f, bool isPrivate, CancellationToken ct = default)
    {
        var def = Reports.FirstOrDefault(r => r.Key == key) ?? Reports[0];
        if (def.Financial && !isPrivate) return new PharmacyReport(def.Key, def.Title, new[] { "Note" }, Array.Empty<object?[]>(), Note: "Financial pharmacy reports are only available in a Private hospital.");
        var (from, to) = (f.From, f.To);
        var batch = PharmacyCatalogService.T(f.Batch, 50);
        bool Match(int medId, string batchNo) => (f.MedicineId == null || f.MedicineId == medId) && (batch == null || batchNo.Contains(batch, StringComparison.OrdinalIgnoreCase));
        PharmacyReport R(string[] cols, IEnumerable<object?[]> rows, object?[]? totals = null, string? note = null) => new(def.Key, def.Title, cols, rows.ToList(), totals, note);

        if (def.Group == "Issue modes") return await IssueModeReportAsync(def, from, to, Match, R, ct);
        switch (def.Key)
        {
            case "stock":
            {
                var rows = (await MedicineStockAsync(null, f.CategoryId, false, false, ct)).Where(r => f.MedicineId == null || r.MedicineId == f.MedicineId).ToList();
                return R(new[] { "Code", "Medicine", "Generic", "Category", "Unit", "Main pharmacy", "Departments", "Usable", "Expired", "Minimum", "Next expiry" },
                    rows.Select(r => new object?[] { r.Code, r.Name, r.GenericName, r.Category, r.Unit, r.MainStock, r.DepartmentStock, r.Usable, r.Expired, r.MinimumStock, r.NextExpiry }),
                    new object?[] { "Total", null, null, null, null, rows.Sum(r => r.MainStock), rows.Sum(r => r.DepartmentStock), rows.Sum(r => r.Usable), rows.Sum(r => r.Expired), null, null });
            }
            case "batches" or "near-expiry" or "expired" or "department-stock":
            {
                var rows = (await BatchStockAsync(f.LocationId, f.MedicineId,
                        def.Key switch { "near-expiry" => "near", "expired" => "expired", _ => null }, false, true, ct))
                    .Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                if (def.Key == "department-stock" && f.LocationId == null)
                {
                    var main = (await _catalog.MainLocationAsync(ct)).Id;
                    rows = rows.Where(r => r.LocationId != main).ToList();
                }
                return R(new[] { "Location", "Code", "Medicine", "Batch", "Expiry", "Status", "Quantity", "Unit", "Supplier" },
                    rows.Select(r => new object?[] { r.LocationName, r.MedicineCode, r.MedicineName, r.BatchNumber, r.Expiry, r.State switch { ExpiryState.Expired => "Expired", ExpiryState.NearExpiry => "Near expiry", _ => "OK" }, r.Quantity, r.Unit, r.Supplier }),
                    new object?[] { "Total", null, null, null, null, null, rows.Sum(r => r.Quantity), null, null });
            }
            case "low-stock":
            {
                var rows = (await MedicineStockAsync(null, f.CategoryId, false, true, ct)).Where(r => r.IsLow).OrderBy(r => r.Usable).ToList();
                return R(new[] { "Code", "Medicine", "Category", "Usable stock", "Minimum level", "Shortfall", "Unit" },
                    rows.Select(r => new object?[] { r.Code, r.Name, r.Category, r.Usable, r.MinimumStock, Math.Max(0, r.MinimumStock - r.Usable), r.Unit }));
            }
            case "receiving":
            {
                var rows = await _db.StockReceiptItems.AsNoTracking().Where(i => i.StockReceipt!.BusinessDate >= from && i.StockReceipt.BusinessDate <= to
                        && (f.SupplierId == null || i.StockReceipt.SupplierId == f.SupplierId))
                    .OrderBy(i => i.StockReceiptId).Select(i => new { i.StockReceipt!.Number, i.StockReceipt.ReceivedDate, Party = i.StockReceipt.Supplier != null ? i.StockReceipt.Supplier.Name : i.StockReceipt.SourceName,
                        i.StockReceipt.ReferenceNumber, i.MedicineId, i.MedicineName, i.BatchNumber, i.ExpiryDate, i.Quantity, i.StockReceipt.CreatedByUsername }).ToListAsync(ct);
                rows = rows.Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                return R(new[] { "GRN", "Received", "Supplier / source", "Reference", "Medicine", "Batch", "Expiry", "Quantity", "Received by" },
                    rows.Select(r => new object?[] { r.Number, r.ReceivedDate, r.Party, r.ReferenceNumber, r.MedicineName, r.BatchNumber, r.ExpiryDate, r.Quantity, r.CreatedByUsername }),
                    new object?[] { "Total", null, null, null, null, null, null, rows.Sum(r => r.Quantity), null });
            }
            case "issuance":
            {
                var types = new[] { InventoryTxnType.PatientIssue, InventoryTxnType.Sale, InventoryTxnType.TransferOut, InventoryTxnType.SupplierReturn, InventoryTxnType.AdjustmentDecrease };
                var rows = (await MovementsAsync(f.MedicineId, null, f.LocationId, from, to, 5000, ct)).Where(m => types.Contains(m.Type) && (batch == null || m.BatchNumber.Contains(batch, StringComparison.OrdinalIgnoreCase))).ToList();
                return R(new[] { "Date", "Document", "Type", "Location", "Medicine", "Batch", "Quantity", "Balance after", "By" },
                    rows.Select(m => new object?[] { m.BusinessDate, m.DocumentNumber, Label(m.Type), m.Location, m.MedicineName, m.BatchNumber, -m.Quantity, m.BalanceAfter, m.By }),
                    new object?[] { "Total", null, null, null, null, null, rows.Sum(m => -m.Quantity), null, null });
            }
            case "transfers" or "ot-transfers" or "department-transfers":
            {
                int? ot = def.Key == "ot-transfers" ? await _db.StockLocations.Where(l => l.Code == "MOT").Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) : null;
                var rows = await _db.StockTransferItems.AsNoTracking().Where(i => i.Transfer!.BusinessDate >= from && i.Transfer.BusinessDate <= to
                        && (ot == null || i.Transfer.ToLocationId == ot) && (f.LocationId == null || i.Transfer.ToLocationId == f.LocationId || i.Transfer.FromLocationId == f.LocationId))
                    .OrderBy(i => i.TransferId).Select(i => new { i.Transfer!.Number, i.Transfer.BusinessDate, From = i.Transfer.FromLocation!.Name, To = i.Transfer.ToLocation!.Name, i.Transfer.Reference,
                        i.MedicineId, i.MedicineName, i.BatchNumber, i.ExpiryDate, i.Quantity, i.Transfer.CreatedByUsername }).ToListAsync(ct);
                rows = rows.Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                if (def.Key == "department-transfers")
                    return R(new[] { "Department", "Transfers", "Units received", "Medicines" },
                        rows.GroupBy(r => r.To).Select(g => new object?[] { g.Key, g.Select(x => x.Number).Distinct().Count(), g.Sum(x => x.Quantity), g.Select(x => x.MedicineId).Distinct().Count() }));
                return R(new[] { "Transfer", "Date", "From", "To", "Reference", "Medicine", "Batch", "Expiry", "Quantity", "By" },
                    rows.Select(r => new object?[] { r.Number, r.BusinessDate, r.From, r.To, r.Reference, r.MedicineName, r.BatchNumber, r.ExpiryDate, r.Quantity, r.CreatedByUsername }),
                    new object?[] { "Total", null, null, null, null, null, null, null, rows.Sum(r => r.Quantity), null });
            }
            case "adjustments" or "physical-difference":
            {
                var rows = await _db.StockAdjustments.AsNoTracking().Where(a => a.BusinessDate >= from && a.BusinessDate <= to
                        && (def.Key != "physical-difference" || a.Reason == StockAdjustmentReason.PhysicalCount))
                    .OrderBy(a => a.Id).Select(a => new { a.Number, a.BusinessDate, Location = a.Location!.Name, a.MedicineId, Medicine = a.Medicine!.Name, a.Batch!.BatchNumber,
                        a.Increase, a.Quantity, a.QuantityBefore, a.QuantityAfter, a.Reason, a.Note, a.Status, a.RequestedByUsername, a.DecidedByUsername }).ToListAsync(ct);
                rows = rows.Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                return R(new[] { "Adjustment", "Date", "Location", "Medicine", "Batch", "Change", "Before", "After", "Reason", "Note", "Status", "Requested by", "Decided by" },
                    rows.Select(r => new object?[] { r.Number, r.BusinessDate, r.Location, r.Medicine, r.BatchNumber, r.Increase ? r.Quantity : -r.Quantity, r.QuantityBefore, r.QuantityAfter,
                        r.Reason.ToString(), r.Note, r.Status.ToString(), r.RequestedByUsername, r.DecidedByUsername }));
            }
            case "supplier-returns" or "purchase-returns":
            {
                var rows = await _db.SupplierReturnItems.AsNoTracking().Where(i => i.SupplierReturn!.BusinessDate >= from && i.SupplierReturn.BusinessDate <= to
                        && (f.SupplierId == null || i.SupplierReturn.SupplierId == f.SupplierId))
                    .OrderBy(i => i.SupplierReturnId).Select(i => new { i.SupplierReturn!.Number, i.SupplierReturn.BusinessDate,
                        Party = i.SupplierReturn.Supplier != null ? i.SupplierReturn.Supplier.Name : i.SupplierReturn.SourceName, i.SupplierReturn.ReferenceInvoice, i.SupplierReturn.Reason,
                        i.MedicineId, i.MedicineName, i.BatchNumber, i.Quantity, i.Amount }).ToListAsync(ct);
                rows = rows.Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                var money = def.Key == "purchase-returns";
                var cols = new List<string> { "Return", "Date", "Supplier / source", "Invoice", "Medicine", "Batch", "Quantity", "Reason" };
                if (money) cols.Add("Amount");
                return R(cols.ToArray(), rows.Select(r => money
                        ? new object?[] { r.Number, r.BusinessDate, r.Party, r.ReferenceInvoice, r.MedicineName, r.BatchNumber, r.Quantity, r.Reason, r.Amount }
                        : new object?[] { r.Number, r.BusinessDate, r.Party, r.ReferenceInvoice, r.MedicineName, r.BatchNumber, r.Quantity, r.Reason }),
                    money ? new object?[] { "Total", null, null, null, null, null, rows.Sum(r => r.Quantity), null, rows.Sum(r => r.Amount ?? 0) } : null);
            }
            case "patient-issues" or "opd-issues" or "emergency-issues" or "daily-issues" or "medicine-issues":
            {
                EncounterType? type = def.Key switch { "opd-issues" => EncounterType.Opd, "emergency-issues" => EncounterType.Emergency, _ => null };
                var rows = await _db.PatientMedicineIssueItems.AsNoTracking().Where(i => i.Issue!.BusinessDate >= from && i.Issue.BusinessDate <= to
                        && (type == null || i.Issue.Encounter!.Type == type))
                    .OrderBy(i => i.IssueId).Select(i => new { i.Issue!.Number, i.Issue.BusinessDate, i.Issue.Patient!.PatientNumber, Patient = i.Issue.Patient.FullName,
                        Encounter = i.Issue.Encounter != null ? i.Issue.Encounter.EncounterNumber : null, i.MedicineId, i.MedicineName, i.BatchNumber, i.Quantity, i.ReturnedQuantity, i.Issue.CreatedByUsername })
                    .ToListAsync(ct);
                rows = rows.Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                if (def.Key == "daily-issues")
                    return R(new[] { "Date", "Issues", "Patients", "Units issued", "Units returned" },
                        rows.GroupBy(r => r.BusinessDate).OrderBy(g => g.Key).Select(g => new object?[] { g.Key, g.Select(x => x.Number).Distinct().Count(), g.Select(x => x.PatientNumber).Distinct().Count(), g.Sum(x => x.Quantity), g.Sum(x => x.ReturnedQuantity) }),
                        new object?[] { "Total", rows.Select(x => x.Number).Distinct().Count(), rows.Select(x => x.PatientNumber).Distinct().Count(), rows.Sum(x => x.Quantity), rows.Sum(x => x.ReturnedQuantity) });
                if (def.Key == "medicine-issues")
                    return R(new[] { "Medicine", "Issues", "Patients", "Units issued", "Units returned" },
                        rows.GroupBy(r => r.MedicineName).OrderByDescending(g => g.Sum(x => x.Quantity)).Select(g => new object?[] { g.Key, g.Select(x => x.Number).Distinct().Count(), g.Select(x => x.PatientNumber).Distinct().Count(), g.Sum(x => x.Quantity), g.Sum(x => x.ReturnedQuantity) }));
                return R(new[] { "Issue", "Date", "Patient ID", "Patient", "Visit", "Medicine", "Batch", "Quantity", "Returned", "Issued by" },
                    rows.Select(r => new object?[] { r.Number, r.BusinessDate, r.PatientNumber, r.Patient, r.Encounter, r.MedicineName, r.BatchNumber, r.Quantity, r.ReturnedQuantity, r.CreatedByUsername }),
                    new object?[] { "Total", null, null, null, null, null, null, rows.Sum(r => r.Quantity), rows.Sum(r => r.ReturnedQuantity), null });
            }
            case "sales-daily" or "sales-monthly":
            {
                var sales = await _db.PharmacySales.AsNoTracking().Where(s => s.BusinessDate >= from && s.BusinessDate <= to).Select(s => new { s.BusinessDate, s.GrossAmount, s.DiscountAmount, s.NetAmount }).ToListAsync(ct);
                var returns = await _db.MedicineReturns.AsNoTracking().Where(r => r.Kind == MedicineReturnKind.SaleReturn && r.BusinessDate >= from && r.BusinessDate <= to)
                    .Select(r => new { r.BusinessDate, Amount = r.Amount ?? 0 }).ToListAsync(ct);
                DateOnly Key(DateOnly d) => def.Key == "sales-monthly" ? new DateOnly(d.Year, d.Month, 1) : d;
                var keys = sales.Select(s => Key(s.BusinessDate)).Concat(returns.Select(r => Key(r.BusinessDate))).Distinct().OrderBy(k => k).ToList();
                return R(new[] { def.Key == "sales-monthly" ? "Month" : "Date", "Sales", "Gross", "Discount", "Net sales", "Returns", "Net after returns" },
                    keys.Select(k =>
                    {
                        var s = sales.Where(x => Key(x.BusinessDate) == k).ToList();
                        var r = returns.Where(x => Key(x.BusinessDate) == k).Sum(x => x.Amount);
                        return new object?[] { def.Key == "sales-monthly" ? k.ToString("MMM yyyy") : k, s.Count, s.Sum(x => x.GrossAmount), s.Sum(x => x.DiscountAmount), s.Sum(x => x.NetAmount), r, s.Sum(x => x.NetAmount) - r };
                    }),
                    new object?[] { "Total", sales.Count, sales.Sum(x => x.GrossAmount), sales.Sum(x => x.DiscountAmount), sales.Sum(x => x.NetAmount), returns.Sum(x => x.Amount), sales.Sum(x => x.NetAmount) - returns.Sum(x => x.Amount) });
            }
            case "sales-medicine" or "gross-profit":
            {
                var sold = await _db.PharmacySaleItems.AsNoTracking().Where(i => i.Sale!.BusinessDate >= from && i.Sale.BusinessDate <= to)
                    .Select(i => new { i.MedicineId, i.MedicineName, i.BatchNumber, i.Quantity, i.ReturnedQuantity, i.LineTotal, i.Discount, i.UnitCost, Source = "Retail" }).ToListAsync(ct);
                var issued = await _db.PatientMedicineIssueItems.AsNoTracking().Where(i => i.Issue!.BusinessDate >= from && i.Issue.BusinessDate <= to && i.LineTotal != null)
                    .Select(i => new { i.MedicineId, i.MedicineName, i.BatchNumber, i.Quantity, i.ReturnedQuantity, LineTotal = i.LineTotal!.Value, Discount = i.Discount ?? 0, i.UnitCost, Source = "Patients" }).ToListAsync(ct);
                var all = sold.Concat(issued).Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                var groups = all.GroupBy(r => r.MedicineName).OrderBy(g => g.Key).Select(g =>
                {
                    var qty = g.Sum(x => x.Quantity - x.ReturnedQuantity);
                    var revenue = g.Sum(x => x.Quantity == 0 ? 0 : FinanceMath.Round(x.LineTotal * (x.Quantity - x.ReturnedQuantity) / x.Quantity));
                    var cost = g.Sum(x => FinanceMath.Round((x.UnitCost ?? 0) * (x.Quantity - x.ReturnedQuantity)));
                    return (Name: g.Key, Qty: qty, Discount: g.Sum(x => x.Discount), Revenue: revenue, Cost: cost, Missing: g.Any(x => x.UnitCost is null));
                }).ToList();
                if (def.Key == "sales-medicine")
                    return R(new[] { "Medicine", "Units sold (net of returns)", "Discount", "Net sales" },
                        groups.Select(g => new object?[] { g.Name, g.Qty, g.Discount, g.Revenue }),
                        new object?[] { "Total", groups.Sum(g => g.Qty), groups.Sum(g => g.Discount), groups.Sum(g => g.Revenue) },
                        "Retail sales and priced patient issues, after returns.");
                return R(new[] { "Medicine", "Units", "Net sales", "Cost", "Gross profit", "Margin %" },
                    groups.Select(g => new object?[] { g.Name + (g.Missing ? " *" : ""), g.Qty, g.Revenue, g.Cost, g.Revenue - g.Cost, g.Revenue == 0 ? null : Math.Round((g.Revenue - g.Cost) * 100 / g.Revenue, 1) }),
                    new object?[] { "Total", groups.Sum(g => g.Qty), groups.Sum(g => g.Revenue), groups.Sum(g => g.Cost), groups.Sum(g => g.Revenue - g.Cost),
                        groups.Sum(g => g.Revenue) == 0 ? null : Math.Round(groups.Sum(g => g.Revenue - g.Cost) * 100 / groups.Sum(g => g.Revenue), 1) },
                    "Cost = purchase price of the batch sold. * = some units had no purchase price.");
            }
            case "purchases":
            {
                var rows = await _db.StockReceipts.AsNoTracking().Where(r => r.Mode == HospitalType.Private && r.BusinessDate >= from && r.BusinessDate <= to
                        && (f.SupplierId == null || r.SupplierId == f.SupplierId))
                    .OrderBy(r => r.Id).Select(r => new { r.Number, r.ReceivedDate, Party = r.Supplier != null ? r.Supplier.Name : r.SourceName, r.ReferenceNumber, r.InvoiceDate,
                        r.GrossAmount, r.DiscountAmount, r.AdditionalCharges, r.NetAmount }).ToListAsync(ct);
                return R(new[] { "GRN", "Received", "Supplier", "Invoice", "Invoice date", "Gross", "Discount", "Additional", "Net" },
                    rows.Select(r => new object?[] { r.Number, r.ReceivedDate, r.Party, r.ReferenceNumber, r.InvoiceDate, r.GrossAmount, r.DiscountAmount, r.AdditionalCharges, r.NetAmount }),
                    new object?[] { "Total", null, null, null, null, rows.Sum(r => r.GrossAmount ?? 0), rows.Sum(r => r.DiscountAmount ?? 0), rows.Sum(r => r.AdditionalCharges ?? 0), rows.Sum(r => r.NetAmount ?? 0) });
            }
            case "supplier-balances":
            {
                var rows = await _db.Suppliers.AsNoTracking().OrderBy(s => s.Name).Select(s => new
                {
                    s.Code, s.Name, s.Phone, s.IsActive,
                    Purchases = _db.SupplierLedger.Where(e => e.SupplierId == s.Id && e.Type == SupplierEntryType.Purchase).Sum(e => (decimal?)e.Amount) ?? 0,
                    Payments = -(_db.SupplierLedger.Where(e => e.SupplierId == s.Id && e.Type == SupplierEntryType.Payment).Sum(e => (decimal?)e.Amount) ?? 0),
                    Returns = -(_db.SupplierLedger.Where(e => e.SupplierId == s.Id && e.Type == SupplierEntryType.PurchaseReturn).Sum(e => (decimal?)e.Amount) ?? 0),
                }).ToListAsync(ct);
                return R(new[] { "Code", "Supplier", "Phone", "Purchases", "Returns", "Payments", "Outstanding" },
                    rows.Where(r => r.Purchases != 0 || r.IsActive).Select(r => new object?[] { r.Code, r.Name, r.Phone, r.Purchases, r.Returns, r.Payments, r.Purchases - r.Returns - r.Payments }),
                    new object?[] { "Total", null, null, rows.Sum(r => r.Purchases), rows.Sum(r => r.Returns), rows.Sum(r => r.Payments), rows.Sum(r => r.Purchases - r.Returns - r.Payments) });
            }
            case "sales-returns":
            {
                var rows = await _db.MedicineReturnItems.AsNoTracking().Where(i => i.Return!.BusinessDate >= from && i.Return.BusinessDate <= to)
                    .OrderBy(i => i.ReturnId).Select(i => new { i.Return!.Number, i.Return.BusinessDate, i.Return.Kind, Against = i.Return.Sale != null ? i.Return.Sale.Number : i.Return.Issue!.Number,
                        i.MedicineId, i.MedicineName, i.BatchNumber, i.Quantity, i.Restocked, i.Amount, i.Return.Reason, Refund = i.Return.Refund != null ? i.Return.Refund.RefundNumber + " (" + i.Return.Refund.Status + ")" : null })
                    .ToListAsync(ct);
                rows = rows.Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                return R(new[] { "Return", "Date", "Against", "Medicine", "Batch", "Quantity", "Back to stock", "Amount", "Refund", "Reason" },
                    rows.Select(r => new object?[] { r.Number, r.BusinessDate, r.Against, r.MedicineName, r.BatchNumber, r.Quantity, r.Restocked ? "Yes" : "No", r.Amount, r.Refund, r.Reason }),
                    new object?[] { "Total", null, null, null, null, rows.Sum(r => r.Quantity), null, rows.Sum(r => r.Amount ?? 0), null, null });
            }
            case "valuation":
            {
                var rows = (await BatchStockAsync(f.LocationId, f.MedicineId, null, true, true, ct)).Where(r => Match(r.MedicineId, r.BatchNumber)).ToList();
                return R(new[] { "Location", "Medicine", "Batch", "Expiry", "Quantity", "Unit cost", "Cost value", "Sale price", "Sale value" },
                    rows.Select(r => new object?[] { r.LocationName, r.MedicineName, r.BatchNumber, r.Expiry, r.Quantity, r.UnitCost, FinanceMath.Round(r.Quantity * (r.UnitCost ?? 0)), r.SalePrice, FinanceMath.Round(r.Quantity * (r.SalePrice ?? 0)) }),
                    new object?[] { "Total", null, null, null, rows.Sum(r => r.Quantity), null, rows.Sum(r => FinanceMath.Round(r.Quantity * (r.UnitCost ?? 0))), null, rows.Sum(r => FinanceMath.Round(r.Quantity * (r.SalePrice ?? 0))) },
                    "Includes expired batches still in stock.");
            }
        }
        return R(new[] { "Note" }, Array.Empty<object?[]>());
    }

    private sealed record ModeDoc(string Kind, string Number, long Id, DateOnly Date, string? PatientNumber, string? Patient, MedicineIssueMode Mode,
        decimal Value, decimal Discount, decimal Subsidy, decimal Payable, decimal Collected, int Units, string? User, string? Reason, FreeIssueApproval? Approval);

    /// <summary>Update 17: issue-mode reports (patient issues and walk-in sales) with value, discount, subsidy and patient payment kept apart.</summary>
    private async Task<PharmacyReport> IssueModeReportAsync(ReportDefinition def, DateOnly from, DateOnly to, Func<int, string, bool> match,
        Func<string[], IEnumerable<object?[]>, object?[]?, string?, PharmacyReport> R, CancellationToken ct)
    {
        var issues = await _db.PatientMedicineIssues.AsNoTracking().Where(i => i.BusinessDate >= from && i.BusinessDate <= to)
            .Select(i => new { i.Id, i.Number, i.BusinessDate, i.Patient!.PatientNumber, Patient = i.Patient.FullName, i.Mode, i.IssueMode, i.TotalValue, i.GrossAmount,
                i.DiscountAmount, i.SubsidyAmount, i.NetAmount, i.CreatedByUsername, i.FreeReason, i.ApprovalStatus, Units = i.Items.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        var issueIds = issues.Select(i => i.Id).ToList();
        var paid = await _db.Charges.AsNoTracking().Where(c => c.PharmacyIssueId != null && issueIds.Contains(c.PharmacyIssueId.Value))
            .GroupBy(c => c.PharmacyIssueId!.Value).Select(g => new { g.Key, Paid = g.Sum(c => c.PaidAmount - c.RefundedAmount) }).ToDictionaryAsync(x => x.Key, x => x.Paid, ct);
        var reasons = await _db.Charges.AsNoTracking().Where(c => c.PharmacyIssueId != null && issueIds.Contains(c.PharmacyIssueId.Value) && c.DiscountReason != null)
            .Select(c => new { Id = c.PharmacyIssueId!.Value, c.DiscountReason }).ToListAsync(ct);
        var sales = await _db.PharmacySales.AsNoTracking().Where(x => x.BusinessDate >= from && x.BusinessDate <= to)
            .Select(x => new { x.Id, x.Number, x.BusinessDate, x.CustomerName, x.GrossAmount, x.DiscountAmount, x.NetAmount, x.CreatedByUsername, Units = x.Items.Sum(i => i.Quantity) })
            .ToListAsync(ct);
        var docs = issues.Select(i =>
            {
                var mode = i.IssueMode ?? (i.Mode == HospitalType.Government ? MedicineIssueMode.Free : (i.DiscountAmount ?? 0) > 0 ? MedicineIssueMode.Discounted : MedicineIssueMode.Paid);
                var free = mode == MedicineIssueMode.Free;
                return new ModeDoc("Issue", i.Number, i.Id, i.BusinessDate, i.PatientNumber, i.Patient, mode, i.TotalValue ?? i.GrossAmount ?? 0, i.DiscountAmount ?? 0,
                    i.SubsidyAmount ?? 0, free ? 0 : i.NetAmount ?? 0, paid.GetValueOrDefault(i.Id), i.Units, i.CreatedByUsername,
                    free ? i.FreeReason : reasons.FirstOrDefault(r => r.Id == i.Id)?.DiscountReason, i.ApprovalStatus);
            })
            .Concat(sales.Select(x => new ModeDoc("Retail sale", x.Number, x.Id, x.BusinessDate, null, x.CustomerName ?? "Walk-in", x.DiscountAmount > 0 ? MedicineIssueMode.Discounted : MedicineIssueMode.Paid,
                x.GrossAmount, x.DiscountAmount, 0, x.NetAmount, x.NetAmount, x.Units, x.CreatedByUsername, null, null)))
            .OrderBy(d => d.Date).ThenBy(d => d.Number).ToList();
        // A free issue that is not (yet) approved took no stock: listed in the free issue report only.
        var effective = docs.Where(d => d.Approval is not (FreeIssueApproval.Pending or FreeIssueApproval.Rejected)).ToList();
        var returns = await _db.MedicineReturns.AsNoTracking().Where(r => r.BusinessDate >= from && r.BusinessDate <= to)
            .Select(r => new { r.Number, r.BusinessDate, r.Kind, Against = r.Sale != null ? r.Sale.Number : r.Issue!.Number, Amount = r.Amount ?? 0, r.Reason,
                Units = r.Items.Sum(i => i.Quantity), Refund = r.Refund != null ? r.Refund.RefundNumber + " (" + r.Refund.Status + ")" : null, r.CreatedByUsername })
            .ToListAsync(ct);
        static string Lbl(MedicineIssueMode m) => PharmacyService.ModeLabel(m);
        switch (def.Key)
        {
            case "free-issues":
            {
                var rows = docs.Where(d => d.Mode == MedicineIssueMode.Free).ToList();
                return R(new[] { "Issue", "Date", "Patient ID", "Patient", "Units", "Medicine value", "Reason", "Approval", "Issued by" },
                    rows.Select(d => new object?[] { d.Number, d.Date, d.PatientNumber, d.Patient, d.Units, d.Value, d.Reason, d.Approval?.ToString() ?? "—", d.User }),
                    new object?[] { "Total", null, null, null, rows.Sum(d => d.Units), rows.Sum(d => d.Value), null, null, null },
                    "Free medicines reduce stock; their value is not revenue.");
            }
            case "paid-sales":
            {
                var rows = effective.Where(d => d.Mode is MedicineIssueMode.Paid or MedicineIssueMode.Discounted).ToList();
                return R(new[] { "Document", "Type", "Date", "Patient / customer", "Units", "Value", "Discount", "Net sale", "Collected", "By" },
                    rows.Select(d => new object?[] { d.Number, d.Kind, d.Date, d.PatientNumber is null ? d.Patient : $"{d.PatientNumber} {d.Patient}", d.Units, d.Value, d.Discount, d.Payable, d.Collected, d.User }),
                    new object?[] { "Total", null, null, null, rows.Sum(d => d.Units), rows.Sum(d => d.Value), rows.Sum(d => d.Discount), rows.Sum(d => d.Payable), rows.Sum(d => d.Collected), null }, null);
            }
            case "subsidy-issues":
            {
                var rows = effective.Where(d => d.Mode == MedicineIssueMode.Subsidized).ToList();
                return R(new[] { "Issue", "Date", "Patient ID", "Patient", "Medicine value", "Discount", "Subsidy", "Patient pays", "Collected", "By" },
                    rows.Select(d => new object?[] { d.Number, d.Date, d.PatientNumber, d.Patient, d.Value, d.Discount, d.Subsidy, d.Payable, d.Collected, d.User }),
                    new object?[] { "Total", null, null, null, rows.Sum(d => d.Value), rows.Sum(d => d.Discount), rows.Sum(d => d.Subsidy), rows.Sum(d => d.Payable), rows.Sum(d => d.Collected), null },
                    "Subsidy is not collected revenue.");
            }
            case "discounts":
            {
                var rows = effective.Where(d => d.Discount > 0).ToList();
                return R(new[] { "Document", "Type", "Date", "Patient / customer", "Original value", "Discount", "Final payable", "Reason", "By" },
                    rows.Select(d => new object?[] { d.Number, d.Kind, d.Date, d.PatientNumber is null ? d.Patient : $"{d.PatientNumber} {d.Patient}", d.Value, d.Discount, d.Payable, d.Reason, d.User }),
                    new object?[] { "Total", null, null, null, rows.Sum(d => d.Value), rows.Sum(d => d.Discount), rows.Sum(d => d.Payable), null, null }, null);
            }
            case "returns-all":
                return R(new[] { "Return", "Date", "Kind", "Against", "Units", "Amount", "Refund", "Reason", "By" },
                    returns.OrderBy(r => r.BusinessDate).Select(r => new object?[] { r.Number, r.BusinessDate, r.Kind == MedicineReturnKind.SaleReturn ? "Sale return" : "Patient return", r.Against, r.Units, r.Amount, r.Refund, r.Reason, r.CreatedByUsername }),
                    new object?[] { "Total", null, null, null, returns.Sum(r => r.Units), returns.Sum(r => r.Amount), null, null, null }, null);
            case "user-issues":
            {
                var groups = effective.GroupBy(d => d.User ?? "—").OrderBy(g => g.Key).ToList();
                object? M(IEnumerable<ModeDoc> g, MedicineIssueMode m) => g.Count(d => d.Mode == m);
                return R(new[] { "User", "Documents", Lbl(MedicineIssueMode.Free), Lbl(MedicineIssueMode.Paid), Lbl(MedicineIssueMode.Subsidized), Lbl(MedicineIssueMode.Discounted), "Units", "Medicine value", "Free value", "Patient payable" },
                    groups.Select(g => new object?[] { g.Key, g.Count(), M(g, MedicineIssueMode.Free), M(g, MedicineIssueMode.Paid), M(g, MedicineIssueMode.Subsidized), M(g, MedicineIssueMode.Discounted),
                        g.Sum(d => d.Units), g.Sum(d => d.Value), g.Where(d => d.Mode == MedicineIssueMode.Free).Sum(d => d.Value), g.Sum(d => d.Payable) }),
                    new object?[] { "Total", effective.Count, null, null, null, null, effective.Sum(d => d.Units), effective.Sum(d => d.Value), effective.Where(d => d.Mode == MedicineIssueMode.Free).Sum(d => d.Value), effective.Sum(d => d.Payable) }, null);
            }
            default: // pharmacy-daily / pharmacy-monthly
            {
                var monthly = def.Key == "pharmacy-monthly";
                DateOnly Key(DateOnly d) => monthly ? new DateOnly(d.Year, d.Month, 1) : d;
                var keys = effective.Select(d => Key(d.Date)).Concat(returns.Select(r => Key(r.BusinessDate))).Distinct().OrderBy(k => k).ToList();
                object?[] Row(object? label, List<ModeDoc> g, decimal ret)
                {
                    var free = g.Where(d => d.Mode == MedicineIssueMode.Free).Sum(d => d.Value);
                    var sold = g.Where(d => d.Mode != MedicineIssueMode.Free).Sum(d => d.Payable);
                    var collected = g.Where(d => d.Mode != MedicineIssueMode.Free).Sum(d => d.Collected);
                    return new object?[] { label, g.Sum(d => d.Value), free, sold, collected, g.Sum(d => d.Subsidy), g.Sum(d => d.Discount), ret, sold - ret };
                }
                var cols = new[] { monthly ? "Month" : "Date", "Total medicine value", "Free medicine value", "Paid sales", "Patient payments received", "Subsidy", "Discounts", "Returns / refunds", "Net medicine sales" };
                return R(cols, keys.Select(k => Row(monthly ? k.ToString("MMM yyyy") : k, effective.Where(d => Key(d.Date) == k).ToList(), returns.Where(r => Key(r.BusinessDate) == k).Sum(r => r.Amount))),
                    Row("Total", effective, returns.Sum(r => r.Amount)), "Free medicine value is never counted as revenue. Net medicine sales = paid sales − returns.");
            }
        }
    }

    public static string Label(InventoryTxnType t) => t switch
    {
        InventoryTxnType.Receipt => "Received",
        InventoryTxnType.PatientIssue => "Patient issue",
        InventoryTxnType.Sale => "Retail sale",
        InventoryTxnType.TransferOut => "Transfer out",
        InventoryTxnType.TransferIn => "Transfer in",
        InventoryTxnType.AdjustmentIncrease => "Adjustment +",
        InventoryTxnType.AdjustmentDecrease => "Adjustment −",
        InventoryTxnType.SaleReturn => "Sale return",
        InventoryTxnType.IssueReturn => "Patient return",
        InventoryTxnType.SupplierReturn => "Supplier return",
        _ => t.ToString()
    };
}
