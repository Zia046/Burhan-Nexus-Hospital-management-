using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Printing;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

/// <summary>Per-workstation thermal printer settings (each reception computer can have its own printer).</summary>
[HasPermission(Perm.PrinterSettings)]
public class PrintersModel : HmsPageModel
{
    private readonly IPrinterProfileService _printers;
    private readonly Core.Abstractions.IRequestContext _ctx;

    public PrintersModel(IPrinterProfileService printers, Core.Abstractions.IRequestContext ctx)
    {
        _printers = printers;
        _ctx = ctx;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public PrinterProfileInput Input { get; set; } = new();

    public IReadOnlyList<PrinterProfile> Profiles { get; private set; } = Array.Empty<PrinterProfile>();
    public string Workstation { get; private set; } = "";
    public PrinterProfile Effective { get; private set; } = null!;
    public PrinterProfile? Editing { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "printers";
        ViewData["SettingsTab"] = "printers";
        Workstation = _ctx.Workstation ?? "";
        Profiles = await _printers.ListAsync();
        Effective = await _printers.ForWorkstationAsync(Workstation);
        Editing = Id is int id ? Profiles.FirstOrDefault(p => p.Id == id) : null;
    }

    public async Task<IActionResult> OnGetAsync(string? ws, bool @new)
    {
        await LoadAsync();
        if (Id is not null && Editing is null) return NotFound();
        var target = string.IsNullOrEmpty(ws ?? Workstation) ? PrinterProfile.DefaultWorkstation : (ws ?? Workstation);
        var existing = Profiles.FirstOrDefault(p => p.WorkstationName.Equals(target, StringComparison.OrdinalIgnoreCase));
        var src = Editing ?? (@new ? null : existing);
        if (src is not null && Editing is null) { Id = src.Id; Editing = src; }
        src ??= new PrinterProfile { WorkstationName = @new && existing is not null ? "" : target };
        Input = new PrinterProfileInput
        {
            WorkstationName = src.WorkstationName, Mode = src.Mode, PaperWidthMm = src.PaperWidthMm, WindowsPrinterName = src.WindowsPrinterName,
            LanHost = src.LanHost, LanPort = src.LanPort, AgentUrl = src.AgentUrl, Alignment = src.Alignment, FontSize = src.FontSize,
            ShowLogo = src.ShowLogo, BoldText = src.BoldText, TokenSize = src.TokenSize, AutoCut = src.AutoCut, Copies = src.Copies
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _printers.SaveAsync(Id, Input);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Printer settings saved for {(Input.WorkstationName == PrinterProfile.DefaultWorkstation ? "all computers (default)" : Input.WorkstationName)}.";
        return RedirectToPage(new { id = r.Value });
    }
}
