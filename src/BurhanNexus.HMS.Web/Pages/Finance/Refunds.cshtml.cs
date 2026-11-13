using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

[HasPermission(Perm.FinanceApproveRefunds)]
public class RefundsModel : HmsPageModel
{
    private readonly IBillingService _billing;
    public RefundsModel(IBillingService billing) => _billing = billing;

    [BindProperty] public string? Note { get; set; }
    public IReadOnlyList<Refund> Pending { get; private set; } = Array.Empty<Refund>();
    public IReadOnlyList<Refund> History { get; private set; } = Array.Empty<Refund>();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-refunds";
        ViewData["FinanceTab"] = "refunds";
        Pending = await _billing.RefundsAsync(RefundStatus.Pending, null, null, null);
        History = (await _billing.RefundsAsync(null, null, null, null)).Where(r => r.Status != RefundStatus.Pending).Take(300).ToList();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostApproveAsync(long id)
    {
        var r = await _billing.ApproveRefundAsync(id, Note);
        if (r.Succeeded) { FlashSuccess = "Refund completed."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(long id)
    {
        var r = await _billing.RejectRefundAsync(id, Note);
        if (r.Succeeded) { FlashSuccess = "Refund rejected."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }
}
