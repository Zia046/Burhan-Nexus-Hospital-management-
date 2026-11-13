using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Opd;

[HasPermission(Perm.OpdView)]
public class VisitModel : HmsPageModel
{
    private readonly IEncounterService _encounters;
    private readonly IRegistrationSetupService _setup;
    private readonly IDepartmentService _departments;

    public VisitModel(IEncounterService encounters, IRegistrationSetupService setup, IDepartmentService departments)
    {
        _encounters = encounters;
        _setup = setup;
        _departments = departments;
    }

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public OpdVisitInput Visit { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }

    public PatientEncounter Encounter { get; private set; } = null!;
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<ServiceItem> Services { get; private set; } = Array.Empty<ServiceItem>();

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "opd";
        var e = await _encounters.GetAsync(Id);
        if (e is null || e.Type != EncounterType.Opd) return NotFound();
        Encounter = e;
        Departments = await _departments.ListAsync();
        Doctors = await _setup.DoctorsAsync(activeOnly: false);
        Services = (await _setup.ServiceItemsAsync(activeOnly: false)).Where(s => s.Category == EncounterType.Opd).ToList();
        RowVersion = e.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.OpdEdit) is { } deny) return deny;
        var r = await _encounters.UpdateOpdAsync(Id, Visit, RowVersion);
        if (r.Succeeded) FlashSuccess = "Visit details corrected.";
        else FlashError = r.ErrorText;
        return Redirect($"/Opd/Visit/{Id}");
    }
}
