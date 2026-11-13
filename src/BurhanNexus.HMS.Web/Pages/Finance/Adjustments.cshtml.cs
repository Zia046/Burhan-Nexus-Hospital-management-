using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Authorised balance corrections with a mandatory reason. Old records are never edited.</summary>
[HasPermission(Perm.FinanceAdjustments)]
public class AdjustmentsModel : HmsPageModel
{
    private readonly ICashAccountService _cash;
    private readonly IRegistrationSetupService _registration;

    public AdjustmentsModel(ICashAccountService cash, IRegistrationSetupService registration)
    {
        _cash = cash;
        _registration = registration;
    }

    /// <summary>"C:{userId}" for a cashier or "D:{doctorId}" for a doctor – one choice, so the two can never be mixed up.</summary>
    [BindProperty] public string? Account { get; set; }
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public string? Reason { get; set; }

    public IReadOnlyList<CashierBalanceRow> Cashiers { get; private set; } = Array.Empty<CashierBalanceRow>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<FinancialAdjustment> History { get; private set; } = Array.Empty<FinancialAdjustment>();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-adjustments";
        ViewData["FinanceTab"] = "adjustments";
        Cashiers = await _cash.BalancesAsync();
        Doctors = await _registration.DoctorsAsync(activeOnly: false);
        History = await _cash.AdjustmentsAsync(null, null);
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var parts = (Account ?? "").Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var id) || parts[0] is not ("C" or "D"))
        {
            await LoadAsync();
            ModelState.AddModelError(string.Empty, "Select the cashier or doctor.");
            return Page();
        }
        var r = await _cash.AdjustAsync(parts[0] == "C" ? AdjustmentTarget.Cashier : AdjustmentTarget.Doctor, id, Amount, Reason);
        if (r.Succeeded) { FlashSuccess = $"Adjustment {r.Value!.AdjustmentNumber} recorded."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }
}
