using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

[HasPermission(Perm.PatientsAdd, Perm.LabRegisterPatient)]
public class RegisterModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IEncounterService _encounters;
    public RegisterModel(IPatientService patients, IEncounterService encounters) { _patients = patients; _encounters = encounters; }

    [BindProperty] public PatientInput Patient { get; set; } = new() { AgeUnit = Core.Domain.AgeUnit.Years, GuardianRelation = Core.Domain.GuardianRelation.SonOf, Address = PatientInput.DefaultAddress };
    [BindProperty] public bool ConfirmNotDuplicate { get; set; }
    /// <summary>What to do after registering: "opd", "emergency" or "profile".</summary>
    [BindProperty(SupportsGet = true)] public string? Next { get; set; }

    public IReadOnlyList<DuplicateCandidate> Duplicates { get; private set; } = Array.Empty<DuplicateCandidate>();
    /// <summary>Update 17: the patient search bar at the top (New Appointment / OPD): Patient ID (MR no.) or name.</summary>
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public bool Searched => !string.IsNullOrWhiteSpace(Q);
    public bool SearchMode => Next is "appointment" or "opd" or "consultant";

    public async Task OnGetAsync(string? name, string? mobile, string? cnic)
    {
        ViewData["Nav"] = Next == "appointment" ? "appointment-book" : "patient-register";
        Patient.FullName = name;
        Patient.MobileNumber = mobile;
        Patient.Cnic = cnic;
        if (SearchMode && Searched)
        {
            Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q!.Trim(), PageSize = 20, Routine = !await CanAsync(Perm.PatientsSearch) })).Items;
            // No match by name: the name is carried into the new registration form.
            if (Matches.Count == 0 && !Q.Any(char.IsDigit)) Patient.FullName ??= Q.Trim();
        }
    }

    /// <summary>Where "Select" takes an existing patient (no second registration).</summary>
    public string SelectUrl(long patientId) => Next switch
    {
        "opd" => $"/Opd/Register?patientId={patientId}",
        _ => $"/Appointments/Book?patientId={patientId}",
    };

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
        if (Next == "lab" && await CanAsync(Perm.LabBook)) return Redirect($"/Lab/Book?patientId={p.Id}");
        if (Next == "radiology" && await CanAsync(Perm.RadCreateBooking)) return Redirect($"/Radiology/Book?patientId={p.Id}");
        if (Next == "appointment" && await CanAsync(Perm.AppointmentsAdd)) return Redirect($"/Appointments/Book?patientId={p.Id}");
        if (Next == "consultant" && await CanAsync(Perm.AppointmentsAdd)) return Redirect($"/Appointments/Book?patientId={p.Id}");
        if (Next == "opd" && await CanAsync(Perm.OpdAdd))
        {
            // Update 16: the OPD button registers the visit straight away and prints the A4 / A5 OPD slip.
            var visit = await _encounters.RegisterOpdAsync(new OpdVisitInput { PatientId = p.Id });
            if (visit.Succeeded) { FlashSuccess = $"Patient {p.PatientNumber} registered – OPD slip {visit.Value!.EncounterNumber}."; return Redirect($"/Print/Opd/{visit.Value.Id}?autoprint=true"); }
            FlashError = string.Join(" ", visit.Errors);
            return Redirect($"/Opd/Register?patientId={p.Id}");
        }
        if (Next == "emergency" && await CanAsync(Perm.EmergencyAdd)) return Redirect($"/Emergency/Register?patientId={p.Id}");
        return await CanAsync(Perm.PatientsView) ? Redirect($"/Patients/Profile/{p.Id}") : Redirect("/Patients/Register");
    }
}
