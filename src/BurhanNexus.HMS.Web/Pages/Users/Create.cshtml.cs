using System.ComponentModel.DataAnnotations;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Users;

[HasPermission(Perm.UsersAdd)]
public class CreateModel : HmsPageModel
{
    private readonly IUserService _users;
    private readonly IDepartmentService _departments;

    public CreateModel(IUserService users, IDepartmentService departments)
    {
        _users = users;
        _departments = departments;
    }

    [BindProperty, Required(ErrorMessage = "Full name is required."), StringLength(150)] public string FullName { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "Username is required."), StringLength(64)] public string Username { get; set; } = string.Empty;
    [BindProperty, StringLength(100)] public string? Designation { get; set; }
    [BindProperty, StringLength(50)] public string? ContactNumber { get; set; }
    [BindProperty, StringLength(200)] public string? Email { get; set; }
    [BindProperty, Required(ErrorMessage = "Password is required."), StringLength(128)] public string Password { get; set; } = string.Empty;
    [BindProperty, Compare(nameof(Password), ErrorMessage = "The passwords do not match.")] public string? ConfirmPassword { get; set; }
    [BindProperty] public bool MustChangePassword { get; set; } = true;
    [BindProperty] public List<int> DepartmentIds { get; set; } = new();

    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public bool CanAssignDepartments { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "users";
        Departments = await _departments.ListAsync(includeInactive: false);
        CanAssignDepartments = await CanAsync(Perm.UsersAssignDepartments);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!CanAssignDepartments) DepartmentIds.Clear();
        if (!ModelState.IsValid) return Page();

        var result = await _users.CreateAsync(new UserInput
        {
            FullName = FullName, Username = Username, Designation = Designation, ContactNumber = ContactNumber, Email = Email
        }, Password, MustChangePassword, DepartmentIds);

        if (!result.Succeeded)
        {
            AddErrors(result);
            return Page();
        }
        FlashSuccess = $"User '{Username.Trim()}' created. You can now assign responsibilities and permissions.";
        return Redirect($"/Users/Edit/{result.Value}");
    }
}
