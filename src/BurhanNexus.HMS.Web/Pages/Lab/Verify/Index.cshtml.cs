using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Lab.Verify;

/// <summary>Verification queue: patient visits with results waiting for approval.</summary>
[HasPermission(Perm.LabVerifyResults, Perm.LabApproveReports)]
public class IndexModel : LabListModel
{
    public Task OnGetAsync() => LoadAsync("verification", "lab-verify", "verify");
}
