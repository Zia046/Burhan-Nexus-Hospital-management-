using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Users;

[HasPermission(Perm.UsersAssignPermissions)]
public class PermissionsModel : HmsPageModel
{
    private readonly IUserService _users;
    public PermissionsModel(IUserService users) => _users = users;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public Dictionary<string, string> P { get; set; } = new();

    public User? TargetUser { get; private set; }
    public IReadOnlyList<PermissionModuleRow> Matrix { get; private set; } = Array.Empty<PermissionModuleRow>();
    public bool ReadOnly { get; private set; }
    public string? ReadOnlyReason { get; private set; }

    private async Task<bool> LoadAsync()
    {
        ViewData["Nav"] = "users";
        TargetUser = await _users.GetAsync(Id);
        if (TargetUser is null) return false;
        Matrix = await _users.GetPermissionMatrixAsync(Id);
        if (TargetUser.Id == CurrentUserId) { ReadOnly = true; ReadOnlyReason = "You cannot change your own permissions. Another administrator must do this."; }
        else if (TargetUser.IsSystemAdministrator) { ReadOnly = true; ReadOnlyReason = "The primary system administrator always has every permission."; }
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync()
    {
        var overrides = new Dictionary<string, PermissionOverride>();
        foreach (var (key, value) in P)
        {
            if (!PermissionCatalog.AllKeys.Contains(key)) continue;
            overrides[key] = value switch
            {
                "grant" => PermissionOverride.Grant,
                "deny" => PermissionOverride.Deny,
                _ => PermissionOverride.Inherit
            };
        }
        var r = await _users.SetPermissionOverridesAsync(Id, overrides);
        if (r.Succeeded) FlashSuccess = "Permissions saved. Changes apply immediately on every computer.";
        else FlashError = r.ErrorText;
        return Redirect($"/Users/Permissions/{Id}");
    }
}
