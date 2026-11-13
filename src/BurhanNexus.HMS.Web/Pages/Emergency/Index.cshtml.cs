using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Emergency;

[HasPermission(Perm.EmergencyView)]
public class IndexModel : HmsPageModel
{
    private readonly IEncounterService _encounters;
    private readonly IRegistrationSetupService _setup;

    public IndexModel(IEncounterService encounters, IRegistrationSetupService setup)
    {
        _encounters = encounters;
        _setup = setup;
    }

    [BindProperty(SupportsGet = true, Name = "status")] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "from")] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "category")] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<EncounterListItem> Result { get; private set; } = new(Array.Empty<EncounterListItem>(), 0, 1, 50);
    public IReadOnlyList<EmergencyCategory> Categories { get; private set; } = Array.Empty<EmergencyCategory>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "emergency";
        Status ??= "active";
        Result = await _encounters.ListAsync(new EncounterQuery
        {
            Type = EncounterType.Emergency, Status = Status == "all" ? null : Status, From = From, To = To, CategoryId = CategoryId, Q = Q,
            Page = PageNumber, PageSize = 50
        });
        Categories = await _setup.CategoriesAsync(false);
    }
}
