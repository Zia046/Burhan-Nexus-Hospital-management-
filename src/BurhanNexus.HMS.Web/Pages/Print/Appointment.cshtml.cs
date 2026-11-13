using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Printing;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>
/// Thermal appointment slip: preview + print. The token printed is always the token stored in the database.
/// First copy needs "Print Appointment Slip", further copies need "Reprint Appointment Slip" (checked on the server).
/// </summary>
public class AppointmentModel : HmsPageModel
{
    private readonly IAppointmentService _appointments;
    private readonly IPrinterProfileService _printers;
    private readonly INetworkPrinter _network;
    private readonly AppointmentSlipFactory _slips;
    private readonly IAuditService _audit;
    private readonly Core.Abstractions.IRequestContext _ctx;

    public AppointmentModel(IAppointmentService appointments, IPrinterProfileService printers, INetworkPrinter network,
        AppointmentSlipFactory slips, IAuditService audit, Core.Abstractions.IRequestContext ctx)
    {
        _appointments = appointments;
        _printers = printers;
        _network = network;
        _slips = slips;
        _audit = audit;
        _ctx = ctx;
    }

    public Appointment Appointment { get; private set; } = null!;
    public AppointmentSlipData Slip { get; private set; } = null!;
    public PrinterProfile Printer { get; private set; } = null!;
    public int PrintCount { get; private set; }
    public bool CanPrintNow { get; private set; }
    public bool AutoPrint { get; private set; }
    public string Workstation { get; private set; } = "";

    private async Task<IActionResult?> LoadAsync(long id, int? width)
    {
        var a = await _appointments.GetAsync(id);
        if (a is null) return NotFound();
        Appointment = a;
        PrintCount = await _appointments.PrintCountAsync(id);
        var canPrint = await CanAsync(Perm.AppointmentsPrint);
        var canReprint = await CanAsync(Perm.AppointmentsReprint);
        if (!canPrint && !canReprint) return Forbid();
        CanPrintNow = a.Status != AppointmentStatus.Cancelled && (PrintCount == 0 ? canPrint : canReprint);
        Workstation = _ctx.Workstation ?? "";
        Printer = await _printers.ForWorkstationAsync(Workstation);
        if (width is 58 or 80) Printer.PaperWidthMm = width.Value;
        Slip = await _slips.BuildAsync(a, PrintCount > 0);
        return null;
    }

    public async Task<IActionResult> OnGetAsync(long id, int? width, bool autoprint)
    {
        if (await LoadAsync(id, width) is { } r) return r;
        AutoPrint = autoprint && CanPrintNow;
        return Page();
    }

    private async Task<IActionResult?> GuardPrintAsync(long id)
    {
        var count = await _appointments.PrintCountAsync(id);
        var a = await _appointments.GetAsync(id);
        if (a is null) return NotFound();
        if (a.Status == AppointmentStatus.Cancelled) return BadRequest(new { error = "Cancelled appointments cannot be printed." });
        if (!await CanAsync(count == 0 ? Perm.AppointmentsPrint : Perm.AppointmentsReprint))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = count == 0 ? "No permission to print appointment slips." : "No permission to reprint appointment slips." });
        return null;
    }

    /// <summary>Records a browser or Print-Agent print (called just before/after printing).</summary>
    public async Task<IActionResult> OnPostRecordAsync(long id, int width, string? method)
    {
        if (await GuardPrintAsync(id) is { } deny) return deny;
        var m = method is "agent" ? "Print Agent" : "browser";
        var r = await _appointments.RecordPrintAsync(id, $"{(width is 58 ? 58 : 80)}mm", m);
        return r.Succeeded ? new JsonResult(new { ok = true, copy = r.Value }) : BadRequest(new { error = r.ErrorText });
    }

    /// <summary>ESC/POS bytes for the Print Agent (USB / Windows printer on this computer).</summary>
    public async Task<IActionResult> OnPostEscPosAsync(long id)
    {
        if (await GuardPrintAsync(id) is { } deny) return deny;
        await LoadAsync(id, null);
        var bytes = ThermalSlipComposer.Appointment(Slip, Printer);
        return new JsonResult(new
        {
            data = Convert.ToBase64String(bytes), printer = Printer.WindowsPrinterName, host = Printer.LanHost, port = Printer.LanPort,
            agent = Printer.AgentUrl, job = $"Token {Appointment.TokenNumber} - {Appointment.Encounter!.EncounterNumber}"
        });
    }

    /// <summary>Server prints directly to the workstation's LAN thermal printer (silent printing).</summary>
    public async Task<IActionResult> OnPostLanAsync(long id)
    {
        if (await GuardPrintAsync(id) is { } deny) return deny;
        await LoadAsync(id, null);
        if (Printer.Mode != PrintMode.LanDirect || string.IsNullOrWhiteSpace(Printer.LanHost))
            return BadRequest(new { error = "No LAN printer is configured for this computer." });
        var bytes = ThermalSlipComposer.Appointment(Slip, Printer);
        var sent = await _network.SendAsync(Printer.LanHost, Printer.LanPort, bytes);
        if (!sent.Succeeded)
        {
            await _audit.LogAsync(AuditActions.AppointmentPrintFailed, "Appointment", Appointment.Encounter!.EncounterNumber,
                $"LAN print of {Appointment.Encounter.EncounterNumber} to {Printer.LanHost}:{Printer.LanPort} failed", success: false);
            return BadRequest(new { error = sent.ErrorText });
        }
        var r = await _appointments.RecordPrintAsync(id, $"{Printer.PaperWidthMm}mm", $"LAN printer {Printer.LanHost}:{Printer.LanPort}");
        return new JsonResult(new { ok = true, copy = r.Value });
    }
}
