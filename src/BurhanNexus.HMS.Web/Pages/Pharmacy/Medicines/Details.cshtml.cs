using Microsoft.EntityFrameworkCore;
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
    /// <summary>Update 17: stock classification (when on) – class of every batch, changed by users with Manage Pharmacy Settings.</summary>
    public bool ClassificationOn { get; private set; }
    public bool CanClassify { get; private set; }
    public Dictionary<long, MedicineStockClass?> Classes { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await InitAsync("medicines");
        var m = await Catalog.MedicineAsync(id);
        if (m is null) return NotFound();
        Medicine = m;
        CanPrices = IsPrivate && await CanAsync(Perm.PharmManagePrices);
        Batches = await Query.BatchStockAsync(null, id, null, ShowPrices, onlyInStock: false);
        ClassificationOn = (await Catalog.SettingsAsync()).StockClassificationEnabled;
        CanClassify = ClassificationOn && await CanAsync(Perm.PharmSettings);
        if (ClassificationOn)
            Classes = await HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Persistence.HmsDbContext>().MedicineBatches
                .Where(b => b.MedicineId == id).ToDictionaryAsync(b => b.Id, b => b.StockClass);
        Movements = await Query.MovementsAsync(id, null, null, null, null, 300);
        if (ShowPrices) Prices = await Catalog.PriceHistoryAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostBatchClassAsync(int id, long batchId, MedicineStockClass? stockClass)
    {
        if (await DenyUnlessAsync(Perm.PharmSettings) is { } deny) return deny;
        var r = await Pharmacy.SetBatchClassAsync(batchId, stockClass);
        if (r.Succeeded) FlashSuccess = "Stock class saved."; else FlashError = r.ErrorText;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostBatchPriceAsync(int id, long batchId, decimal? salePrice)
    {
        if (await DenyUnlessAsync(Perm.PharmManagePrices) is { } deny) return deny;
        var r = await Catalog.SetBatchSalePriceAsync(batchId, salePrice);
        if (r.Succeeded) FlashSuccess = "Batch price saved."; else FlashError = r.ErrorText;
        return RedirectToPage(new { id });
    }
}
