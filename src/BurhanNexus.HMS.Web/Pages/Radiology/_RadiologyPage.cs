using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Shared helpers of the radiology pages (modality access, services).</summary>
public abstract class RadiologyPageModel : HmsPageModel
{
    protected IRadiologyService Rad => HttpContext.RequestServices.GetRequiredService<IRadiologyService>();
    protected IRadiologyCatalogService Catalog => HttpContext.RequestServices.GetRequiredService<IRadiologyCatalogService>();

    public static string WorklistPermission(RadiologyModality m) => m == RadiologyModality.XRay ? Perm.RadViewXRay : Perm.RadViewUltrasound;

    /// <summary>X-Ray staff see X-Ray work, Ultrasound staff see Ultrasound work; reporting / verifying users see both.</summary>
    protected async Task<bool> CanSeeAsync(RadiologyModality m) =>
        await CanAsync(WorklistPermission(m)) || await CanAsync(Perm.RadEnterReport) || await CanAsync(Perm.RadVerifyReport)
        || await CanAsync(Perm.RadViewHistory) || await CanAsync(Perm.RadDashboard);

    protected async Task<RadiologyModality?> OnlyModalityAsync()
    {
        var x = await CanAsync(Perm.RadViewXRay);
        var u = await CanAsync(Perm.RadViewUltrasound);
        return x == u ? null : x ? RadiologyModality.XRay : RadiologyModality.Ultrasound;
    }

    protected void Nav(string nav, string tab)
    {
        ViewData["Nav"] = nav;
        ViewData["RadTab"] = tab;
    }
}
