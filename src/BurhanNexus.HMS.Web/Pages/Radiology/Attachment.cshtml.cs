using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Opens / downloads an examination file (permission-checked and audited). Images and PDFs open in the browser; DICOM files download.</summary>
[HasPermission(Perm.RadViewAttachments)]
public class AttachmentModel : RadiologyPageModel
{
    public async Task<IActionResult> OnGetAsync(long id, bool download)
    {
        var file = await Rad.OpenAttachmentAsync(id);
        if (file is null) return NotFound();
        var exam = await Rad.ExamAsync(file.Value.Attachment.ExamId);
        if (exam is null || !await CanSeeAsync(exam.Modality)) return Forbid();
        var a = file.Value.Attachment;
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var stream = System.IO.File.OpenRead(file.Value.Path);
        // DICOM is not displayed by browsers (no PACS viewer is included): always a download.
        return download || a.Kind == Core.Domain.RadiologyFileKind.Dicom
            ? File(stream, a.ContentType, a.OriginalFileName)
            : new FileStreamResult(stream, a.ContentType) { EnableRangeProcessing = true };
    }
}
