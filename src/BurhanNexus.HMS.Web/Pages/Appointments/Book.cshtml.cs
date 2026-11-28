using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
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
    public bool CanCollect { get; private set; }
    public bool CanMarkFree { get; private set; }
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public IReadOnlyList<Counter> Counters { get; private set; } = Array.Empty<Counter>();
    public IReadOnlyList<DoctorDayLoad> AllDoctors => Doctors;

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "appointment-book";
        Settings = await _setup.GetSettingsAsync();
        IsPrivate = true; // Update 16: one unified HMS
        CanCollect = await CanAsync(Perm.AppointmentsCollectFee);
        CanMarkFree = await CanAsync(Perm.AppointmentsFreeByDoctor);
        // The fee is shown to whoever collects or waives it (it is printed on the token slip).
        ShowFees = CanCollect || CanMarkFree || await CanAsync(Perm.AppointmentsFees);
        var finance = HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>();
        Methods = await finance.PaymentMethodsAsync(activeOnly: true);
        if (User.IsSystemAdministrator()) Counters = (await finance.CountersAsync(activeOnly: true)).Select(c => c.Counter).ToList();
        else if (CurrentUserId is int uid) Counters = await finance.CountersOfUserAsync(uid);
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
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15, Routine = !await CanAsync(Perm.PatientsSearch) })).Items;
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Update 17: New Appointment opens the registration screen with the patient search at the top.
        if (PatientId is null && string.IsNullOrWhiteSpace(Q) && await CanAsync(Perm.PatientsAdd))
            return Redirect("/Patients/Register?next=appointment" + (PreselectDoctor is int pd ? $"&doctor={pd}" : ""));
        await LoadAsync();
        Input.AppointmentDate = Today;
        Input.DoctorId = PreselectDoctor ?? 0;
        Input.IdempotencyKey = Guid.NewGuid();
        return Page();
    }

    /// <summary>Update 18: optional disease of this visit (central Disease Master, visit-wise history).</summary>
    [Microsoft.AspNetCore.Mvc.BindProperty] public string? DiseaseName { get; set; }

    private async Task RecordDiseaseAsync(long patientId, long encounterId, long? appointmentId = null)
    {
        if (string.IsNullOrWhiteSpace(DiseaseName)) return;
        var svc = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Patients.IDiseaseService>();
        var d = await svc.RecordAsync(patientId, DiseaseName, encounterId, appointmentId, await CanAsync(Perm.DiseasesAdd));
        if (!d.Succeeded) FlashError = "The disease was not saved: " + string.Join(" ", d.Errors);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        if (!(await _setup.GetSettingsAsync()).AppointmentTimeEnabled) Input.AppointmentTime = null;
        var r = await _appointments.BookAsync(Input, await CanAsync(Perm.AppointmentsCollectFee), await CanAsync(Perm.AppointmentsFreeByDoctor));
        if (!r.Succeeded)
        {
            await LoadAsync();
            if (Input.IdempotencyKey == Guid.Empty) Input.IdempotencyKey = Guid.NewGuid();
            AddErrors(r);
            return Page();
        }
        var a = r.Value!;
        FlashSuccess = $"Appointment {a.Encounter?.EncounterNumber} booked – token {a.TokenNumber}.";
        await RecordDiseaseAsync(a.PatientId, a.EncounterId, a.Id);
        if (await CanAsync(Perm.AppointmentsPrint))
            return Redirect($"/Print/Appointment/{a.Id}" + ((await _setup.GetSettingsAsync()).AutoPrintAfterRegistration ? "?autoprint=true" : ""));
        return Redirect($"/Appointments/Details/{a.Id}");
    }

}
