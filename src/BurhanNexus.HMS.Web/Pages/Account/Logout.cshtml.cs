using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BurhanNexus.HMS.Web.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly IAuthService _auth;
    public LogoutModel(IAuthService auth) => _auth = auth;

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Page() : RedirectToPage("/Account/Login");

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.GetSessionId() is Guid sid) await _auth.LogoutAsync(sid, "User logout");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login", new { reason = "logout" });
    }
}
