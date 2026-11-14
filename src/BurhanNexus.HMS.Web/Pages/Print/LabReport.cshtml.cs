using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>
/// A4 laboratory report: preview, print, PDF (browser "Save as PDF"), reprint, selected tests.
/// Only verified results are ever included – unverified tests are listed as "pending" and never printed as final.
/// Printing first records the copy (verified → released); reprints need "Reprint Laboratory Reports".
/// </summary>
[HasPermission(Perm.LabPrintReports, Perm.LabReprintReports, Perm.LabExportPdf)]
public class LabReportModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly ILabCatalogService _catalog;
    private readonly ISettingsService _settings;

    public LabReportModel(ILabService lab, ILabCatalogService catalog, ISettingsService settings)
    {
        _lab = lab;
        _catalog = catalog;
        _settings = settings;
    }

    [BindProperty(SupportsGet = true)] public string? Items { get; set; }
    [BindProperty(SupportsGet = true)] public bool Print { get; set; }
    [BindProperty] public List<long> ItemIds { get; set; } = new();

    public LabOrder Order { get; private set; } = null!;
    public LabSettings Lab { get; private set; } = null!;
    public HospitalProfile Hospital { get; private set; } = null!;
    public IReadOnlyList<LabOrderItem> Final { get; private set; } = Array.Empty<LabOrderItem>();
    public IReadOnlyList<LabOrderItem> Selected { get; private set; } = Array.Empty<LabOrderItem>();
    public IReadOnlyList<LabOrderItem> NotReady { get; private set; } = Array.Empty<LabOrderItem>();
    public IReadOnlyList<LabReportPrint> Prints { get; private set; } = Array.Empty<LabReportPrint>();
    public bool IsReprint => Selected.Any(i => i.Status == LabOrderItemStatus.Released);
    public bool CanPrintNow { get; private set; }
    public string? Blocked { get; private set; }

    public static bool IsFinal(LabOrderItem i) => i.Status is LabOrderItemStatus.Verified or LabOrderItemStatus.Released;

    private static List<long> Parse(string? s) =>
        (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => long.TryParse(x, out var v) ? v : 0).Where(v => v > 0).ToList();

    private async Task<bool> LoadAsync(long id, IReadOnlyList<long> wanted)
    {
        var o = await _lab.OrderAsync(id);
        if (o is null) return false;
        Order = o;
        Lab = await _catalog.SettingsAsync();
        Hospital = (await _settings.GetProfileAsync())!;
        Prints = await _lab.PrintsAsync(id);
        Final = o.Items.Where(IsFinal).OrderBy(i => i.SortOrder).ToList();
        Selected = wanted.Count == 0 ? Final : Final.Where(i => wanted.Contains(i.Id)).ToList();
        NotReady = o.Items.Where(i => !IsFinal(i) && i.Status != LabOrderItemStatus.Cancelled).ToList();
        CanPrintNow = Selected.Count > 0 && await CanAsync(IsReprint ? Perm.LabReprintReports : Perm.LabPrintReports);
        if (Selected.Count > 0 && Lab.RequirePaymentBeforeRelease)
        {
            var pay = await _lab.PaymentsAsync(id);
            if (Selected.Any(i => pay.TryGetValue(i.Id, out var p) && p.State is LabPaymentState.Unpaid or LabPaymentState.PartiallyPaid))
                Blocked = "Payment is outstanding – the laboratory settings require full payment before the report is released.";
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id, Parse(Items)) ? Page() : NotFound();

    /// <summary>Records the printed copy (and releases verified results), then reloads the page to open the print dialog.</summary>
    public async Task<IActionResult> OnPostRecordAsync(long id)
    {
        if (!await LoadAsync(id, ItemIds)) return NotFound();
        if (Selected.Count == 0) { FlashError = "Only verified results can be printed as a final report."; return RedirectToPage(new { id }); }
        if (await DenyUnlessAsync(IsReprint ? Perm.LabReprintReports : Perm.LabPrintReports) is { } deny) return deny;
        var r = await _lab.RecordPrintAsync(id, Selected.Select(i => i.Id).ToList(), "A4");
        if (!r.Succeeded) { FlashError = r.ErrorText; return RedirectToPage(new { id, items = string.Join(",", Selected.Select(i => i.Id)) }); }
        return RedirectToPage(new { id, items = string.Join(",", Selected.Select(i => i.Id)), print = true });
    }
}
