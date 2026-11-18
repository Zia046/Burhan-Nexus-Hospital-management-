using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Settings;

/// <summary>OPD settings (Update 13: moved here from OPD Setup) – token numbers, default department and default fee references.</summary>
[HasPermission(Perm.OpdSettings)]
public class OpdModel : HmsPageModel
{
    private readonly IRegistrationSetupService _setup;
    private readonly IDepartmentService _departments;
    public OpdModel(IRegistrationSetupService setup, IDepartmentService departments) { _setup = setup; _departments = departments; }

    public RegistrationSettings Settings { get; private set; } = null!;
    public IReadOnlyList<ServiceItem> Services { get; private set; } = Array.Empty<ServiceItem>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    [BindProperty] public OpdSettingsInput OpdForm { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "settings";
        ViewData["SettingsTab"] = "opd";
        Settings = await _setup.GetSettingsAsync();
        Services = await _setup.ServiceItemsAsync(true);
        Departments = await _departments.ListAsync(includeInactive: false);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _setup.UpdateOpdAsync(OpdForm, RowVersion);
        if (r.Succeeded) FlashSuccess = "OPD settings saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
