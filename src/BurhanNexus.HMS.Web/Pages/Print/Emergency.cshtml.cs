using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

[HasPermission(Perm.EmergencyPrint)]
public class EmergencyModel : SlipModelBase
{
    public EmergencyModel(IEncounterService e, IRegistrationSetupService s, DisplayFormatter f) : base(e, s, f) { }

    public Task<IActionResult> OnGetAsync(long id, string? paper, bool autoprint) =>
        LoadAsync(id, EncounterType.Emergency, paper, autoprint, s => s.EmergencySlipPaper, _ => PrintedDocumentType.EmergencySlip);
}
