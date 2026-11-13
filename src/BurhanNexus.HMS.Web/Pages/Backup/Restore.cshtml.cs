using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Backup;
using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Backup;

/// <summary>Protected restore workflow: permission + warning + password + typed confirmation + automatic safety backup.</summary>
[HasPermission(Perm.BackupRestore)]
public class RestoreModel : HmsPageModel
{
    private readonly IBackupService _backups;
    private readonly IAuthService _auth;

    public RestoreModel(IBackupService backups, IAuthService auth)
    {
        _backups = backups;
        _auth = auth;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? Confirmation { get; set; }
    [BindProperty] public bool Understood { get; set; }

    public BackupHistory? Backup { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "backup";
        Backup = await _backups.GetAsync(Id);
        return Backup is null || Backup.Status != BackupStatus.Completed ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Nav"] = "backup";
        Backup = await _backups.GetAsync(Id);
        if (Backup is null) return NotFound();
        if (!Understood) ModelState.AddModelError(string.Empty, "Please confirm that you understand current data will be replaced.");
        if (Confirmation?.Trim() != "RESTORE") ModelState.AddModelError(string.Empty, "Type RESTORE in capital letters to confirm.");
        if (string.IsNullOrEmpty(Password)) ModelState.AddModelError(string.Empty, "Enter your password.");
        if (!ModelState.IsValid) return Page();

        var r = await _backups.RestoreAsync(Id, Password!, HttpContext.RequestAborted);
        if (!r.Succeeded)
        {
            AddErrors(r);
            return Page();
        }

        // The restored database has its own session list, so every user (including this one) logs in again.
        if (User.GetSessionId() is Guid sid) await _auth.LogoutAsync(sid, "Database restored");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["FlashSuccess"] = $"Database restored from {Backup.FileName}. A safety backup of the previous data was saved as {r.Value}. Please log in again.";
        return Redirect("/Account/Login");
    }
}
