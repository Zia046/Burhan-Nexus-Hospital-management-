using BurhanNexus.HMS.Infrastructure.Radiology;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Radiology report logo / signature (any signed-in user – shown on printed reports).</summary>
[Authorize]
public class ImageModel : HmsPageModel
{
    private readonly IRadiologyCatalogService _catalog;
    public ImageModel(IRadiologyCatalogService catalog) => _catalog = catalog;

    public async Task<IActionResult> OnGetAsync(string kind)
    {
        var s = await _catalog.SettingsAsync();
        var (data, type) = kind == "signature" ? (s.Signature, s.SignatureContentType) : (s.Logo, s.LogoContentType);
        if (data is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(data, type ?? "image/png");
    }
}
