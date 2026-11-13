using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.SettingsView)]
public class PreferencesModel : HmsPageModel
{
    private readonly IHospitalAdminService _hospital;
    public PreferencesModel(IHospitalAdminService hospital) => _hospital = hospital;

    [BindProperty] public PreferencesInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    public bool CanEdit { get; private set; }

    private async Task LoadAsync(bool bind)
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "preferences";
        CanEdit = await CanAsync(Perm.SettingsEdit);
        if (!bind) return;
        var c = await _hospital.GetConfigurationAsync();
        Input = new PreferencesInput
        {
            CurrencyCode = c.CurrencyCode, CurrencySymbol = c.CurrencySymbol, TimeZoneId = c.TimeZoneId,
            DateFormat = c.DateFormat, TimeFormat = c.TimeFormat, DefaultLanguage = c.DefaultLanguage
        };
        RowVersion = c.RowVersion;
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.SettingsEdit) is { } deny) return deny;
        var r = await _hospital.UpdatePreferencesAsync(Input, RowVersion);
        if (!r.Succeeded)
        {
            await LoadAsync(false);
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Preferences saved.";
        return RedirectToPage();
    }
}
