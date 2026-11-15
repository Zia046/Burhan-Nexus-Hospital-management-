using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

[HasPermission(Perm.IpdDashboard)]
public class IndexModel : IpdPageModel
{
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? DepartmentId { get; set; }
    [BindProperty(SupportsGet = true)] public int? WardId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public string PresetKey { get; private set; } = "today";
    public IpdDashboard Data { get; private set; } = null!;
    public IReadOnlyList<AdmissionRow> Patients { get; private set; } = Array.Empty<AdmissionRow>();
    public IReadOnlyList<IpdWard> Wards { get; private set; } = Array.Empty<IpdWard>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();

    public async Task OnGetAsync()
    {
        Nav("ipd-dashboard", "dashboard");
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, await Clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        var wards = await WardsAsync();
        // Financial figures only with the IPD / Phase 3 financial permissions.
        var finance = await CanAsync(Perm.IpdViewFinancial) && await CanAsync(Perm.FinanceDashboard);
        Data = await Query.DashboardAsync(from, to, DepartmentId, WardId, wards, finance);
        var scope = Status is "discharge" or "attention" or "discharged" or "admitted" ? Status : "active";
        Patients = await Query.AdmissionsAsync(scope, null, WardId, null, wards, scope is "discharged" or "admitted" ? from : null, scope is "discharged" or "admitted" ? to : null, take: 200);
        Wards = (await Setup.WardsAsync(activeOnly: true)).Where(w => wards is null || wards.Contains(w.Id)).ToList();
        Departments = await HttpContext.RequestServices.GetRequiredService<HmsDbContext>().Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
    }
}
