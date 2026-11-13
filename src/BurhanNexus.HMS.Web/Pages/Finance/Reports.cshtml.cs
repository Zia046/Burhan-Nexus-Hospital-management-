using System.Globalization;
using System.Text;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Detailed financial transactions with filters; Excel / CSV export and print (PDF through the browser).</summary>
[HasPermission(Perm.FinanceDashboard)]
public class ReportsModel : FinanceFilterModel
{
    private readonly IFinanceReportService _reports;
    private readonly IAuditService _audit;

    public ReportsModel(IFinanceReportService reports, IAuditService audit)
    {
        _reports = reports;
        _audit = audit;
    }

    public IReadOnlyList<TransactionRow> Rows { get; private set; } = Array.Empty<TransactionRow>();
    public FinanceDashboard Summary { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "finance-reports";
        ViewData["FinanceTab"] = "reports";
        await LoadFilterAsync();
        Rows = await _reports.TransactionsAsync(Filter, 3000);
        Summary = await _reports.DashboardAsync(Filter);
    }

    public async Task<IActionResult> OnGetExportAsync(string format)
    {
        if (await DenyUnlessAsync(Perm.FinanceExport) is { } deny) return deny;
        await LoadFilterAsync();
        var rows = await _reports.TransactionsAsync(Filter, 50_000);
        var summary = await _reports.DashboardAsync(Filter);
        var name = $"financial-report-{Filter.From:yyyyMMdd}-{Filter.To:yyyyMMdd}";
        await _audit.LogAsync(AuditActions.FinanceExported, "FinanceReport", name, $"Financial report exported ({format}, {rows.Count} lines, {Filter.From:yyyy-MM-dd}..{Filter.To:yyyy-MM-dd})");
        var header = new object?[] { "Receipt", "Date", "Time (local)", "Status", "Patient ID", "Patient / payer", "Service", "Department", "Doctor", "Counter", "Cashier", "Method", "Reference", "Fee", "Discount", "Net", "Paid", "Balance", "Refunded", "Doctor share", "Cancel reason" };
        IEnumerable<object?[]> Data() => rows.Select(r => new object?[]
        {
            r.ReceiptNumber, r.BusinessDate, Fmt(r.CreatedUtc), r.Status, r.PatientNumber, r.PatientName, r.Service, r.Department, r.Doctor, r.Counter, r.Cashier,
            r.Method, r.Reference, r.Fee, r.Discount, r.Net, r.Paid, r.Balance, r.Refunded, r.DoctorShare, r.CancelReason
        });
        if (format == "csv")
        {
            var sb = new StringBuilder();
            foreach (var row in new[] { header }.Concat(Data()))
                sb.AppendLine(string.Join(",", row.Select(Csv)));
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", name + ".csv");
        }
        var x = new XlsxWriter()
            .Sheet("Transactions", new[] { header }.Concat(Data()))
            .Sheet("Summary", new List<object?[]>
            {
                new object?[] { "Item", "Value" },
                new object?[] { "Period", $"{Filter.From:yyyy-MM-dd} – {Filter.To:yyyy-MM-dd}" },
                new object?[] { "Total collection", summary.Collected }, new object?[] { "Refunds", summary.Refunds },
                new object?[] { "Net collection", summary.NetCollection }, new object?[] { "Valid receipts", summary.ValidReceipts },
                new object?[] { "Cancelled receipts", summary.CancelledReceipts }, new object?[] { "Cancelled amount", summary.CancelledAmount },
                new object?[] { "Confirmed deposits", summary.DepositsConfirmed }, new object?[] { "Outstanding with cashiers (now)", summary.CashOutstanding },
                new object?[] { "Doctor consultation collections", summary.DoctorConsultationCollections }, new object?[] { "Pending doctor payments (now)", summary.DoctorsPayableNow },
            }.Concat(summary.ByDepartment.Select(g => new object?[] { "Department: " + g.Name, g.Amount }))
             .Concat(summary.ByCounter.Select(g => new object?[] { "Counter: " + g.Name, g.Amount }))
             .Concat(summary.ByUser.Select(g => new object?[] { "Cashier: " + g.Name, g.Amount }))
             .Concat(summary.ByDoctor.Select(g => new object?[] { "Doctor: " + g.Name, g.Amount }))
             .Concat(summary.ByMethod.Select(g => new object?[] { "Method: " + g.Name, g.Amount })));
        return File(x.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
    }

    private string Fmt(DateTime utc) => HttpContext.RequestServices.GetRequiredService<Services.DisplayFormatter>().DateTimeText(utc);

    private static string Csv(object? v)
    {
        var s = v switch { null => "", decimal d => d.ToString("0.00", CultureInfo.InvariantCulture), DateOnly d => d.ToString("yyyy-MM-dd"), _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" };
        if (s.Length > 0 && "=+-@".Contains(s[0]) && v is not decimal) s = "'" + s;
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
