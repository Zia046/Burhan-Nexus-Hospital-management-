using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy.Returns;

/// <summary>Patient return against a medicine issue (both modes). Private: unpaid charges are corrected, paid ones refunded after approval.</summary>
[HasPermission(Perm.PharmProcessReturns)]
public class IssueModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    public IssueModel(HmsDbContext db) => _db = db;

    [BindProperty] public ReturnInput Input { get; set; } = new();
    public PatientMedicineIssue Issue { get; private set; } = null!;
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();

    private async Task<bool> LoadAsync(long id)
    {
        await InitAsync("documents");
        var s = await _db.PatientMedicineIssues.AsNoTracking().Include(x => x.Items).Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return false;
        Issue = s;
        if (s.Mode == HospitalType.Private) Methods = await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().PaymentMethodsAsync(activeOnly: true);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Input = new ReturnInput { IdempotencyKey = Guid.NewGuid(), RefundPaymentMethodId = Methods.FirstOrDefault()?.Id, Lines = Issue.Items.Select(i => new ReturnLineInput { ItemId = i.Id, Restock = true }).ToList() };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        var r = await Pharmacy.ReturnIssueAsync(id, Input);
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved." : $"Return {r.Value.Number} saved.";
        return Redirect($"/Pharmacy/Doc/RET/{r.Value.Id}");
    }
}
