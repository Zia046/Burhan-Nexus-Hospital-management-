using System.ComponentModel.DataAnnotations;
using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Account;

public class ChangePasswordModel : HmsPageModel
{
    private readonly IAuthService _auth;
    public ChangePasswordModel(IAuthService auth) => _auth = auth;

    [BindProperty, Required(ErrorMessage = "Current password is required."), DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty, Required(ErrorMessage = "New password is required."), DataType(DataType.Password), StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty, Required(ErrorMessage = "Please confirm the new password."), DataType(DataType.Password),
     Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool Forced => User.MustChangePasswordFlag();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await _auth.ChangeOwnPasswordAsync(CurrentUserId!.Value, User.GetSessionId()!.Value, CurrentPassword, NewPassword);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return Page();
        }
        // End this session too; the user logs in again with the new password.
        await _auth.LogoutAsync(User.GetSessionId()!.Value, "Password changed");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login", new { reason = "password" });
    }
}
