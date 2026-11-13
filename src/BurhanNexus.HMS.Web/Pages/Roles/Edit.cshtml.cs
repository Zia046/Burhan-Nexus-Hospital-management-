using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Roles;

[HasPermission(Perm.RolesManage)]
public class EditModel : HmsPageModel
{
    private readonly IRoleService _roles;
    public EditModel(IRoleService roles) => _roles = roles;

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public List<string> Keys { get; set; } = new();

    public IReadOnlySet<string> MyPermissions { get; private set; } = new HashSet<string>();

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "roles";
        MyPermissions = await Permissions.GetAsync();
        if (Id is int id)
        {
            var role = await _roles.GetAsync(id);
            if (role is null) return NotFound();
            Name = role.Name;
            Description = role.Description;
            IsActive = role.IsActive;
            RowVersion = role.RowVersion;
            Keys = role.Permissions.Select(p => p.Permission!.Key).ToList();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Nav"] = "roles";
        var r = await _roles.SaveAsync(Id, Name, Description, IsActive, Keys, RowVersion);
        if (!r.Succeeded)
        {
            MyPermissions = await Permissions.GetAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Responsibility '{Name.Trim()}' saved.";
        return RedirectToPage("/Roles/Index");
    }
}
