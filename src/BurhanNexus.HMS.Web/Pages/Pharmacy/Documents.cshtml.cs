using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Pharmacy documents: receivings, patient issues, sales, transfers, adjustments and returns (search all dates by number / name).</summary>
[HasPermission(Perm.PharmViewCatalogue, Perm.PharmViewStockReports, Perm.PharmIssueToPatients, Perm.PharmRetailSales, Perm.PharmTransferStock, Perm.PharmProcessReturns)]
public class DocumentsModel : PharmacyPageModel
{
    private readonly HospitalClock _clock;
    public DocumentsModel(HospitalClock clock) => _clock = clock;

    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    public string PresetKey { get; private set; } = "today";
    public IReadOnlyList<DocumentRow> Rows { get; private set; } = Array.Empty<DocumentRow>();
    public IReadOnlyList<(string Key, string Name)> Types { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await InitAsync("documents");
        var types = new List<(string, string)> { ("issues", "Patient issues"), ("receipts", "Stock receiving"), ("transfers", "Transfers"), ("adjustments", "Adjustments"), ("returns", "Returns"), ("supplier-returns", "Supplier returns") };
        if (IsPrivate) types.Insert(1, ("sales", "Retail sales"));
        Types = types;
        Type = Types.Any(t => t.Key == Type) ? Type : "issues";
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, await _clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        Rows = await Query.DocumentsAsync(Type!, from, to, Q, ShowPrices);
    }
}
