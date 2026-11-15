using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>X-Ray / Ultrasound worklist (queue) of one day – open work of earlier days stays visible.</summary>
[HasPermission(Perm.RadViewXRay, Perm.RadViewUltrasound)]
public class WorklistModel : RadiologyPageModel
{
    private readonly HospitalClock _clock;
    public WorklistModel(HospitalClock clock) => _clock = clock;

    [BindProperty(SupportsGet = true)] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty] public string? TechnicalNotes { get; set; }
    [BindProperty] public string? PerformedBy { get; set; }

    public RadiologyModality Modality { get; private set; }
    public IReadOnlyList<RadiologyExamRow> Rows { get; private set; } = Array.Empty<RadiologyExamRow>();
    public DateOnly Today { get; private set; }

    private async Task<IActionResult?> InitAsync(string modality)
    {
        if (!Enum.TryParse<RadiologyModality>(modality, true, out var m)) return NotFound();
        Modality = m;
        if (await DenyUnlessAsync(WorklistPermission(m)) is { } deny) return deny;
        Nav(m == RadiologyModality.XRay ? "rad-xray" : "rad-ultrasound", m == RadiologyModality.XRay ? "xray" : "ultrasound");
        return null;
    }

    public async Task<IActionResult> OnGetAsync(string modality)
    {
        if (await InitAsync(modality) is { } r) return r;
        Today = await _clock.TodayAsync();
        var date = Date ?? Today;
        Date = date;
        var scope = Status switch { "queue" or null or "" => "queue", "reporting" => "reporting", "verification" => "verification", "final" => "final", "cancelled" => "cancelled", _ => "all" };
        Rows = await Rad.ExamsAsync(new RadiologyWorklistQuery
        {
            Modality = Modality, Scope = scope, From = date, To = date, Q = Q, IncludeOpenEarlier = date == Today && scope is "queue" or "reporting" or "verification", Take = 500
        });
        return Page();
    }

    private async Task<IActionResult> DoneAsync(string modality, long examId, Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded) FlashSuccess = ok; else FlashError = r.ErrorText;
        return Redirect($"/Radiology/Worklist/{modality}?date={Date:yyyy-MM-dd}&status={Status}&q={Uri.EscapeDataString(Q ?? "")}#exam-{examId}");
    }

    private async Task<IActionResult?> GuardAsync(string modality, long examId, string permission)
    {
        if (await InitAsync(modality) is { } r) return r;
        if (await DenyUnlessAsync(permission) is { } deny) return deny;
        var e = await Rad.ExamAsync(examId);
        return e is null || e.Modality != Modality ? NotFound() : null;
    }

    public async Task<IActionResult> OnPostArriveAsync(string modality, long examId) =>
        await GuardAsync(modality, examId, Perm.RadStartExam) ?? await DoneAsync(modality, examId, await Rad.ArriveAsync(examId), "Patient marked as waiting.");

    public async Task<IActionResult> OnPostStartAsync(string modality, long examId) =>
        await GuardAsync(modality, examId, Perm.RadStartExam) ?? await DoneAsync(modality, examId, await Rad.StartAsync(examId), "Examination started.");

    public async Task<IActionResult> OnPostCompleteAsync(string modality, long examId) =>
        await GuardAsync(modality, examId, Perm.RadCompleteExam) ?? await DoneAsync(modality, examId, await Rad.CompleteAsync(examId, TechnicalNotes, PerformedBy), "Examination completed – it is now in the reporting list.");
}
