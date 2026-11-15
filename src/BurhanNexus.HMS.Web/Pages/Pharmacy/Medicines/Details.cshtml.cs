using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Medicines;

/// <summary>Complete medicine history: details, batches per location, every stock movement and (Private) price changes.</summary>
[HasPermission(Perm.PharmViewCatalogue)]
public class DetailsModel : PharmacyPageModel
{
    public Medicine Medicine { get; private set; } = null!;
    public IReadOnlyList<BatchStockRow> Batches { get; private set; } = Array.Empty<BatchStockRow>();
    public IReadOnlyList<MovementRow> Movements { get; private set; } = Array.Empty<MovementRow>();
    public IReadOnlyList<MedicinePriceChange> Prices { get; private set; } = Array.Empty<MedicinePriceChange>();
    public bool CanPrices { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await InitAsync("medicines");
        var m = await Catalog.MedicineAsync(id);
        if (m is null) return NotFound();
        Medicine = m;
        CanPrices = IsPrivate && await CanAsync(Perm.PharmManagePrices);
        Batches = await Query.BatchStockAsync(null, id, null, ShowPrices, onlyInStock: false);
        Movements = await Query.MovementsAsync(id, null, null, null, null, 300);
        if (ShowPrices) Prices = await Catalog.PriceHistoryAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostBatchPriceAsync(int id, long batchId, decimal? salePrice)
    {
        if (await DenyUnlessAsync(Perm.PharmManagePrices) is { } deny) return deny;
        var r = await Catalog.SetBatchSalePriceAsync(batchId, salePrice);
        if (r.Succeeded) FlashSuccess = "Batch price saved."; else FlashError = r.ErrorText;
        return RedirectToPage(new { id });
    }
}
