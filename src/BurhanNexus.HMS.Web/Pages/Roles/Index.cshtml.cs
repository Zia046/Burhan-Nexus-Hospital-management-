using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Roles;

[HasPermission(Perm.RolesManage)]
public class IndexModel : HmsPageModel
{
    private readonly IRoleService _roles;
    public IndexModel(IRoleService roles) => _roles = roles;
    public IReadOnlyList<RoleListItem> Items { get; private set; } = Array.Empty<RoleListItem>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "roles";
        Items = await _roles.ListAsync();
    }
}
