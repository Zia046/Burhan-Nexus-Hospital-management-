using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Emergency;

/// <summary>Fast emergency registration: existing patient, new patient with minimum details, or unidentified patient.</summary>
[HasPermission(Perm.EmergencyAdd)]
public class RegisterModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IEncounterService _encounters;
    private readonly IRegistrationSetupService _setup;
    private readonly HospitalClock _clock;

    public RegisterModel(IPatientService patients, IEncounterService encounters, IRegistrationSetupService setup, HospitalClock clock)
    {
        _patients = patients;
        _encounters = encounters;
        _setup = setup;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public PatientInput Patient { get; set; } = new() { AgeUnit = AgeUnit.Years, GuardianRelation = GuardianRelation.SonOf };
    [BindProperty] public EmergencyDetailsInput Visit { get; set; } = new();
    [BindProperty] public DateTime? ArrivalLocal { get; set; }

    public Patient? Existing { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<EmergencyCategory> Categories { get; private set; } = Array.Empty<EmergencyCategory>();
    public bool CanRegisterNewPatient { get; private set; }
    public IReadOnlyList<DuplicateCandidate> Duplicates { get; private set; } = Array.Empty<DuplicateCandidate>();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "emergency";
        Categories = await _setup.CategoriesAsync(activeOnly: true);
        CanRegisterNewPatient = await CanAsync(Perm.PatientsAdd);
        if (PatientId is long id)
        {
            Existing = await _patients.GetAsync(id);
            if (Existing is not null) AgeText = AgeCalculator.Of(Existing, await _clock.TodayAsync())?.ToString() ?? "—";
        }
        else if (!string.IsNullOrWhiteSpace(Q))
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 10 })).Items;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        ArrivalLocal = await _clock.LocalNowAsync();
        ArrivalLocal = ArrivalLocal.Value.AddSeconds(-ArrivalLocal.Value.Second).AddMilliseconds(-ArrivalLocal.Value.Millisecond);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (PatientId is null && !await CanAsync(Perm.PatientsAdd))
            return Forbid();
        var input = new EmergencyInput
        {
            PatientId = PatientId, NewPatient = PatientId is null ? Patient : null, ArrivalLocal = ArrivalLocal,
            CategoryId = Visit.CategoryId, BriefComplaint = Visit.BriefComplaint, BroughtBy = Visit.BroughtBy,
            AttendantName = Visit.AttendantName, AttendantContact = Visit.AttendantContact, ReferredBy = Visit.ReferredBy,
            IsMedicoLegal = Visit.IsMedicoLegal, Notes = Visit.Notes
        };
        var r = await _encounters.RegisterEmergencyAsync(input);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        var enc = r.Value!;
        FlashSuccess = $"Emergency {enc.EncounterNumber} registered. Patient ID: {enc.Patient?.PatientNumber}";
        var settings = await _setup.GetSettingsAsync();
        if (await CanAsync(Perm.EmergencyPrint))
            return Redirect($"/Print/Emergency/{enc.Id}" + (settings.AutoPrintAfterRegistration ? "?autoprint=true" : ""));
        return Redirect($"/Emergency/Visit/{enc.Id}");
    }
}
