using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Update 10: patient-wise account statement – every charge (OPD, laboratory, radiology, admissions …), payments and advances.</summary>
[HasPermission(Perm.IpdViewStatements, Perm.FinanceViewAllAccounts)]
public class StatementModel : HmsPageModel
{
    private readonly HmsDbContext _db;
    public StatementModel(HmsDbContext db) => _db = db;

    public Patient Patient { get; private set; } = null!;
    public IReadOnlyList<Charge> Charges { get; private set; } = Array.Empty<Charge>();
    public Dictionary<long, string> Admissions { get; private set; } = new();
    public IReadOnlyList<PatientAdvance> Advances { get; private set; } = Array.Empty<PatientAdvance>();
    public IReadOnlyList<Receipt> Receipts { get; private set; } = Array.Empty<Receipt>();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        ViewData["Nav"] = "finance-receipts";
        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        Patient = p;
        Charges = await _db.Charges.AsNoTracking().Include(c => c.Department).Where(c => c.PatientId == id).OrderBy(c => c.CreatedUtc).ThenBy(c => c.Id).ToListAsync();
        var admIds = Charges.Where(c => c.IpdAdmissionId != null).Select(c => c.IpdAdmissionId!.Value).Distinct().ToList();
        Admissions = await _db.IpdAdmissions.AsNoTracking().Where(a => admIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.AdmissionNumber);
        Advances = await _db.PatientAdvances.AsNoTracking().Where(a => a.PatientId == id).OrderBy(a => a.Id).ToListAsync();
        Receipts = await _db.Receipts.AsNoTracking().Include(r => r.PaymentMethod).Where(r => r.PatientId == id).OrderBy(r => r.CreatedUtc).ToListAsync();
        return Page();
    }
}
