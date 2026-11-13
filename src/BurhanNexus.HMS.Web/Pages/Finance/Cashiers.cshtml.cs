using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Finance;

[HasPermission(Perm.FinanceViewAllAccounts)]
public class CashiersModel : HmsPageModel
{
    private readonly ICashAccountService _cash;
    public CashiersModel(ICashAccountService cash) => _cash = cash;
    public IReadOnlyList<CashierBalanceRow> Rows { get; private set; } = Array.Empty<CashierBalanceRow>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "finance-cashiers";
        ViewData["FinanceTab"] = "cashiers";
        Rows = await _cash.BalancesAsync();
    }
}
