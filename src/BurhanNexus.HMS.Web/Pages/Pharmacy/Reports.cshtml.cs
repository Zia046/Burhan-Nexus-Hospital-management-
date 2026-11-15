using System.Globalization;
using System.Text;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Pharmacy reports with filters, Excel / CSV export and print (PDF via the browser). Financial reports: Private mode only.</summary>
[HasPermission(Perm.PharmViewStockReports, Perm.PharmViewExpiryReports, Perm.PharmViewLowStockReports, Perm.PharmViewPatientHistory,
    Perm.PharmViewDepartmentStock, Perm.PharmViewFinancialReports)]
public class ReportsModel : PharmacyPageModel
{
    private readonly HospitalClock _clock;
    private readonly IAuditService _audit;
    public ReportsModel(HospitalClock clock, IAuditService audit) { _clock = clock; _audit = audit; }

    [BindProperty(SupportsGet = true, Name = "report")] public string? Key { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? LocationId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MedicineId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SupplierId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Batch { get; set; }
    public string PresetKey { get; private set; } = "month";
    public IReadOnlyList<ReportDefinition> Available { get; private set; } = Array.Empty<ReportDefinition>();
    public PharmacyReport? Report { get; private set; }
    public IReadOnlyList<StockLocation> Locations { get; private set; } = Array.Empty<StockLocation>();
    public IReadOnlyList<PharmacyLookup> Categories { get; private set; } = Array.Empty<PharmacyLookup>();
    public IReadOnlyList<Supplier> Suppliers { get; private set; } = Array.Empty<Supplier>();
    public IReadOnlyList<Medicine> Medicines { get; private set; } = Array.Empty<Medicine>();

    private async Task<IActionResult?> LoadAsync()
    {
        await InitAsync("reports");
        var list = new List<ReportDefinition>();
        foreach (var r in PharmacyQueryService.Reports)
            if ((!r.Financial || IsPrivate) && await CanAsync(r.Permission)) list.Add(r);
        Available = list;
        if (Available.Count == 0) return Forbid();
        var def = Available.FirstOrDefault(r => r.Key == Key);
        if (Key is not null && def is null)
            return PharmacyQueryService.Reports.Any(r => r.Key == Key) ? Forbid() : NotFound();
        def ??= Available[0];
        Key = def.Key;
        var (from, to, key) = FinancePeriod.Resolve(Period ?? (From is null && To is null ? "month" : null), From, To, await _clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        Locations = await Catalog.LocationsAsync(activeOnly: false);
        Categories = await Catalog.LookupsAsync(PharmacyLookupKind.Category);
        Suppliers = await Catalog.SuppliersAsync(null, activeOnly: false);
        Medicines = await Catalog.MedicinesAsync(null, activeOnly: false, take: 2000);
        Report = await Query.ReportAsync(def.Key, new PharmacyReportFilter
        {
            From = from, To = to, LocationId = LocationId, MedicineId = MedicineId, CategoryId = CategoryId, SupplierId = SupplierId, Batch = Batch
        }, IsPrivate);
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnGetExportAsync(string format)
    {
        if (await LoadAsync() is { } fail) return fail;
        var r = Report!;
        var name = $"pharmacy-{r.Key}-{From:yyyyMMdd}-{To:yyyyMMdd}";
        await _audit.LogAsync(AuditActions.PharmExported, "PharmacyReport", r.Key, $"Pharmacy report “{r.Title}” exported ({format}, {From:yyyy-MM-dd}..{To:yyyy-MM-dd})");
        var rows = new List<object?[]> { r.Columns.Cast<object?>().ToArray() };
        rows.AddRange(r.Rows);
        if (r.Totals is not null) rows.Add(r.Totals);
        if (format == "csv")
        {
            var sb = new StringBuilder();
            foreach (var row in rows) sb.AppendLine(string.Join(",", row.Select(Csv)));
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv", name + ".csv");
        }
        var x = new XlsxWriter().Sheet(r.Title.Length > 31 ? r.Title[..31] : r.Title, rows);
        return File(x.ToBytes(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx");
    }

    public string Cell(object? v) => v switch
    {
        null => "",
        DateOnly d => Fmt.Day(d),
        decimal m => m.ToString("N2", CultureInfo.InvariantCulture),
        int i => i.ToString("N0", CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };

    private static string Csv(object? v)
    {
        var s = v switch { null => "", DateOnly d => d.ToString("yyyy-MM-dd"), IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) s = "'" + s;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
