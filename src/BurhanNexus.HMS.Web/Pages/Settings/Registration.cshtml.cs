using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.PatientsSettings)]
public class RegistrationModel : HmsPageModel
{
    private readonly IRegistrationSetupService _setup;
    public RegistrationModel(IRegistrationSetupService setup) => _setup = setup;

    [BindProperty] public RegistrationSettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    public RegistrationSettings Current { get; private set; } = null!;
    public IRegistrationSetupService Setup => _setup;

    private async Task LoadAsync(bool bind)
    {
        ViewData["Nav"] = "registration-settings";
        ViewData["SettingsTab"] = "registration";
        Current = await _setup.GetSettingsAsync();
        if (!bind) return;
        Input = new RegistrationSettingsInput
        {
            PatientNumberPrefix = Current.PatientNumberPrefix, PatientNumberDigits = Current.PatientNumberDigits,
            OpdNumberPrefix = Current.OpdNumberPrefix, EmergencyNumberPrefix = Current.EmergencyNumberPrefix,
            EncounterNumberIncludesYear = Current.EncounterNumberIncludesYear, EncounterNumberDigits = Current.EncounterNumberDigits,
            OpdSlipPaper = Current.OpdSlipPaper, EmergencySlipPaper = Current.EmergencySlipPaper,
            AutoPrintAfterRegistration = Current.AutoPrintAfterRegistration
        };
        RowVersion = Current.RowVersion;
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _setup.UpdateNumberingAsync(Input, RowVersion);
        if (!r.Succeeded)
        {
            await LoadAsync(false);
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Registration settings saved. Existing Patient IDs and visit numbers are never changed.";
        return RedirectToPage();
    }
}
