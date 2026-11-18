using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Supplier invoice history (Update 12): search by invoice number, supplier and date; open the complete invoice (all items) and print it.
/// Finalised invoices are read-only – corrections go through an authorised stock adjustment with a reason.</summary>
[HasPermission(Perm.PharmReceiveStock, Perm.PharmViewStockReports, Perm.PharmManagePurchases, Perm.PharmViewCatalogue)]
public class InvoicesModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    public InvoicesModel(HmsDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? SupplierId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }

    public sealed record Row(long Id, string Number, string? Invoice, DateOnly? InvoiceDate, DateOnly ReceivedDate, string Supplier, int Items, int Quantity, decimal? Net, string? By);
    public IReadOnlyList<Row> Rows { get; private set; } = Array.Empty<Row>();
    public IReadOnlyList<Supplier> Suppliers { get; private set; } = Array.Empty<Supplier>();

    public async Task OnGetAsync()
    {
        await InitAsync("invoices");
        Suppliers = await Catalog.SuppliersAsync(null, activeOnly: false);
        var q = _db.StockReceipts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var t = Q.Trim().ToUpper();
            q = q.Where(r => (r.ReferenceNumber != null && r.ReferenceNumber.ToUpper().Contains(t)) || r.Number.ToUpper().Contains(t));
        }
        if (SupplierId is int s) q = q.Where(r => r.SupplierId == s);
        if (From is DateOnly f) q = q.Where(r => (r.InvoiceDate ?? r.ReceivedDate) >= f);
        if (To is DateOnly to) q = q.Where(r => (r.InvoiceDate ?? r.ReceivedDate) <= to);
        Rows = await q.OrderByDescending(r => r.Id).Take(300).Select(r => new Row(r.Id, r.Number, r.ReferenceNumber, r.InvoiceDate, r.ReceivedDate,
                r.Supplier != null ? r.Supplier.Name : (r.SourceName ?? "—"), r.Items.Count, r.Items.Sum(i => i.Quantity), r.NetAmount, r.CreatedByUsername))
            .ToListAsync();
    }
}
