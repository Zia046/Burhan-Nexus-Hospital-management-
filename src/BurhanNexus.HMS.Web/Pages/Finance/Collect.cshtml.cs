using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Cashier screen: patient → services (or outstanding balance) → payment → receipt.</summary>
[HasPermission(Perm.FinanceCollect)]
public class CollectModel : HmsPageModel
{
    public const int LineSlots = 8;
    private readonly IBillingService _billing;
    private readonly IFinanceSetupService _setup;
    private readonly IPatientService _patients;
    private readonly IEncounterService _encounters;
    private readonly IRegistrationSetupService _registration;
    private readonly HospitalClock _clock;

    public CollectModel(IBillingService billing, IFinanceSetupService setup, IPatientService patients, IEncounterService encounters,
        IRegistrationSetupService registration, HospitalClock clock)
    {
        _billing = billing;
        _setup = setup;
        _patients = patients;
        _encounters = encounters;
        _registration = registration;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true)] public long? EncounterId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public bool WalkIn { get; set; }
    /// <summary>Phase 4: coming from a laboratory visit – its open test fees are pre-selected.</summary>
    [BindProperty(SupportsGet = true)] public long? LabOrderId { get; set; }
    [BindProperty] public CollectInput Input { get; set; } = new();
    [BindProperty] public List<NewChargeLine> Lines { get; set; } = new();
    [BindProperty] public List<BalancePayment> Balances { get; set; } = new();

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<Counter> Counters { get; private set; } = Array.Empty<Counter>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public IReadOnlyList<Charge> OpenCharges { get; private set; } = Array.Empty<Charge>();
    public IReadOnlyList<Charge> EncounterCharges { get; private set; } = Array.Empty<Charge>();
    public PatientEncounter? Encounter { get; private set; }
    public bool CanDiscount { get; private set; }
    public bool NotAssigned { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-collect";
        ViewData["FinanceTab"] = "collect";
        var me = CurrentUserId!.Value;
        var isAdmin = User.IsSystemAdministrator();
        Counters = isAdmin ? (await _setup.CountersAsync(activeOnly: true)).Select(c => c.Counter).ToList() : await _setup.CountersOfUserAsync(me);
        NotAssigned = Counters.Count == 0;
        Services = await _setup.ServicesAsync(activeOnly: true);
        Doctors = await _registration.DoctorsAsync(activeOnly: true);
        Methods = await _setup.PaymentMethodsAsync(activeOnly: true);
        CanDiscount = await CanAsync(Perm.FinanceDiscount);
        if (EncounterId is long eid)
        {
            Encounter = await _encounters.GetAsync(eid);
            if (Encounter is not null)
            {
                PatientId = Encounter.PatientId;
                EncounterCharges = await _billing.ChargesOfEncounterAsync(eid);
            }
        }
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is not null)
            {
                AgeText = AgeCalculator.Of(Patient, await _clock.TodayAsync())?.ToString() ?? "—";
                OpenCharges = await _billing.OpenChargesAsync(pid);
            }
        }
        else if (!string.IsNullOrWhiteSpace(Q))
        {
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        Input.IdempotencyKey = Guid.NewGuid();
        Input.CounterId = Counters.Count == 1 ? Counters[0].Id : 0;
        Input.PaymentMethodId = Methods.FirstOrDefault(m => m.CountsAsCashInHand)?.Id ?? Methods.FirstOrDefault()?.Id ?? 0;
        // Coming from an OPD visit / appointment: suggest its service (unless it is already billed).
        if (Encounter?.ServiceItemId is int sid && EncounterCharges.All(c => c.Status == ChargeStatus.Cancelled))
            Lines.Add(new NewChargeLine { ServiceItemId = sid, DoctorId = Encounter.DoctorId, EncounterId = Encounter.Id });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId;
        Input.Lines = Lines.Where(l => l.ServiceItemId > 0).ToList();
        Input.Balances = Balances.Where(b => b.ChargeId > 0 && b.PayNow > 0).ToList();
        var r = await _billing.CollectAsync(Input, await CanAsync(Perm.FinanceDiscount));
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        var result = r.Value!;
        if (result.Receipt is null)
        {
            FlashSuccess = "Free service recorded (no receipt is issued for free services – see financial settings).";
            return RedirectToPage(new { patientId = PatientId });
        }
        FlashSuccess = result.WasDuplicate
            ? $"This payment was already saved as receipt {result.Receipt.ReceiptNumber}. No second payment was recorded."
            : $"Receipt {result.Receipt.ReceiptNumber} saved – {result.Receipt.AmountPaid:N2} received.";
        if (await CanAsync(Perm.FinanceIssueReceipts))
            return Redirect($"/Print/Receipt/{result.Receipt.Id}" + (result.WasDuplicate ? "" : "?autoprint=true"));
        return Redirect($"/Finance/Receipts/Details/{result.Receipt.Id}");
    }
}
