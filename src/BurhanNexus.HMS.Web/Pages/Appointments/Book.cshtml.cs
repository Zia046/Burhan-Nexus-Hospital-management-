using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Appointments;

/// <summary>Private hospital reception: find/register patient → select doctor → doctor-wise token → print thermal slip.</summary>
[HasPermission(Perm.AppointmentsAdd)]
public class BookModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IAppointmentService _appointments;
    private readonly IRegistrationSetupService _setup;
    private readonly IHospitalAdminService _hospital;
    private readonly HospitalClock _clock;

    public BookModel(IPatientService patients, IAppointmentService appointments, IRegistrationSetupService setup,
        IHospitalAdminService hospital, HospitalClock clock)
    {
        _patients = patients;
        _appointments = appointments;
        _setup = setup;
        _hospital = hospital;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "doctor")] public int? PreselectDoctor { get; set; }
    [BindProperty] public BookAppointmentInput Input { get; set; } = new();

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<DoctorDayLoad> Doctors { get; private set; } = Array.Empty<DoctorDayLoad>();
    public IReadOnlyList<AppointmentListItem> Recent { get; private set; } = Array.Empty<AppointmentListItem>();
    public RegistrationSettings Settings { get; private set; } = null!;
    public bool IsPrivate { get; private set; }
    public bool ShowFees { get; private set; }
    public DateOnly Today { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "appointment-book";
        Settings = await _setup.GetSettingsAsync();
        IsPrivate = (await _hospital.GetConfigurationAsync()).HospitalType == HospitalType.Private;
        ShowFees = await CanAsync(Perm.AppointmentsFees);
        Today = await _clock.TodayAsync();
        Doctors = await _appointments.DayLoadAsync(Input.AppointmentDate ?? Today);
        if (PatientId is long id)
        {
            Patient = await _patients.GetAsync(id);
            if (Patient is not null)
            {
                AgeText = AgeCalculator.Of(Patient, Today)?.ToString() ?? "—";
                Recent = (await _appointments.ListAsync(new AppointmentQuery { PatientId = id, From = Today.AddDays(-365), PageSize = 50 }))
                    .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.Id).Take(5).ToList();
            }
        }
        else if (!string.IsNullOrWhiteSpace(Q))
        {
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        Input.AppointmentDate = Today;
        Input.DoctorId = PreselectDoctor ?? 0;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        if (!(await _setup.GetSettingsAsync()).AppointmentTimeEnabled) Input.AppointmentTime = null;
        var r = await _appointments.BookAsync(Input);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        var a = r.Value!;
        FlashSuccess = $"Appointment {a.Encounter?.EncounterNumber} booked – token {a.TokenNumber}.";
        if (await CanAsync(Perm.AppointmentsPrint))
            return Redirect($"/Print/Appointment/{a.Id}" + ((await _setup.GetSettingsAsync()).AutoPrintAfterRegistration ? "?autoprint=true" : ""));
        return Redirect($"/Appointments/Details/{a.Id}");
    }

}
