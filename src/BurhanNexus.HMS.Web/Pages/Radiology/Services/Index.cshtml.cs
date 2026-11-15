using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Radiology.Services;

[HasPermission(Perm.RadManageServices, Perm.RadManageTemplates)]
public class IndexModel : RadiologyPageModel
{
    public IReadOnlyList<RadiologyServiceRow> Services { get; private set; } = Array.Empty<RadiologyServiceRow>();
    public IReadOnlyList<Core.Domain.RadiologyTemplate> Templates { get; private set; } = Array.Empty<Core.Domain.RadiologyTemplate>();
    public bool ShowFees { get; private set; }

    public async Task OnGetAsync()
    {
        Nav("rad-services", "services");
        ShowFees = await CanAsync(Perm.RadViewFinancial) || await CanAsync(Perm.RadManageFees);
        Services = await Catalog.ServicesAsync(activeOnly: false);
        Templates = await Catalog.TemplatesAsync(activeOnly: false);
    }
}
