using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Appointments;

/// <summary>Doctor-wise appointment lists for one day: every doctor has his own token sequence and his own list.</summary>
[HasPermission(Perm.AppointmentsView)]
public class IndexModel : HmsPageModel
{
    private readonly IAppointmentService _appointments;
    private readonly IRegistrationSetupService _setup;
    private readonly HospitalClock _clock;

    public IndexModel(IAppointmentService appointments, IRegistrationSetupService setup, HospitalClock clock)
    {
        _appointments = appointments;
        _setup = setup;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true, Name = "doctor")] public int? DoctorId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }

    public DateOnly Today { get; private set; }
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<AppointmentListItem> Items { get; private set; } = Array.Empty<AppointmentListItem>();
    public IReadOnlyList<DoctorAppointmentStats> Stats { get; private set; } = Array.Empty<DoctorAppointmentStats>();
    public bool ShowFees { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "appointments";
        Today = await _clock.TodayAsync();
        Date ??= Today;
        Doctors = await _setup.DoctorsAsync(activeOnly: false);
        ShowFees = await CanAsync(Perm.AppointmentsFees);
        Items = await _appointments.ListAsync(new AppointmentQuery { From = Date, To = Date, DoctorId = DoctorId, Status = Status, Q = Q, PageSize = 2000 });
        Stats = (await _appointments.StatsAsync(Date.Value, Date.Value)).Where(s => DoctorId is null || s.DoctorId == DoctorId).ToList();
    }
}
