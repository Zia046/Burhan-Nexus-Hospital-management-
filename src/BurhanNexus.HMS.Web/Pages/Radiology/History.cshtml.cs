using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Find a patient to open the radiology history.</summary>
[HasPermission(Perm.RadViewHistory)]
public class HistoryModel : RadiologyPageModel
{
    private readonly IPatientService _patients;
    public HistoryModel(IPatientService patients) => _patients = patients;

    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();

    public async Task OnGetAsync()
    {
        Nav("rad-history", "history");
        if (!string.IsNullOrWhiteSpace(Q)) Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 20 })).Items;
    }
}
