using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>Verification queue: submitted reports waiting for an authorised radiologist.</summary>
[HasPermission(Perm.RadVerifyReport)]
public class VerifyModel : RadiologyListModel
{
    public Task OnGetAsync() => LoadAsync("verification", "rad-verify", "verify");
}
