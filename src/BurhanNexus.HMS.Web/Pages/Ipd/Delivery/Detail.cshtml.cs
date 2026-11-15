using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Delivery;

/// <summary>A delivery record (read-only – deliveries are never edited or deleted) with its babies and print links.</summary>
[HasPermission(Perm.IpdManageLabour, Perm.IpdEnterDelivery, Perm.IpdViewPatients)]
public class DetailModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    public DetailModel(HmsDbContext db) => _db = db;
    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    public DeliveryRecord D { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        Nav("ipd-labour", "labour");
        var d = await _db.DeliveryRecords.AsNoTracking().Include(x => x.Mother).Include(x => x.Admission).Include(x => x.Newborns).ThenInclude(n => n.NewbornPatient)
            .FirstOrDefaultAsync(x => x.Id == Id);
        if (d is null || !await CanAccessAsync(d.Admission!)) return NotFound();
        D = d;
        return Page();
    }
}
