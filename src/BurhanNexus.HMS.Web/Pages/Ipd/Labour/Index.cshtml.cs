using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Labour;

/// <summary>Labour Room dashboard: women in labour by status, Labour Room beds and today's deliveries.</summary>
[HasPermission(Perm.IpdManageLabour, Perm.IpdLabourObservations)]
public class IndexModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    public IndexModel(HmsDbContext db) => _db = db;

    public IReadOnlyList<LabourRecord> Active { get; private set; } = Array.Empty<LabourRecord>();
    public IReadOnlyList<DeliveryRecord> Recent { get; private set; } = Array.Empty<DeliveryRecord>();
    public IReadOnlyList<IpdBed> Beds { get; private set; } = Array.Empty<IpdBed>();
    public Dictionary<long, DateTime> LastObservation { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Nav("ipd-labour", "labour");
        var wards = await WardsAsync();
        var open = new[] { LabourStatus.Admitted, LabourStatus.UnderObservation, LabourStatus.EarlyLabour, LabourStatus.ActiveLabour };
        var q = _db.LabourRecords.AsNoTracking().Include(l => l.Admission).ThenInclude(a => a!.Patient).Include(l => l.Admission).ThenInclude(a => a!.CurrentBed)
            .Where(l => open.Contains(l.Status) && IpdService.ActiveStatuses.Contains(l.Admission!.Status));
        if (wards is not null) q = q.Where(l => l.Admission!.CurrentWardId == null || wards.Contains(l.Admission.CurrentWardId.Value));
        Active = (await q.ToListAsync()).OrderByDescending(l => (int)l.Status).ThenBy(l => l.Admission!.AdmittedUtc).ToList();
        var ids = Active.Select(l => l.AdmissionId).ToList();
        LastObservation = await _db.ClinicalRecords.AsNoTracking().Where(r => r.Kind == RecordKind.LabourObservation && r.AdmissionId != null && ids.Contains(r.AdmissionId.Value))
            .GroupBy(r => r.AdmissionId!.Value).Select(g => new { g.Key, Max = g.Max(r => r.RecordedUtc) }).ToDictionaryAsync(x => x.Key, x => x.Max);
        var since = DateTime.UtcNow.AddDays(-3);
        Recent = await _db.DeliveryRecords.AsNoTracking().Include(d => d.Mother).Include(d => d.Newborns).Where(d => d.DeliveredUtc >= since).OrderByDescending(d => d.DeliveredUtc).Take(50).ToListAsync();
        Beds = (await Setup.BedsAsync(null, activeOnly: true)).Where(b => b.Ward!.Type == WardType.LabourRoom || b.Type == BedType.Labour).ToList();
    }
}
