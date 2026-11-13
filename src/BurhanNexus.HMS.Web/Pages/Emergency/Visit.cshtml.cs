using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Emergency;

[HasPermission(Perm.EmergencyView)]
public class VisitModel : HmsPageModel
{
    private readonly IEncounterService _encounters;
    private readonly IRegistrationSetupService _setup;
    private readonly HospitalClock _clock;

    public VisitModel(IEncounterService encounters, IRegistrationSetupService setup, HospitalClock clock)
    {
        _encounters = encounters;
        _setup = setup;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public EmergencyDetailsInput Details { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public EmergencyStatus NewStatus { get; set; }
    [BindProperty] public string? Reason { get; set; }

    public PatientEncounter Encounter { get; private set; } = null!;
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<EmergencyStatus> AllowedStatuses { get; private set; } = Array.Empty<EmergencyStatus>();
    public IReadOnlyList<EmergencyCategory> Categories { get; private set; } = Array.Empty<EmergencyCategory>();

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "emergency";
        var e = await _encounters.GetAsync(Id);
        if (e?.Emergency is null) return NotFound();
        Encounter = e;
        AgeText = AgeCalculator.Of(e.Patient!, await _clock.LocalDateOfAsync(e.EncounterUtc))?.ToString() ?? "—";
        AllowedStatuses = _encounters.AllowedStatusChanges(e.Emergency.Status, await Permissions.GetAsync());
        Categories = await _setup.CategoriesAsync(false);
        RowVersion = e.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync()
    {
        // Fine-grained status rules (basic vs restricted statuses) are enforced inside the service.
        if (!await Permissions.HasAnyAsync(Perm.EmergencyStatus, Perm.EmergencyClose)) return Forbid();
        var r = await _encounters.ChangeEmergencyStatusAsync(Id, NewStatus, Reason);
        if (r.Succeeded) FlashSuccess = $"Status changed to {EncounterService.Label(NewStatus)}.";
        else FlashError = r.ErrorText;
        return Redirect($"/Emergency/Visit/{Id}");
    }

    public async Task<IActionResult> OnPostDetailsAsync()
    {
        if (await DenyUnlessAsync(Perm.EmergencyEdit) is { } deny) return deny;
        var r = await _encounters.UpdateEmergencyDetailsAsync(Id, Details, RowVersion);
        if (r.Succeeded) FlashSuccess = "Emergency details updated.";
        else FlashError = r.ErrorText;
        return Redirect($"/Emergency/Visit/{Id}");
    }
}
