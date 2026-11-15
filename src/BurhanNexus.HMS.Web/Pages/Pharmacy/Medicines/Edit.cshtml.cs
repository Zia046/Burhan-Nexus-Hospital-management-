using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Medicines;

/// <summary>Add / edit a medicine. Warns about similar medicines (never merges). Prices only in Private mode with "Manage Medicine Prices".</summary>
[HasPermission(Perm.PharmAddMedicine, Perm.PharmEditMedicine)]
public class EditModel : PharmacyPageModel
{
    [BindProperty] public MedicineInput Input { get; set; } = new();
    public Medicine? Medicine { get; private set; }
    public IReadOnlyList<PharmacyLookup> Lookups { get; private set; } = Array.Empty<PharmacyLookup>();
    public IReadOnlyList<SimilarMedicine> Similar { get; private set; } = Array.Empty<SimilarMedicine>();
    public bool CanPrices { get; private set; }

    private async Task<bool> LoadAsync(int? id)
    {
        await InitAsync("medicines");
        Lookups = await Catalog.LookupsAsync(null, activeOnly: false);
        CanPrices = IsPrivate && await CanAsync(Perm.PharmManagePrices);
        if (id is int mid)
        {
            Medicine = await Catalog.MedicineAsync(mid);
            if (Medicine is null) return false;
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (await DenyUnlessAsync(id is null ? Perm.PharmAddMedicine : Perm.PharmEditMedicine) is { } deny) return deny;
        if (!await LoadAsync(id)) return NotFound();
        if (Medicine is { } m)
            Input = new MedicineInput
            {
                Name = m.Name, GenericName = m.GenericName, BrandName = m.BrandName, ManufacturerId = m.ManufacturerId, CategoryId = m.CategoryId, DosageFormId = m.DosageFormId,
                Strength = m.Strength, PackSize = m.PackSize, Unit = m.Unit, UnitsPerPack = m.UnitsPerPack, MinimumStock = m.MinimumStock,
                StorageInstructions = m.StorageInstructions, IsActive = m.IsActive, PurchasePrice = m.PurchasePrice, SalePrice = m.SalePrice,
                AllowDiscount = m.AllowDiscount, MaxDiscountPercent = m.MaxDiscountPercent
            };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (await DenyUnlessAsync(id is null ? Perm.PharmAddMedicine : Perm.PharmEditMedicine) is { } deny) return deny;
        if (!await LoadAsync(id)) return NotFound();
        try
        {
            var r = await Catalog.SaveMedicineAsync(id, Input, CanPrices);
            if (!r.Succeeded) { AddErrors(r); return Page(); }
            FlashSuccess = "Medicine saved.";
            return Redirect($"/Pharmacy/Medicines/{r.Value}");
        }
        catch (DuplicateMedicineException ex)
        {
            Similar = ex.Similar;
            ModelState.AddModelError(string.Empty, "A similar medicine already exists. Check the list below – open the existing medicine, or tick “Save anyway” if this is really a different medicine.");
            return Page();
        }
    }
}
