using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>
/// Update 16: IPD, ward / bed, NICU, Gynae / maternity (labour room) and OT fees are managed here – no longer in Fees &amp; Services.
/// Changing a fee is for the administrator only (also enforced in <see cref="FinanceSetupService"/>).
/// </summary>
[HasPermission(Perm.IpdSettings)]
public class FeesModel : IpdPageModel
{
    private IFinanceSetupService Finance => HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>();

    public static readonly (string Code, string Title)[] Groups =
    {
        (ServiceCatalogue.Ipd, "Admission, rooms, ICU / NICU, labour room & maternity"),
        (ServiceCatalogue.Ward, "Ward & bed charges"),
        (ServiceCatalogue.Nicu, "NICU equipment & services"),
        (ServiceCatalogue.Ot, "Operation theatre (Gynae / surgery)"),
    };

    public IReadOnlyList<ServiceRow> Rows { get; private set; } = Array.Empty<ServiceRow>();
    public bool IsAdmin { get; private set; }

    public static string GroupOf(ServiceItem s) =>
        s.ServiceCategory?.Code is { } c && Groups.Any(g => g.Code == c) ? c : s.Kind == ServiceKind.Inpatient ? ServiceCatalogue.Ipd : "";

    public async Task OnGetAsync()
    {
        Nav("ipd-fees", "fees");
        IsAdmin = User.IsSystemAdministrator();
        Rows = (await Finance.ServicesAsync(activeOnly: false)).Where(r => GroupOf(r.Item) != "")
            .OrderBy(r => r.Item.IsActive ? 0 : 1).ThenBy(r => r.Item.Name).ToList();
    }

    public async Task<IActionResult> OnPostAsync(int id, decimal fee, bool isFree)
    {
        if (!User.IsSystemAdministrator()) return Forbid();
        var row = await Finance.ServiceAsync(id);
        if (row is null || GroupOf(row.Item) == "") return NotFound();
        var r = await Finance.SetFeeAsync(id, fee, isFree, null);
        if (r.Succeeded) FlashSuccess = $"Fee of {row.Item.Name} saved. It applies to new charges only – old bills keep their fee.";
        else FlashError = string.Join(" ", r.Errors);
        return Redirect("/Ipd/Fees");
    }
}
