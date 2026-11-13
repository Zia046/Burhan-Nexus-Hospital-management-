using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Admin: confirm (count) or reject cash deposits. A deposit can never be confirmed by the person who submitted it.</summary>
[HasPermission(Perm.FinanceConfirmDeposits)]
public class DepositsModel : HmsPageModel
{
    private readonly ICashAccountService _cash;
    public DepositsModel(ICashAccountService cash) => _cash = cash;

    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty] public decimal? ReceivedAmount { get; set; }
    [BindProperty] public string? Remarks { get; set; }

    public IReadOnlyList<CashDeposit> Pending { get; private set; } = Array.Empty<CashDeposit>();
    public IReadOnlyList<CashDeposit> History { get; private set; } = Array.Empty<CashDeposit>();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-deposits";
        ViewData["FinanceTab"] = "deposits";
        Pending = await _cash.DepositsAsync(null, DepositStatus.Pending, null, null);
        History = (await _cash.DepositsAsync(null, Status switch { "confirmed" => DepositStatus.Confirmed, "rejected" => DepositStatus.Rejected, _ => null }, null, null))
            .Where(d => d.Status != DepositStatus.Pending).Take(300).ToList();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostConfirmAsync(long id)
    {
        var r = await _cash.ConfirmDepositAsync(id, ReceivedAmount, Remarks);
        if (r.Succeeded) { FlashSuccess = "Deposit confirmed. The cashier's outstanding balance has been reduced."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.FinanceRejectDeposits) is { } deny) return deny;
        var r = await _cash.RejectDepositAsync(id, Remarks);
        if (r.Succeeded) { FlashSuccess = "Deposit rejected. The cashier's balance is unchanged."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }
}
