using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>
/// Update 18: "Pay to Hospital" – the administrator sees the outstanding balance of Admin and every user and settles selected (or all)
/// accounts at once. Each account settled is its own transaction in the ledger (never a silent reset). No further approval.
/// </summary>
[HasPermission(Perm.FinanceViewAllAccounts)]
public class PayToHospitalModel : HmsPageModel
{
    private readonly ICashAccountService _cash;
    public PayToHospitalModel(ICashAccountService cash) => _cash = cash;

    [BindProperty] public List<int> UserIds { get; set; } = new();
    [BindProperty] public string? Notes { get; set; }

    public IReadOnlyList<HospitalAccountRow> Accounts { get; private set; } = Array.Empty<HospitalAccountRow>();
    public IReadOnlyList<HospitalSettlement> Recent { get; private set; } = Array.Empty<HospitalSettlement>();
    public bool IsAdmin { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-pay-hospital";
        ViewData["FinanceTab"] = "pay-hospital";
        IsAdmin = User.IsSystemAdministrator();
        Accounts = await _cash.HospitalAccountsAsync();
        Recent = await _cash.SettlementsAsync(null, null);
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.IsSystemAdministrator()) return Forbid();
        var r = await _cash.PayToHospitalAsync(UserIds, Notes);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Paid to hospital: {r.Value!.Count} account(s), total {r.Value.Sum(s => s.Amount):N2} settled.";
        return Redirect("/Finance/PayToHospital");
    }
}
