using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>
/// Update 10: the inpatient account of one admission – every charge (admission fee, bed, pharmacy, laboratory, radiology, OT, NICU
/// equipment, services), payments, advances, refunds and the balance. It reads the Phase 3 records; there is no separate ledger.
/// </summary>
[HasPermission(Perm.IpdViewLedger, Perm.IpdViewBillingHistory, Perm.FinanceViewClearance)]
public class AccountModel : IpdPageModel
{
    private readonly IIpdBillingService _billing;
    public AccountModel(IIpdBillingService billing) => _billing = billing;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    public IpdLedger L { get; private set; } = null!;
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public IReadOnlyList<ServicePackage> Packages { get; private set; } = Array.Empty<ServicePackage>();
    public Guid Key { get; private set; } = Guid.NewGuid();
    public bool CanLedger { get; private set; }

    [BindProperty] public Guid IdempotencyKey { get; set; }
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public int PaymentMethodId { get; set; }
    [BindProperty] public string? Reference { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public int PackageId { get; set; }

    private async Task<IActionResult?> LoadAsync()
    {
        Nav("ipd-admissions", "admissions");
        var a = await Query.AdmissionAsync(Id);
        if (a is null) return NotFound();
        if (!await CanAccessAsync(a)) return Forbid();
        L = (await _billing.LedgerAsync(Id))!;
        CanLedger = await CanAsync(Perm.IpdViewLedger);
        Methods = await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().PaymentMethodsAsync(activeOnly: true);
        Packages = await _billing.PackagesAsync(activeOnly: true);
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    private async Task<IActionResult> Done(ServiceResult r, string ok)
    {
        if (!r.Succeeded) { if (await LoadAsync() is { } x) return x; AddErrors(r); return Page(); }
        FlashSuccess = ok;
        return Redirect($"/Ipd/Account/{Id}");
    }

    public async Task<IActionResult> OnPostAdvanceAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceReceiveAdvances) is { } d) return d;
        var r = await _billing.ReceiveAdvanceAsync(new AdvanceInput { IdempotencyKey = IdempotencyKey, AdmissionId = Id, Amount = Amount, PaymentMethodId = PaymentMethodId, Reference = Reference, Notes = Reason });
        return await Done(r, r.Succeeded ? $"Advance {r.Value!.Number} of {r.Value.Amount:N2} received." : "");
    }

    public async Task<IActionResult> OnPostApplyAdvanceAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceReceiveAdvances) is { } d) return d;
        var r = await _billing.ApplyAdvanceAsync(Id, IdempotencyKey);
        return await Done(r, r.Succeeded ? $"{r.Value:N2} of the advance adjusted against the bill." : "");
    }

    public async Task<IActionResult> OnPostRefundAdvanceAsync(long advanceId)
    {
        if (await DenyUnlessAsync(Perm.FinanceApproveRefunds) is { } d) return d;
        return await Done(await _billing.RefundAdvanceAsync(advanceId, Amount, Reason, PaymentMethodId), "Advance returned to the patient.");
    }

    public async Task<IActionResult> OnPostPackageAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManagePackages) is { } d) return d;
        return await Done(await _billing.ApplyPackageAsync(Id, PackageId), "Package applied – its included services are not billed separately.");
    }

    public async Task<IActionResult> OnPostRemovePackageAsync(long admissionPackageId)
    {
        if (await DenyUnlessAsync(Perm.IpdManagePackages) is { } d) return d;
        return await Done(await _billing.RemovePackageAsync(admissionPackageId, Reason), "Package removed.");
    }

    public async Task<IActionResult> OnPostWaiveFeeAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageAdmissionFee) is { } d) return d;
        return await Done(await _billing.WaiveAdmissionFeeAsync(Id, Reason), "Free admission recorded.");
    }
}
