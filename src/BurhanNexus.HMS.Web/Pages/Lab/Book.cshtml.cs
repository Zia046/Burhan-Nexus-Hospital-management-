using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

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

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "lab-book";
        ViewData["LabTab"] = "book";
        ShowFees = await CanAsync(Perm.LabViewFinancial);
        CanDiscount = ShowFees && await CanAsync(Perm.FinanceDiscount);
        CanRegister = await CanAsync(Perm.PatientsAdd) || await CanAsync(Perm.LabRegisterPatient);
        Tests = await _catalog.TestsAsync(activeOnly: true);
        Doctors = await _registration.DoctorsAsync(activeOnly: true);
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
        Input.TestIds = TestIds;
        Input.Discounts = Discounts.Where(d => d.Value != 0).ToDictionary(d => d.Key, d => d.Value);
        var canDiscount = await CanAsync(Perm.LabViewFinancial) && await CanAsync(Perm.FinanceDiscount);
        var r = await _lab.BookAsync(Input, canDiscount);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = r.Value!.WasDuplicate
            ? $"This booking was already saved as {r.Value.Order.OrderNumber}. No second order was created."
            : $"Laboratory visit {r.Value.Order.OrderNumber} booked. Fees are payable at the cash counter.";
        return Redirect($"/Lab/Order/{r.Value.Order.Id}");
    }
}
