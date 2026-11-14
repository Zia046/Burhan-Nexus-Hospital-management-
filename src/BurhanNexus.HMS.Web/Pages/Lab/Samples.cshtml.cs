using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Lab;

[HasPermission(Perm.LabCollectSamples)]
public class SamplesModel : LabListModel
{
    public Task OnGetAsync() => LoadAsync("samples", "lab-samples", "samples");
}
