using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>Report branding, signatory, verification policy and the inventory foundation.</summary>
[HasPermission(Perm.LabSettings)]
public class SettingsModel : HmsPageModel
{
    private readonly ILabCatalogService _catalog;
    public SettingsModel(ILabCatalogService catalog) => _catalog = catalog;

    [BindProperty] public LabSettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public LabInventoryItem Item { get; set; } = new();
    public LabSettings Settings { get; private set; } = null!;
    public IReadOnlyList<LabInventoryItem> Inventory { get; private set; } = Array.Empty<LabInventoryItem>();

    private async Task LoadAsync(bool fill)
    {
        ViewData["Nav"] = "lab-settings";
        ViewData["LabTab"] = "settings";
        Settings = await _catalog.SettingsAsync();
        Inventory = await _catalog.InventoryAsync();
        if (!fill) return;
        var s = Settings;
        RowVersion = s.RowVersion;
        Input = new LabSettingsInput
        {
            LaboratoryName = s.LaboratoryName, HospitalName = s.HospitalName, Address = s.Address, ContactNumber = s.ContactNumber, Email = s.Email,
            ReportHeader = s.ReportHeader, ReportFooter = s.ReportFooter, Layout = s.Layout, SignatoryName = s.SignatoryName,
            SignatoryDesignation = s.SignatoryDesignation, ShowVerifiedBy = s.ShowVerifiedBy, AllowSelfVerification = s.AllowSelfVerification,
            RequirePaymentBeforeRelease = s.RequirePaymentBeforeRelease
        };
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _catalog.SaveSettingsAsync(Input, RowVersion);
        if (!r.Succeeded) { await LoadAsync(false); AddErrors(r); return Page(); }
        FlashSuccess = "Laboratory settings saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostImageAsync(string kind, IFormFile? file, bool remove)
    {
        byte[]? data = null;
        string? type = null;
        if (!remove)
        {
            if (file is null || file.Length == 0) { FlashError = "Choose an image file."; return RedirectToPage(); }
            if (file.Length > 1_000_000) { FlashError = "Image is too large (max 1 MB)."; return RedirectToPage(); }
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            data = ms.ToArray();
            type = file.ContentType;
        }
        var r = await _catalog.SetImageAsync(kind == "signature" ? "signature" : "logo", data, type);
        if (r.Succeeded) FlashSuccess = remove ? "Image removed." : "Image uploaded."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostInventoryAsync(int? id)
    {
        var r = await _catalog.SaveInventoryItemAsync(id, Item);
        if (r.Succeeded) FlashSuccess = "Inventory item saved."; else FlashError = r.ErrorText;
        return Redirect("/Lab/Settings#inventory");
    }
}
