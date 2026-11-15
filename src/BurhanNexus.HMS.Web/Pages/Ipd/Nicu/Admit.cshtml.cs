using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Nicu;

/// <summary>NICU admission – a baby born here (from the delivery record), a rooming-in newborn, or an external referral.</summary>
[HasPermission(Perm.IpdManageNicu)]
public class AdmitModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    private readonly IPatientService _patients;
    public AdmitModel(HmsDbContext db, IPatientService patients) { _db = db; _patients = patients; }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public NicuInput Input { get; set; } = new();

    public Patient? Patient { get; private set; }
    public NewbornRecord? Birth { get; private set; }
    public IpdAdmission? Current { get; private set; }
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<IpdBed> Cots { get; private set; } = Array.Empty<IpdBed>();

    private async Task LoadAsync()
    {
        Nav("ipd-nicu", "nicu");
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is null) return;
            Birth = await _db.NewbornRecords.AsNoTracking().Include(n => n.Delivery).FirstOrDefaultAsync(n => n.NewbornPatientId == pid);
            Current = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(a => a.PatientId == pid && IpdService.ActiveStatuses.Contains(a.Status));
            Cots = (await Setup.BedsAsync(null, activeOnly: true)).Where(b => b.Ward!.Type == WardType.Nicu && b.Status is BedStatus.Available or BedStatus.Reserved).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(Q)) Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input.IdempotencyKey = Guid.NewGuid();
        if (Birth is not null)
        {
            Input.NewbornRecordId = Birth.Id;
            Input.Source = Current is null ? AdmissionSource.DeliveryRoom : AdmissionSource.PostnatalWard;
            Input.BirthWeightGrams = Birth.BirthWeightGrams;
            Input.GestationalAge = Birth.GestationalAge;
        }
        else Input.Source = AdmissionSource.ExternalReferral;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        var r = await Maternity.AdmitToNicuAsync(Input);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Admitted to the NICU: {r.Value!.AdmissionNumber}.";
        return Redirect($"/Ipd/Admission/{r.Value.Id}?tab=nicu");
    }
}
