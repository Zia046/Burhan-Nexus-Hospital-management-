using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Completed examinations waiting for a report, drafts and reports sent back for correction.</summary>
[HasPermission(Perm.RadEnterReport, Perm.RadEditDraft)]
public class ReportingModel : RadiologyListModel
{
    public Task OnGetAsync() => LoadAsync("reporting", "rad-reporting", "reporting");
}
