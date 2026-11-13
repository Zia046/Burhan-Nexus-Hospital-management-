using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Printing;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>
/// Receipt preview and printing (58 / 80 mm thermal, A4, PDF via the browser). Reprints keep the receipt number.
/// First print needs "Issue Receipts", later copies "Reprint Receipts"; only the cashier's own receipts unless "View All".
/// </summary>
public class ReceiptModel : HmsPageModel
{
    private readonly IBillingService _billing;
    private readonly IPrinterProfileService _printers;
    private readonly INetworkPrinter _network;
    private readonly ReceiptSlipFactory _slips;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;

    public ReceiptModel(IBillingService billing, IPrinterProfileService printers, INetworkPrinter network, ReceiptSlipFactory slips,
        IAuditService audit, IRequestContext ctx)
    {
        _billing = billing;
        _printers = printers;
        _network = network;
        _slips = slips;
        _audit = audit;
        _ctx = ctx;
    }

    public Receipt Receipt { get; private set; } = null!;
    public ReceiptSlipData Slip { get; private set; } = null!;
    public PrinterProfile Printer { get; private set; } = null!;
    public int PrintCount { get; private set; }
    public bool CanPrintNow { get; private set; }
    public bool AutoPrint { get; private set; }
    public bool IsA4 { get; private set; }

    private async Task<IActionResult?> LoadAsync(long id, int? width, string? format)
    {
        var r = await _billing.GetReceiptAsync(id);
        if (r is null) return NotFound();
        if (r.CashierUserId != CurrentUserId && !await CanAsync(Perm.FinanceViewAllAccounts)) return Forbid();
        var canIssue = await CanAsync(Perm.FinanceIssueReceipts);
        var canReprint = await CanAsync(Perm.FinanceReprint);
        if (!canIssue && !canReprint) return Forbid();
        Receipt = r;
        PrintCount = r.Prints.Count;
        CanPrintNow = PrintCount == 0 ? canIssue : canReprint;
        Printer = await _printers.ForWorkstationAsync(_ctx.Workstation ?? "");
        if (width is 58 or 80) Printer.PaperWidthMm = width.Value;
        IsA4 = format == "a4";
        Slip = await _slips.BuildAsync(r, PrintCount > 0);
        return null;
    }

    public async Task<IActionResult> OnGetAsync(long id, int? width, string? format, bool autoprint)
    {
        if (await LoadAsync(id, width, format) is { } res) return res;
        AutoPrint = autoprint && CanPrintNow && !IsA4;
        return Page();
    }

    private async Task<IActionResult?> GuardAsync(long id)
    {
        var r = await _billing.GetReceiptAsync(id);
        if (r is null) return NotFound();
        if (r.CashierUserId != CurrentUserId && !await CanAsync(Perm.FinanceViewAllAccounts))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "This is another cashier's receipt." });
        var count = r.Prints.Count;
        if (!await CanAsync(count == 0 ? Perm.FinanceIssueReceipts : Perm.FinanceReprint))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = count == 0 ? "No permission to print receipts." : "No permission to reprint receipts." });
        return null;
    }

    public async Task<IActionResult> OnPostRecordAsync(long id, string? width, string? method)
    {
        if (await GuardAsync(id) is { } deny) return deny;
        var format = width is "58" or "80" ? width + "mm" : width == "a4" ? "A4" : "80mm";
        var r = await _billing.RecordPrintAsync(id, format, method is "agent" ? "Print Agent" : "browser");
        return r.Succeeded ? new JsonResult(new { ok = true, copy = r.Value }) : BadRequest(new { error = r.ErrorText });
    }

    public async Task<IActionResult> OnPostEscPosAsync(long id)
    {
        if (await GuardAsync(id) is { } deny) return deny;
        await LoadAsync(id, null, null);
        var bytes = ReceiptComposer.Receipt(Slip, Printer);
        return new JsonResult(new
        {
            data = Convert.ToBase64String(bytes), printer = Printer.WindowsPrinterName, host = Printer.LanHost, port = Printer.LanPort,
            agent = Printer.AgentUrl, job = $"Receipt {Receipt.ReceiptNumber}"
        });
    }

    public async Task<IActionResult> OnPostLanAsync(long id)
    {
        if (await GuardAsync(id) is { } deny) return deny;
        await LoadAsync(id, null, null);
        if (Printer.Mode != PrintMode.LanDirect || string.IsNullOrWhiteSpace(Printer.LanHost))
            return BadRequest(new { error = "No LAN printer is configured for this computer." });
        var sent = await _network.SendAsync(Printer.LanHost, Printer.LanPort, ReceiptComposer.Receipt(Slip, Printer));
        if (!sent.Succeeded)
        {
            await _audit.LogAsync(AuditActions.AppointmentPrintFailed, "Receipt", Receipt.ReceiptNumber,
                $"LAN print of receipt {Receipt.ReceiptNumber} to {Printer.LanHost}:{Printer.LanPort} failed", success: false);
            return BadRequest(new { error = sent.ErrorText });
        }
        var r = await _billing.RecordPrintAsync(id, $"{Printer.PaperWidthMm}mm", $"LAN printer {Printer.LanHost}:{Printer.LanPort}");
        return new JsonResult(new { ok = true, copy = r.Value });
    }
}
