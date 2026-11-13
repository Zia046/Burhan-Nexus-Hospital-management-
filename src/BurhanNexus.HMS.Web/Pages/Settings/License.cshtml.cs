using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Licensing;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.LicenseView)]
public class LicenseModel : HmsPageModel
{
    private readonly ILicenseService _license;
    public LicenseModel(ILicenseService license) => _license = license;

    public LicenseStatus Status { get; private set; } = null!;
    public bool ReadOnlyRedirect { get; private set; }
    [BindProperty] public string? ActivationKey { get; set; }

    public async Task OnGetAsync(int? @readonly)
    {
        ViewData["Nav"] = "license";
        ViewData["SettingsTab"] = "license";
        ReadOnlyRedirect = @readonly == 1;
        Status = await _license.GetStatusAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.LicenseActivate) is { } deny) return deny;
        if (string.IsNullOrWhiteSpace(ActivationKey))
        {
            FlashError = "Please enter the activation key.";
            return RedirectToPage();
        }
        if (ActivationKey.Length > 4000)
        {
            FlashError = "The activation key is not valid.";
            return RedirectToPage();
        }
        var r = await _license.ActivateAsync(ActivationKey);
        if (r.Succeeded) FlashSuccess = $"Licence activated: {r.Value!.DisplayName}.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
