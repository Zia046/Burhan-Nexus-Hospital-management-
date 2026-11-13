using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Opd;

[HasPermission(Perm.OpdView)]
public class IndexModel : HmsPageModel
{
    private readonly IEncounterService _encounters;
    private readonly IRegistrationSetupService _setup;
    private readonly IDepartmentService _departments;
    private readonly HospitalClock _clock;

    public IndexModel(IEncounterService encounters, IRegistrationSetupService setup, IDepartmentService departments, HospitalClock clock)
    {
        _encounters = encounters;
        _setup = setup;
        _departments = departments;
        _clock = clock;
    }

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true, Name = "from")] public DateOnly? From { get; set; }
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true, Name = "to")] public DateOnly? To { get; set; }
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true, Name = "dept")] public int? DepartmentId { get; set; }
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true, Name = "doctor")] public int? DoctorId { get; set; }
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<EncounterListItem> Result { get; private set; } = new(Array.Empty<EncounterListItem>(), 0, 1, 50);
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "opd";
        Today = await _clock.TodayAsync();
        From ??= Today;
        To ??= From;
        Result = await _encounters.ListAsync(new EncounterQuery
        {
            Type = EncounterType.Opd, From = From, To = To, DepartmentId = DepartmentId, DoctorId = DoctorId, Q = Q, Page = PageNumber, PageSize = 50
        });
        Departments = await _departments.ListAsync(includeInactive: false);
        Doctors = await _setup.DoctorsAsync(activeOnly: false);
    }
}
