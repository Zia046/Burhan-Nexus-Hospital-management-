using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Medicine categories, manufacturers, dosage forms, strengths, pack sizes and units.</summary>
[HasPermission(Perm.PharmManageCategories)]
public class LookupsModel : PharmacyPageModel
{
    public IReadOnlyList<PharmacyLookup> Items { get; private set; } = Array.Empty<PharmacyLookup>();

    public async Task OnGetAsync()
    {
        await InitAsync("medicines");
        Items = await Catalog.LookupsAsync(null, activeOnly: false);
    }

    public async Task<IActionResult> OnPostAsync(int? id, PharmacyLookupKind kind, string? name, bool isActive, int sortOrder)
    {
        var r = await Catalog.SaveLookupAsync(id, kind, name, isActive, sortOrder);
        if (r.Succeeded) FlashSuccess = "Saved."; else FlashError = r.ErrorText;
        return Redirect($"/Pharmacy/Lookups#{kind}");
    }

    public static string Title(PharmacyLookupKind k) => k switch
    {
        PharmacyLookupKind.Category => "Categories",
        PharmacyLookupKind.Manufacturer => "Manufacturers",
        PharmacyLookupKind.DosageForm => "Dosage forms",
        PharmacyLookupKind.Strength => "Strengths (suggestions)",
        PharmacyLookupKind.PackSize => "Pack sizes (suggestions)",
        _ => "Units (suggestions)"
    };
}
