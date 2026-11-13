using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>
/// "My financial account": a cashier sees only his own collections, deposits and balance.
/// Users with "View All Financial Accounts" may open another user's account (?userId=).
/// </summary>
[HasPermission(Perm.FinanceViewOwnAccount)]
public class IndexModel : HmsPageModel
{
    private readonly ICashAccountService _cash;
    private readonly IBillingService _billing;
    private readonly IFinanceSetupService _setup;
    private readonly HospitalClock _clock;

    public IndexModel(ICashAccountService cash, IBillingService billing, IFinanceSetupService setup, HospitalClock clock)
    {
        _cash = cash;
        _billing = billing;
        _setup = setup;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public int? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty] public decimal DepositAmount { get; set; }
    [BindProperty] public string? DepositNotes { get; set; }

    public CashierAccount Account { get; private set; } = null!;
    public IReadOnlyList<CashierLedgerEntry> Ledger { get; private set; } = Array.Empty<CashierLedgerEntry>();
    public IReadOnlyList<CashDeposit> Deposits { get; private set; } = Array.Empty<CashDeposit>();
    public IReadOnlyList<ReceiptListItem> Receipts { get; private set; } = Array.Empty<ReceiptListItem>();
    public IReadOnlyList<Counter> Counters { get; private set; } = Array.Empty<Counter>();
    public bool IsOwn { get; private set; }
    public string PresetKey { get; private set; } = "today";

    private async Task<IActionResult?> LoadAsync()
    {
        ViewData["Nav"] = "finance-account";
        ViewData["FinanceTab"] = "account";
        var me = CurrentUserId!.Value;
        var target = UserId ?? me;
        IsOwn = target == me;
        if (!IsOwn && !await CanAsync(Perm.FinanceViewAllAccounts)) return Forbid();
        var today = await _clock.TodayAsync();
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, today);
        From = from; To = to; PresetKey = key;
        Account = await _cash.AccountAsync(target, from, to);
        Ledger = await _cash.LedgerAsync(target, from, to);
        Deposits = await _cash.DepositsAsync(target, null, null, null);
        var canCancelled = await CanAsync(Perm.FinanceViewCancelled) || await CanAsync(Perm.FinanceViewAllAccounts);
        Receipts = await _billing.ReceiptsAsync(new ReceiptQuery { CashierUserId = target, From = from, To = to, IncludeCancelled = canCancelled, Take = 200 });
        Counters = await _setup.CountersOfUserAsync(target);
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostDepositAsync()
    {
        if (await DenyUnlessAsync(Perm.FinanceSubmitDeposit) is { } deny) return deny;
        UserId = null; // a deposit is always for the signed-in user's own account
        var r = await _cash.SubmitDepositAsync(DepositAmount, DepositNotes);
        if (r.Succeeded)
        {
            FlashSuccess = $"Deposit {r.Value!.DepositNumber} of {r.Value.Amount:N2} submitted. Your balance decreases when Admin confirms receipt.";
            return RedirectToPage();
        }
        if (await LoadAsync() is { } res) return res;
        AddErrors(r);
        return Page();
    }
}
