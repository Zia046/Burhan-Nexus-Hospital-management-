using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

[HasPermission(Perm.RadDashboard)]
public class IndexModel : RadiologyPageModel
{
    private readonly HospitalClock _clock;
    public IndexModel(HospitalClock clock) => _clock = clock;

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    /// <summary>all | XRay | Ultrasound</summary>
    [BindProperty(SupportsGet = true)] public string? View { get; set; }
    public RadiologyModality? Modality { get; private set; }
    public bool ShowXRay { get; private set; }
    public bool ShowUltrasound { get; private set; }
    public string PresetKey { get; private set; } = "today";
    public RadiologyDashboard Data { get; private set; } = null!;
    public RadiologyDashboard Month { get; private set; } = null!;
    public IReadOnlyList<RadiologyExamRow> Queue { get; private set; } = Array.Empty<RadiologyExamRow>();
    public IReadOnlyList<RadiologyExamRow> Reporting { get; private set; } = Array.Empty<RadiologyExamRow>();
    public IReadOnlyList<RadiologyExamRow> ToVerify { get; private set; } = Array.Empty<RadiologyExamRow>();

    public async Task OnGetAsync()
    {
        Nav("rad-dashboard", "dashboard");
        ShowXRay = await CanAsync(Perm.RadViewXRay);
        ShowUltrasound = await CanAsync(Perm.RadViewUltrasound);
        // Statistics follow the user's worklist permissions: X-Ray staff see X-Ray figures only, and so on.
        Modality = View switch
        {
            "XRay" => RadiologyModality.XRay,
            "Ultrasound" => RadiologyModality.Ultrasound,
            _ => ShowXRay == ShowUltrasound ? null : ShowXRay ? RadiologyModality.XRay : RadiologyModality.Ultrasound,
        };
        if (Modality == RadiologyModality.XRay && !ShowXRay && ShowUltrasound) Modality = RadiologyModality.Ultrasound;
        if (Modality == RadiologyModality.Ultrasound && !ShowUltrasound && ShowXRay) Modality = RadiologyModality.XRay;
        var today = await _clock.TodayAsync();
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, today);
        From = from; To = to; PresetKey = key;
        var finance = await CanAsync(Perm.RadViewFinancial);
        Data = await Rad.DashboardAsync(from, to, Modality, finance);
        Month = await Rad.DashboardAsync(new DateOnly(today.Year, today.Month, 1), today, Modality, finance);
        Queue = await Rad.ExamsAsync(new RadiologyWorklistQuery { Modality = Modality, Scope = "queue", From = today, To = today, IncludeOpenEarlier = true, Take = 50 });
        Reporting = await Rad.ExamsAsync(new RadiologyWorklistQuery { Modality = Modality, Scope = "reporting", From = today, To = today, IncludeOpenEarlier = true, Take = 50 });
        ToVerify = await Rad.ExamsAsync(new RadiologyWorklistQuery { Modality = Modality, Scope = "verification", From = today, To = today, IncludeOpenEarlier = true, Take = 50 });
    }
}
