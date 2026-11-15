using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>Streams a scanned OT document (consent / post-op instructions) after a permission check. Files are not served from wwwroot.</summary>
[HasPermission(Perm.OtViewPreOp, Perm.OtManageConsent, Perm.OtEnterPostOp, Perm.OtDashboard)]
public class DocumentModel : OtPageModel
{
    public async Task<IActionResult> OnGetAsync(long id)
    {
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        var d = await db.OtDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        var path = HttpContext.RequestServices.GetRequiredService<RadiologyFileStore>().PathOf(d.StoredPath);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        return PhysicalFile(path, d.ContentType);
    }
}
