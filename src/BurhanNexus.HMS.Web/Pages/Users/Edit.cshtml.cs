using System.ComponentModel.DataAnnotations;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Users;

[HasPermission(Perm.UsersView)]
public class EditModel : HmsPageModel
{
    private readonly IUserService _users;
    private readonly IDepartmentService _departments;
    private readonly IRoleService _roles;
    private readonly TimeProvider _time;

    public EditModel(IUserService users, IDepartmentService departments, IRoleService roles, TimeProvider time)
    {
        _users = users;
        _departments = departments;
        _roles = roles;
        _time = time;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    [BindProperty, Required(ErrorMessage = "Full name is required."), StringLength(150)] public string FullName { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "Username is required."), StringLength(64)] public string Username { get; set; } = string.Empty;
    [BindProperty, StringLength(100)] public string? Designation { get; set; }
    [BindProperty, StringLength(50)] public string? ContactNumber { get; set; }
    [BindProperty, StringLength(200)] public string? Email { get; set; }
    [BindProperty] public uint RowVersion { get; set; }

    [BindProperty] public List<int> DepartmentIds { get; set; } = new();
    [BindProperty] public List<int> RoleIds { get; set; } = new();
    [BindProperty] public string? NewPassword { get; set; }
    [BindProperty] public bool MustChange { get; set; } = true;

    public User? TargetUser { get; private set; }
    public IReadOnlyList<DepartmentListItem> AllDepartments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<RoleListItem> AllRoles { get; private set; } = Array.Empty<RoleListItem>();
    public bool IsSelf => TargetUser?.Id == CurrentUserId;
    public bool IsLocked => TargetUser?.Credential?.LockoutEndUtc > _time.GetUtcNow().UtcDateTime;
    public bool CanEdit { get; private set; }
    public bool CanActivate { get; private set; }
    public bool CanReset { get; private set; }
    public bool CanDepartments { get; private set; }
    public bool CanRoles { get; private set; }
    public bool CanPermissions { get; private set; }
    public bool CanLoginHistory { get; private set; }

    private async Task<bool> LoadAsync(bool bindForm)
    {
        ViewData["Nav"] = "users";
        TargetUser = await _users.GetAsync(Id);
        if (TargetUser is null) return false;
        AllDepartments = await _departments.ListAsync();
        AllRoles = await _roles.ListAsync();
        var protectedTarget = TargetUser.IsSystemAdministrator && !User.IsSystemAdministrator();
        CanEdit = !protectedTarget && await CanAsync(Perm.UsersEdit);
        CanActivate = !protectedTarget && !IsSelf && await CanAsync(Perm.UsersActivate);
        CanReset = !protectedTarget && !IsSelf && await CanAsync(Perm.UsersResetPassword);
        CanDepartments = !protectedTarget && await CanAsync(Perm.UsersAssignDepartments);
        CanRoles = !protectedTarget && !IsSelf && !TargetUser.IsSystemAdministrator && await CanAsync(Perm.UsersAssignPermissions);
        CanPermissions = CanRoles;
        CanLoginHistory = await CanAsync(Perm.LoginHistoryView);
        if (bindForm)
        {
            FullName = TargetUser.FullName;
            Username = TargetUser.Username;
            Designation = TargetUser.Designation;
            ContactNumber = TargetUser.ContactNumber;
            Email = TargetUser.Email;
            RowVersion = TargetUser.RowVersion;
            DepartmentIds = TargetUser.Departments.Select(d => d.DepartmentId).ToList();
            RoleIds = TargetUser.Roles.Select(r => r.RoleId).ToList();
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync(true) ? Page() : NotFound();

    public async Task<IActionResult> OnPostProfileAsync()
    {
        if (await DenyUnlessAsync(Perm.UsersEdit) is { } deny) return deny;
        if (!ModelState.IsValid)
        {
            await LoadAsync(false);
            return Page();
        }
        var r = await _users.UpdateAsync(Id, new UserInput
        {
            FullName = FullName, Username = Username, Designation = Designation, ContactNumber = ContactNumber, Email = Email
        }, RowVersion);
        return Done(r, "User information saved.");
    }

    public async Task<IActionResult> OnPostDepartmentsAsync()
    {
        if (await DenyUnlessAsync(Perm.UsersAssignDepartments) is { } deny) return deny;
        return Done(await _users.SetDepartmentsAsync(Id, DepartmentIds), "Departments updated.");
    }

    public async Task<IActionResult> OnPostRolesAsync()
    {
        if (await DenyUnlessAsync(Perm.UsersAssignPermissions) is { } deny) return deny;
        return Done(await _users.SetRolesAsync(Id, RoleIds), "Responsibilities updated.");
    }

    public async Task<IActionResult> OnPostActivateAsync(bool active)
    {
        if (await DenyUnlessAsync(Perm.UsersActivate) is { } deny) return deny;
        return Done(await _users.SetActiveAsync(Id, active), active ? "Account activated." : "Account deactivated. The user has been logged out on all computers.");
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        if (await DenyUnlessAsync(Perm.UsersResetPassword) is { } deny) return deny;
        return Done(await _users.ResetPasswordAsync(Id, NewPassword ?? string.Empty, MustChange),
            "Password reset. Give the new password to the user" + (MustChange ? "; they must change it at next login." : "."));
    }

    public async Task<IActionResult> OnPostUnlockAsync()
    {
        if (await DenyUnlessAsync(Perm.UsersResetPassword) is { } deny) return deny;
        return Done(await _users.UnlockAsync(Id), "Account unlocked.");
    }

    private IActionResult Done(Core.Abstractions.ServiceResult r, string success)
    {
        if (r.Succeeded) FlashSuccess = success;
        else FlashError = r.ErrorText;
        return Redirect($"/Users/Edit/{Id}");
    }
}
