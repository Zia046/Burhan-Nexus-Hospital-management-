using System.Text;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>
/// Radiology reports with date filters, print (PDF via the browser) and Excel / CSV export.
/// Revenue comes only from Phase 3 receipts and needs "View Financial Information".
/// </summary>
[HasPermission(Perm.RadViewStatistics)]
public class ReportsModel : RadiologyPageModel
{
    private readonly IRadiologyReportService _reports;
    private readonly HospitalClock _clock;
    private readonly IAuditService _audit;

    public ReportsModel(IRadiologyReportService reports, HospitalClock clock, IAuditService audit)
    {
        _reports = reports;
        _clock = clock;
        _audit = audit;
    }

    public static readonly (string Key, string Title, bool Financial)[] Catalog_ =
    {
        ("summary", "Daily / monthly examination statistics", false),
        ("xray", "Daily X-Ray examinations", false),
        ("ultrasound", "Daily Ultrasound examinations", false),
        ("ecg", "Daily ECG examinations", false),
        ("services", "Service-wise examination count", false),
        ("departments", "Department-wise referrals", false),
        ("doctors", "Doctor-wise referrals", false),
        ("pending-exams", "Pending examinations", false),
        ("pending-reports", "Pending reports", false),
        ("verified", "Verified reports", false),
        ("cancelled", "Cancelled bookings", false),
        ("revenue", "Radiology revenue (Phase 3 receipts)", true),
    };

    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Group { get; set; }
    [BindProperty(SupportsGet = true)] public string? Report { get; set; }
    [BindProperty(SupportsGet = true)] public string? Modality { get; set; }
    public string PresetKey { get; private set; } = "month";
    public bool Financial { get; private set; }
    public string Title { get; private set; } = "";
    public RadiologyStatistics Data { get; private set; } = null!;
    public IReadOnlyList<RadiologyExamRow> Rows { get; private set; } = Array.Empty<RadiologyExamRow>();
    public RadiologyModality? ModalityFilter { get; private set; }

    private async Task<IActionResult?> LoadAsync()
    {
        Nav("rad-reports", "reports");
        Financial = await CanAsync(Perm.RadViewFinancial);
        Report = Catalog_.Any(c => c.Key == Report) ? Report : "summary";
        var def = Catalog_.First(c => c.Key == Report);
        if (def.Financial && !Financial) return Forbid();
        Title = def.Title;
        var (from, to, key) = FinancePeriod.Resolve(Period ?? (From is null && To is null ? (Report is "xray" or "ultrasound" ? "today" : "month") : null), From, To, await _clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        Group = Group is "month" or "year" ? Group : "day";
        // Staff of one modality see only their modality's figures.
        var only = await OnlyModalityAsync();
        ModalityFilter = Report switch
        {
            "xray" => RadiologyModality.XRay,
            "ultrasound" => RadiologyModality.Ultrasound,
            "ecg" => RadiologyModality.Ecg,
            _ => only ?? (Enum.TryParse<RadiologyModality>(Modality, true, out var m) ? m : null),
        };
        if (only is not null && ModalityFilter != only) return Forbid();
        Data = await _reports.StatisticsAsync(from, to, Group, ModalityFilter, Financial);
        var scope = Report switch
        {
            "xray" or "ultrasound" => "all",
            "pending-exams" => "queue",
            "pending-reports" => "reporting",
            "verified" => "final",
            "cancelled" => "cancelled",
            _ => null,
        };
        if (scope is not null)
            Rows = await Rad.ExamsAsync(new RadiologyWorklistQuery { Modality = ModalityFilter, Scope = scope, From = from, To = to, Take = 2000 });
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnGetExportAsync(string format)
    {
        if (await LoadAsync() is { } fail) return fail;
        var d = Data;
        var name = $"radiology-{Report}-{d.From:yyyyMMdd}-{d.To:yyyyMMdd}";
        await _audit.LogAsync(AuditActions.RadExported, "RadiologyReport", name, $"Radiology report “{Title}” exported ({format}, {d.From:yyyy-MM-dd}..{d.To:yyyy-MM-dd})");
        var rows = Table();
        if (format == "csv")
        {
            var sb = new StringBuilder();
            foreach (var row in rows) sb.AppendLine(string.Join(",", row.Select(Csv)));
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", name + ".csv");
        }
        var x = new XlsxWriter().Sheet(Title.Length > 30 ? Title[..30] : Title, rows);
        return File(x.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
    }

    /// <summary>The selected report as rows (header first) – used by the page and the exports.</summary>
    public object?[][] Table()
    {
        var d = Data;
        object?[][] Counts(IEnumerable<RadiologyCountRow> list, string title) =>
            new[] { new object?[] { title, "Modality", "Booked", "Reported", "Pending", "Cancelled" } }
                .Concat(list.Select(r => new object?[] { r.Name, r.Modality ?? "", r.Booked, r.Reported, r.Pending, r.Cancelled })).ToArray();
        return Report switch
        {
            "summary" => new[] { new object?[] { "Period", "X-Ray", "Ultrasound", "ECG", "Total", "Reported", "Cancelled" } }
                .Concat(d.Activity.Select(a => new object?[] { Label(a.Period), a.XRay, a.Ultrasound, a.Ecg, a.XRay + a.Ultrasound + a.Ecg, a.Completed, a.Cancelled })).ToArray(),
            "services" => Counts(d.ByService, "Service"),
            "departments" => Counts(d.ByDepartment, "Referring department"),
            "doctors" => Counts(d.ByDoctor, "Referring doctor"),
            "revenue" => new[] { new object?[] { "Period", "Receipts", "X-Ray", "Ultrasound", "ECG", "Total (net of refunds)", "Refunded" } }
                .Concat((d.Revenue ?? new List<RadiologyRevenueRow>()).Select(r => new object?[] { Label(r.Period), r.Receipts, r.XRay, r.Ultrasound, r.Ecg, r.Total, r.Refunded })).ToArray(),
            _ => new[] { new object?[] { "Date", "Token", "Order", "Patient ID", "Patient", "Examination", "Department", "Status", "Note" } }
                .Concat(Rows.Select(r => new object?[] { r.ScheduledDate, r.TokenNumber, r.OrderNumber, r.PatientNumber, r.PatientName, r.ServiceName, r.ReferringDepartment ?? "",
                    RadiologyWorkflowService.Label(r.Status), r.Status == RadiologyExamStatus.Cancelled ? r.CorrectionReason : r.VerifiedBy is null ? "" : "Verified by " + r.VerifiedBy })).ToArray(),
        };
    }

    public string Label(DateOnly date) => Group switch { "year" => date.Year.ToString(), "month" => date.ToString("MMM yyyy"), _ => date.ToString("dd MMM yyyy") };

    private static string Csv(object? v)
    {
        var s = v switch { null => "", DateOnly d => d.ToString("yyyy-MM-dd"), IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };
        if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
