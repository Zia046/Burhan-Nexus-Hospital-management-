using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Fees;

/// <summary>Update 10: central Fees &amp; Services catalogue – every department takes its fees from here.</summary>
[HasPermission(Perm.SvcViewFees, Perm.SvcManageCatalogue, Perm.SvcManageServices, Perm.SvcManageFees)]
public class IndexModel : HmsPageModel
{
    private readonly IFinanceSetupService _setup;
    public IndexModel(IFinanceSetupService setup) => _setup = setup;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    public IReadOnlyList<ServiceCategoryRow> Categories { get; private set; } = Array.Empty<ServiceCategoryRow>();
    public IReadOnlyList<ServiceRow> Rows { get; private set; } = Array.Empty<ServiceRow>();
    public bool CanEdit { get; private set; }
    public bool CanCategories { get; private set; }
    public bool Government { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "fees-catalogue";
        ViewData["FeesTab"] = "catalogue";
        CanEdit = await CanAsync(Perm.SvcManageServices) || await CanAsync(Perm.SvcManageFees);
        CanCategories = await CanAsync(Perm.SvcManageCategories);
        Categories = await _setup.CategoriesAsync(activeOnly: false);
        Government = await ServiceCatalogue.IsGovernmentAsync(HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Persistence.HmsDbContext>());
        IEnumerable<ServiceRow> rows = await _setup.ServicesAsync(activeOnly: false);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            rows = rows.Where(r => r.Item.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Item.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                                   || (r.Item.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) || (r.Item.Department?.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (Category is int c) rows = c == 0 ? rows.Where(r => r.Item.ServiceCategoryId == null) : rows.Where(r => r.Item.ServiceCategoryId == c);
        rows = Status switch { "inactive" => rows.Where(r => !r.Item.IsActive), "all" => rows, _ => rows.Where(r => r.Item.IsActive) };
        Rows = rows.OrderBy(r => r.Item.ServiceCategory?.SortOrder ?? int.MaxValue).ThenBy(r => r.Item.Name).ToList();
    }

    public static string Unit(ChargeUnit u) => u switch
    {
        ChargeUnit.PerTest => "Per test", ChargeUnit.PerVisit => "Per visit", ChargeUnit.PerAdmission => "Per admission", ChargeUnit.PerDay => "Per day",
        ChargeUnit.PerHour => "Per hour", ChargeUnit.PerSession => "Per session", ChargeUnit.PerProcedure => "Per procedure", ChargeUnit.PerItem => "Per item", _ => "Fixed charge",
    };

    public static string Applies(ServiceApplicability a) => a switch
    {
        ServiceApplicability.GovernmentOnly => "Government only", ServiceApplicability.PrivateOnly => "Private only", _ => "Government & private",
    };
}
