using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Batch-wise stock per location (main pharmacy, Minor OT, other departments) with expiry filters.</summary>
[HasPermission(Perm.PharmViewStockReports, Perm.PharmViewDepartmentStock)]
public class StockModel : PharmacyPageModel
{
    [BindProperty(SupportsGet = true)] public int? Location { get; set; }
    [BindProperty(SupportsGet = true)] public string? Expiry { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    public IReadOnlyList<StockLocation> Locations { get; private set; } = Array.Empty<StockLocation>();
    public IReadOnlyList<BatchStockRow> Rows { get; private set; } = Array.Empty<BatchStockRow>();

    public async Task<IActionResult> OnGetAsync()
    {
        await InitAsync("stock");
        var all = await CanAsync(Perm.PharmViewStockReports);
        Locations = await Catalog.LocationsAsync(activeOnly: false);
        // A user with only "View Department Stock" sees the department stores, not the main pharmacy.
        if (!all) Locations = Locations.Where(l => l.Kind != StockLocationKind.MainPharmacy).ToList();
        if (Location is int lid && Locations.All(l => l.Id != lid)) return Forbid();
        var rows = await Query.BatchStockAsync(Location, null, Expiry, ShowPrices);
        if (!all) rows = rows.Where(r => Locations.Any(l => l.Id == r.LocationId)).ToList();
        if (!string.IsNullOrWhiteSpace(Q))
            rows = rows.Where(r => r.MedicineName.Contains(Q, StringComparison.OrdinalIgnoreCase) || (r.GenericName ?? "").Contains(Q, StringComparison.OrdinalIgnoreCase)
                                   || r.BatchNumber.Contains(Q, StringComparison.OrdinalIgnoreCase) || r.MedicineCode.Contains(Q, StringComparison.OrdinalIgnoreCase)).ToList();
        Rows = rows;
        return Page();
    }
}
