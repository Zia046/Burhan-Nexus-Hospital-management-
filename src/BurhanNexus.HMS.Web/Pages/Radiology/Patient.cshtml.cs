using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Radiology history of one patient: every X-Ray / Ultrasound examination, report status, files and reprint links.</summary>
[HasPermission(Perm.RadViewHistory)]
public class PatientModel : RadiologyPageModel
{
    private readonly IPatientService _patients;
    public PatientModel(IPatientService patients) => _patients = patients;

    public Patient Patient { get; private set; } = null!;
    public IReadOnlyList<RadiologyExamRow> Rows { get; private set; } = Array.Empty<RadiologyExamRow>();
    public bool CanSeeXRay { get; private set; }
    public bool CanSeeUltrasound { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Nav("rad-history", "history");
        var p = await _patients.GetAsync(id);
        if (p is null) return NotFound();
        Patient = p;
        Rows = await Rad.ExamsAsync(new RadiologyWorklistQuery { PatientId = id, Take = 1000 });
        return Page();
    }
}
