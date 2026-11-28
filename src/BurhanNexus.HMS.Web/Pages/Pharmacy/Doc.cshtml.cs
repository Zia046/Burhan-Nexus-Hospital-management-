using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>One pharmacy document (GRN, patient issue, sale, transfer, return, supplier return) with its stock movements.</summary>
[HasPermission(Perm.PharmViewCatalogue, Perm.PharmViewStockReports, Perm.PharmIssueToPatients, Perm.PharmApproveFreeIssue, Perm.PharmRetailSales, Perm.PharmTransferStock,
    Perm.PharmProcessReturns, Perm.PharmReceiveStock, Perm.PharmViewPatientHistory, Perm.PharmViewDepartmentStock)]
public class DocModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    public DocModel(HmsDbContext db) => _db = db;

    public string Type { get; private set; } = "";
    public StockReceipt? Receipt { get; private set; }
    public PatientMedicineIssue? Issue { get; private set; }
    public PharmacySale? Sale { get; private set; }
    public StockTransfer? Transfer { get; private set; }
    public MedicineReturn? Return { get; private set; }
    public SupplierReturn? SupplierReturn { get; private set; }
    public IReadOnlyList<Charge> Charges { get; private set; } = Array.Empty<Charge>();
    public IReadOnlyList<MedicineReturn> Returns { get; private set; } = Array.Empty<MedicineReturn>();
    public IReadOnlyList<InventoryTransaction> Movements { get; private set; } = Array.Empty<InventoryTransaction>();
    public string Number { get; private set; } = "";

    private static readonly Dictionary<string, string[]> Rights = new()
    {
        ["GRN"] = new[] { Perm.PharmReceiveStock, Perm.PharmViewStockReports, Perm.PharmManagePurchases, Perm.PharmViewCatalogue },
        ["ISS"] = new[] { Perm.PharmIssueToPatients, Perm.PharmViewPatientHistory },
        ["SAL"] = new[] { Perm.PharmRetailSales, Perm.PharmViewFinancialReports },
        ["TRF"] = new[] { Perm.PharmTransferStock, Perm.PharmViewDepartmentStock, Perm.PharmViewStockReports },
        ["RET"] = new[] { Perm.PharmProcessReturns, Perm.PharmViewStockReports },
        ["SRT"] = new[] { Perm.PharmProcessReturns, Perm.PharmViewStockReports },
    };

    /// <summary>Update 17: approve (stock is taken now) or reject a free issue waiting for approval.</summary>
    public async Task<IActionResult> OnPostDecideAsync(string type, long id, bool approve, string? note)
    {
        if (await DenyUnlessAsync(Perm.PharmApproveFreeIssue) is { } deny) return deny;
        await InitAsync("issue");
        var r = await Pharmacy.DecideFreeIssueAsync(id, approve, note, await RightsAsync());
        if (r.Succeeded) FlashSuccess = approve ? "Free issue approved – the stock was taken." : "Free issue rejected – no stock was taken.";
        else FlashError = string.Join(" ", r.Errors);
        return Redirect($"/Pharmacy/Doc/ISS/{id}");
    }

    public async Task<IActionResult> OnGetAsync(string type, long id)
    {
        Type = type.ToUpperInvariant();
        if (Type == "ADJ") return Redirect("/Pharmacy/Adjustments");
        if (!Rights.TryGetValue(Type, out var perms)) return NotFound();
        var allowed = false;
        foreach (var p in perms) allowed |= await CanAsync(p);
        if (!allowed) return Forbid();
        await InitAsync("documents");
        switch (Type)
        {
            case "GRN":
                Receipt = await _db.StockReceipts.AsNoTracking().Include(r => r.Items).Include(r => r.Supplier).Include(r => r.Location).FirstOrDefaultAsync(r => r.Id == id);
                Number = Receipt?.Number ?? "";
                break;
            case "ISS":
                Issue = await _db.PatientMedicineIssues.AsNoTracking().Include(r => r.Items).Include(r => r.Patient).Include(r => r.Encounter).FirstOrDefaultAsync(r => r.Id == id);
                Number = Issue?.Number ?? "";
                if (Issue is not null)
                {
                    Charges = await _db.Charges.AsNoTracking().Where(c => c.PharmacyIssueId == id).OrderBy(c => c.Id).ToListAsync();
                    Returns = await _db.MedicineReturns.AsNoTracking().Include(r => r.Items).Include(r => r.Refund).Where(r => r.IssueId == id).OrderBy(r => r.Id).ToListAsync();
                }
                break;
            case "SAL":
                if (!IsPrivate) return NotFound();
                Sale = await _db.PharmacySales.AsNoTracking().Include(r => r.Items).Include(r => r.Receipt).ThenInclude(r => r!.PaymentMethod).Include(r => r.Counter).FirstOrDefaultAsync(r => r.Id == id);
                Number = Sale?.Number ?? "";
                if (Sale is not null) Returns = await _db.MedicineReturns.AsNoTracking().Include(r => r.Items).Include(r => r.Refund).Where(r => r.SaleId == id).OrderBy(r => r.Id).ToListAsync();
                break;
            case "TRF":
                Transfer = await _db.StockTransfers.AsNoTracking().Include(r => r.Items).Include(r => r.FromLocation).Include(r => r.ToLocation).FirstOrDefaultAsync(r => r.Id == id);
                Number = Transfer?.Number ?? "";
                break;
            case "RET":
                Return = await _db.MedicineReturns.AsNoTracking().Include(r => r.Items).Include(r => r.Refund).Include(r => r.Sale).Include(r => r.Issue).FirstOrDefaultAsync(r => r.Id == id);
                Number = Return?.Number ?? "";
                break;
            case "SRT":
                SupplierReturn = await _db.SupplierReturns.AsNoTracking().Include(r => r.Items).Include(r => r.Supplier).FirstOrDefaultAsync(r => r.Id == id);
                Number = SupplierReturn?.Number ?? "";
                break;
        }
        if (Number == "") return NotFound();
        Movements = await _db.InventoryTransactions.AsNoTracking().Include(t => t.Location).Include(t => t.Batch)
            .Where(t => t.DocumentType == Type && t.DocumentId == id).OrderBy(t => t.Id).ToListAsync();
        return Page();
    }
}
