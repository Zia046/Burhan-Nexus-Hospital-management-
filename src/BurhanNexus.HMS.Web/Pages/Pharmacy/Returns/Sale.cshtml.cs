using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Returns;

/// <summary>Private: customer return against an original sale. Refund goes to the Phase 3 refund queue for approval.</summary>
[HasPermission(Perm.PharmProcessReturns)]
public class SaleModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    public SaleModel(HmsDbContext db) => _db = db;

    [BindProperty] public ReturnInput Input { get; set; } = new();
    public PharmacySale Sale { get; private set; } = null!;
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();

    private async Task<bool> LoadAsync(long id)
    {
        await InitAsync("documents");
        if (!IsPrivate) return false;
        var s = await _db.PharmacySales.AsNoTracking().Include(x => x.Items).Include(x => x.Receipt).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return false;
        Sale = s;
        Methods = await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().PaymentMethodsAsync(activeOnly: true);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Input = new ReturnInput { IdempotencyKey = Guid.NewGuid(), RefundPaymentMethodId = Sale.Receipt?.PaymentMethodId, Lines = Sale.Items.Select(i => new ReturnLineInput { ItemId = i.Id, Restock = true }).ToList() };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        var r = await Pharmacy.ReturnSaleAsync(id, Input);
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved." : $"Return {r.Value.Number} saved. The refund waits for approval in Finance → Refunds.";
        return Redirect($"/Pharmacy/Doc/RET/{r.Value.Id}");
    }
}
