using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Account;

[EnableRateLimiting("login")]
public class LoginModel : PageModel
{
    public const string RememberCookie = "BNHMS.RememberUser";
    private readonly IAuthService _auth;
    private readonly Infrastructure.Persistence.HmsDbContext _db;

    public LoginModel(IAuthService auth, Infrastructure.Persistence.HmsDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    [BindProperty, Required(ErrorMessage = "Username is required."), StringLength(64)]
    public string Username { get; set; } = string.Empty;

    [BindProperty, Required(ErrorMessage = "Password is required."), StringLength(128), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty] public bool RememberUsername { get; set; }

    [BindProperty, StringLength(50)] public string? Workstation { get; set; }

    public string? Error { get; set; }
    public string? Info { get; set; }

    public IActionResult OnGet(string? reason, string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(SafeReturn(returnUrl));
        var remembered = Request.Cookies[RememberCookie];
        if (!string.IsNullOrEmpty(remembered))
        {
            Username = remembered;
            RememberUsername = true;
        }
        Workstation = HttpRequestContext.SanitizeWorkstation(Request.Cookies[HttpRequestContext.WorkstationCookie]);
        Info = reason switch
        {
            "timeout" => "Your session ended because of inactivity. Please log in again.",
            "ended" => "Your session has ended. Please log in again.",
            "logout" => "You have been logged out successfully.",
            "password" => "Your password was changed. Please log in with the new password.",
            _ => null
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            Error = "Please enter your username and password.";
            return Page();
        }

        // Remember the computer name first so the login itself is audited with it.
        var ws = HttpRequestContext.SanitizeWorkstation(Workstation);
        if (ws is not null)
        {
            Response.Cookies.Append(HttpRequestContext.WorkstationCookie, ws, LongCookie());
            HttpContext.Items[HttpRequestContext.WorkstationItem] = ws;
        }

        var result = await _auth.LoginAsync(Username, Password, HttpContext.RequestAborted);
        if (!result.Succeeded || result.Session is null)
        {
            Error = result.Error;
            Password = string.Empty;
            return Page();
        }

        if (RememberUsername) Response.Cookies.Append(RememberCookie, result.Session.Username, LongCookie());
        else Response.Cookies.Delete(RememberCookie);

        var isSysAdmin = await _db.Users.AnyAsync(u => u.Id == result.Session.UserId && u.IsSystemAdministrator);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Session.UserId.ToString()),
            new(ClaimTypes.Name, result.Session.Username),
            new(HmsClaims.FullName, result.Session.FullName),
            new(HmsClaims.SessionId, result.Session.SessionId.ToString()),
            new(HmsClaims.SecurityStamp, result.Session.SecurityStamp),
        };
        if (result.Session.MustChangePassword) claims.Add(new Claim(HmsClaims.MustChangePassword, "1"));
        if (isSysAdmin) claims.Add(new Claim(HmsClaims.SystemAdministrator, "1"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

        if (result.Session.MustChangePassword) return RedirectToPage("/Account/ChangePassword");
        return LocalRedirect(SafeReturn(returnUrl));
    }

    private string SafeReturn(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
            ? returnUrl : "/";

    private CookieOptions LongCookie() => new()
    {
        HttpOnly = true, SameSite = SameSiteMode.Strict, IsEssential = true,
        Secure = Request.IsHttps, Expires = DateTimeOffset.UtcNow.AddYears(1)
    };
}
