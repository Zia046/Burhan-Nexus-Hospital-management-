using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>Visual bed map: every ward, room and bed with its status and current patient.</summary>
[HasPermission(Perm.IpdViewPatients, Perm.IpdAllocateBed, Perm.IpdManageBeds)]
public class BedsModel : IpdPageModel
{
    [BindProperty(SupportsGet = true)] public int? WardId { get; set; }
    [BindProperty] public BedStatus NewStatus { get; set; }
    [BindProperty] public string? Note { get; set; }
    public IReadOnlyList<IpdBed> Beds { get; private set; } = Array.Empty<IpdBed>();
    public IReadOnlyList<IpdWard> Wards { get; private set; } = Array.Empty<IpdWard>();
    public Dictionary<long, (string Number, string Patient, string PatientNumber)> Occupants { get; private set; } = new();
    public Dictionary<int, List<string>> RoomingIn { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Nav("ipd-beds", "beds");
        var wards = await WardsAsync();
        Wards = (await Setup.WardsAsync(activeOnly: true)).Where(w => wards is null || wards.Contains(w.Id)).ToList();
        Beds = (await Setup.BedsAsync(WardId, activeOnly: true)).Where(b => wards is null || wards.Contains(b.WardId)).ToList();
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        var ids = Beds.Where(b => b.CurrentAdmissionId is not null).Select(b => b.CurrentAdmissionId!.Value).ToList();
        Occupants = await db.IpdAdmissions.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.AdmissionNumber, a.Patient!.FullName, a.Patient.PatientNumber })
            .ToDictionaryAsync(a => a.Id, a => (a.AdmissionNumber, a.FullName, a.PatientNumber));
        var bedIds = Beds.Select(b => b.Id).ToList();
        RoomingIn = (await db.IpdBedAllocations.AsNoTracking().Where(x => x.IsShared && x.ToUtc == null && bedIds.Contains(x.BedId))
                .Join(db.IpdAdmissions, x => x.AdmissionId, a => a.Id, (x, a) => new { x.BedId, a.Patient!.FullName }).ToListAsync())
            .GroupBy(x => x.BedId).ToDictionary(g => g.Key, g => g.Select(x => x.FullName).ToList());
    }

    public async Task<IActionResult> OnPostStatusAsync(int bedId)
    {
        if (!await CanAsync(Perm.IpdManageBeds) && !await CanAsync(Perm.IpdAllocateBed)) return Forbid();
        var r = await Setup.SetBedStatusAsync(bedId, NewStatus, Note);
        if (r.Succeeded) FlashSuccess = "Bed status changed."; else FlashError = r.ErrorText;
        return RedirectToPage(new { WardId });
    }
}
