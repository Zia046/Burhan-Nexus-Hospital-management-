using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Shared helpers of the radiology pages (modality access, services).</summary>
public abstract class RadiologyPageModel : HmsPageModel
{
    protected IRadiologyService Rad => HttpContext.RequestServices.GetRequiredService<IRadiologyService>();
    protected IRadiologyCatalogService Catalog => HttpContext.RequestServices.GetRequiredService<IRadiologyCatalogService>();

    public static string WorklistPermission(RadiologyModality m) => m switch
    {
        RadiologyModality.XRay => Perm.RadViewXRay, RadiologyModality.Ecg => Perm.RadViewEcg, _ => Perm.RadViewUltrasound,
    };

    /// <summary>X-Ray staff see X-Ray work, Ultrasound staff see Ultrasound work; reporting / verifying users see both.</summary>
    protected async Task<bool> CanSeeAsync(RadiologyModality m) =>
        await CanAsync(WorklistPermission(m)) || await CanAsync(Perm.RadEnterReport) || await CanAsync(Perm.RadVerifyReport)
        || await CanAsync(Perm.RadViewHistory) || await CanAsync(Perm.RadDashboard);

    protected async Task<RadiologyModality?> OnlyModalityAsync()
    {
        // Update 15: three modalities – staff who may see exactly one of them see only that one.
        var seen = new List<RadiologyModality>();
        if (await CanAsync(Perm.RadViewXRay)) seen.Add(RadiologyModality.XRay);
        if (await CanAsync(Perm.RadViewUltrasound)) seen.Add(RadiologyModality.Ultrasound);
        if (await CanAsync(Perm.RadViewEcg)) seen.Add(RadiologyModality.Ecg);
        return seen.Count == 1 ? seen[0] : null;
    }

    /// <summary>Update 15: "xray", "ultrasound" or "ecg" (navigation keys).</summary>
    public static string NavKey(RadiologyModality m) => m switch { RadiologyModality.XRay => "xray", RadiologyModality.Ecg => "ecg", _ => "ultrasound" };

    protected void Nav(string nav, string tab)
    {
        ViewData["Nav"] = nav;
        ViewData["RadTab"] = tab;
    }
}
