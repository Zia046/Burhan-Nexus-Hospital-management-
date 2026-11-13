using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance.Receipts;

/// <summary>Receipt history. Without "View All Financial Accounts" a user only ever sees receipts he issued himself.</summary>
[HasPermission(Perm.FinanceViewOwnReceipts)]
public class IndexModel : HmsPageModel
{
    private readonly IBillingService _billing;
    private readonly IFinanceSetupService _setup;
    private readonly HospitalClock _clock;

    public IndexModel(IBillingService billing, IFinanceSetupService setup, HospitalClock clock)
    {
        _billing = billing;
        _setup = setup;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "cashier")] public int? Cashier { get; set; }
    [BindProperty(SupportsGet = true, Name = "counter")] public int? Counter { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }

    public IReadOnlyList<ReceiptListItem> Items { get; private set; } = Array.Empty<ReceiptListItem>();
    public IReadOnlyList<CounterRow> Counters { get; private set; } = Array.Empty<CounterRow>();
    public bool SeeAll { get; private set; }
    public string PresetKey { get; private set; } = "today";

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "finance-receipts";
        ViewData["FinanceTab"] = "receipts";
        SeeAll = await CanAsync(Perm.FinanceViewAllAccounts);
        var today = await _clock.TodayAsync();
        // Searching by receipt / patient: look further back by default.
        var (from, to, key) = FinancePeriod.Resolve(Period ?? (string.IsNullOrWhiteSpace(Q) && PatientId is null ? null : "year"), From, To, today);
        From = from; To = to; PresetKey = key;
        if (SeeAll) Counters = await _setup.CountersAsync(activeOnly: false);
        var includeCancelled = SeeAll || await CanAsync(Perm.FinanceViewCancelled);
        Items = await _billing.ReceiptsAsync(new ReceiptQuery
        {
            From = from, To = to, CashierUserId = SeeAll ? Cashier : CurrentUserId, CounterId = SeeAll ? Counter : null,
            Status = includeCancelled ? Status : Status == "cancelled" ? "none" : Status, IncludeCancelled = includeCancelled,
            PatientId = PatientId, Q = Q, Take = 1000
        });
    }
}
