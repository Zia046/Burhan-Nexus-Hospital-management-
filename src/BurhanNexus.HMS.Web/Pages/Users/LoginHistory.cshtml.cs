using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Users;

[HasPermission(Perm.LoginHistoryView)]
public class LoginHistoryModel : HmsPageModel
{
    private readonly IAuditQueryService _audit;
    private readonly IUserService _users;

    public LoginHistoryModel(IAuditQueryService audit, IUserService users)
    {
        _audit = audit;
        _users = users;
    }

    [BindProperty(SupportsGet = true, Name = "user")] public int? UserId { get; set; }
    [BindProperty(SupportsGet = true, Name = "result")] public string? ResultFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<LoginHistory> Result { get; private set; } = new(Array.Empty<LoginHistory>(), 0, 1, 50);
    public string? UserName { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "logins";
        if (UserId is int id) UserName = (await _users.GetAsync(id))?.FullName;
        Result = await _audit.LoginHistoryAsync(new LoginHistoryQuery
        {
            UserId = UserId,
            Success = ResultFilter switch { "success" => true, "failed" => false, _ => null },
            Page = PageNumber,
            PageSize = 50
        });
    }
}
