using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

[HasPermission(Perm.PatientsAdd)]
public class RegisterModel : HmsPageModel
{
    private readonly IPatientService _patients;
    public RegisterModel(IPatientService patients) => _patients = patients;

    [BindProperty] public PatientInput Patient { get; set; } = new() { AgeUnit = Core.Domain.AgeUnit.Years, GuardianRelation = Core.Domain.GuardianRelation.SonOf };
    [BindProperty] public bool ConfirmNotDuplicate { get; set; }
    /// <summary>What to do after registering: "opd", "emergency" or "profile".</summary>
    [BindProperty(SupportsGet = true)] public string? Next { get; set; }

    public IReadOnlyList<DuplicateCandidate> Duplicates { get; private set; } = Array.Empty<DuplicateCandidate>();

    public void OnGet(string? name, string? mobile, string? cnic)
    {
        ViewData["Nav"] = "patient-register";
        Patient.FullName = name;
        Patient.MobileNumber = mobile;
        Patient.Cnic = cnic;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Nav"] = "patient-register";
        var result = await _patients.RegisterAsync(Patient, ConfirmNotDuplicate);
        if (result.NeedsDuplicateConfirmation)
        {
            Duplicates = result.Duplicates;
            return Page();
        }
        if (!result.Succeeded)
        {
            AddErrors(result);
            Duplicates = await _patients.FindDuplicatesAsync(Patient);
            return Page();
        }

        var p = result.Patient!;
        FlashSuccess = $"Patient registered. Patient ID: {p.PatientNumber}";
        if (Next == "appointment" && await CanAsync(Perm.AppointmentsAdd)) return Redirect($"/Appointments/Book?patientId={p.Id}");
        if (Next == "opd" && await CanAsync(Perm.OpdAdd)) return Redirect($"/Opd/Register?patientId={p.Id}");
        if (Next == "emergency" && await CanAsync(Perm.EmergencyAdd)) return Redirect($"/Emergency/Register?patientId={p.Id}");
        return await CanAsync(Perm.PatientsView) ? Redirect($"/Patients/Profile/{p.Id}") : Redirect("/Patients/Register");
    }
}
