using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance.Doctors;

[HasPermission(Perm.FinanceManageDoctorAccounts)]
public class IndexModel : HmsPageModel
{
    private readonly IDoctorAccountService _doctors;
    public IndexModel(IDoctorAccountService doctors) => _doctors = doctors;
    public IReadOnlyList<DoctorAccount> Accounts { get; private set; } = Array.Empty<DoctorAccount>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "finance-doctors";
        ViewData["FinanceTab"] = "doctors";
        Accounts = await _doctors.AccountsAsync(null, null);
    }
}
