using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>
/// Supplier invoice – the one stock receiving screen (Update 12). The supplier and invoice number are entered once; every medicine of
/// the invoice is a row of the same table (medicine search inside the row, quantity, expiry, batch, purchase / sale price loaded from the
/// medicine). "Save &amp; receive complete invoice" stores the header and all rows in ONE transaction (all or nothing, idempotent,
/// duplicate supplier invoice refused). A medicine missing from the catalogue can be created from the invoice without leaving it.
/// Government mode has no price columns.
/// </summary>
[HasPermission(Perm.PharmReceiveStock)]
public class ReceiveModel : PharmacyPageModel
{
    public const int MaxRows = 500;

    public sealed class Line
    {
        public int MedicineId { get; set; }
        public string? MedicineName { get; set; }
        public string? BatchNumber { get; set; }
        public string? Expiry { get; set; }
        public string? Manufactured { get; set; }
        public int? Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal? Discount { get; set; }
    }

    private readonly HmsDbContext _db;
    public ReceiveModel(HmsDbContext db) => _db = db;

    [BindProperty] public ReceiveInput Input { get; set; } = new();
    [BindProperty] public List<Line> Lines { get; set; } = new();
    /// <summary>Update 17: stock classification is on – new batches get a stock class.</summary>
    public bool ClassificationOn { get; private set; }
    public IReadOnlyList<Supplier> Suppliers { get; private set; } = Array.Empty<Supplier>();
    public IReadOnlyList<PharmacyLookup> DosageForms { get; private set; } = Array.Empty<PharmacyLookup>();
    public bool WithPrices { get; private set; }
    public bool CanSalePrice { get; private set; }
    public bool CanAddMedicine { get; private set; }

    private async Task LoadAsync()
    {
        await InitAsync("receive");
        ClassificationOn = (await Catalog.SettingsAsync()).StockClassificationEnabled;
        Suppliers = await Catalog.SuppliersAsync(null, activeOnly: true);
        WithPrices = IsPrivate && await CanAsync(Perm.PharmManagePurchases);
        CanSalePrice = WithPrices && await CanAsync(Perm.PharmManagePrices);
        CanAddMedicine = await CanAsync(Perm.PharmAddMedicine);
        if (CanAddMedicine) DosageForms = await Catalog.LookupsAsync(PharmacyLookupKind.DosageForm, activeOnly: true);
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input = new ReceiveInput { IdempotencyKey = Guid.NewGuid() };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        var errors = new List<string>();
        if (Input.SupplierId is not null && string.IsNullOrWhiteSpace(Input.ReferenceNumber)) errors.Add("Enter the supplier invoice number.");
        if (Input.SupplierId is null && string.IsNullOrWhiteSpace(Input.SourceName)) errors.Add("Select the supplier (or enter the stock source, e.g. a donation).");
        // A row without a medicine but with entries is a mistake – an untouched empty row is ignored.
        for (var i = 0; i < Lines.Count; i++)
        {
            var l = Lines[i];
            if (l.MedicineId <= 0 && (l.Quantity is > 0 || !string.IsNullOrWhiteSpace(l.Expiry) || !string.IsNullOrWhiteSpace(l.BatchNumber) || !string.IsNullOrWhiteSpace(l.MedicineName)))
                errors.Add($"Row {i + 1}: choose the medicine from the list (or remove the row).");
        }
        var rows = Lines.Where(l => l.MedicineId > 0).ToList();
        if (rows.Count > MaxRows) errors.Add($"At most {MaxRows} rows per invoice.");
        for (var i = 0; i < Lines.Count; i++)
            if (Lines[i].MedicineId > 0 && Lines[i].Quantity is null or <= 0) errors.Add($"Row {i + 1} ({Lines[i].MedicineName}): enter the quantity received.");
        if (errors.Count > 0)
        {
            foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
            return Page();
        }

        // The sale price column is filled from the medicine: an unchanged price is not a price change (no permission needed, no history).
        // A changed one is checked by the service ("Manage Medicine Prices"); prices posted in Government mode are refused there.
        var ids = rows.Select(r => r.MedicineId).Distinct().ToList();
        var current = await _db.Medicines.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.SalePrice);
        decimal? Sale(Line l) => l.SalePrice is null || current.TryGetValue(l.MedicineId, out var cur) && cur == l.SalePrice ? null : l.SalePrice;

        Input.Lines = rows.Select(l => new ReceiveLineInput
        {
            MedicineId = l.MedicineId, BatchNumber = l.BatchNumber, ExpiryDate = ParseExpiry(l.Expiry),
            ManufactureDate = ParseExpiry(l.Manufactured) is { } md ? new DateOnly(md.Year, md.Month, 1) : null,
            Quantity = l.Quantity ?? 0, UnitCost = l.UnitCost, SalePrice = Sale(l), Discount = l.Discount
        }).ToList();
        var r = await Pharmacy.ReceiveAsync(Input, await RightsAsync());
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate
            ? $"Invoice {r.Value.Number} was already saved – stock was not added twice."
            : $"Invoice received: {r.Value.Number} – {rows.Count} item(s), {rows.Sum(x => x.Quantity ?? 0)} unit(s) added to stock.";
        return Redirect($"/Pharmacy/Doc/GRN/{r.Value.Id}");
    }

    public sealed class NewMedicineForm
    {
        public string? Name { get; set; }
        public string? GenericName { get; set; }
        public string? Strength { get; set; }
        public int? DosageFormId { get; set; }
        public string? Unit { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? SalePrice { get; set; }
        public bool ConfirmDuplicate { get; set; }
    }

    /// <summary>Creates a medicine that is not in the catalogue yet, from inside the invoice (JSON, the invoice on screen is kept).</summary>
    public async Task<IActionResult> OnPostNewMedicineAsync([FromForm] NewMedicineForm m)
    {
        if (await DenyUnlessAsync(Perm.PharmAddMedicine) is { } deny) return deny;
        await LoadAsync();
        var canPrices = IsPrivate && await CanAsync(Perm.PharmManagePrices);
        var input = new MedicineInput
        {
            Name = m.Name, GenericName = m.GenericName, Strength = m.Strength, DosageFormId = m.DosageFormId, Unit = m.Unit, IsActive = true,
            PurchasePrice = IsPrivate ? m.PurchasePrice : null, SalePrice = canPrices ? m.SalePrice : null, ConfirmDuplicate = m.ConfirmDuplicate
        };
        try
        {
            var r = await Catalog.SaveMedicineAsync(null, input, canPrices);
            if (!r.Succeeded) return new JsonResult(new { ok = false, errors = r.Errors });
            var med = await _db.Medicines.AsNoTracking().FirstAsync(x => x.Id == r.Value);
            return new JsonResult(new { ok = true, id = med.Id, code = med.Code, name = med.DisplayName, cost = med.PurchasePrice, price = med.SalePrice });
        }
        catch (DuplicateMedicineException ex)
        {
            return new JsonResult(new { ok = false, duplicate = ex.Similar.Select(s => $"{s.DisplayName} ({s.Code}) – {s.Why}").ToList() });
        }
    }
}
