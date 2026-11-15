using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>
/// Medicine issue to a registered patient: find the patient → choose the OPD / Emergency visit → medicines (FEFO batch suggested) → save.
/// Government: only against a registered, recent OPD / Emergency visit. Private: priced lines become Phase 3 charges paid at the cash counter.
/// </summary>
[HasPermission(Perm.PharmIssueToPatients)]
public class IssueModel : PharmacyPageModel
{
    private readonly IPatientService _patients;
    private readonly HmsDbContext _db;
    private readonly HospitalClock _clock;

    public IssueModel(IPatientService patients, HmsDbContext db, HospitalClock clock)
    {
        _patients = patients;
        _db = db;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true)] public long? EncounterId { get; set; }
    /// <summary>Phase 7: issue for an inpatient admission (opened from the IPD record).</summary>
    [BindProperty(SupportsGet = true)] public long? AdmissionId { get; set; }
    public IpdAdmission? Admission { get; private set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public IssueInput Input { get; set; } = new();

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<PatientEncounter> Encounters { get; private set; } = Array.Empty<PatientEncounter>();
    public IReadOnlyList<PatientMedicineRow> History { get; private set; } = Array.Empty<PatientMedicineRow>();
    public int LocationId { get; private set; }
    public bool CanDiscount { get; private set; }
    public int ValidDays { get; private set; }

    private async Task LoadAsync()
    {
        await InitAsync("issue");
        LocationId = (await Catalog.MainLocationAsync()).Id;
        CanDiscount = IsPrivate && await CanAsync(Perm.PharmApplyDiscount);
        ValidDays = (await Catalog.SettingsAsync()).EncounterValidDays;
        if (AdmissionId is long aid)
        {
            Admission = await _db.IpdAdmissions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aid);
            if (Admission is not null) PatientId ??= Admission.PatientId;
        }
        if (EncounterId is long eid && PatientId is null)
            PatientId = await _db.PatientEncounters.Where(e => e.Id == eid).Select(e => (long?)e.PatientId).FirstOrDefaultAsync();
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is null) return;
            var today = await _clock.TodayAsync();
            AgeText = AgeCalculator.Of(Patient, today)?.ToString() ?? "—";
            var since = today.AddDays(-Math.Max(ValidDays, IsPrivate ? 30 : 0));
            Encounters = await _db.PatientEncounters.AsNoTracking().Include(e => e.Department).Include(e => e.Doctor)
                .Where(e => e.PatientId == pid && (e.Type == EncounterType.Opd || e.Type == EncounterType.Emergency) && e.EncounterDate >= since)
                .OrderByDescending(e => e.EncounterUtc).Take(10).ToListAsync();
            if (await CanAsync(Perm.PharmViewPatientHistory)) History = (await Query.PatientHistoryAsync(pid, ShowPrices)).Take(15).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(Q))
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input = new IssueInput { IdempotencyKey = Guid.NewGuid(), PatientId = PatientId ?? 0, IpdAdmissionId = Admission?.Id,
            EncounterId = Admission is null ? EncounterId ?? Encounters.FirstOrDefault()?.Id : null };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        PatientId = Input.PatientId;
        AdmissionId = Input.IpdAdmissionId;
        await LoadAsync();
        var r = await Pharmacy.IssueAsync(Input, await RightsAsync());
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved – stock was not deducted twice." : $"Medicines issued: {r.Value.Number}.";
        return Redirect($"/Pharmacy/Doc/ISS/{r.Value.Id}");
    }
}
