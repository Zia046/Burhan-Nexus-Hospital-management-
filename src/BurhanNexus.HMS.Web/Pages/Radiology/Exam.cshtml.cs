using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>One examination: queue steps, report (read-only), verification, release, printing, attachments and full history.</summary>
[HasPermission(Perm.RadViewXRay, Perm.RadViewUltrasound, Perm.RadViewEcg, Perm.RadEnterReport, Perm.RadVerifyReport, Perm.RadViewHistory, Perm.RadDashboard)]
[RequestFormLimits(MultipartBodyLengthLimit = 210_000_000)]
[RequestSizeLimit(210_000_000)]
public class ExamModel : RadiologyPageModel
{
    public RadiologyExam Exam { get; private set; } = null!;
    public RadiologyOrder Order => Exam.Order!;
    public IReadOnlyList<RadiologyExamEvent> Events { get; private set; } = Array.Empty<RadiologyExamEvent>();
    public IReadOnlyList<RadiologyReportRevision> Revisions { get; private set; } = Array.Empty<RadiologyReportRevision>();
    public IReadOnlyList<RadiologyReportPrint> Prints { get; private set; } = Array.Empty<RadiologyReportPrint>();
    public IReadOnlyList<RadiologyAttachment> Attachments { get; private set; } = Array.Empty<RadiologyAttachment>();
    public ExamPayment? Payment { get; private set; }
    public RadiologySettings Settings { get; private set; } = null!;
    public bool ShowFees { get; private set; }
    public bool IsOwnReport { get; private set; }

    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? TechnicalNotes { get; set; }
    [BindProperty] public string? PerformedBy { get; set; }
    [BindProperty] public DateOnly? NewDate { get; set; }
    [BindProperty] public TimeOnly? NewTime { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public IFormFile? Upload { get; set; }

    private async Task<IActionResult?> LoadAsync(long id)
    {
        var e = await Rad.ExamAsync(id);
        if (e is null) return NotFound();
        if (!await CanSeeAsync(e.Modality)) return Forbid();
        Exam = e;
        Nav("rad-" + NavKey(e.Modality), NavKey(e.Modality));
        Settings = await Catalog.SettingsAsync();
        ShowFees = await CanAsync(Perm.RadViewFinancial);
        if (ShowFees) Payment = (await Rad.PaymentsAsync(e.OrderId)).GetValueOrDefault(e.Id);
        Events = await Rad.EventsAsync(new[] { e.Id });
        Revisions = await Rad.RevisionsAsync(e.Id);
        Prints = await Rad.PrintsAsync(e.Id);
        if (await CanAsync(Perm.RadViewAttachments) || await CanAsync(Perm.RadManageAttachments)) Attachments = await Rad.AttachmentsAsync(e.Id);
        IsOwnReport = e.ReportedByUserId == CurrentUserId;
        return null;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ?? Page();

    private async Task<IActionResult> DoneAsync(long id, ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return RedirectToPage(new { id }); }
        if (await LoadAsync(id) is { } fail) return fail;
        AddErrors(r);
        return Page();
    }

    /// <summary>Every action re-checks the permission and the user's access to the modality on the server.</summary>
    private async Task<IActionResult?> GuardAsync(long id, string permission)
    {
        if (await DenyUnlessAsync(permission) is { } deny) return deny;
        var e = await Rad.ExamAsync(id);
        if (e is null) return NotFound();
        return await CanSeeAsync(e.Modality) ? null : Forbid();
    }

    public async Task<IActionResult> OnPostArriveAsync(long id) =>
        await GuardAsync(id, Perm.RadStartExam) ?? await DoneAsync(id, await Rad.ArriveAsync(id), "Patient marked as waiting.");

    public async Task<IActionResult> OnPostStartAsync(long id) =>
        await GuardAsync(id, Perm.RadStartExam) ?? await DoneAsync(id, await Rad.StartAsync(id), "Examination started.");

    public async Task<IActionResult> OnPostCompleteAsync(long id) =>
        await GuardAsync(id, Perm.RadCompleteExam) ?? await DoneAsync(id, await Rad.CompleteAsync(id, TechnicalNotes, PerformedBy), "Examination completed – it is now in the reporting list.");

    public async Task<IActionResult> OnPostVerifyAsync(long id) =>
        await GuardAsync(id, Perm.RadVerifyReport) ?? await DoneAsync(id, await Rad.VerifyAsync(id, await CanAsync(Perm.RadVerifyOwnReport)), "Report verified. It can now be released.");

    public async Task<IActionResult> OnPostRejectAsync(long id) =>
        await GuardAsync(id, Perm.RadVerifyReport) ?? await DoneAsync(id, await Rad.RejectAsync(id, Reason), "Report sent back for correction. The rejected version is kept in the revision history.");

    public async Task<IActionResult> OnPostReleaseAsync(long id) =>
        await GuardAsync(id, Perm.RadReleaseReport) ?? await DoneAsync(id, await Rad.ReleaseAsync(id), "Report released as final.");

    public async Task<IActionResult> OnPostAmendAsync(long id) =>
        await GuardAsync(id, Perm.RadAmendReport) ?? await DoneAsync(id, await Rad.AmendAsync(id, Reason), "Report re-opened for amendment. The verified version is kept as a revision; it must be verified again.");

    public async Task<IActionResult> OnPostCancelAsync(long id) =>
        await GuardAsync(id, Perm.RadCancelBooking) ?? await DoneAsync(id, await Rad.CancelAsync(id, Reason), "Examination cancelled. It stays in the history with the reason.");

    public async Task<IActionResult> OnPostRescheduleAsync(long id)
    {
        if (await GuardAsync(id, Perm.RadEditBooking) is { } deny) return deny;
        if (NewDate is null) return await DoneAsync(id, ServiceResult.Fail("Select the new date."), "");
        return await DoneAsync(id, await Rad.RescheduleAsync(id, NewDate.Value.ToDateTime(NewTime ?? new TimeOnly(9, 0))), "Examination rescheduled.");
    }

    public async Task<IActionResult> OnPostUploadAsync(long id)
    {
        if (await GuardAsync(id, Perm.RadManageAttachments) is { } deny) return deny;
        if (Upload is null || Upload.Length == 0) return await DoneAsync(id, ServiceResult.Fail("Choose a file to attach."), "");
        await using var stream = Upload.OpenReadStream();
        var r = await Rad.AddAttachmentAsync(id, stream, Upload.FileName, Description);
        return await DoneAsync(id, r, "File attached.");
    }

    public async Task<IActionResult> OnPostRemoveFileAsync(long id, long attachmentId)
    {
        if (await GuardAsync(id, Perm.RadManageAttachments) is { } deny) return deny;
        if ((await Rad.AttachmentsAsync(id)).All(a => a.Id != attachmentId)) return NotFound();
        return await DoneAsync(id, await Rad.RemoveAttachmentAsync(attachmentId, Reason), "File removed from view (kept on the server with the reason).");
    }
}
