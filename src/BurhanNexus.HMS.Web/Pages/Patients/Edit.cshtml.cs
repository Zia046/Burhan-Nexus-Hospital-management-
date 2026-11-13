using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

[HasPermission(Perm.PatientsEdit)]
public class EditModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly HospitalClock _clock;

    public EditModel(IPatientService patients, HospitalClock clock)
    {
        _patients = patients;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public PatientInput Patient { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    public Patient? Current { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "patients";
        Current = await _patients.GetAsync(Id);
        if (Current is null) return NotFound();
        if (Current.Status == PatientStatus.Merged) return Redirect($"/Patients/Profile/{Id}");
        var age = AgeCalculator.Of(Current, await _clock.TodayAsync());
        Patient = new PatientInput
        {
            FullName = Current.IsUnidentified ? null : Current.FullName, GuardianRelation = Current.GuardianRelation ?? GuardianRelation.SonOf,
            GuardianName = Current.GuardianName, Gender = Current.Gender,
            DateOfBirth = Current.DateOfBirthEstimated ? null : Current.DateOfBirth,
            Age = age?.Value, AgeUnit = age?.Unit ?? AgeUnit.Years, Cnic = Current.Cnic, MobileNumber = Current.MobileNumber,
            AlternateContact = Current.AlternateContact, Address = Current.Address, CityOrVillage = Current.CityOrVillage,
            BloodGroup = Current.BloodGroup, Notes = Current.Notes, IsUnidentified = Current.IsUnidentified
        };
        RowVersion = Current.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Nav"] = "patients";
        var r = await _patients.UpdateAsync(Id, Patient, RowVersion);
        if (!r.Succeeded)
        {
            Current = await _patients.GetAsync(Id);
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Patient information saved. The Patient ID and visit history are unchanged.";
        return Redirect($"/Patients/Profile/{Id}");
    }
}
