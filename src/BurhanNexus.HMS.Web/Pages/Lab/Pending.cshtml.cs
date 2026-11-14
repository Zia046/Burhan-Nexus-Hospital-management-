using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>Tests not yet performed, grouped by patient. Saved results disappear from this list.</summary>
[HasPermission(Perm.LabEnterResults)]
public class PendingModel : LabListModel
{
    public Task OnGetAsync() => LoadAsync("pending", "lab-pending", "pending");
}
