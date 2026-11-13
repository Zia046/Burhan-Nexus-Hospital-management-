using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

[HasPermission(Perm.PatientsView)]
public class ProfileModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IEncounterService _encounters;
    private readonly HospitalClock _clock;

    public ProfileModel(IPatientService patients, IEncounterService encounters, HospitalClock clock)
    {
        _patients = patients;
        _encounters = encounters;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "type")] public string? TypeFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "from")] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;
    [BindProperty] public string? Reason { get; set; }

    public Patient Patient { get; private set; } = null!;
    public string AgeText { get; private set; } = "—";
    public bool CanSeeVisits { get; private set; }
    public PagedResult<EncounterListItem> Visits { get; private set; } = new(Array.Empty<EncounterListItem>(), 0, 1, 25);
    public int OpdCount { get; private set; }
    public int EmergencyCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "patients";
        var p = await _patients.GetAsync(Id);
        if (p is null) return NotFound();
        Patient = p;
        AgeText = AgeCalculator.Of(p, await _clock.TodayAsync())?.ToString() ?? "—";
        CanSeeVisits = await CanAsync(Perm.PatientsVisits);
        if (CanSeeVisits)
        {
            EncounterType? type = TypeFilter switch { "opd" => EncounterType.Opd, "emergency" => EncounterType.Emergency, _ => null };
            Visits = await _encounters.ListAsync(new EncounterQuery { PatientId = Id, Type = type, From = From, To = To, Page = PageNumber, PageSize = 25 });
            var all = await _encounters.ListAsync(new EncounterQuery { PatientId = Id, PageSize = 200 });
            OpdCount = all.Items.Count(v => v.Type == EncounterType.Opd);
            EmergencyCount = all.Items.Count(v => v.Type == EncounterType.Emergency);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostArchiveAsync(bool archive)
    {
        if (await DenyUnlessAsync(Perm.PatientsArchive) is { } deny) return deny;
        var r = await _patients.SetArchivedAsync(Id, archive, Reason);
        if (r.Succeeded) FlashSuccess = archive ? "Patient record archived. It is kept and can be restored at any time." : "Patient record restored.";
        else FlashError = r.ErrorText;
        return Redirect($"/Patients/Profile/{Id}");
    }
}
