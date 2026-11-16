using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>New laboratory visit: find / register the patient → select tests → referral → save (fees go to the Phase 3 cashier).</summary>
[HasPermission(Perm.LabBook)]
public class BookModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly ILabCatalogService _catalog;
    private readonly IPatientService _patients;
    private readonly IRegistrationSetupService _registration;
    private readonly HospitalClock _clock;

    public BookModel(ILabService lab, ILabCatalogService catalog, IPatientService patients, IRegistrationSetupService registration, HospitalClock clock)
    {
        _lab = lab;
        _catalog = catalog;
        _patients = patients;
        _registration = registration;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true)] public long? EncounterId { get; set; }
    /// <summary>Phase 7: tests ordered for an inpatient admission.</summary>
    [BindProperty(SupportsGet = true)] public long? AdmissionId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public LabBookingInput Input { get; set; } = new();
    [BindProperty] public List<int> TestIds { get; set; } = new();
    [BindProperty] public Dictionary<int, decimal> Discounts { get; set; } = new();

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<LabTestRow> Tests { get; private set; } = Array.Empty<LabTestRow>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<LabOrderSummary> Previous { get; private set; } = Array.Empty<LabOrderSummary>();
    public bool ShowFees { get; private set; }
    public bool CanDiscount { get; private set; }
    public bool CanRegister { get; private set; }
    /// <summary>Update 10: the payment is recorded at booking (Phase 3 receipt, two-copy laboratory receipt).</summary>
    public bool CanCollect { get; private set; }
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public IReadOnlyList<Counter> Counters { get; private set; } = Array.Empty<Counter>();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "lab-book";
        ViewData["LabTab"] = "book";
        ShowFees = await CanAsync(Perm.LabViewFinancial);
        CanDiscount = ShowFees && await CanAsync(Perm.FinanceDiscount);
        CanRegister = await CanAsync(Perm.PatientsAdd) || await CanAsync(Perm.LabRegisterPatient);
        CanCollect = await CanAsync(Perm.LabCollectPayments) && AdmissionId is null; // inpatient tests go on the IPD account
        ShowFees |= CanCollect;
        if (CanCollect)
        {
            var finance = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Finance.IFinanceSetupService>();
            Methods = await finance.PaymentMethodsAsync(activeOnly: true);
            if (User.IsSystemAdministrator()) Counters = (await finance.CountersAsync(activeOnly: true)).Select(c => c.Counter).ToList();
            else if (CurrentUserId is int uid) Counters = await finance.CountersOfUserAsync(uid);
        }
        Tests = await _catalog.TestsAsync(activeOnly: true);
        Doctors = await _registration.DoctorsAsync(activeOnly: true);
        if (AdmissionId is long aid && PatientId is null)
            PatientId = await HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Persistence.HmsDbContext>().IpdAdmissions
                .Where(a => a.Id == aid).Select(a => (long?)a.PatientId).FirstOrDefaultAsync();
        if (EncounterId is long eid && PatientId is null)
            PatientId = (await HttpContext.RequestServices.GetRequiredService<IEncounterService>().GetAsync(eid))?.PatientId;
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is not null)
            {
                AgeText = AgeCalculator.Of(Patient, await _clock.TodayAsync())?.ToString() ?? "—";
                Previous = await _lab.OrdersAsync(new LabOrderQuery { PatientId = pid, Take = 5 });
            }
        }
        else if (!string.IsNullOrWhiteSpace(Q))
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        Input.IdempotencyKey = Guid.NewGuid();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        Input.EncounterId = EncounterId;
        Input.IpdAdmissionId = AdmissionId;
        Input.TestIds = TestIds;
        Input.Discounts = Discounts.Where(d => d.Value != 0).ToDictionary(d => d.Key, d => d.Value);
        var canDiscount = await CanAsync(Perm.LabViewFinancial) && await CanAsync(Perm.FinanceDiscount);
        var canCollect = await CanAsync(Perm.LabCollectPayments) && AdmissionId is null;
        if (AdmissionId is not null) Input.CollectPayment = false;
        var r = await _lab.BookAsync(Input, canDiscount, canCollect);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = r.Value!.WasDuplicate
            ? $"This booking was already saved as {r.Value.Order.OrderNumber}. No second order or payment was created."
            : Input.CollectPayment ? $"Laboratory visit {r.Value.Order.OrderNumber} booked and the payment recorded. Print the receipt (two copies)."
            : AdmissionId is not null ? $"Laboratory visit {r.Value.Order.OrderNumber} booked – the fees are on the patient's IPD account."
            : $"Laboratory visit {r.Value.Order.OrderNumber} booked. Fees are payable at the cash counter.";
        return Redirect($"/Lab/Order/{r.Value.Order.Id}");
    }
}
