using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance.Receipts;

[HasPermission(Perm.FinanceViewOwnReceipts)]
public class DetailsModel : HmsPageModel
{
    private readonly IBillingService _billing;
    private readonly IFinanceSetupService _setup;

    public DetailsModel(IBillingService billing, IFinanceSetupService setup)
    {
        _billing = billing;
        _setup = setup;
    }

    public Receipt Receipt { get; private set; } = null!;
    public IReadOnlyList<Refund> Refunds { get; private set; } = Array.Empty<Refund>();
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public decimal Refundable { get; private set; }
    [BindProperty] public string? CancelReason { get; set; }
    [BindProperty] public decimal RefundAmount { get; set; }
    [BindProperty] public int RefundMethodId { get; set; }
    [BindProperty] public string? RefundReason { get; set; }
    [BindProperty] public bool ApproveNow { get; set; }

    /// <summary>Loads the receipt; a user without "View All" may only open his own receipts.</summary>
    private async Task<IActionResult?> LoadAsync(long id)
    {
        ViewData["Nav"] = "finance-receipts";
        ViewData["FinanceTab"] = "receipts";
        var r = await _billing.GetReceiptAsync(id);
        if (r is null) return NotFound();
        if (r.CashierUserId != CurrentUserId && !await CanAsync(Perm.FinanceViewAllAccounts)) return Forbid();
        if (r.Status == ReceiptStatus.Cancelled && r.CashierUserId == CurrentUserId
            && !await CanAsync(Perm.FinanceViewCancelled) && !await CanAsync(Perm.FinanceViewAllAccounts)) return Forbid();
        Receipt = r;
        Refunds = await _billing.RefundsAsync(null, id, null, null);
        Methods = await _setup.PaymentMethodsAsync(activeOnly: true);
        Refundable = await _billing.RefundableAsync(id);
        return null;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ?? Page();

    public async Task<IActionResult> OnPostCancelAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.FinanceCancelReceipts) is { } deny) return deny;
        var r = await _billing.CancelReceiptAsync(id, CancelReason);
        if (r.Succeeded) { FlashSuccess = "Receipt cancelled. It stays in the records and its number is never reused."; return RedirectToPage(new { id }); }
        if (await LoadAsync(id) is { } res) return res;
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostRefundAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.FinanceProcessRefunds) is { } deny) return deny;
        var r = await _billing.RequestRefundAsync(id, RefundAmount, RefundMethodId, RefundReason);
        if (r.Succeeded && ApproveNow && await CanAsync(Perm.FinanceApproveRefunds))
        {
            var a = await _billing.ApproveRefundAsync(r.Value!.Id, "Approved when entered");
            if (a.Succeeded) { FlashSuccess = $"Refund {r.Value.RefundNumber} of {r.Value.Amount:N2} completed."; return RedirectToPage(new { id }); }
            FlashError = a.ErrorText;
            return RedirectToPage(new { id });
        }
        if (r.Succeeded) { FlashSuccess = $"Refund {r.Value!.RefundNumber} requested – waiting for approval."; return RedirectToPage(new { id }); }
        if (await LoadAsync(id) is { } res) return res;
        AddErrors(r);
        return Page();
    }
}
