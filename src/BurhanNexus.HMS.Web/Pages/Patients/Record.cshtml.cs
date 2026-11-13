using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

/// <summary>Printable patient record summary (demographics + visit list).</summary>
[HasPermission(Perm.PatientsPrint)]
public class RecordModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IEncounterService _encounters;
    private readonly HospitalClock _clock;

    public RecordModel(IPatientService patients, IEncounterService encounters, HospitalClock clock)
    {
        _patients = patients;
        _encounters = encounters;
        _clock = clock;
    }

    public Patient Patient { get; private set; } = null!;
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<EncounterListItem> Visits { get; private set; } = Array.Empty<EncounterListItem>();
    public bool ShowVisits { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var p = await _patients.GetAsync(id);
        if (p is null) return NotFound();
        Patient = p;
        AgeText = AgeCalculator.Of(p, await _clock.TodayAsync())?.ToString() ?? "—";
        ShowVisits = await CanAsync(Perm.PatientsVisits);
        if (ShowVisits) Visits = (await _encounters.ListAsync(new EncounterQuery { PatientId = id, PageSize = 200 })).Items;
        return Page();
    }
}
