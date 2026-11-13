using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BurhanNexus.HMS.Web.Pages;

/// <summary>Base page with flash messages and server-side permission checks for individual actions.</summary>
public abstract class HmsPageModel : PageModel
{
    [TempData] public string? FlashSuccess { get; set; }
    [TempData] public string? FlashError { get; set; }

    protected UserPermissionContext Permissions => HttpContext.RequestServices.GetRequiredService<UserPermissionContext>();

    protected Task<bool> CanAsync(string permission) => Permissions.HasAsync(permission);

    /// <summary>Returns a 403 result (and logs it via the access-denied handler) when the permission is missing.</summary>
    protected async Task<IActionResult?> DenyUnlessAsync(string permission)
    {
        if (await Permissions.HasAsync(permission)) return null;
        return Forbid();
    }

    protected void AddErrors(ServiceResult result)
    {
        foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e);
    }

    protected int? CurrentUserId => User.GetUserId();
}
