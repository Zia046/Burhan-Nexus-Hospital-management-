using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Daily cash closing: a dated summary; transactions are never reset or deleted.</summary>
[HasPermission(Perm.FinanceDailyClosing)]
public class ClosingModel : HmsPageModel
{
    private readonly ICashAccountService _cash;
    private readonly IFinanceSetupService _setup;
    private readonly HospitalClock _clock;

    public ClosingModel(ICashAccountService cash, IFinanceSetupService setup, HospitalClock clock)
    {
        _cash = cash;
        _setup = setup;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public ClosingScope Scope { get; set; } = ClosingScope.Cashier;
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty] public string? Notes { get; set; }

    public ClosingFigures Figures { get; private set; } = null!;
    public IReadOnlyList<DailyCashClosing> History { get; private set; } = Array.Empty<DailyCashClosing>();
    public IReadOnlyList<CashierBalanceRow> Cashiers { get; private set; } = Array.Empty<CashierBalanceRow>();
    public IReadOnlyList<CounterRow> Counters { get; private set; } = Array.Empty<CounterRow>();
    public bool SeeAll { get; private set; }
    public bool SeeCounters { get; private set; }

    private async Task<IActionResult?> LoadAsync()
    {
        ViewData["Nav"] = "finance-closing";
        ViewData["FinanceTab"] = "closing";
        SeeAll = await CanAsync(Perm.FinanceViewAllAccounts);
        SeeCounters = SeeAll || await CanAsync(Perm.FinanceCounterReports);
        var me = CurrentUserId!.Value;
        if (Scope == ClosingScope.Cashier) Id ??= me;
        // A cashier may only close his own day; other cashiers, counters and the hospital need wider permissions.
        if (Scope == ClosingScope.Cashier && Id != me && !SeeAll) return Forbid();
        if (Scope == ClosingScope.Counter && !SeeCounters) return Forbid();
        if (Scope == ClosingScope.Hospital && !SeeAll) return Forbid();
        if (Scope == ClosingScope.Counter && Id is null)
        {
            Counters = await _setup.CountersAsync(false);
            Id = Counters.FirstOrDefault()?.Counter.Id;
            if (Id is null) return RedirectToPage(new { scope = ClosingScope.Cashier });
        }
        Date ??= await _clock.TodayAsync();
        Figures = await _cash.ClosingFiguresAsync(Scope, Scope == ClosingScope.Hospital ? null : Id, Date.Value);
        History = await _cash.ClosingsAsync(Scope, Scope == ClosingScope.Cashier ? Id : null, null, null);
        if (Scope == ClosingScope.Counter) History = History.Where(h => h.CounterId == Id).ToList();
        if (SeeAll) Cashiers = await _cash.BalancesAsync();
        if (SeeCounters && Counters.Count == 0) Counters = await _setup.CountersAsync(false);
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (await LoadAsync() is { } res) return res;
        var r = await _cash.CloseDayAsync(Scope, Scope == ClosingScope.Hospital ? null : Id, Date!.Value, Notes);
        if (r.Succeeded)
        {
            FlashSuccess = $"Daily closing for {Figures.ScopeName} on {Date:dd MMM yyyy} saved. No transaction was changed.";
            return RedirectToPage(new { scope = Scope, id = Id, date = Date?.ToString("yyyy-MM-dd") });
        }
        AddErrors(r);
        return Page();
    }
}
