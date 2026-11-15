using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Delivery;

/// <summary>Delivery record for a Labour Room admission – one or more babies, each registered with its own Patient ID.</summary>
[HasPermission(Perm.IpdEnterDelivery)]
public class NewModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    public NewModel(HmsDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public long AdmissionId { get; set; }
    [BindProperty(SupportsGet = true, Name = "babies")] public int BabyCount { get; set; } = 1;
    [BindProperty] public DeliveryInput Input { get; set; } = new();

    public IpdAdmission A { get; private set; } = null!;
    public LabourRecord? Labour { get; private set; }
    public IReadOnlyList<IpdBed> NicuBeds { get; private set; } = Array.Empty<IpdBed>();

    private async Task<bool> LoadAsync()
    {
        Nav("ipd-labour", "labour");
        var a = await Query.AdmissionAsync(AdmissionId);
        if (a is null || !await CanAccessAsync(a)) return false;
        A = a;
        Labour = await _db.LabourRecords.AsNoTracking().FirstOrDefaultAsync(l => l.AdmissionId == AdmissionId);
        NicuBeds = (await Setup.BedsAsync(null, activeOnly: true)).Where(b => b.Ward!.Type == WardType.Nicu && b.Status is BedStatus.Available or BedStatus.Reserved).ToList();
        BabyCount = Math.Clamp(BabyCount, 1, 6);
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Input.IdempotencyKey = Guid.NewGuid();
        Input.GestationalAge = Labour?.GestationalAge;
        Input.DeliveringDoctor = A.AdmittingDoctor?.Name ?? A.AdmittingDoctorName;
        for (var n = 0; n < BabyCount; n++) Input.Babies.Add(new BabyInput());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await Maternity.RecordDeliveryAsync(AdmissionId, Input);
        if (r.Succeeded)
        {
            FlashSuccess = $"Delivery {r.Value!.Number} recorded – {r.Value.BabyCount} baby(ies) registered with their own Patient IDs.";
            return Redirect($"/Ipd/Delivery/{r.Value.Id}");
        }
        if (!await LoadAsync()) return NotFound();
        BabyCount = Math.Max(1, Input.Babies.Count);
        while (Input.Babies.Count < BabyCount) Input.Babies.Add(new BabyInput());
        AddErrors(r);
        return Page();
    }
}
