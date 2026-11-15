using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Pharmacy dashboard: stock alerts, today's work and (Private, with permission) money figures – all from inventory transactions.</summary>
[HasPermission(Perm.PharmDashboard)]
public class IndexModel : PharmacyPageModel
{
    private readonly HospitalClock _clock;
    public IndexModel(HospitalClock clock) => _clock = clock;

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    public string PresetKey { get; private set; } = "today";
    public PharmacyDashboard Data { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        await InitAsync("dashboard", "pharm-dashboard");
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, await _clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        Data = await Query.DashboardAsync(from, to, IsPrivate && await CanAsync(Perm.PharmViewFinancialReports));
    }
}
