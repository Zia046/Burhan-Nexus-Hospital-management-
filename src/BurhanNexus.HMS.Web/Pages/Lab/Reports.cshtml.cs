using System.Text;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>Laboratory statistics (daily/monthly/yearly) with Excel/CSV export and print (PDF via the browser).
/// Collection figures come from the Phase 3 receipts and need "View Laboratory Financial Data".</summary>
[HasPermission(Perm.LabReports)]
public class ReportsModel : HmsPageModel
{
    private readonly ILabReportService _reports;
    private readonly HospitalClock _clock;
    private readonly IAuditService _audit;

    public ReportsModel(ILabReportService reports, HospitalClock clock, IAuditService audit)
    {
        _reports = reports;
        _clock = clock;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Group { get; set; }
    public string PresetKey { get; private set; } = "month";
    public LabStatistics Data { get; private set; } = null!;

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "lab-reports";
        ViewData["LabTab"] = "reports";
        var (from, to, key) = FinancePeriod.Resolve(Period ?? (From is null && To is null ? "month" : null), From, To, await _clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        Group = Group is "month" or "year" ? Group : "day";
        Data = await _reports.StatisticsAsync(from, to, Group, await CanAsync(Perm.LabViewFinancial));
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnGetExportAsync(string format)
    {
        await LoadAsync();
        var d = Data;
        var name = $"laboratory-report-{d.From:yyyyMMdd}-{d.To:yyyyMMdd}";
        await _audit.LogAsync(AuditActions.LabExported, "LabReport", name, $"Laboratory report exported ({format}, {d.From:yyyy-MM-dd}..{d.To:yyyy-MM-dd})");
        var activity = new[] { new object?[] { "Period", "Visits", "Tests booked", "Completed", "Cancelled" } }
            .Concat(d.Activity.Select(a => new object?[] { Label(a.Date), a.Orders, a.Booked, a.Completed, a.Cancelled }));
        object?[][] Counts(IEnumerable<LabCount> rows, string title) =>
            new[] { new object?[] { title, "Booked", "Completed", "Pending", "Cancelled" } }.Concat(rows.Select(r => new object?[] { r.Name, r.Booked, r.Completed, r.Pending, r.Cancelled })).ToArray();
        object?[][] List(IEnumerable<LabListRow> rows) =>
            new[] { new object?[] { "Date", "Visit", "Patient ID", "Patient", "Test", "Reason", "By" } }.Concat(rows.Select(r => new object?[] { r.Date, r.OrderNumber, r.PatientNumber, r.PatientName, r.Test, r.Detail, r.By })).ToArray();
        if (format == "csv")
        {
            var sb = new StringBuilder();
            foreach (var row in activity) sb.AppendLine(string.Join(",", row.Select(Csv)));
            sb.AppendLine();
            foreach (var row in Counts(d.ByTest, "Test")) sb.AppendLine(string.Join(",", row.Select(Csv)));
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", name + ".csv");
        }
        var x = new XlsxWriter()
            .Sheet("Activity", activity)
            .Sheet("By test", Counts(d.ByTest, "Test"))
            .Sheet("By category", Counts(d.ByCategory, "Category"))
            .Sheet("By doctor", Counts(d.ByDoctor, "Referring doctor"))
            .Sheet("Cancelled tests", List(d.Cancelled))
            .Sheet("Rejected samples", List(d.SampleRejections))
            .Sheet("Verification", new[] { new object?[] { "User", "Verified", "Sent back", "Withdrawn" } }.Concat(d.Verification.Select(v => new object?[] { v.User, v.Verified, v.SentBack, v.Withdrawn })));
        if (d.Collection is { } col)
            x.Sheet("Collection", new[] { new object?[] { "Date", "Receipts", "Collected", "Refunded", "Net" } }.Concat(col.Select(c => new object?[] { c.Date, c.Receipts, c.Amount, c.Refunded, c.Amount - c.Refunded })));
        return File(x.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
    }

    public string Label(DateOnly date) => Group switch { "year" => date.Year.ToString(), "month" => date.ToString("MMM yyyy"), _ => date.ToString("dd MMM yyyy") };

    private static string Csv(object? v)
    {
        var s = v switch { null => "", DateOnly d => d.ToString("yyyy-MM-dd"), IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };
        if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
