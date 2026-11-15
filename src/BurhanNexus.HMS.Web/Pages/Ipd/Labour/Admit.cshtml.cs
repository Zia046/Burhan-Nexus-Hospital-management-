using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Labour;

/// <summary>Labour Room admission: existing patient → pregnancy → labour bed → initial labour details.</summary>
[HasPermission(Perm.IpdManageLabour)]
public class AdmitModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    private readonly IPatientService _patients;
    public AdmitModel(HmsDbContext db, IPatientService patients) { _db = db; _patients = patients; }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true)] public long? PregnancyId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public AdmitInput Input { get; set; } = new();
    [BindProperty] public LabourInput Labour { get; set; } = new();

    public Patient? Patient { get; private set; }
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public Pregnancy? ActivePregnancy { get; private set; }
    public IpdAdmission? Current { get; private set; }
    public IReadOnlyList<IpdBed> Beds { get; private set; } = Array.Empty<IpdBed>();
    public IReadOnlyList<PatientEncounter> Visits { get; private set; } = Array.Empty<PatientEncounter>();

    private async Task LoadAsync()
    {
        Nav("ipd-labour", "labour");
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is null) return;
            ActivePregnancy = await _db.Pregnancies.AsNoTracking().FirstOrDefaultAsync(p => p.PatientId == pid && p.Status == PregnancyStatus.Active);
            Current = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(a => a.PatientId == pid && IpdService.ActiveStatuses.Contains(a.Status));
            Beds = (await Setup.BedsAsync(null, activeOnly: true)).Where(b => b.Status is BedStatus.Available or BedStatus.Reserved && (b.Ward!.Type == WardType.LabourRoom || b.Type == BedType.Labour)).ToList();
            Visits = await _db.PatientEncounters.AsNoTracking().Where(e => e.PatientId == pid).OrderByDescending(e => e.EncounterUtc).Take(3).ToListAsync();
        }
        else if (!string.IsNullOrWhiteSpace(Q)) Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input.IdempotencyKey = Guid.NewGuid();
        Input.Source = AdmissionSource.Maternity;
        Input.SourceEncounterId = Visits.FirstOrDefault()?.Id;
        Input.DepartmentId = await _db.Departments.Where(d => d.Name == "Gynae").Select(d => (int?)d.Id).FirstOrDefaultAsync();
        Labour.PregnancyId = PregnancyId ?? ActivePregnancy?.Id;
        if (ActivePregnancy?.GestationOn(await Clock.TodayAsync()) is string ga) Labour.GestationalAge = ga;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        Input.Kind = AdmissionKind.Labour;
        Input.PregnancyId = Labour.PregnancyId;
        var r = await Maternity.AdmitToLabourAsync(Input, Labour);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Admitted to the Labour Room: {r.Value!.AdmissionNumber}.";
        return Redirect($"/Ipd/Admission/{r.Value.Id}?tab=labour");
    }
}
