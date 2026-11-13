using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>Government OPD slip or Private hospital visit document (chosen by the hospital type at the time of the visit).</summary>
[HasPermission(Perm.OpdPrint)]
public class OpdModel : SlipModelBase
{
    public OpdModel(IEncounterService e, IRegistrationSetupService s, DisplayFormatter f) : base(e, s, f) { }

    public bool IsPrivate => Encounter.HospitalTypeAtVisit == HospitalType.Private;

    public async Task<IActionResult> OnGetAsync(long id, string? paper, bool autoprint)
    {
        // Private-hospital appointments print the doctor-wise thermal token slip instead of the A5/A4 document.
        var appointments = HttpContext.RequestServices.GetRequiredService<IAppointmentService>();
        if (await appointments.GetByEncounterAsync(id) is { } appt)
            return Redirect($"/Print/Appointment/{appt.Id}" + (autoprint ? "?autoprint=true" : ""));
        return await LoadAsync(id, EncounterType.Opd, paper, autoprint, s => s.OpdSlipPaper,
            e => e.HospitalTypeAtVisit == HospitalType.Private ? PrintedDocumentType.PrivateVisitDocument : PrintedDocumentType.OpdSlip);
    }
}
