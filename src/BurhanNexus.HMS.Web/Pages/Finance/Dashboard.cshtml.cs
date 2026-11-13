using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Hospital financial dashboard – Admin / explicitly authorised users only. Every figure is computed from transactions.</summary>
[HasPermission(Perm.FinanceDashboard)]
public class DashboardModel : FinanceFilterModel
{
    private readonly IFinanceReportService _reports;
    public DashboardModel(IFinanceReportService reports) => _reports = reports;
    public FinanceDashboard Data { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "finance-dashboard";
        ViewData["FinanceTab"] = "dashboard";
        await LoadFilterAsync();
        Data = await _reports.DashboardAsync(Filter);
    }
}
