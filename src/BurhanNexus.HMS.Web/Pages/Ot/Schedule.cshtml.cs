using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>Daily / weekly OT schedule by theatre (bookings with their preparation / cleaning interval).</summary>
[HasPermission(Perm.OtDashboard, Perm.OtCreateBooking)]
public class ScheduleModel : OtPageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public string View { get; set; } = "day";
    [BindProperty(SupportsGet = true)] public int? TheatreId { get; set; }
    public IReadOnlyList<OtTheatre> Theatres { get; private set; } = Array.Empty<OtTheatre>();
    public IReadOnlyList<SurgeryRow> Rows { get; private set; } = Array.Empty<SurgeryRow>();
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }

    public async Task OnGetAsync()
    {
        Nav("ot-schedule", "schedule");
        var d = Date ?? await Clock.TodayAsync();
        Date = d;
        if (View == "week") { From = d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); To = From.AddDays(6); } else { View = "day"; From = d; To = d; }
        Theatres = (await Setup.TheatresAsync(activeOnly: true)).Where(t => TheatreId is null || t.Id == TheatreId).ToList();
        Rows = (await Query.SurgeriesAsync(new OtFilter { From = From, To = To, TheatreId = TheatreId })).Where(r => r.Status != SurgeryStatus.Cancelled).ToList();
    }
}
