using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>Patient-based results list: one row per visit with verification and report status. A search looks through all dates.</summary>
[HasPermission(Perm.LabViewResults)]
public class ResultsModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly HospitalClock _clock;
    public ResultsModel(ILabService lab, HospitalClock clock) { _lab = lab; _clock = clock; }

    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Scope { get; set; }
    public string PresetKey { get; private set; } = "today";
    public IReadOnlyList<LabOrderSummary> Orders { get; private set; } = Array.Empty<LabOrderSummary>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "lab-results";
        ViewData["LabTab"] = "results";
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, await _clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        var search = !string.IsNullOrWhiteSpace(Q);
        Orders = await _lab.OrdersAsync(new LabOrderQuery
        {
            From = search ? null : from, To = search ? null : to, Q = Q,
            Scope = Scope is "pending" or "verification" or "verified" or "released" ? Scope : null, Take = 500
        });
    }
}
