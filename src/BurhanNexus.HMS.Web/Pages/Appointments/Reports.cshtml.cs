using System.Globalization;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Appointments;

/// <summary>Doctor-wise daily, monthly and yearly appointment statistics and cancelled-appointment history.</summary>
[HasPermission(Perm.AppointmentsReports)]
public class ReportsModel : HmsPageModel
{
    private readonly IAppointmentService _appointments;
    private readonly IRegistrationSetupService _setup;
    private readonly HospitalClock _clock;

    public ReportsModel(IAppointmentService appointments, IRegistrationSetupService setup, HospitalClock clock)
    {
        _appointments = appointments;
        _setup = setup;
        _clock = clock;
    }

    /// <summary>day | month | year</summary>
    [BindProperty(SupportsGet = true)] public string Period { get; set; } = "day";
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public string? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    [BindProperty(SupportsGet = true, Name = "doctor")] public int? DoctorId { get; set; }

    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public string Title { get; private set; } = "";
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<DoctorAppointmentStats> Stats { get; private set; } = Array.Empty<DoctorAppointmentStats>();
    public IReadOnlyList<AppointmentDayCount> Daily { get; private set; } = Array.Empty<AppointmentDayCount>();
    public IReadOnlyList<AppointmentListItem> Cancelled { get; private set; } = Array.Empty<AppointmentListItem>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "appointment-reports";
        var today = await _clock.TodayAsync();
        Doctors = await _setup.DoctorsAsync(activeOnly: false);
        switch (Period)
        {
            case "month":
                var m = DateOnly.TryParseExact((Month ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var md)
                    ? md : new DateOnly(today.Year, today.Month, 1);
                Month = m.ToString("yyyy-MM");
                From = m; To = m.AddMonths(1).AddDays(-1);
                Title = m.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
                break;
            case "year":
                var y = Year is >= 2000 and <= 2200 ? Year.Value : today.Year;
                Year = y;
                From = new DateOnly(y, 1, 1); To = new DateOnly(y, 12, 31);
                Title = y.ToString(CultureInfo.InvariantCulture);
                break;
            default:
                Period = "day";
                Date ??= today;
                From = To = Date.Value;
                Title = DayTitle(Date.Value);
                break;
        }
        Stats = (await _appointments.StatsAsync(From, To)).Where(s => DoctorId is null || s.DoctorId == DoctorId).ToList();
        Daily = (await _appointments.DailyCountsAsync(From, To)).Where(d => DoctorId is null || d.DoctorId == DoctorId).ToList();
        Cancelled = await _appointments.ListAsync(new AppointmentQuery { From = From, To = To, DoctorId = DoctorId, Status = "cancelled", PageSize = 1000 });
    }

    private static string DayTitle(DateOnly d) => d.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
}
