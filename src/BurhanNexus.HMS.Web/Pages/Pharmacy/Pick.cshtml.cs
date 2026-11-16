using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>JSON for the medicine picker: issuable (not expired) batches at a location, earliest expiry first.</summary>
[HasPermission(Perm.PharmViewCatalogue, Perm.PharmIssueToPatients, Perm.PharmRetailSales, Perm.PharmTransferStock, Perm.PharmReceiveStock,
    Perm.PharmAdjustStock, Perm.PharmProcessReturns)]
public class PickModel : PharmacyPageModel
{
    public async Task<IActionResult> OnGetAsync(string? q, int? location, string? ids)
    {
        await InitAsync("pick");
        var loc = location ?? (await Catalog.MainLocationAsync()).Id;
        var idList = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var v) ? v : 0).Where(v => v > 0).Take(60).ToList();
        var prices = IsPrivate && (ShowPrices || await CanAsync(Perm.PharmIssueToPatients));
        var list = await Query.PickAsync(q, loc, prices, idList, idList.Count > 0 ? idList.Count : 20);
        return new JsonResult(list.Select(m => new
        {
            id = m.Id, code = m.Code, name = m.Name, generic = m.GenericName, unit = m.Unit, available = m.Available, price = m.SalePrice, cost = m.PurchasePrice,
            discount = m.AllowDiscount, maxDiscount = m.MaxDiscountPercent,
            batches = m.Batches.Select(b => new { id = b.Id, number = b.BatchNumber, expiry = b.Expiry.ToString("yyyy-MM-dd"), available = b.Available, price = b.SalePrice, near = b.NearExpiry })
        }));
    }
}
