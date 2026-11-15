using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Report header / footer, pre-printed letterhead, signatory, verification policy, booking rules and file storage.</summary>
[HasPermission(Perm.RadSettings)]
public class SettingsModel : RadiologyPageModel
{
    [BindProperty] public RadiologySettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    public RadiologySettings Current { get; private set; } = null!;
    public string StoragePath { get; private set; } = "";
    public bool IsPrivate { get; private set; }

    private async Task LoadAsync()
    {
        Nav("rad-settings", "settings");
        Current = await Catalog.SettingsAsync();
        StoragePath = HttpContext.RequestServices.GetRequiredService<RadiologyFileStore>().Root;
        IsPrivate = await Catalog.IsPrivateAsync();
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        var s = Current;
        RowVersion = s.RowVersion;
        Input = new RadiologySettingsInput
        {
            DepartmentName = s.DepartmentName, HospitalName = s.HospitalName, Address = s.Address, ContactNumber = s.ContactNumber, Email = s.Email,
            ReportHeader = s.ReportHeader, ReportFooter = s.ReportFooter, PrintHeader = s.PrintHeader, LetterheadTopMarginMm = s.LetterheadTopMarginMm,
            DefaultReportingDoctor = s.DefaultReportingDoctor, DefaultDesignation = s.DefaultDesignation, ShowVerifiedBy = s.ShowVerifiedBy,
            AllowSelfVerification = s.AllowSelfVerification, RequirePaymentBeforeRelease = s.RequirePaymentBeforeRelease,
            AllowBookingWithoutVisit = s.AllowBookingWithoutVisit, VisitValidDays = s.VisitValidDays, MaxUploadMb = s.MaxUploadMb
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await Catalog.SaveSettingsAsync(Input, RowVersion);
        if (r.Succeeded) { FlashSuccess = "Radiology settings saved."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostImageAsync(string kind, IFormFile? file, bool remove)
    {
        byte[]? data = null;
        string? type = null;
        if (!remove)
        {
            if (file is null || file.Length == 0) { FlashError = "Choose an image."; return RedirectToPage(); }
            if (file.Length > 1_000_000) { FlashError = "Image is too large (max 1 MB)."; return RedirectToPage(); }
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            data = ms.ToArray();
            type = file.ContentType;
        }
        var r = await Catalog.SetImageAsync(kind == "signature" ? "signature" : "logo", data, type);
        if (r.Succeeded) FlashSuccess = remove ? "Image removed." : "Image uploaded."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
