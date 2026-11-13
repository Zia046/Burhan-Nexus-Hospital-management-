using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.SettingsView)]
public class PrintingModel : HmsPageModel
{
    private readonly IHospitalAdminService _hospital;
    public PrintingModel(IHospitalAdminService hospital) => _hospital = hospital;

    [BindProperty] public PrintInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    public bool CanEdit { get; private set; }

    private async Task LoadAsync(bool bind)
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "printing";
        CanEdit = await CanAsync(Perm.SettingsEdit);
        if (!bind) return;
        var c = await _hospital.GetConfigurationAsync();
        Input = new PrintInput
        {
            ReceiptPrinterName = c.ReceiptPrinterName, ReceiptPaperSize = c.ReceiptPaperSize, ReceiptCopies = c.ReceiptCopies,
            ReportPrinterName = c.ReportPrinterName, ReportPaperSize = c.ReportPaperSize, ReportOrientation = c.ReportOrientation
        };
        RowVersion = c.RowVersion;
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.SettingsEdit) is { } deny) return deny;
        var r = await _hospital.UpdatePrintingAsync(Input, RowVersion);
        if (!r.Succeeded)
        {
            await LoadAsync(false);
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Printer settings saved.";
        return RedirectToPage();
    }
}
