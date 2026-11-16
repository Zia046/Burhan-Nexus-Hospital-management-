using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>
/// Update 11: doctor account prints – daily OPD list (?day=), settlement report (?from=&amp;to=) and handover voucher (?voucher=).
/// Printing only reads stored records. A doctor without the manager task can print only his own account.
/// </summary>
[HasPermission(Perm.FinancePrintDoctorReports, Perm.FinanceViewOwnDoctorAccount)]
public class DoctorModel : HmsPageModel
{
    private readonly IDoctorAccountService _doctors;
    private readonly ISettingsService _settings;
    private readonly HospitalClock _clock;
    public DoctorModel(IDoctorAccountService doctors, ISettingsService settings, HospitalClock clock) { _doctors = doctors; _settings = settings; _clock = clock; }

    [BindProperty(SupportsGet = true)] public DateOnly? Day { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public long? Voucher { get; set; }

    public string Mode { get; private set; } = "day";
    public HospitalProfile Hospital { get; private set; } = null!;
    public DoctorAccount Account { get; private set; } = null!;
    public DoctorAccount? Period { get; private set; }
    public DoctorDay? DayData { get; private set; }
    public DoctorSettlement? Settlement { get; private set; }
    public IReadOnlyList<DoctorSettlement> Settlements { get; private set; } = Array.Empty<DoctorSettlement>();
    public IReadOnlyList<DoctorLedgerEntry> Ledger { get; private set; } = Array.Empty<DoctorLedgerEntry>();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var manager = await CanAsync(Perm.FinancePrintDoctorReports);
        if (!manager && !(await CanAsync(Perm.FinanceViewOwnDoctorAccount) && CurrentUserId is int uid && await _doctors.DoctorOfUserAsync(uid) == id)) return Forbid();
        var a = await _doctors.AccountAsync(id, null, null);
        if (a is null) return NotFound();
        Account = a;
        Hospital = (await _settings.GetProfileAsync())!;
        var today = await _clock.TodayAsync();
        if (Voucher is long v)
        {
            Mode = "voucher";
            Settlement = (await _doctors.SettlementsAsync(id, null, null)).FirstOrDefault(s => s.Id == v);
            if (Settlement is null) return NotFound();
        }
        else if (From is not null || To is not null)
        {
            Mode = "settlement";
            From ??= today.AddDays(-30); To ??= today;
            if (From > To) (From, To) = (To, From);
            Period = await _doctors.AccountAsync(id, From, To);
            Settlements = await _doctors.SettlementsAsync(id, From, To);
            Ledger = await _doctors.LedgerAsync(id, From, To);
        }
        else
        {
            Day ??= today;
            DayData = await _doctors.DayAsync(id, Day.Value);
        }
        return Page();
    }
}
