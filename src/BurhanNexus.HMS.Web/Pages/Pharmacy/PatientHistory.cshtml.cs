using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>All medicines issued to one patient (medicine, quantity, batch, date, issued by, visit).</summary>
[HasPermission(Perm.PharmViewPatientHistory)]
public class PatientHistoryModel : PharmacyPageModel
{
    private readonly IPatientService _patients;
    public PatientHistoryModel(IPatientService patients) => _patients = patients;

    public Patient Patient { get; private set; } = null!;
    public IReadOnlyList<PatientMedicineRow> Rows { get; private set; } = Array.Empty<PatientMedicineRow>();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        await InitAsync("issue");
        var p = await _patients.GetAsync(id);
        if (p is null) return NotFound();
        Patient = p;
        Rows = await Query.PatientHistoryAsync(id, ShowPrices);
        return Page();
    }
}
