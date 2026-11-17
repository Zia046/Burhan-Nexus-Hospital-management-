using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>Update 12: A4 print of a complete supplier invoice / stock receiving record (all rows). Reads stored records only.</summary>
public class SupplierInvoiceModel : Pages.Pharmacy.DocModel
{
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;
    public SupplierInvoiceModel(HmsDbContext db, ISettingsService settings, IAuditService audit) : base(db) { _settings = settings; _audit = audit; }

    public HospitalProfile Hospital { get; private set; } = null!;

    public async Task<IActionResult> OnGetPrintAsync(long id)
    {
        var r = await OnGetAsync("GRN", id);
        if (r is not PageResult) return r;
        if (Receipt is null) return NotFound();
        Hospital = (await _settings.GetProfileAsync())!;
        await _audit.LogAsync(AuditActions.PharmSlipPrinted, "Pharmacy", Number, $"Supplier invoice {Receipt.ReferenceNumber} ({Number}) opened for printing");
        return Page();
    }
}
