using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>
/// A4 radiology report: preview, print, save as PDF (browser) and reprint. Only a verified report is shown, and only a
/// released one is printed as final ("Release &amp; print" does both). Reprinting records a copy – it never creates a new order or report.
/// </summary>
[HasPermission(Perm.RadPrintReport, Perm.RadReprintReport, Perm.RadExportPdf)]
public class RadiologyReportModel : HmsPageModel
{
    private readonly IRadiologyService _rad;
    private readonly IRadiologyCatalogService _catalog;
    private readonly ISettingsService _settings;

    public RadiologyReportModel(IRadiologyService rad, IRadiologyCatalogService catalog, ISettingsService settings)
    {
        _rad = rad;
        _catalog = catalog;
        _settings = settings;
    }

    [BindProperty(SupportsGet = true)] public bool Print { get; set; }
    [BindProperty] public string? Format { get; set; }

    public RadiologyExam Exam { get; private set; } = null!;
    public RadiologySettings Rad { get; private set; } = null!;
    public HospitalProfile Hospital { get; private set; } = null!;
    public IReadOnlyList<RadiologyReportPrint> Prints { get; private set; } = Array.Empty<RadiologyReportPrint>();
    public bool IsFinal => Exam.Status is RadiologyExamStatus.Verified or RadiologyExamStatus.Released;
    public bool IsReleased => Exam.Status == RadiologyExamStatus.Released;
    public bool CanPrint { get; private set; }
    public bool CanPdf { get; private set; }
    public bool CanRelease { get; private set; }

    private async Task<IActionResult?> LoadAsync(long id)
    {
        var e = await _rad.ExamAsync(id);
        if (e is null) return NotFound();
        var seeModality = await CanAsync(e.Modality == RadiologyModality.XRay ? Perm.RadViewXRay : Perm.RadViewUltrasound)
                          || await CanAsync(Perm.RadViewHistory) || await CanAsync(Perm.RadVerifyReport) || await CanAsync(Perm.RadEnterReport);
        if (!seeModality) return Forbid();
        Exam = e;
        Rad = await _catalog.SettingsAsync();
        Hospital = (await _settings.GetProfileAsync())!;
        Prints = await _rad.PrintsAsync(id);
        CanPrint = await CanAsync(Prints.Count == 0 ? Perm.RadPrintReport : Perm.RadReprintReport);
        CanPdf = await CanAsync(Perm.RadExportPdf);
        CanRelease = await CanAsync(Perm.RadReleaseReport);
        return null;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ?? Page();

    /// <summary>Records the copy (and releases a verified report when allowed), then reloads the page to open the print dialog.</summary>
    public async Task<IActionResult> OnPostRecordAsync(long id)
    {
        if (await LoadAsync(id) is { } fail) return fail;
        var pdf = Format == "PDF";
        if (!IsFinal) { FlashError = "Only a verified report can be printed as final."; return RedirectToPage(new { id }); }
        if (!(pdf ? CanPdf : CanPrint)) return Forbid();
        if (!IsReleased && !CanRelease) return Forbid();
        var r = await _rad.RecordPrintAsync(id, pdf ? "PDF" : "A4", release: !IsReleased);
        if (!r.Succeeded) { FlashError = r.ErrorText; return RedirectToPage(new { id }); }
        return RedirectToPage(new { id, print = true });
    }
}
