using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>
/// Update 10: discharge financial clearance – the complete bill, advances, payments, discounts, refunds and the remaining balance.
/// The cashier receives the balance here; an authorised user may override the clearance with a reason (audited). Clinical discharge
/// stays on the admission page and is never blocked for clinical care.
/// </summary>
[HasPermission(Perm.FinanceViewClearance)]
public class ClearanceModel : IpdPageModel
{
    private readonly IIpdBillingService _billing;
    public ClearanceModel(IIpdBillingService billing) => _billing = billing;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    public IpdLedger L { get; private set; } = null!;
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public Guid Key { get; private set; } = Guid.NewGuid();
    public bool RequireClearance { get; private set; }
    public long? LastReceiptId { get; private set; }

    [BindProperty] public Guid IdempotencyKey { get; set; }
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public int PaymentMethodId { get; set; }
    [BindProperty] public string? Reference { get; set; }
    [BindProperty] public string? Reason { get; set; }

    private async Task<IActionResult?> LoadAsync()
    {
        Nav("ipd-admissions", "admissions");
        var a = await Query.AdmissionAsync(Id);
        if (a is null) return NotFound();
        if (!await CanAccessAsync(a)) return Forbid();
        L = (await _billing.LedgerAsync(Id))!;
        Methods = await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().PaymentMethodsAsync(activeOnly: true);
        RequireClearance = (await Setup.SettingsAsync()).RequireBillClearanceForDischarge;
        if (long.TryParse(Request.Query["receipt"], out var rid)) LastReceiptId = rid;
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostPayAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceReceiveInpatientPayments) is { } d) return d;
        var r = await _billing.ReceivePaymentAsync(Id, IdempotencyKey, Amount, PaymentMethodId, null, Reference);
        if (!r.Succeeded) { if (await LoadAsync() is { } x) return x; AddErrors(r); return Page(); }
        FlashSuccess = $"Payment received{(r.Value is { } rec ? " – receipt " + rec.ReceiptNumber : "")}.";
        return Redirect($"/Ipd/Clearance/{Id}" + (r.Value is { } rc ? $"?receipt={rc.Id}" : ""));
    }

    public async Task<IActionResult> OnPostFinaliseAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageBilling) is { } d) return d;
        var r = await Ipd.FinaliseBillAsync(Id);
        if (!r.Succeeded) { if (await LoadAsync() is { } x) return x; AddErrors(r); return Page(); }
        FlashSuccess = $"Final bill prepared ({r.Value} charge(s) posted up to now).";
        return Redirect($"/Ipd/Clearance/{Id}");
    }

    public async Task<IActionResult> OnPostApplyAdvanceAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceReceiveAdvances) is { } d) return d;
        var r = await _billing.ApplyAdvanceAsync(Id, IdempotencyKey);
        if (!r.Succeeded) { if (await LoadAsync() is { } x) return x; AddErrors(r); return Page(); }
        FlashSuccess = $"{r.Value:N2} of the advance adjusted.";
        return Redirect($"/Ipd/Clearance/{Id}");
    }

    public async Task<IActionResult> OnPostOverrideAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceOverrideClearance) is { } d) return d;
        var r = await _billing.OverrideClearanceAsync(Id, Reason);
        if (!r.Succeeded) { if (await LoadAsync() is { } x) return x; AddErrors(r); return Page(); }
        FlashSuccess = "Financial clearance overridden – the patient can be discharged. The outstanding balance stays on the account.";
        return Redirect($"/Ipd/Clearance/{Id}");
    }
}
