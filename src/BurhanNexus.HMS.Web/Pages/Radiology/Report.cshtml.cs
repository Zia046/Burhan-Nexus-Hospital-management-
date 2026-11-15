using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>
/// Structured / free-text report entry. Templates only pre-fill the boxes (later template edits never change saved reports).
/// Nothing is mandatory for a draft; submitting needs a conclusion (findings or impression) and the reporting doctor.
/// </summary>
[HasPermission(Perm.RadEnterReport, Perm.RadEditDraft)]
public class ReportModel : RadiologyPageModel
{
    [BindProperty] public RadiologyReportInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }

    public RadiologyExam Exam { get; private set; } = null!;
    public IReadOnlyList<RadiologyTemplate> Templates { get; private set; } = Array.Empty<RadiologyTemplate>();
    public bool CanSubmit { get; private set; }
    public bool Editable { get; private set; }

    private static readonly RadiologyExamStatus[] Reportable =
        { RadiologyExamStatus.ExamCompleted, RadiologyExamStatus.ReportPending, RadiologyExamStatus.ReportEntered, RadiologyExamStatus.AwaitingVerification };

    private async Task<IActionResult?> LoadAsync(long id)
    {
        var e = await Rad.ExamAsync(id);
        if (e is null) return NotFound();
        if (!await CanSeeAsync(e.Modality)) return Forbid();
        Exam = e;
        Nav("rad-reporting", "reporting");
        Templates = await Catalog.TemplatesAsync(activeOnly: true, e.Modality);
        CanSubmit = await CanAsync(Perm.RadSubmitReport);
        Editable = Reportable.Contains(e.Status);
        return null;
    }

    /// <summary>A new report needs "Enter Report"; changing an existing draft needs "Edit Draft Report" (or being its author with "Enter Report").</summary>
    private async Task<bool> MayWriteAsync(RadiologyExam e) =>
        e.ReportSavedUtc is null ? await CanAsync(Perm.RadEnterReport)
            : await CanAsync(Perm.RadEditDraft) || (e.ReportedByUserId == CurrentUserId && await CanAsync(Perm.RadEnterReport));

    public async Task<IActionResult> OnGetAsync(long id, int? template)
    {
        if (await LoadAsync(id) is { } r) return r;
        if (Editable && !await MayWriteAsync(Exam)) return Forbid();
        var settings = await Catalog.SettingsAsync();
        var me = CurrentUserId is int uid ? await HttpContext.RequestServices.GetRequiredService<HmsDbContext>().Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid) : null;
        Input = new RadiologyReportInput
        {
            ClinicalIndication = Exam.ClinicalIndication, Technique = Exam.Technique, Findings = Exam.Findings, Impression = Exam.Impression,
            Recommendations = Exam.Recommendations, TemplateName = Exam.TemplateName,
            ReportingDoctorName = Exam.ReportingDoctorName ?? settings.DefaultReportingDoctor ?? me?.FullName,
            ReportingDoctorDesignation = Exam.ReportingDoctorDesignation ?? settings.DefaultDesignation ?? me?.Designation
        };
        // A new report starts from the service's default template (or the one chosen).
        var tid = template ?? (Exam.ReportSavedUtc is null ? Exam.Service?.DefaultTemplateId : null);
        if (tid is int t && Templates.FirstOrDefault(x => x.Id == t) is { } tpl && (template is not null || Exam.ReportSavedUtc is null))
        {
            Input.Technique = tpl.Technique; Input.Findings = tpl.Findings; Input.Impression = tpl.Impression; Input.Recommendations = tpl.Recommendations; Input.TemplateName = tpl.Name;
        }
        RowVersion = Exam.RowVersion;
        return Page();
    }

    private async Task<IActionResult> SaveAsync(long id, bool submit)
    {
        var e = await Rad.ExamAsync(id);
        if (e is null) return NotFound();
        if (!await CanSeeAsync(e.Modality) || !await MayWriteAsync(e)) return Forbid();
        if (submit && await DenyUnlessAsync(Perm.RadSubmitReport) is { } deny) return deny;
        var r = await Rad.SaveReportAsync(id, Input, submit, RowVersion);
        if (r.Succeeded)
        {
            FlashSuccess = submit ? "Report submitted for verification." : "Draft saved.";
            return submit ? Redirect($"/Radiology/Exam/{id}#report") : RedirectToPage(new { id });
        }
        if (await LoadAsync(id) is { } fail) return fail;
        AddErrors(r);
        return Page();
    }

    public Task<IActionResult> OnPostAsync(long id) => SaveAsync(id, submit: false);
    public Task<IActionResult> OnPostSubmitAsync(long id) => SaveAsync(id, submit: true);
}
