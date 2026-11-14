using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

[HasPermission(Perm.LabDashboard)]
public class IndexModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly HospitalClock _clock;

    public IndexModel(ILabService lab, HospitalClock clock)
    {
        _lab = lab;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    public string PresetKey { get; private set; } = "today";
    public LabDashboard Data { get; private set; } = null!;
    public IReadOnlyList<LabOrderSummary> Pending { get; private set; } = Array.Empty<LabOrderSummary>();
    public IReadOnlyList<LabOrderSummary> ToVerify { get; private set; } = Array.Empty<LabOrderSummary>();
    public IReadOnlyList<LabOrderSummary> ToPrint { get; private set; } = Array.Empty<LabOrderSummary>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "lab-dashboard";
        ViewData["LabTab"] = "dashboard";
        var today = await _clock.TodayAsync();
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, today);
        From = from; To = to; PresetKey = key;
        Data = await _lab.DashboardAsync(from, to, await CanAsync(Perm.LabViewFinancial));
        // Work lists are not limited to the period: open work from earlier days still needs attention.
        Pending = await _lab.OrdersAsync(new LabOrderQuery { Scope = "pending", Take = 50 });
        ToVerify = await _lab.OrdersAsync(new LabOrderQuery { Scope = "verification", Take = 50 });
        ToPrint = await _lab.OrdersAsync(new LabOrderQuery { Scope = "verified", Take = 50 });
    }
}
