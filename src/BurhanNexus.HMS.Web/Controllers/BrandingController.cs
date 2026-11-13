using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Controllers;

/// <summary>Serves the hospital logo from the central database so every computer shows the same branding.</summary>
[AllowAnonymous]
[Route("branding")]
public sealed class BrandingController : Controller
{
    private readonly IHospitalAdminService _hospital;

    public BrandingController(IHospitalAdminService hospital) => _hospital = hospital;

    [HttpGet("logo")]
    public async Task<IActionResult> Logo(CancellationToken ct)
    {
        var logo = await _hospital.GetLogoAsync(ct);
        if (logo is null) return Redirect("/img/default-logo.svg");
        Response.Headers.CacheControl = "public, max-age=300";
        return File(logo.Value.Content, logo.Value.ContentType);
    }
}
