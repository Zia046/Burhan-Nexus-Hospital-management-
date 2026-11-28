using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Update 17: free medicine issues waiting for approval (only when "Free Issue Approval Required" is on).</summary>
[HasPermission(Perm.PharmApproveFreeIssue)]
public class ApprovalsModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    public ApprovalsModel(HmsDbContext db) => _db = db;

    public IReadOnlyList<PatientMedicineIssue> Pending { get; private set; } = Array.Empty<PatientMedicineIssue>();

    public async Task OnGetAsync()
    {
        await InitAsync("approvals", "pharm-approvals");
        Pending = await _db.PatientMedicineIssues.AsNoTracking().Include(i => i.Patient).Include(i => i.Items)
            .Where(i => i.ApprovalStatus == FreeIssueApproval.Pending).OrderBy(i => i.Id).Take(300).ToListAsync();
    }
}
