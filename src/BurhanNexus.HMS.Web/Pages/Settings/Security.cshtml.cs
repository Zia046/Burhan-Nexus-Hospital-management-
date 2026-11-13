using System.ComponentModel.DataAnnotations;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.SettingsSecurity)]
public class SecurityModel : HmsPageModel
{
    private readonly ISettingsService _settings;
    public SecurityModel(ISettingsService settings) => _settings = settings;

    [BindProperty, Range(5, 720)] public int SessionTimeoutMinutes { get; set; }
    [BindProperty, Range(3, 20)] public int MaxFailedLoginAttempts { get; set; }
    [BindProperty, Range(1, 1440)] public int LockoutMinutes { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "security";
        var s = await _settings.GetSecurityAsync();
        SessionTimeoutMinutes = s.SessionTimeoutMinutes;
        MaxFailedLoginAttempts = s.MaxFailedLoginAttempts;
        LockoutMinutes = s.LockoutMinutes;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "security";
        var r = await _settings.UpdateSecurityAsync(new SecuritySettings(SessionTimeoutMinutes, MaxFailedLoginAttempts, LockoutMinutes));
        if (!r.Succeeded)
        {
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Security settings saved.";
        return RedirectToPage();
    }
}
