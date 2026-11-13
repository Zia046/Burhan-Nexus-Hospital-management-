using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.SettingsView)]
[RequestSizeLimit(6 * 1024 * 1024)]
public class IndexModel : HmsPageModel
{
    private readonly IHospitalAdminService _hospital;
    public IndexModel(IHospitalAdminService hospital) => _hospital = hospital;

    [BindProperty] public HospitalInfoInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public IFormFile? Logo { get; set; }

    public HospitalSettings? Current { get; private set; }
    public bool CanEdit { get; private set; }

    private async Task LoadAsync(bool bind)
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "hospital";
        Current = await _hospital.GetSettingsAsync();
        CanEdit = await CanAsync(Perm.SettingsEdit);
        if (bind)
        {
            Input = new HospitalInfoInput
            {
                HospitalName = Current.HospitalName, RegistrationNumber = Current.RegistrationNumber, Address = Current.Address,
                City = Current.City, Province = Current.Province, ContactNumber = Current.ContactNumber, Email = Current.Email,
                Website = Current.Website, ReceiptHeader = Current.ReceiptHeader, ReceiptFooter = Current.ReceiptFooter,
                ReportHeader = Current.ReportHeader, ReportFooter = Current.ReportFooter
            };
            RowVersion = Current.RowVersion;
        }
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    public async Task<IActionResult> OnPostInfoAsync()
    {
        if (await DenyUnlessAsync(Perm.SettingsEdit) is { } deny) return deny;
        var r = await _hospital.UpdateInfoAsync(Input, RowVersion);
        if (!r.Succeeded)
        {
            await LoadAsync(false);
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Hospital information saved. All printed documents will use the new details.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogoAsync(bool remove)
    {
        if (await DenyUnlessAsync(Perm.SettingsEdit) is { } deny) return deny;
        byte[]? bytes = null;
        if (!remove)
        {
            if (Logo is null || Logo.Length == 0)
            {
                FlashError = "Please choose an image file.";
                return RedirectToPage();
            }
            if (Logo.Length > HospitalAdminService.MaxLogoBytes)
            {
                FlashError = "The logo must be smaller than 2 MB.";
                return RedirectToPage();
            }
            using var ms = new MemoryStream();
            await Logo.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        var r = await _hospital.UpdateLogoAsync(bytes);
        if (r.Succeeded) FlashSuccess = remove ? "Logo removed." : "Logo updated.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
