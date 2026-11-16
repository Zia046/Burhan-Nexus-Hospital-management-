using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>
/// Update 10: laboratory receipt – exactly two copies (patient copy and laboratory copy) with the same receipt and visit numbers.
/// Printing or reprinting reads the existing records only: no payment, receipt or number is created.
/// </summary>
[HasPermission(Perm.LabPrintReceipts)]
public class LabReceiptModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly ISettingsService _settings;
    private readonly HmsDbContext _db;
    public LabReceiptModel(ILabService lab, ISettingsService settings, HmsDbContext db) { _lab = lab; _settings = settings; _db = db; }

    [BindProperty(SupportsGet = true)] public int? Width { get; set; }
    public LabOrder Order { get; private set; } = null!;
    public HospitalProfile Hospital { get; private set; } = null!;
    public LabPayment Payment { get; private set; } = null!;
    public IReadOnlyList<Charge> Charges { get; private set; } = Array.Empty<Charge>();
    public IReadOnlyList<Receipt> Receipts { get; private set; } = Array.Empty<Receipt>();

    public static readonly string[] Copies = { "PATIENT COPY", "LABORATORY COPY" };

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var o = await _lab.OrderAsync(id);
        if (o is null) return NotFound();
        Order = o;
        Hospital = (await _settings.GetProfileAsync())!;
        Payment = await _lab.OrderPaymentAsync(id);
        Charges = await _db.Charges.AsNoTracking().Where(c => c.LabOrderId == id && c.Status != ChargeStatus.Cancelled).OrderBy(c => c.Id).ToListAsync();
        var chargeIds = Charges.Select(c => c.Id).ToList();
        Receipts = await _db.Receipts.AsNoTracking().Include(r => r.PaymentMethod)
            .Where(r => r.Status == ReceiptStatus.Valid && _db.ReceiptLines.Any(l => l.ReceiptId == r.Id && chargeIds.Contains(l.ChargeId)))
            .OrderBy(r => r.Id).ToListAsync();
        Width = Width is 58 or 80 ? Width : 80;
        return Page();
    }

    public string StatusText => Payment.State switch
    {
        LabPaymentState.Paid => "PAID", LabPaymentState.PartiallyPaid => "PARTIAL", LabPaymentState.Free => "FREE", LabPaymentState.Unpaid => "UNPAID", _ => Payment.State.ToString().ToUpperInvariant(),
    };
}
