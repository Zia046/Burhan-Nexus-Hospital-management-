using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Hospital financial dashboard – Admin / explicitly authorised users only. Every figure is computed from transactions.</summary>
[HasPermission(Perm.FinanceDashboard)]
public class DashboardModel : FinanceFilterModel
{
    private readonly IFinanceReportService _reports;
    private readonly ICashAccountService _cash;
    public DashboardModel(IFinanceReportService reports, ICashAccountService cash) { _reports = reports; _cash = cash; }
    public FinanceDashboard Data { get; private set; } = null!;
    /// <summary>Update 18: Admin / user outstanding balances, deposits and money paid to the hospital.</summary>
    public HospitalFinanceSummary Accounts { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "finance-account"; // update 18: the sidebar "Finance" item
        ViewData["FinanceTab"] = "dashboard";
        await LoadFilterAsync();
        Data = await _reports.DashboardAsync(Filter);
        Accounts = await _cash.HospitalSummaryAsync();
    }
}
