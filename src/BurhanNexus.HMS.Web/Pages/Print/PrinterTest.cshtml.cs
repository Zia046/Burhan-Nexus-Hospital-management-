using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Printing;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>Test print for a printer profile – same three routes as real slips (browser, LAN, Print Agent).</summary>
[HasPermission(Perm.PrinterSettings)]
public class PrinterTestModel : HmsPageModel
{
    private readonly IPrinterProfileService _printers;
    private readonly INetworkPrinter _network;
    private readonly IAuditService _audit;
    private readonly Services.DisplayFormatter _fmt;

    public PrinterTestModel(IPrinterProfileService printers, INetworkPrinter network, IAuditService audit, Services.DisplayFormatter fmt)
    {
        _printers = printers;
        _network = network;
        _audit = audit;
        _fmt = fmt;
    }

    [BindProperty(SupportsGet = true)] public int? Profile { get; set; }
    public PrinterProfile Printer { get; private set; } = null!;
    public AppointmentSlipData Slip { get; private set; } = null!;

    private async Task<bool> LoadAsync()
    {
        var all = await _printers.ListAsync();
        var p = Profile is int id ? all.FirstOrDefault(x => x.Id == id) : null;
        if (Profile is not null && p is null) return false;
        Printer = p ?? new PrinterProfile();
        var h = (await _fmt.ProfileAsync())!;
        Slip = new AppointmentSlipData(h.HospitalName, "Ph: " + h.ContactNumber, "Dr. Test Doctor", "MBBS", "PRINTER TEST", 7,
            "Test Patient", "TEST-000001", "30Y / M", _fmt.Day(DateOnly.FromDateTime(DateTime.UtcNow)), null, null, null,
            "Printer test – " + (Printer.WorkstationName == "*" ? "default profile" : Printer.WorkstationName), "TEST",
            "Printed " + _fmt.DateTimeText(DateTime.UtcNow), false, false);
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostRecordAsync(string? method)
    {
        if (!await LoadAsync()) return NotFound();
        await _audit.LogAsync(AuditActions.PrinterTestPrint, "PrinterProfile", Printer.WorkstationName,
            $"Test print ({Printer.PaperWidthMm}mm, {(method == "agent" ? "Print Agent" : "browser")}) for '{Printer.WorkstationName}'");
        return new JsonResult(new { ok = true, copy = 1 });
    }

    public async Task<IActionResult> OnPostEscPosAsync()
    {
        if (!await LoadAsync()) return NotFound();
        var bytes = ThermalSlipComposer.Appointment(Slip, Printer);
        return new JsonResult(new { data = Convert.ToBase64String(bytes), printer = Printer.WindowsPrinterName, host = Printer.LanHost, port = Printer.LanPort, agent = Printer.AgentUrl, job = "Burhan Nexus printer test" });
    }

    public async Task<IActionResult> OnPostLanAsync()
    {
        if (!await LoadAsync()) return NotFound();
        if (string.IsNullOrWhiteSpace(Printer.LanHost)) return BadRequest(new { error = "No LAN printer IP in this profile." });
        var sent = await _network.SendAsync(Printer.LanHost, Printer.LanPort, ThermalSlipComposer.Appointment(Slip, Printer));
        await _audit.LogAsync(AuditActions.PrinterTestPrint, "PrinterProfile", Printer.WorkstationName,
            $"LAN test print to {Printer.LanHost}:{Printer.LanPort} – {(sent.Succeeded ? "sent" : "failed")}", success: sent.Succeeded);
        return sent.Succeeded ? new JsonResult(new { ok = true, copy = 1 }) : BadRequest(new { error = sent.ErrorText });
    }

}
