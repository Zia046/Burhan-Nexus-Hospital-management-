using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Private hospital only: walk-in retail sale. Stock and the Phase 3 receipt are saved together; no Patient ID needed.</summary>
[HasPermission(Perm.PharmRetailSales)]
public class SaleModel : PharmacyPageModel
{
    private readonly IFinanceSetupService _setup;
    public SaleModel(IFinanceSetupService setup) => _setup = setup;

    [BindProperty] public SaleInput Input { get; set; } = new();
    public IReadOnlyList<Counter> Counters { get; private set; } = Array.Empty<Counter>();
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public int LocationId { get; private set; }
    public bool CanDiscount { get; private set; }

    private async Task LoadAsync()
    {
        await InitAsync("sale");
        LocationId = (await Catalog.MainLocationAsync()).Id;
        CanDiscount = await CanAsync(Perm.PharmApplyDiscount);
        Counters = User.IsSystemAdministrator() ? (await _setup.CountersAsync(activeOnly: true)).Select(c => c.Counter).ToList() : await _setup.CountersOfUserAsync(CurrentUserId!.Value);
        Methods = await _setup.PaymentMethodsAsync(activeOnly: true);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (!(await Catalog.SettingsAsync()).PaidSaleEnabled) return NotFound(); // update 17: Paid Sale enabled
        Input = new SaleInput { IdempotencyKey = Guid.NewGuid(), CounterId = Counters.FirstOrDefault()?.Id ?? 0, PaymentMethodId = Methods.FirstOrDefault()?.Id ?? 0 };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!(await Catalog.SettingsAsync()).PaidSaleEnabled) return NotFound(); // update 17: Paid Sale enabled
        var r = await Pharmacy.SellAsync(Input, await RightsAsync());
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved – it was not charged twice." : $"Sale {r.Value.Number} completed.";
        return Redirect($"/Pharmacy/Doc/SAL/{r.Value.Id}");
    }
}
