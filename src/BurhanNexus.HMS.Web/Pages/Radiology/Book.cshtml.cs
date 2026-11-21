using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>
/// New radiology booking from the doctor's handwritten advice: find the patient (registration only through the existing
/// patient registration) → OPD / Emergency visit → examinations → date / time, referral, indication → save (fees go to the Phase 3 cashier).
/// </summary>
[HasPermission(Perm.RadCreateBooking)]
public class BookModel : RadiologyPageModel
{
    private readonly IPatientService _patients;
    private readonly IRegistrationSetupService _registration;
    private readonly HospitalClock _clock;
    private readonly HmsDbContext _db;

    public BookModel(IPatientService patients, IRegistrationSetupService registration, HospitalClock clock, HmsDbContext db)
    {
        _patients = patients;
        _registration = registration;
        _clock = clock;
        _db = db;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true)] public long? EncounterId { get; set; }
    /// <summary>Phase 7: examinations ordered for an inpatient admission.</summary>
    [BindProperty(SupportsGet = true)] public long? AdmissionId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public RadiologyBookingInput Input { get; set; } = new();
    [BindProperty] public List<int> ServiceIds { get; set; } = new();
    // Update 14: nullable – an empty discount box no longer gives "The value '' is invalid".
    [BindProperty] public Dictionary<int, decimal?> Discounts { get; set; } = new();
    [BindProperty] public DateOnly? ExamDate { get; set; }
    [BindProperty] public TimeOnly? ExamTime { get; set; }
    [BindProperty] public string? VisitChoice { get; set; }

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<RadiologyServiceRow> Services { get; private set; } = Array.Empty<RadiologyServiceRow>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<PatientEncounter> Visits { get; private set; } = Array.Empty<PatientEncounter>();
    public IReadOnlyList<RadiologyExamRow> Previous { get; private set; } = Array.Empty<RadiologyExamRow>();
    public RadiologySettings Settings { get; private set; } = null!;
    public bool ShowFees { get; private set; }
    public bool CanDiscount { get; private set; }
    public bool CanRegister { get; private set; }
    public DateOnly Today { get; private set; }

    private async Task LoadAsync()
    {
        Nav("rad-book", "book");
        ShowFees = await CanAsync(Perm.RadViewFinancial);
        CanDiscount = ShowFees && await CanAsync(Perm.FinanceDiscount);
        CanRegister = await CanAsync(Perm.PatientsAdd);
        Settings = await Catalog.SettingsAsync();
        Today = await _clock.TodayAsync();
        // Staff of one modality only see (and book) their own examinations.
        var only = await OnlyModalityAsync();
        Services = (await Catalog.ServicesAsync(activeOnly: true)).Where(s => only is null || s.Service.Modality == only).ToList();
        Doctors = await _registration.DoctorsAsync(activeOnly: true);
        Departments = await _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        if (AdmissionId is long aid && PatientId is null)
            PatientId = await _db.IpdAdmissions.Where(a => a.Id == aid).Select(a => (long?)a.PatientId).FirstOrDefaultAsync();
        if (EncounterId is long eid && PatientId is null)
            PatientId = await _db.PatientEncounters.Where(e => e.Id == eid).Select(e => (long?)e.PatientId).FirstOrDefaultAsync();
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is not null)
            {
                AgeText = AgeCalculator.Of(Patient, Today)?.ToString() ?? "—";
                var since = Today.AddDays(-Settings.VisitValidDays);
                Visits = await _db.PatientEncounters.AsNoTracking().Include(e => e.Department).Include(e => e.Doctor)
                    .Where(e => e.PatientId == pid && (e.Type == EncounterType.Opd || e.Type == EncounterType.Emergency) && e.EncounterDate >= since)
                    .OrderByDescending(e => e.EncounterUtc).Take(10).ToListAsync();
                if (await CanAsync(Perm.RadViewHistory))
                    Previous = await Rad.ExamsAsync(new RadiologyWorklistQuery { PatientId = pid, Take = 6 });
            }
        }
        else if (!string.IsNullOrWhiteSpace(Q))
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15, Routine = true })).Items;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        Input.IdempotencyKey = Guid.NewGuid();
        Input.EncounterId = EncounterId ?? Visits.FirstOrDefault()?.Id;
        VisitChoice = Input.EncounterId?.ToString() ?? "";
        if (Patient is not null && Visits.FirstOrDefault(v => v.Id == Input.EncounterId) is { } v)
        {
            // Referring department / doctor default to the selected visit (the handwritten advice usually comes from there).
            Input.ReferringDepartmentId = v.DepartmentId;
            if (v.DoctorId is int doc) { Input.ReferralType = RadiologyReferralType.HospitalDoctor; Input.ReferringDoctorId = doc; }
        }
        ExamDate = Today;
        ExamTime = TimeOnly.FromDateTime(await _clock.LocalNowAsync());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        Input.EncounterId = long.TryParse(VisitChoice, out var visit) ? visit : null;
        Input.ServiceIds = ServiceIds;
        Input.IpdAdmissionId = AdmissionId;
        Input.Discounts = Discounts.Where(d => d.Value is decimal v && v != 0).ToDictionary(d => d.Key, d => d.Value!.Value);
        var today = await _clock.TodayAsync();
        var local = await _clock.LocalNowAsync();
        var date = ExamDate ?? today;
        Input.ScheduledLocal = date == today && ExamTime is null ? local : date.ToDateTime(ExamTime ?? new TimeOnly(9, 0));
        // Users of one modality may only book that modality (enforced here, not only in the page).
        if (await OnlyModalityAsync() is { } only)
        {
            var other = await HttpContext.RequestServices.GetRequiredService<HmsDbContext>().RadiologyServices
                .AnyAsync(s => ServiceIds.Contains(s.Id) && s.Modality != only);
            if (other) return Forbid();
        }
        var canDiscount = await CanAsync(Perm.RadViewFinancial) && await CanAsync(Perm.FinanceDiscount);
        var r = await Rad.BookAsync(Input, canDiscount);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        var tokens = string.Join(", ", r.Value!.Order.Exams.GroupBy(e => e.Modality).Select(g => $"{RadiologyCatalogService.Label(g.Key)} token {g.First().TokenNumber}"));
        FlashSuccess = r.Value.WasDuplicate
            ? $"This booking was already saved as {r.Value.Order.OrderNumber}. No second order was created."
            : $"Radiology order {r.Value.Order.OrderNumber} booked ({tokens}). Fees are payable at the cash counter.";
        return Redirect($"/Radiology/Order/{r.Value.Order.Id}");
    }
}
