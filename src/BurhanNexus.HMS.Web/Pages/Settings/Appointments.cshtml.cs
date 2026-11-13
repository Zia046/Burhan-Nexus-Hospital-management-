using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

[HasPermission(Perm.AppointmentsSettings)]
public class AppointmentsModel : HmsPageModel
{
    private readonly IRegistrationSetupService _setup;
    public AppointmentsModel(IRegistrationSetupService setup) => _setup = setup;

    [BindProperty] public AppointmentSettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "appointment-settings";
        ViewData["SettingsTab"] = "appointments";
        var s = await _setup.GetSettingsAsync();
        Input = new AppointmentSettingsInput
        {
            AppointmentTimeEnabled = s.AppointmentTimeEnabled, ShowFeeOnAppointmentSlip = s.ShowFeeOnAppointmentSlip,
            AppointmentMaxDaysAhead = s.AppointmentMaxDaysAhead, AppointmentSlipMessage = s.AppointmentSlipMessage, AppointmentSlipContact = s.AppointmentSlipContact
        };
        RowVersion = s.RowVersion;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _setup.UpdateAppointmentSettingsAsync(Input, RowVersion);
        if (!r.Succeeded)
        {
            ViewData["Nav"] = "appointment-settings";
            ViewData["SettingsTab"] = "appointments";
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Appointment slip settings saved.";
        return RedirectToPage();
    }
}
