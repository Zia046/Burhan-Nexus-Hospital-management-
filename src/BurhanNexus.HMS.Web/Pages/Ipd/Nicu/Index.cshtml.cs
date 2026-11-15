using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Nicu;

/// <summary>NICU dashboard: babies, cots / incubators / warmers and the latest observation time per baby.</summary>
[HasPermission(Perm.IpdManageNicu, Perm.IpdNicuObservations)]
public class IndexModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    public IndexModel(HmsDbContext db) => _db = db;
    public IReadOnlyList<AdmissionRow> Babies { get; private set; } = Array.Empty<AdmissionRow>();
    public IReadOnlyList<AdmissionRow> RoomingIn { get; private set; } = Array.Empty<AdmissionRow>();
    public IReadOnlyList<IpdBed> Cots { get; private set; } = Array.Empty<IpdBed>();
    public Dictionary<long, DateTime> LastObservation { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Nav("ipd-nicu", "nicu");
        var wards = await WardsAsync();
        Babies = await Query.AdmissionsAsync("active", null, null, AdmissionKind.Nicu, wards);
        RoomingIn = await Query.AdmissionsAsync("active", null, null, AdmissionKind.Newborn, wards);
        Cots = (await Setup.BedsAsync(null, activeOnly: true)).Where(b => b.Ward!.Type == WardType.Nicu).ToList();
        var ids = Babies.Select(b => b.Id).ToList();
        LastObservation = await _db.ClinicalRecords.AsNoTracking().Where(r => r.Kind == RecordKind.NicuObservation && r.AdmissionId != null && ids.Contains(r.AdmissionId.Value))
            .GroupBy(r => r.AdmissionId!.Value).Select(g => new { g.Key, Max = g.Max(r => r.RecordedUtc) }).ToDictionaryAsync(x => x.Key, x => x.Max);
    }
}
