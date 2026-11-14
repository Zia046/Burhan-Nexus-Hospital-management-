using BurhanNexus.HMS.Infrastructure.Laboratory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>Laboratory logo / signature image for reports and the settings preview (any signed-in user).</summary>
[Authorize]
public class ImageModel : HmsPageModel
{
    private readonly ILabCatalogService _catalog;
    public ImageModel(ILabCatalogService catalog) => _catalog = catalog;

    public async Task<IActionResult> OnGetAsync(string kind)
    {
        var s = await _catalog.SettingsAsync();
        var (data, type) = kind == "signature" ? (s.Signature, s.SignatureContentType) : (s.Logo, s.LogoContentType);
        if (data is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(data, type ?? "image/png");
    }
}
