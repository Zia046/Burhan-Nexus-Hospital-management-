using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.SettingsView)]
public class HospitalTypeModel : HmsPageModel
{
    private readonly IHospitalAdminService _hospital;
    private readonly IPasswordService _passwords;
    private readonly HmsDbContext _db;

    public HospitalTypeModel(IHospitalAdminService hospital, IPasswordService passwords, HmsDbContext db)
    {
        _hospital = hospital;
        _passwords = passwords;
        _db = db;
    }

    public HospitalConfiguration Config { get; private set; } = null!;
    public bool CanChangeType { get; private set; }

    [BindProperty] public OperationalRulesInput Rules { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public HospitalType NewType { get; set; }
    [BindProperty] public bool ApplyDefaults { get; set; } = true;
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? Confirmation { get; set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "type";
        Config = await _hospital.GetConfigurationAsync();
        CanChangeType = await CanAsync(Perm.SettingsChangeType);
        Rules = new OperationalRulesInput
        {
            PharmacyPricingEnabled = Config.PharmacyPricingEnabled, PharmacyRetailSalesEnabled = Config.PharmacyRetailSalesEnabled,
            PharmacyProfitTrackingEnabled = Config.PharmacyProfitTrackingEnabled, GovernmentStockIssueEnabled = Config.GovernmentStockIssueEnabled,
            PatientChargesEnabled = Config.PatientChargesEnabled
        };
        RowVersion = Config.RowVersion;
        NewType = Config.HospitalType == HospitalType.Government ? HospitalType.Private : HospitalType.Government;
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostRulesAsync()
    {
        if (await DenyUnlessAsync(Perm.SettingsChangeType) is { } deny) return deny;
        var r = await _hospital.UpdateOperationalRulesAsync(Rules, RowVersion);
        if (r.Succeeded) FlashSuccess = "Operational rules saved.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    /// <summary>Changing the hospital type is protected: permission + password re-entry + typed confirmation.</summary>
    public async Task<IActionResult> OnPostChangeTypeAsync()
    {
        if (await DenyUnlessAsync(Perm.SettingsChangeType) is { } deny) return deny;
        if (!string.Equals(Confirmation?.Trim(), "CHANGE", StringComparison.Ordinal))
        {
            FlashError = "Type CHANGE in capital letters to confirm.";
            return RedirectToPage();
        }
        var hash = await _db.UserCredentials.Where(c => c.UserId == CurrentUserId).Select(c => c.PasswordHash).FirstOrDefaultAsync();
        if (hash is null || !_passwords.Verify(hash, Password ?? string.Empty, out _))
        {
            FlashError = "Your password is incorrect. The hospital type was not changed.";
            return RedirectToPage();
        }
        var r = await _hospital.ChangeHospitalTypeAsync(NewType, ApplyDefaults);
        if (r.Succeeded) FlashSuccess = $"Hospital type changed to {NewType}.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
