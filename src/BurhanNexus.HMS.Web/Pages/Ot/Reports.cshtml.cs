using System.Text;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>The 15 OT reports with date / theatre / surgeon / department / procedure / status filters, print (PDF via the browser) and Excel / CSV export.
/// The revenue report uses Phase 3 charges only and needs "View OT billing".</summary>
[HasPermission(Perm.OtViewStatistics)]
public class ReportsModel : OtPageModel
{
    [BindProperty(SupportsGet = true)] public string? Report { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? TheatreId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SurgeonId { get; set; }
    [BindProperty(SupportsGet = true)] public int? DepartmentId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProcedureId { get; set; }
    [BindProperty(SupportsGet = true)] public SurgeryStatus? Status { get; set; }
    public string PresetKey { get; private set; } = "month";
    public bool Financial { get; private set; }
    public string Title { get; private set; } = "";
    public IReadOnlyList<OtReport> Data { get; private set; } = Array.Empty<OtReport>();
    public IReadOnlyList<OtTheatre> Theatres { get; private set; } = Array.Empty<OtTheatre>();
    public IReadOnlyList<(int Id, string Name)> Surgeons { get; private set; } = Array.Empty<(int, string)>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<OtProcedure> Procedures { get; private set; } = Array.Empty<OtProcedure>();

    private async Task<IActionResult?> LoadAsync()
    {
        Nav("ot-reports", "reports");
        Financial = await CanAsync(Perm.OtViewBilling);
        Report = OtQueryService.Reports.Any(c => c.Key == Report) ? Report : "daily";
        var def = OtQueryService.Reports.First(c => c.Key == Report);
        if (def.Financial && !Financial) return Forbid();
        Title = def.Title;
        var (from, to, key) = FinancePeriod.Resolve(Period ?? (From is null && To is null ? (Report == "daily" ? "today" : "month") : null), From, To, await Clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        Data = await Query.ReportAsync(Report!, new OtFilter { From = from, To = to, TheatreId = TheatreId, SurgeonId = SurgeonId, DepartmentId = DepartmentId, ProcedureId = ProcedureId, Status = Status },
            Financial, await Setup.IsPrivateAsync());
        Theatres = await Setup.TheatresAsync(activeOnly: false);
        Surgeons = await Query.SurgeonsAsync();
        Procedures = await Setup.ProceduresAsync(activeOnly: false);
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        Departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnGetExportAsync(string format)
    {
        if (await DenyUnlessAsync(Perm.OtExportReports) is { } deny) return deny;
        if (await LoadAsync() is { } fail) return fail;
        var name = $"ot-{Report}-{From:yyyyMMdd}-{To:yyyyMMdd}";
        await HttpContext.RequestServices.GetRequiredService<IAuditService>().LogAsync(AuditActions.OtReportExported, "OtReport", name, $"OT report “{Title}” exported ({format}, {From:yyyy-MM-dd}..{To:yyyy-MM-dd})");
        if (format == "csv")
        {
            var sb = new StringBuilder();
            foreach (var t in Data)
            {
                if (Data.Count > 1) sb.AppendLine(Csv(t.Title));
                foreach (var row in Rows(t)) sb.AppendLine(string.Join(",", row.Select(c => Csv(c?.ToString()))));
                sb.AppendLine();
            }
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", name + ".csv");
        }
        var x = new XlsxWriter();
        var i = 0;
        foreach (var t in Data) { var n = Data.Count > 1 ? $"{++i}-{t.Title}" : t.Title; x.Sheet(n.Length > 31 ? n[..31] : n, Rows(t)); }
        if (Data.Count == 0) x.Sheet("Empty", new[] { new object?[] { "No data" } });
        return File(x.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
    }

    private static IEnumerable<object?[]> Rows(OtReport t)
    {
        yield return t.Columns.Cast<object?>().ToArray();
        foreach (var r in t.Rows) yield return r.Cast<object?>().ToArray();
        if (t.Totals is not null) yield return t.Totals.Cast<object?>().ToArray();
    }

    private static string Csv(string? v) => v is null ? "" : v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
