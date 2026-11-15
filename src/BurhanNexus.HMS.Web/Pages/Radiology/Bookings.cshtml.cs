using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>All radiology bookings of a period (any status, cancelled included).</summary>
[HasPermission(Perm.RadCreateBooking, Perm.RadEditBooking, Perm.RadViewXRay, Perm.RadViewUltrasound)]
public class BookingsModel : RadiologyListModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }

    public async Task OnGetAsync()
    {
        var today = await HttpContext.RequestServices.GetRequiredService<HospitalClock>().TodayAsync();
        From ??= string.IsNullOrWhiteSpace(Q) ? today : today.AddYears(-5);
        To ??= today.AddDays(90);
        await LoadAsync("all", "rad-bookings", "bookings", From, To, openEarlier: false);
    }
}
