using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Medicines;

/// <summary>Medicine catalogue with current stock; search by name, generic name, brand or code.</summary>
[HasPermission(Perm.PharmViewCatalogue)]
public class IndexModel : PharmacyPageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Inactive { get; set; }
    public IReadOnlyList<MedicineStockRow> Rows { get; private set; } = Array.Empty<MedicineStockRow>();
    public IReadOnlyList<PharmacyLookup> Categories { get; private set; } = Array.Empty<PharmacyLookup>();
    public IReadOnlyDictionary<int, Medicine> Medicines { get; private set; } = new Dictionary<int, Medicine>();

    public async Task OnGetAsync()
    {
        await InitAsync("medicines");
        Categories = await Catalog.LookupsAsync(PharmacyLookupKind.Category);
        Rows = await Query.MedicineStockAsync(Q, CategoryId, false, !Inactive);
        Medicines = (await Catalog.MedicinesAsync(Q, !Inactive, CategoryId, 2000)).ToDictionary(m => m.Id);
    }
}
