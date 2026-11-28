using System.Globalization;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Diseases;

/// <summary>
/// Update 18: disease-wise patient analytics – unique patients (once per period), visits (repeat visits counted), gender,
/// age groups and date-wise figures from the recorded visit-wise disease history. Nothing is estimated or invented.
/// </summary>
[HasPermission(Perm.DiseasesAnalytics)]
public class AnalyticsModel : HmsPageModel
{
    private readonly IDiseaseService _diseases;
    private readonly IAuditService _audit;
    public AnalyticsModel(IDiseaseService diseases, IAuditService audit) { _diseases = diseases; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Disease { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? MinAge { get; set; }
    [BindProperty(SupportsGet = true)] public int? MaxAge { get; set; }
    [BindProperty(SupportsGet = true)] public Gender? Gender { get; set; }
    /// <summary>Upper bounds of the age groups, e.g. "5,12,18,35,50,65".</summary>
    [BindProperty(SupportsGet = true)] public string? AgeGroups { get; set; }

    public DiseaseAnalytics? Result { get; private set; }
    public string? NotFound { get; private set; }
    public bool CanAdd { get; private set; }
    public bool CanExport { get; private set; }
    public string DateText => From is null && To is null ? "All dates" : $"{From?.ToString("dd-MM-yyyy") ?? "start"} to {To?.ToString("dd-MM-yyyy") ?? "today"}";
    public string AgeText => MinAge is null && MaxAge is null ? "All ages" : $"{MinAge?.ToString() ?? "0"} – {MaxAge?.ToString() ?? "any"} years";
    public string GenderText => Gender?.ToString() ?? "All";

    private int[] Bounds() => (AgeGroups ?? "").Split(',', ';', ' ').Select(x => int.TryParse(x, out var v) ? v : -1).Where(v => v >= 0 && v < 130).ToArray() is { Length: > 0 } b
        ? b : DiseaseService.DefaultAgeBounds;

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "diseases";
        CanAdd = await CanAsync(Perm.DiseasesAdd);
        CanExport = await CanAsync(Perm.DiseasesExport);
        if (MinAge is int a && MaxAge is int b && a > b) (MinAge, MaxAge) = (b, a);
        if (From is { } f && To is { } t && f > t) (From, To) = (t, f);
        int? id = null;
        if (!string.IsNullOrWhiteSpace(Disease))
        {
            var norm = DiseaseService.Normalize(Disease);
            var match = (await _diseases.ListAsync(null, null)).FirstOrDefault(r => r.Disease.NormalizedName == norm);
            if (match is null) { NotFound = Disease.Trim(); return; }
            id = match.Disease.Id;
            Disease = match.Disease.Name;
        }
        AgeGroups = string.Join(",", Bounds());
        Result = await _diseases.AnalyticsAsync(new DiseaseAnalyticsFilter { DiseaseId = id, From = From, To = To, MinAge = MinAge, MaxAge = MaxAge, Gender = Gender, AgeBounds = Bounds() });
    }

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>"Add New Disease" from the analytics search (when allowed) – immediately available everywhere.</summary>
    public async Task<IActionResult> OnPostAddAsync(string name)
    {
        if (await DenyUnlessAsync(Perm.DiseasesAdd) is { } deny) return deny;
        var r = await _diseases.ResolveAsync(name, true);
        if (r.Succeeded) FlashSuccess = $"'{r.Value!.Name}' added to the Disease Master.";
        else FlashError = string.Join(" ", r.Errors);
        return Redirect("/Diseases/Analytics?disease=" + Uri.EscapeDataString(r.Value?.Name ?? name));
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (await DenyUnlessAsync(Perm.DiseasesExport) is { } deny) return deny;
        await LoadAsync();
        if (Result is null) return Redirect("/Diseases/Analytics");
        var r = Result;
        var rows = new List<object?[]>
        {
            new object?[] { "Disease", Disease ?? "All diseases" }, new object?[] { "Date", DateText }, new object?[] { "Age", AgeText }, new object?[] { "Gender", GenderText },
            Array.Empty<object?>(),
            new object?[] { "Total unique patients", r.UniquePatients }, new object?[] { "Total visits", r.Visits },
            new object?[] { "Male", r.Male }, new object?[] { "Female", r.Female }, new object?[] { "Other", r.Other },
            Array.Empty<object?>(), new object?[] { "Age group", "Patients" },
        };
        rows.AddRange(r.AgeGroups.Select(g => new object?[] { g.Label, g.Patients }));
        if (r.AgeUnknown > 0) rows.Add(new object?[] { "Age not recorded", r.AgeUnknown });
        rows.Add(Array.Empty<object?>());
        rows.Add(new object?[] { "Date", "Unique patients", "Visits" });
        rows.AddRange(r.ByDate.Select(d => new object?[] { d.Date, d.Patients, d.Visits }));
        if (Disease is null)
        {
            rows.Add(Array.Empty<object?>());
            rows.Add(new object?[] { "Disease", "Unique patients", "Visits", "Male", "Female", "Other" });
            rows.AddRange(r.ByDisease.Select(d => new object?[] { d.Disease, d.Patients, d.Visits, d.Male, d.Female, d.Other }));
        }
        await _audit.LogAsync(AuditActions.DiseaseReportExported, "DiseaseAnalytics", Disease ?? "all", $"Disease analytics exported ({Disease ?? "all diseases"}, {DateText}, {AgeText}, {GenderText})");
        var x = new XlsxWriter().Sheet("Disease analytics", rows);
        return File(x.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"disease-analytics-{(Disease is null ? "all" : DiseaseService.Normalize(Disease).ToLowerInvariant())}-{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.xlsx");
    }
}
