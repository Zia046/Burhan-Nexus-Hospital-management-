using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>
/// Pharmacy → Minor OT (and other department stores) stock transfer. Main stock decreases and the department stock increases in one step.
/// OT staff never use the software; the department stock is not reduced per patient.
/// </summary>
[HasPermission(Perm.PharmTransferStock)]
public class TransferModel : PharmacyPageModel
{
    [BindProperty] public TransferInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public int? From { get; set; }
    [BindProperty(SupportsGet = true)] public int? To { get; set; }
    public IReadOnlyList<StockLocation> Locations { get; private set; } = Array.Empty<StockLocation>();

    private async Task LoadAsync()
    {
        await InitAsync("transfer");
        Locations = await Catalog.LocationsAsync(activeOnly: true);
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        var main = Locations.First(l => l.Kind == StockLocationKind.MainPharmacy);
        var from = From ?? main.Id;
        Input = new TransferInput
        {
            IdempotencyKey = Guid.NewGuid(), FromLocationId = from,
            ToLocationId = To ?? (from == main.Id ? Locations.FirstOrDefault(l => l.Code == "MOT")?.Id ?? Locations.FirstOrDefault(l => l.Id != main.Id)?.Id ?? 0 : main.Id)
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        var r = await Pharmacy.TransferAsync(Input, await RightsAsync());
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved – stock was not moved twice." : $"Transfer {r.Value.Number} completed.";
        return Redirect($"/Pharmacy/Doc/TRF/{r.Value.Id}");
    }
}
