using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance.Doctors;

[HasPermission(Perm.FinanceManageDoctorAccounts, Perm.FinanceViewOwnDoctorAccount)]
public class IndexModel : HmsPageModel
{
    private readonly IDoctorAccountService _doctors;
    public IndexModel(IDoctorAccountService doctors) => _doctors = doctors;
    public IReadOnlyList<DoctorAccount> Accounts { get; private set; } = Array.Empty<DoctorAccount>();

    public async Task<IActionResult> OnGetAsync()
    {
        // Update 11: a doctor without the manager task goes straight to his own account.
        if (!await CanAsync(Perm.FinanceManageDoctorAccounts))
            return CurrentUserId is int uid && await _doctors.DoctorOfUserAsync(uid) is int own ? Redirect($"/Finance/Doctors/Account/{own}") : Forbid();
        ViewData["Nav"] = "finance-doctors";
        ViewData["FinanceTab"] = "doctors";
        Accounts = await _doctors.AccountsAsync(null, null);
        return Page();
    }
}
