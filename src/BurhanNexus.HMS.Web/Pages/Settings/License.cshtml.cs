using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Licensing;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.LicenseView)]
public class LicenseModel : HmsPageModel
{
    private readonly ILicenseService _license;
    public LicenseModel(ILicenseService license) => _license = license;

    public LicenseStatus Status { get; private set; } = null!;
    public bool ReadOnlyRedirect { get; private set; }
    [BindProperty] public string? ActivationKey { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public bool Confirm { get; set; }
    public string MachineId => MachineIdentity.Current;
    [BindProperty] public string? RevocationList { get; set; }
    /// <summary>Update 15: the active key is shown only by its fingerprint (never in full).</summary>
    public string? KeyFingerprint { get; private set; }

    public async Task OnGetAsync(int? @readonly)
    {
        ViewData["Nav"] = "license";
        ViewData["SettingsTab"] = "license";
        ReadOnlyRedirect = @readonly == 1;
        Status = await _license.GetStatusAsync();
        KeyFingerprint = Status.LicenseId is null ? null : await HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Persistence.HmsDbContext>()
            .LicenseInformation.Select(l => l.KeyFingerprint).FirstOrDefaultAsync();
    }

    public async Task<IActionResult> OnPostRevocationsAsync()
    {
        if (await DenyUnlessAsync(Perm.LicenseActivate) is { } deny) return deny;
        if (string.IsNullOrWhiteSpace(RevocationList) || RevocationList.Length > 20000) { FlashError = "Paste the revocation list."; return RedirectToPage(); }
        var r = await _license.ImportRevocationsAsync(RevocationList);
        if (r.Succeeded) FlashSuccess = $"Revocation list imported ({r.Value} new licence ID(s)).";
        else FlashError = r.ErrorText;
        return RedirectToPage();
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

    public async Task<IActionResult> OnPostDeactivateAsync()
    {
        if (await DenyUnlessAsync(Perm.LicenseActivate) is { } deny) return deny;
        if (!Confirm)
        {
            FlashError = "Tick the confirmation to deactivate the licence.";
            return RedirectToPage();
        }
        var r = await _license.DeactivateAsync(Reason ?? string.Empty);
        if (r.Succeeded) FlashSuccess = "The licence was deactivated. The system is read-only until a licence key is activated.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
