using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Opd;

/// <summary>OPD configuration: tokens, defaults, doctors and fee references.</summary>
[HasPermission(Perm.OpdSettings)]
public class SetupModel : HmsPageModel
{
    private readonly IRegistrationSetupService _setup;
    private readonly IDepartmentService _departments;

    public SetupModel(IRegistrationSetupService setup, IDepartmentService departments)
    {
        _setup = setup;
        _departments = departments;
    }

    public RegistrationSettings Settings { get; private set; } = null!;
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<ServiceItem> Services { get; private set; } = Array.Empty<ServiceItem>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    [BindProperty(SupportsGet = true, Name = "doctor")] public int? EditDoctorId { get; set; }
    [BindProperty(SupportsGet = true, Name = "service")] public int? EditServiceId { get; set; }

    [BindProperty] public OpdSettingsInput OpdForm { get; set; } = new();
    [BindProperty] public DoctorInput DoctorForm { get; set; } = new();
    [BindProperty] public ServiceItemInput ServiceForm { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "opd";
        Settings = await _setup.GetSettingsAsync();
        Doctors = await _setup.DoctorsAsync(false);
        Services = await _setup.ServiceItemsAsync(false);
        Departments = await _departments.ListAsync(includeInactive: false);
    }

    public async Task<IActionResult> OnPostSettingsAsync()
    {
        var r = await _setup.UpdateOpdAsync(OpdForm, RowVersion);
        if (r.Succeeded) FlashSuccess = "OPD settings saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDoctorAsync(int? id)
    {
        var r = await _setup.SaveDoctorAsync(id, DoctorForm);
        if (r.Succeeded) FlashSuccess = $"Doctor '{DoctorForm.Name.Trim()}' saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostServiceAsync(int? id)
    {
        var r = await _setup.SaveServiceItemAsync(id, ServiceForm);
        if (r.Succeeded) FlashSuccess = "Fee reference saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
