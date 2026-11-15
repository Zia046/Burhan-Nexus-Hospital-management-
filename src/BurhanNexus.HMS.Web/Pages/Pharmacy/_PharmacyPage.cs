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
