using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>All previous laboratory visits of one patient with the original (as reported) values side by side.</summary>
[HasPermission(Perm.LabViewPrevious, Perm.LabViewResults)]
public class PatientModel : HmsPageModel
{
    private readonly HmsDbContext _db;
    public PatientModel(HmsDbContext db) => _db = db;

    public Patient Patient { get; private set; } = null!;
    public IReadOnlyList<LabOrder> Orders { get; private set; } = Array.Empty<LabOrder>();
    /// <summary>Test code → parameter → values by visit (verified/released results only), for the trend table.</summary>
    public IReadOnlyList<(string Test, string Parameter, string? Unit, IReadOnlyDictionary<long, LabResultValue> ByOrder)> Trend { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(long id)
    {
        ViewData["Nav"] = "lab-results";
        ViewData["LabTab"] = "results";
        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        Patient = p;
        Orders = await _db.LabOrders.AsNoTracking().Where(o => o.PatientId == id)
            .Include(o => o.Items).ThenInclude(i => i.Values)
            .OrderByDescending(o => o.Id).Take(50).AsSplitQuery().ToListAsync();
        var final = Orders.SelectMany(o => o.Items.Where(i => i.Status is LabOrderItemStatus.Verified or LabOrderItemStatus.Released)
            .SelectMany(i => i.Values.Where(v => v.Value != null).Select(v => (o.Id, i.TestCode, v)))).ToList();
        Trend = final.GroupBy(x => (x.TestCode, x.v.Name))
            .Where(g => g.Select(x => x.Id).Distinct().Count() > 1)
            .Select(g => (g.Key.TestCode, g.Key.Name, g.First().v.Unit, (IReadOnlyDictionary<long, LabResultValue>)g.GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First().v)))
            .OrderBy(x => x.TestCode).ToList();
        return Page();
    }
}
