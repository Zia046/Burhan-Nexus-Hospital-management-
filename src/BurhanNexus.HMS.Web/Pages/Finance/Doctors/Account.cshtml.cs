using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance.Doctors;

/// <summary>Doctor account (Update 11): today's OPD list, collected / handed to doctor / handed to Admin / payable, handover with voucher.
/// Managers see every doctor; a doctor with "View Own Doctor Account" sees only his own account (checked on the server).</summary>
[HasPermission(Perm.FinanceManageDoctorAccounts, Perm.FinanceViewOwnDoctorAccount)]
public class AccountModel : HmsPageModel
{
    private readonly IDoctorAccountService _doctors;
    private readonly IFinanceSetupService _setup;
    private readonly HospitalClock _clock;

    public AccountModel(IDoctorAccountService doctors, IFinanceSetupService setup, HospitalClock clock)
    {
        _doctors = doctors;
        _setup = setup;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    /// <summary>Update 11: the day of the OPD list (default: today).</summary>
    [BindProperty(SupportsGet = true)] public DateOnly? Day { get; set; }
    public DoctorDay Today { get; private set; } = null!;
    public bool IsManager { get; private set; }
    public bool CanPay { get; private set; }
    public bool CanHandToAdmin { get; private set; }
    public bool CanPrint { get; private set; }
    [BindProperty] public SettlementInput Pay { get; set; } = new();
    [BindProperty] public DoctorShareType ShareType { get; set; }
    [BindProperty] public decimal ShareValue { get; set; }

    public DoctorAccount Account { get; private set; } = null!;
    public DoctorAccount PeriodAccount { get; private set; } = null!;
    public IReadOnlyList<DoctorLedgerEntry> Ledger { get; private set; } = Array.Empty<DoctorLedgerEntry>();
    public IReadOnlyList<DoctorSettlement> Settlements { get; private set; } = Array.Empty<DoctorSettlement>();
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public string PresetKey { get; private set; } = "month";

    /// <summary>Managers see every doctor; otherwise only the doctor linked to the signed-in user.</summary>
    private async Task<bool> MayOpenAsync(int id)
    {
        if (await CanAsync(Perm.FinanceManageDoctorAccounts)) return true;
        return await CanAsync(Perm.FinanceViewOwnDoctorAccount) && CurrentUserId is int uid && await _doctors.DoctorOfUserAsync(uid) == id;
    }

    private async Task<bool> LoadAsync(int id)
    {
        ViewData["Nav"] = "finance-doctors";
        ViewData["FinanceTab"] = "doctors";
        var a = await _doctors.AccountAsync(id, null, null);
        if (a is null) return false;
        Account = a;
        IsManager = await CanAsync(Perm.FinanceManageDoctorAccounts);
        CanPay = IsManager && await CanAsync(Perm.FinanceRecordDoctorPayments);
        CanHandToAdmin = IsManager && await CanAsync(Perm.FinanceHandDoctorMoneyToAdmin);
        CanPrint = IsManager ? await CanAsync(Perm.FinancePrintDoctorReports) : await CanAsync(Perm.FinanceViewOwnDoctorAccount);
        var today = await _clock.TodayAsync();
        Day ??= today;
        if (Day > today) Day = today;
        Today = await _doctors.DayAsync(id, Day.Value);
        var (from, to, key) = FinancePeriod.Resolve(Period ?? "month", From, To, today);
        From = from; To = to; PresetKey = key;
        PeriodAccount = (await _doctors.AccountAsync(id, from, to))!;
        Ledger = await _doctors.LedgerAsync(id, from, to);
        Settlements = await _doctors.SettlementsAsync(id, null, null);
        Methods = await _setup.PaymentMethodsAsync(activeOnly: true);
        ShareType = a.ShareType;
        ShareValue = a.ShareValue;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await MayOpenAsync(id)) return Forbid();
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostPayAsync(int id)
    {
        if (await DenyUnlessAsync(Perm.FinanceManageDoctorAccounts) is { } deny0) return deny0;
        if (await DenyUnlessAsync(Pay.ToAdmin ? Perm.FinanceHandDoctorMoneyToAdmin : Perm.FinanceRecordDoctorPayments) is { } deny) return deny;
        var r = await _doctors.PayAsync(id, Pay);
        if (r.Succeeded)
        {
            FlashSuccess = Pay.ToAdmin ? $"Handed to Admin: {r.Value!.SettlementNumber} of {r.Value.Amount:N2} recorded." : $"Handed to doctor: {r.Value!.SettlementNumber} of {r.Value.Amount:N2} recorded.";
            return Redirect($"/Finance/Doctors/Account/{id}");
        }
        if (!await LoadAsync(id)) return NotFound();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostShareAsync(int id)
    {
        if (await DenyUnlessAsync(Perm.FinanceManageDoctorAccounts) is { } deny) return deny;
        var type = ShareType;
        var value = ShareValue;
        var r = await _setup.SetDoctorShareAsync(id, type, value);
        if (r.Succeeded) { FlashSuccess = "Fee arrangement saved. It applies to new consultations."; return RedirectToPage(new { id }); }
        if (!await LoadAsync(id)) return NotFound();
        ShareType = type;
        ShareValue = value;
        AddErrors(r);
        return Page();
    }
}
