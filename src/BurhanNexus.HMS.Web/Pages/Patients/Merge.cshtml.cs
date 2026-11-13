using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

/// <summary>Controlled correction of a duplicate registration (admin only, history preserved).</summary>
[HasPermission(Perm.PatientsMerge)]
public class MergeModel : HmsPageModel
{
    private readonly IPatientService _patients;
    public MergeModel(IPatientService patients) => _patients = patients;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "target")] public string? TargetNumber { get; set; }
    [BindProperty] public long TargetId { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? Confirmation { get; set; }

    public Patient? Duplicate { get; private set; }
    public Patient? Target { get; private set; }
    public IReadOnlyList<DuplicateCandidate> Suggestions { get; private set; } = Array.Empty<DuplicateCandidate>();

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "patients";
        Duplicate = await _patients.GetAsync(Id);
        if (Duplicate is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(TargetNumber))
        {
            Target = await _patients.FindByNumberAsync(TargetNumber);
            if (Target is null) FlashError = $"No patient with ID '{TargetNumber}'.";
            else if (Target.Id == Duplicate.Id || Target.Status == PatientStatus.Merged) { FlashError = "Choose a different, active patient."; Target = null; }
        }
        Suggestions = await _patients.FindDuplicatesAsync(new PatientInput
        {
            FullName = Duplicate.FullName, GuardianName = Duplicate.GuardianName, Cnic = Duplicate.Cnic, MobileNumber = Duplicate.MobileNumber
        }, Duplicate.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Confirmation?.Trim() != "MERGE")
        {
            FlashError = "Type MERGE in capital letters to confirm.";
            return Redirect($"/Patients/Merge/{Id}?target={Uri.EscapeDataString(TargetNumber ?? "")}");
        }
        var r = await _patients.MergeAsync(Id, TargetId, Reason);
        if (!r.Succeeded)
        {
            FlashError = r.ErrorText;
            return Redirect($"/Patients/Merge/{Id}?target={Uri.EscapeDataString(TargetNumber ?? "")}");
        }
        FlashSuccess = "Duplicate registration corrected. Visits were moved and the old Patient ID now points to this patient.";
        return Redirect($"/Patients/Profile/{TargetId}");
    }
}
