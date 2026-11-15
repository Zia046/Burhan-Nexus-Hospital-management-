using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Returns;

/// <summary>Medicines returned to the supplier / source from the main pharmacy (Private: credited to the supplier account).</summary>
[HasPermission(Perm.PharmProcessReturns)]
public class SupplierModel : PharmacyPageModel
{
    [BindProperty] public SupplierReturnInput Input { get; set; } = new();
    public IReadOnlyList<Supplier> Suppliers { get; private set; } = Array.Empty<Supplier>();
    public IReadOnlyList<BatchStockRow> Batches { get; private set; } = Array.Empty<BatchStockRow>();

    private async Task LoadAsync()
    {
        await InitAsync("documents");
        Suppliers = await Catalog.SuppliersAsync(null, activeOnly: false);
        Batches = await Query.BatchStockAsync((await Catalog.MainLocationAsync()).Id, null, null, false);
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input = new SupplierReturnInput { IdempotencyKey = Guid.NewGuid(), Lines = { new SupplierReturnLineInput(), new SupplierReturnLineInput(), new SupplierReturnLineInput() } };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        var r = await Pharmacy.ReturnToSupplierAsync(Input);
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved." : $"Supplier return {r.Value.Number} saved.";
        return Redirect($"/Pharmacy/Doc/SRT/{r.Value.Id}");
    }
}
