using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Settings;

/// <summary>Only the primary system administrator may re-run the setup wizard (users and data are kept).</summary>
public class ResetSetupModel : HmsPageModel
{
    private readonly ISetupService _setup;
    private readonly IPasswordService _passwords;
    private readonly HmsDbContext _db;

    public ResetSetupModel(ISetupService setup, IPasswordService passwords, HmsDbContext db)
    {
        _setup = setup;
        _passwords = passwords;
        _db = db;
    }

    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? Confirmation { get; set; }

    public IActionResult OnGet()
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "reset";
        return User.IsSystemAdministrator() ? Page() : Forbid();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!User.IsSystemAdministrator() || !await _db.Users.AnyAsync(u => u.Id == CurrentUserId && u.IsSystemAdministrator)) return Forbid();
        if (Confirmation?.Trim() != "RESET")
        {
            FlashError = "Type RESET in capital letters to confirm.";
            return RedirectToPage();
        }
        var hash = await _db.UserCredentials.Where(c => c.UserId == CurrentUserId).Select(c => c.PasswordHash).FirstOrDefaultAsync();
        if (hash is null || !_passwords.Verify(hash, Password ?? string.Empty, out _))
        {
            FlashError = "Your password is incorrect.";
            return RedirectToPage();
        }
        var r = await _setup.RequestResetAsync();
        if (!r.Succeeded)
        {
            FlashError = r.ErrorText;
            return RedirectToPage();
        }
        return Redirect("/Setup");
    }
}
