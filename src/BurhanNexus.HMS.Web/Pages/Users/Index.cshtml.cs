using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Users;

[HasPermission(Perm.UsersView)]
public class IndexModel : HmsPageModel
{
    private readonly IUserService _users;
    private readonly IDepartmentService _departments;

    public IndexModel(IUserService users, IDepartmentService departments)
    {
        _users = users;
        _departments = departments;
    }

    [BindProperty(SupportsGet = true, Name = "q")] public string? Query { get; set; }
    [BindProperty(SupportsGet = true, Name = "status")] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "dept")] public int? DepartmentId { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<UserListItem> Result { get; private set; } = new(Array.Empty<UserListItem>(), 0, 1, 25);
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "users";
        bool? active = Status switch { "active" => true, "inactive" => false, _ => null };
        Result = await _users.SearchAsync(Query, active, DepartmentId, PageNumber, 25);
        Departments = await _departments.ListAsync();
    }
}
