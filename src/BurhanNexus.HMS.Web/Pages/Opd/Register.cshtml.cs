using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Opd;

[HasPermission(Perm.OpdAdd)]
public class RegisterModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IEncounterService _encounters;
    private readonly IRegistrationSetupService _setup;
    private readonly IDepartmentService _departments;
    private readonly IHospitalAdminService _hospital;
    private readonly HospitalClock _clock;

    public RegisterModel(IPatientService patients, IEncounterService encounters, IRegistrationSetupService setup,
        IDepartmentService departments, IHospitalAdminService hospital, HospitalClock clock)
    {
        _patients = patients;
        _encounters = encounters;
        _setup = setup;
        _departments = departments;
        _hospital = hospital;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public OpdVisitInput Visit { get; set; } = new();

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<EncounterListItem> RecentVisits { get; private set; } = Array.Empty<EncounterListItem>();
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<ServiceItem> Services { get; private set; } = Array.Empty<ServiceItem>();
    public RegistrationSettings Settings { get; private set; } = null!;
    public HospitalType HospitalType { get; private set; }
    public bool DoctorRequired { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "opd";
        Settings = await _setup.GetSettingsAsync();
        HospitalType = (await _hospital.GetConfigurationAsync()).HospitalType;
        Departments = await _departments.ListAsync(includeInactive: false);
        Doctors = await _setup.DoctorsAsync(activeOnly: true);
        Services = (await _setup.ServiceItemsAsync(activeOnly: true)).Where(s => s.Category == EncounterType.Opd).ToList();
        DoctorRequired = false; // Update 16: OPD doctor is optional (the Consultant button books a doctor)
        if (PatientId is long id)
        {
            Patient = await _patients.GetAsync(id);
            if (Patient is not null)
            {
                AgeText = AgeCalculator.Of(Patient, await _clock.TodayAsync())?.ToString() ?? "—";
                if (await CanAsync(Perm.PatientsVisits))
                    RecentVisits = (await _encounters.ListAsync(new EncounterQuery { PatientId = id, PageSize = 5 })).Items;
            }
        }
        else if (!string.IsNullOrWhiteSpace(Q))
        {
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15, Routine = true })).Items;
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Update 16: OPD opens the new-patient registration form; the patient search opens only with the Search button.
        if (PatientId is null && string.IsNullOrWhiteSpace(Q) && !Search && await CanAsync(Perm.PatientsAdd))
            return Redirect("/Patients/Register?next=opd");
        await LoadAsync();
        Visit.DepartmentId = Settings.DefaultOpdDepartmentId;
        Visit.ServiceItemId = Settings.DefaultOpdServiceItemId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Visit.PatientId = PatientId ?? 0;
        var r = await _encounters.RegisterOpdAsync(Visit);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        var enc = r.Value!;
        FlashSuccess = $"OPD visit {enc.EncounterNumber} registered" + (enc.TokenNumber is int t ? $" – token {t}" : "") + ".";
        var settings = await _setup.GetSettingsAsync();
        if (await CanAsync(Perm.OpdPrint))
        {
            // Update 15/16: the OPD slip (A4 / A5 from the registration settings) prints straight away; a fee set in Fees & Services
            // goes to the registering user's cash account.
            return Redirect($"/Print/Opd/{enc.Id}?autoprint=true");
        }
        return Redirect($"/Opd/Visit/{enc.Id}");
    }
}
