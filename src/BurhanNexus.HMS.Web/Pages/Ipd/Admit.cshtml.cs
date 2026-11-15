using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>IPD admission (general / Gynae): existing patient → source visit → department, doctors, diagnosis, attendant → bed.</summary>
[HasPermission(Perm.IpdRegisterAdmission)]
public class AdmitModel : IpdPageModel
{
    private readonly IPatientService _patients;
    private readonly HmsDbContext _db;
    public AdmitModel(IPatientService patients, HmsDbContext db)
    {
        _patients = patients;
        _db = db;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true)] public long? EncounterId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public AdmitInput Input { get; set; } = new();

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<PatientEncounter> Visits { get; private set; } = Array.Empty<PatientEncounter>();
    public IReadOnlyList<IpdBed> Beds { get; private set; } = Array.Empty<IpdBed>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IpdAdmission? Current { get; private set; }
    public bool IsGynae => Kind == "gynae";

    private async Task LoadAsync()
    {
        Nav("ipd-admit", "admit");
        if (EncounterId is long eid && PatientId is null)
            PatientId = await _db.PatientEncounters.Where(e => e.Id == eid).Select(e => (long?)e.PatientId).FirstOrDefaultAsync();
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is null) return;
            AgeText = AgeCalculator.Of(Patient, await Clock.TodayAsync())?.ToString() ?? "—";
            Visits = await _db.PatientEncounters.AsNoTracking().Include(e => e.Department).Include(e => e.Doctor).Where(e => e.PatientId == pid)
                .OrderByDescending(e => e.EncounterUtc).Take(5).ToListAsync();
            Current = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(a => a.PatientId == pid && IpdService.ActiveStatuses.Contains(a.Status));
            var wards = await WardsAsync();
            Beds = (await Setup.BedsAsync(null, activeOnly: true))
                .Where(b => b.Status is BedStatus.Available or BedStatus.Reserved && b.Ward!.IsActive && b.Ward.Type is not (WardType.Nicu or WardType.LabourRoom)
                            && (!IsGynae || b.Ward.Type is WardType.Gynae or WardType.Maternity or WardType.Postnatal)).ToList();
            Departments = await _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
            Doctors = await HttpContext.RequestServices.GetRequiredService<IRegistrationSetupService>().DoctorsAsync(activeOnly: true);
        }
        else if (!string.IsNullOrWhiteSpace(Q))
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        Input.IdempotencyKey = Guid.NewGuid();
        Input.Kind = IsGynae ? AdmissionKind.Gynae : AdmissionKind.General;
        var v = Visits.FirstOrDefault(x => x.Id == EncounterId) ?? Visits.FirstOrDefault(x => x.EncounterDate >= (await Clock.TodayAsync()).AddDays(-1));
        if (v is not null)
        {
            Input.SourceEncounterId = v.Id;
            Input.Source = IsGynae ? AdmissionSource.Gynae : v.Type == EncounterType.Emergency ? AdmissionSource.Emergency : AdmissionSource.Opd;
            Input.ReferringDoctor = v.Doctor?.Name;
        }
        else Input.Source = IsGynae ? AdmissionSource.Gynae : AdmissionSource.Direct;
        Input.DepartmentId = Departments.FirstOrDefault(d => d.Name == (IsGynae ? "Gynae" : "IPD"))?.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        if (Input.Kind is not (AdmissionKind.General or AdmissionKind.Gynae)) Input.Kind = AdmissionKind.General;
        var r = await Ipd.AdmitAsync(Input);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Patient admitted: {r.Value!.AdmissionNumber}.";
        return Redirect($"/Ipd/Admission/{r.Value.Id}");
    }
}
