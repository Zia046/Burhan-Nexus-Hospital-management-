using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Stock receiving / purchase (GRN): supplier or source, then batch, expiry and quantity per medicine. Government mode has no price fields.</summary>
[HasPermission(Perm.PharmReceiveStock)]
public class ReceiveModel : PharmacyPageModel
{
    public sealed class Line
    {
        public int MedicineId { get; set; }
        public string? MedicineName { get; set; }
        public string? BatchNumber { get; set; }
        public string? Expiry { get; set; }
        public string? Manufactured { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal? Discount { get; set; }
    }

    [BindProperty] public ReceiveInput Input { get; set; } = new();
    [BindProperty] public List<Line> Lines { get; set; } = new();
    public IReadOnlyList<Supplier> Suppliers { get; private set; } = Array.Empty<Supplier>();
    public bool WithPrices { get; private set; }
    public bool CanSalePrice { get; private set; }

    private async Task LoadAsync()
    {
        await InitAsync("receive");
        Suppliers = await Catalog.SuppliersAsync(null, activeOnly: true);
        WithPrices = IsPrivate && await CanAsync(Perm.PharmManagePurchases);
        CanSalePrice = WithPrices && await CanAsync(Perm.PharmManagePrices);
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input = new ReceiveInput { IdempotencyKey = Guid.NewGuid() };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        Input.Lines = Lines.Where(l => l.MedicineId > 0).Select(l => new ReceiveLineInput
        {
            MedicineId = l.MedicineId, BatchNumber = l.BatchNumber, ExpiryDate = ParseExpiry(l.Expiry), ManufactureDate = ParseExpiry(l.Manufactured) is { } md ? new DateOnly(md.Year, md.Month, 1) : null,
            Quantity = l.Quantity, UnitCost = l.UnitCost, SalePrice = l.SalePrice, Discount = l.Discount
        }).ToList();
        var r = await Pharmacy.ReceiveAsync(Input, await RightsAsync());
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved – stock was not added twice." : $"Stock received: {r.Value.Number}.";
        return Redirect($"/Pharmacy/Doc/GRN/{r.Value.Id}");
    }
}
