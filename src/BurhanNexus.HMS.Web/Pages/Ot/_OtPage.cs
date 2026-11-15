using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Patients;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>Shared helpers of the Operation Theatre pages.</summary>
public abstract class OtPageModel : HmsPageModel
{
    protected IOtSetupService Setup => HttpContext.RequestServices.GetRequiredService<IOtSetupService>();
    protected ISurgeryService Surgeries => HttpContext.RequestServices.GetRequiredService<ISurgeryService>();
    protected IOtClinicalService Clinical => HttpContext.RequestServices.GetRequiredService<IOtClinicalService>();
    protected IOtBillingService Billing => HttpContext.RequestServices.GetRequiredService<IOtBillingService>();
    protected ICssdService Cssd => HttpContext.RequestServices.GetRequiredService<ICssdService>();
    protected IOtQueryService Query => HttpContext.RequestServices.GetRequiredService<IOtQueryService>();
    protected HospitalClock Clock => HttpContext.RequestServices.GetRequiredService<HospitalClock>();

    protected void Nav(string nav, string tab)
    {
        ViewData["Nav"] = nav;
        ViewData["OtTab"] = tab;
    }

    public static string StatusClass(SurgeryStatus s) => s switch
    {
        SurgeryStatus.Requested or SurgeryStatus.Postponed => "status-warn",
        SurgeryStatus.InProgress => "status-info",
        SurgeryStatus.Completed => "status-ok",
        SurgeryStatus.Cancelled => "status-bad",
        _ => "status-info",
    };
}
