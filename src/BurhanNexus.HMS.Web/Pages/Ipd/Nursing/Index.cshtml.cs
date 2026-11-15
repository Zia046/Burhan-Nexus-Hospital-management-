using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Nursing;

/// <summary>Nursing dashboard – only the wards of the nurse's department(s): patients, due / overdue doses, tasks and handovers to acknowledge.</summary>
[HasPermission(Perm.IpdManageNursing, Perm.IpdNursingNotes, Perm.IpdRecordMedication, Perm.IpdHandover)]
public class IndexModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    public IndexModel(HmsDbContext db) => _db = db;

    [BindProperty(SupportsGet = true, Name = "ward")] public int? WardId { get; set; }
    public IReadOnlyList<IpdWard> Wards { get; private set; } = Array.Empty<IpdWard>();
    public IReadOnlyList<AdmissionRow> Patients { get; private set; } = Array.Empty<AdmissionRow>();
    public IReadOnlyList<MedicationDose> DueDoses { get; private set; } = Array.Empty<MedicationDose>();
    public IReadOnlyList<NursingTask> Tasks { get; private set; } = Array.Empty<NursingTask>();
    public IReadOnlyList<ShiftHandover> Handovers { get; private set; } = Array.Empty<ShiftHandover>();
    public Dictionary<long, AdmissionRow> ById { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Nav("ipd-nursing", "nursing");
        var visible = await WardsAsync();
        Wards = (await Setup.WardsAsync(activeOnly: true)).Where(w => visible is null || visible.Contains(w.Id)).ToList();
        if (WardId is int w && visible is not null && !visible.Contains(w)) WardId = null;
        Patients = await Query.AdmissionsAsync("active", null, WardId, null, visible);
        ById = Patients.ToDictionary(p => p.Id);
        var ids = Patients.Select(p => p.Id).ToList();
        var until = DateTime.UtcNow.AddHours(2);
        var due = new[] { DoseStatus.Scheduled, DoseStatus.Pending };
        DueDoses = await _db.MedicationDoses.AsNoTracking().Include(d => d.Order).Where(d => ids.Contains(d.AdmissionId) && due.Contains(d.Status) && d.ScheduledUtc <= until)
            .OrderBy(d => d.ScheduledUtc).Take(300).ToListAsync();
        Tasks = await _db.NursingTasks.AsNoTracking().Where(t => ids.Contains(t.AdmissionId) && t.Status == NursingTaskStatus.Pending && t.DueUtc <= until)
            .OrderBy(t => t.DueUtc).Take(300).ToListAsync();
        Handovers = await _db.ShiftHandovers.AsNoTracking().Where(h => ids.Contains(h.AdmissionId) && h.AcknowledgedUtc == null).OrderBy(h => h.CreatedUtc).ToListAsync();
    }
}
