using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>
/// Shared base for pharmacy pages. The hospital type (Government / Private) is read on the server for every request;
/// commercial fields are only shown – and only accepted by the services – in Private mode.
/// </summary>
public abstract class PharmacyPageModel : HmsPageModel
{
    protected IPharmacyCatalogService Catalog => HttpContext.RequestServices.GetRequiredService<IPharmacyCatalogService>();
    protected IPharmacyQueryService Query => HttpContext.RequestServices.GetRequiredService<IPharmacyQueryService>();
    protected IPharmacyService Pharmacy => HttpContext.RequestServices.GetRequiredService<IPharmacyService>();

    public bool IsPrivate { get; private set; }
    /// <summary>Private mode and the user may see pharmacy prices / amounts.</summary>
    public bool ShowPrices { get; private set; }

    protected async Task InitAsync(string tab, string? nav = null)
    {
        ViewData["Nav"] = nav ?? "pharm-" + tab;
        ViewData["PharmTab"] = tab;
        IsPrivate = await Catalog.IsPrivateAsync();
        ViewData["PharmPrivate"] = IsPrivate;
        ShowPrices = IsPrivate && (await CanAsync(Perm.PharmViewFinancialReports) || await CanAsync(Perm.PharmRetailSales) || await CanAsync(Perm.PharmManagePrices)
                                   || await CanAsync(Perm.PharmManagePurchases));
    }

    // ---- Update 11: IPD medicine issuance – pay now or leave outstanding on the IPD account ----
    [BindProperty] public string? PayAction { get; set; }
    [BindProperty] public decimal? PayAmount { get; set; }
    [BindProperty] public int? PayMethodId { get; set; }
    [BindProperty] public string? PayReference { get; set; }
    public bool CanReceivePayment { get; private set; }
    public IReadOnlyList<BurhanNexus.HMS.Core.Domain.PaymentMethod> PayMethods { get; private set; } = Array.Empty<BurhanNexus.HMS.Core.Domain.PaymentMethod>();

    protected async Task LoadIpdPaymentAsync()
    {
        CanReceivePayment = IsPrivate && await CanAsync(Perm.FinanceReceiveInpatientPayments);
        if (CanReceivePayment)
            PayMethods = (await HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Finance.IFinanceSetupService>().PaymentMethodsAsync(activeOnly: true))
                .Where(m => m.Code != "ADVANCE").ToList();
    }

    /// <summary>After an IPD issuance: a Phase 3 receipt for exactly that issuance (partial allowed), or the amount stays outstanding – never marked paid.</summary>
    protected async Task SettleIpdIssueAsync(long admissionId, long issueId, string number, Guid key)
    {
        var billing = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Ipd.IIpdBillingService>();
        var due = await billing.MedicineIssueDueAsync(admissionId, issueId);
        if (due <= 0) return;
        if (PayAction == "pay" && await Catalog.IsPrivateAsync() && await CanAsync(Perm.FinanceReceiveInpatientPayments) && PayMethodId is int m)
        {
            var amount = PayAmount ?? due;
            var pay = await billing.PayMedicineIssueAsync(admissionId, issueId, key, m, null, amount, PayReference);
            if (pay.Succeeded)
                FlashSuccess += amount < due ? $" Received {amount:N2} (receipt {pay.Value}); {due - amount:N2} stays outstanding on the IPD account." : $" Paid in full (receipt {pay.Value}).";
            else FlashError = $"{number} was issued, but the payment was not recorded: {pay.ErrorText} The amount stays outstanding on the IPD account.";
        }
        else FlashSuccess += $" {due:N2} added to the IPD account (outstanding until paid).";
    }

    protected async Task<PharmacyRights> RightsAsync() => new(
        SelectBatch: await CanAsync(Perm.PharmSelectBatch),
        Discount: IsPrivate && await CanAsync(Perm.PharmApplyDiscount),
        ManagePurchases: IsPrivate && await CanAsync(Perm.PharmManagePurchases),
        ManagePrices: IsPrivate && await CanAsync(Perm.PharmManagePrices),
        ApproveAdjustments: await CanAsync(Perm.PharmApproveAdjustments));

    /// <summary>Converts "2027-05" (month input) or "2027-05-31" to a date; a month means its last day.</summary>
    public static DateOnly? ParseExpiry(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d)) return d;
        if (DateOnly.TryParseExact(s + "-01", "yyyy-MM-dd", out var m)) return m.AddMonths(1).AddDays(-1);
        return null;
    }
}
