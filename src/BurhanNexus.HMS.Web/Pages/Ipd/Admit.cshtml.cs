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
    // ---- Update 10: admission fee ----
    public decimal? AdmissionFee { get; private set; }
    public bool CanPayFee { get; private set; }
    public bool CanWaiveFee { get; private set; }
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    /// <summary>pay | waive | later</summary>
    /// <summary>Update 11: admission date and time are separate inputs (the date picker closes on pick); combined into Input.AdmittedLocal.</summary>
    [BindProperty] public string? AdmitDate { get; set; }
    [BindProperty] public string? AdmitTime { get; set; }
    [BindProperty] public string? FeeAction { get; set; }
    [BindProperty] public int? FeeMethodId { get; set; }
    [BindProperty] public string? FeeReference { get; set; }
    [BindProperty] public string? FeeWaiverReason { get; set; }

    private async Task LoadAsync()
    {
        Nav("ipd-admit", "admit");
        var ipdSettings = await Setup.SettingsAsync();
        if (ipdSettings.AdmissionServiceItemId is int feeSvc && await CanAsync(Perm.IpdViewFinancial) | await CanAsync(Perm.IpdRecordAdmissionPayment))
        {
            var finance = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Finance.IFinanceSetupService>();
            AdmissionFee = await finance.FeeOnAsync(feeSvc, await Clock.TodayAsync());
            CanPayFee = await CanAsync(Perm.IpdRecordAdmissionPayment);
            CanWaiveFee = await CanAsync(Perm.IpdManageAdmissionFee);
            if (CanPayFee) Methods = await finance.PaymentMethodsAsync(activeOnly: true);
        }
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
        var localNow = await Clock.LocalNowAsync();
        AdmitDate = localNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        AdmitTime = localNow.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Input.IdempotencyKey = Guid.NewGuid();
        Input.Kind = IsGynae ? AdmissionKind.Gynae : AdmissionKind.General;
        var since = (await Clock.TodayAsync()).AddDays(-1);
        var v = Visits.FirstOrDefault(x => x.Id == EncounterId) ?? Visits.FirstOrDefault(x => x.EncounterDate >= since);
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
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (string.IsNullOrWhiteSpace(AdmitDate) && string.IsNullOrWhiteSpace(AdmitTime)) Input.AdmittedLocal = null; // posted without the fields → current time
        else if (!DateOnly.TryParseExact(AdmitDate, "yyyy-MM-dd", inv, System.Globalization.DateTimeStyles.None, out var ad)
            || !TimeOnly.TryParseExact(AdmitTime, new[] { "HH:mm", "HH:mm:ss" }, inv, System.Globalization.DateTimeStyles.None, out var at))
        {
            await LoadAsync();
            ModelState.AddModelError(string.Empty, "Enter a valid admission date and time.");
            return Page();
        }
        else Input.AdmittedLocal = ad.ToDateTime(at);
        var r = await Ipd.AdmitAsync(Input);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Patient admitted: {r.Value!.AdmissionNumber}.";
        // Update 10: admission fee – recorded only when actually received, or waived by an authorised user with a reason.
        var billing = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Ipd.IIpdBillingService>();
        if (FeeAction == "pay" && FeeMethodId is int m && await CanAsync(Perm.IpdRecordAdmissionPayment))
        {
            var pay = await billing.PayAdmissionFeeAsync(r.Value.Id, Input.IdempotencyKey == Guid.Empty ? Guid.NewGuid() : Input.IdempotencyKey, m, null, FeeReference);
            if (pay.Succeeded) FlashSuccess += " Admission fee received."; else FlashError = "Admission saved, but the admission fee was not recorded: " + pay.ErrorText;
        }
        else if (FeeAction == "waive" && await CanAsync(Perm.IpdManageAdmissionFee))
        {
            var w = await billing.WaiveAdmissionFeeAsync(r.Value.Id, FeeWaiverReason);
            if (w.Succeeded) FlashSuccess += " Free admission recorded."; else FlashError = "Admission saved, but the waiver was not recorded: " + w.ErrorText;
        }
        return Redirect($"/Ipd/Admission/{r.Value.Id}");
    }
}
