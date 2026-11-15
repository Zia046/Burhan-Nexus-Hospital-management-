using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Update 9: queue of nursing medicine requests from the wards (NICU, Gynae, Maternity, Labour, General …).</summary>
[HasPermission(Perm.PharmProcessWardRequests)]
public class WardRequestsModel : PharmacyPageModel
{
    private readonly IMedicineRequestService _req;
    public WardRequestsModel(IMedicineRequestService req) => _req = req;

    [BindProperty(SupportsGet = true)] public bool All { get; set; }
    public IReadOnlyList<MedicineRequestRow> Rows { get; private set; } = Array.Empty<MedicineRequestRow>();

    public async Task OnGetAsync()
    {
        await InitAsync("ward");
        Rows = await _req.ListAsync(!All, null, null, 300);
    }
}
