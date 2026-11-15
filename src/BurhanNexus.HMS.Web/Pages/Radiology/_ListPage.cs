using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Radiology;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Shared base of the radiology lists – rows are limited to the modalities the user may see.</summary>
public abstract class RadiologyListModel : RadiologyPageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Modality { get; set; }
    public IReadOnlyList<RadiologyExamRow> Rows { get; protected set; } = Array.Empty<RadiologyExamRow>();

    protected async Task<RadiologyModality?> ModalityFilterAsync()
    {
        var only = await OnlyModalityAsync();
        if (only is not null && !await CanAsync(Core.Security.Perm.RadEnterReport) && !await CanAsync(Core.Security.Perm.RadVerifyReport)) return only;
        return Enum.TryParse<RadiologyModality>(Modality, true, out var m) ? m : null;
    }

    protected async Task LoadAsync(string scope, string nav, string tab, DateOnly? from = null, DateOnly? to = null, bool openEarlier = true)
    {
        Nav(nav, tab);
        var today = await HttpContext.RequestServices.GetRequiredService<HospitalClock>().TodayAsync();
        Rows = await Rad.ExamsAsync(new RadiologyWorklistQuery
        {
            Modality = await ModalityFilterAsync(), Scope = scope, Q = Q, From = from ?? today, To = to ?? today, IncludeOpenEarlier = openEarlier, Take = 500
        });
    }
}
