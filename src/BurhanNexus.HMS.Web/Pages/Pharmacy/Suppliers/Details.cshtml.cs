using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Suppliers;

/// <summary>Supplier details, purchase / receiving history and (Private) account with payments.</summary>
[HasPermission(Perm.PharmManageSuppliers, Perm.PharmManagePurchases)]
public class DetailsModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    public DetailsModel(HmsDbContext db) => _db = db;

    public Supplier Supplier { get; private set; } = null!;
    public IReadOnlyList<StockReceipt> Receipts { get; private set; } = Array.Empty<StockReceipt>();
    public IReadOnlyList<SupplierLedgerEntry> Ledger { get; private set; } = Array.Empty<SupplierLedgerEntry>();
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public decimal Balance { get; private set; }
    public bool ShowAccount { get; private set; }
    [BindProperty] public SupplierPaymentInput Payment { get; set; } = new();

    private async Task<bool> LoadAsync(int id)
    {
        await InitAsync("suppliers");
        var s = await Catalog.SupplierAsync(id);
        if (s is null) return false;
        Supplier = s;
        ShowAccount = IsPrivate && await CanAsync(Perm.PharmManagePurchases);
        Receipts = await _db.StockReceipts.AsNoTracking().Include(r => r.Items).Where(r => r.SupplierId == id).OrderByDescending(r => r.Id).Take(200).ToListAsync();
        if (ShowAccount)
        {
            Ledger = await Query.SupplierLedgerAsync(id);
            Balance = Ledger.Sum(e => e.Amount);
            Methods = await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().PaymentMethodsAsync(activeOnly: true);
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Payment = new SupplierPaymentInput { IdempotencyKey = Guid.NewGuid(), SupplierId = id };
        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(int id)
    {
        if (await DenyUnlessAsync(Perm.PharmManagePurchases) is { } deny) return deny;
        Payment.SupplierId = id;
        var r = await Pharmacy.PaySupplierAsync(Payment);
        if (!r.Succeeded)
        {
            if (!await LoadAsync(id)) return NotFound();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already recorded." : $"Payment {r.Value.Number} recorded.";
        return RedirectToPage(new { id });
    }
}
