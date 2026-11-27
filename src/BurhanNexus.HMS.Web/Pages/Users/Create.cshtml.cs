using System.ComponentModel.DataAnnotations;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Users;

[HasPermission(Perm.UsersAdd)]
public class CreateModel : HmsPageModel
{
    private readonly IUserService _users;
    private readonly IDepartmentService _departments;
    private readonly IFinanceSetupService _finance;

    public CreateModel(IUserService users, IDepartmentService departments, IFinanceSetupService finance)
    {
        _users = users;
        _departments = departments;
        _finance = finance;
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
    /// <summary>Update 16: the cash counter of the new user (changed later in Finance → Counters).</summary>
    [BindProperty] public int? CounterId { get; set; }
    public IReadOnlyList<BurhanNexus.HMS.Core.Domain.Counter> Counters { get; private set; } = Array.Empty<BurhanNexus.HMS.Core.Domain.Counter>();
    public bool CanAssignCounter { get; private set; }

    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public bool CanAssignDepartments { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "users";
        Departments = await _departments.ListAsync(includeInactive: false);
        CanAssignDepartments = await CanAsync(Perm.UsersAssignDepartments);
        CanAssignCounter = await CanAsync(Perm.FinanceManageCounters);
        if (CanAssignCounter) Counters = (await _finance.CountersAsync(activeOnly: true)).Select(c => c.Counter).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!CanAssignDepartments) DepartmentIds.Clear();
        if (!CanAssignCounter) CounterId = null;
        if (CounterId is int cid && Counters.All(c => c.Id != cid)) ModelState.AddModelError(nameof(CounterId), "Select an active counter.");
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
        if (CounterId is int counter)
        {
            var assigned = await _finance.AssignAsync(counter, result.Value, null);
            if (assigned.Succeeded) FlashSuccess += $" Counter: {Counters.First(c => c.Id == counter).Name}.";
            else FlashError = "The user was created but the counter could not be assigned: " + string.Join(" ", assigned.Errors);
        }
        return Redirect($"/Users/Edit/{result.Value}");
    }
}
