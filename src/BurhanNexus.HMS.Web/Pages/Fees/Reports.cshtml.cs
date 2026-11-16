using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Fees;

/// <summary>Update 10: service-wise usage and revenue, taken from the Phase 3 charges (the patient bills) – no separate ledger.</summary>
[HasPermission(Perm.SvcReports)]
public class ReportsModel : HmsPageModel
{
    private readonly HmsDbContext _db;
    private readonly HospitalClock _clock;
    public ReportsModel(HmsDbContext db, HospitalClock clock) { _db = db; _clock = clock; }

    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? Category { get; set; }
    [BindProperty(SupportsGet = true)] public int? Department { get; set; }

    public sealed record Row(int ServiceId, string Code, string Name, string? Category, int Count, decimal Gross, decimal Discount, decimal Net, decimal Paid, decimal Refunded, int Free);
    public IReadOnlyList<Row> Rows { get; private set; } = Array.Empty<Row>();
    public IReadOnlyList<ServiceCategory> Categories { get; private set; } = Array.Empty<ServiceCategory>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "fees-reports";
        ViewData["FeesTab"] = "reports";
        var today = await _clock.TodayAsync();
        From ??= new DateOnly(today.Year, today.Month, 1);
        To ??= today;
        if (To < From) (From, To) = (To, From);
        Categories = await _db.ServiceCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync();
        Departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
        var q = _db.Charges.AsNoTracking().Where(c => c.Status != ChargeStatus.Cancelled && c.BusinessDate >= From && c.BusinessDate <= To);
        if (Category is int cat) q = q.Where(c => c.ServiceItem!.ServiceCategoryId == cat);
        if (Department is int dep) q = q.Where(c => c.DepartmentId == dep || c.ServiceItem!.DepartmentId == dep);
        Rows = (await q.GroupBy(c => new { c.ServiceItemId, c.ServiceItem!.Code, c.ServiceItem.Name, Cat = c.ServiceItem.ServiceCategory!.Name })
                .Select(g => new Row(g.Key.ServiceItemId, g.Key.Code, g.Key.Name, g.Key.Cat, g.Count(), g.Sum(c => c.Fee), g.Sum(c => c.Discount), g.Sum(c => c.NetAmount),
                    g.Sum(c => c.PaidAmount), g.Sum(c => c.RefundedAmount), g.Count(c => c.NetAmount == 0)))
                .ToListAsync())
            .OrderBy(r => r.Category).ThenByDescending(r => r.Net).ToList();
    }
}
