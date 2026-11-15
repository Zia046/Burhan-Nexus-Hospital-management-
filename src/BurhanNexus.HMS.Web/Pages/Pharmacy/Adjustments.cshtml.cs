using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Controlled stock corrections with a mandatory reason; applied when approved (never a silent overwrite).</summary>
[HasPermission(Perm.PharmAdjustStock, Perm.PharmApproveAdjustments)]
public class AdjustmentsModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    private readonly HospitalClock _clock;
    public AdjustmentsModel(HmsDbContext db, HospitalClock clock) { _db = db; _clock = clock; }

    [BindProperty(SupportsGet = true)] public int? Location { get; set; }
    [BindProperty(SupportsGet = true)] public long? Batch { get; set; }
    [BindProperty] public AdjustmentInput Input { get; set; } = new();
    public IReadOnlyList<StockLocation> Locations { get; private set; } = Array.Empty<StockLocation>();
    public IReadOnlyList<BatchStockRow> Batches { get; private set; } = Array.Empty<BatchStockRow>();
    public IReadOnlyList<StockAdjustment> Pending { get; private set; } = Array.Empty<StockAdjustment>();
    public IReadOnlyList<StockAdjustment> Recent { get; private set; } = Array.Empty<StockAdjustment>();
    public bool CanApprove { get; private set; }
    public bool CanRequest { get; private set; }

    private async Task LoadAsync()
    {
        await InitAsync("adjust");
        CanApprove = await CanAsync(Perm.PharmApproveAdjustments);
        CanRequest = await CanAsync(Perm.PharmAdjustStock);
        Locations = await Catalog.LocationsAsync(activeOnly: true);
        var loc = Location ?? Input.LocationId;
        if (loc == 0) loc = Locations.First(l => l.Kind == StockLocationKind.MainPharmacy).Id;
        Location = loc;
        Batches = await Query.BatchStockAsync(loc, null, null, false, onlyInStock: false);
        IQueryable<StockAdjustment> q = _db.StockAdjustments.AsNoTracking().Include(a => a.Medicine).Include(a => a.Batch).Include(a => a.Location);
        Pending = await q.Where(a => a.Status == StockAdjustmentStatus.Pending).OrderBy(a => a.Id).ToListAsync();
        var since = (await _clock.TodayAsync()).AddDays(-30);
        Recent = await q.Where(a => a.Status != StockAdjustmentStatus.Pending && a.BusinessDate >= since).OrderByDescending(a => a.Id).Take(50).ToListAsync();
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input = new AdjustmentInput { IdempotencyKey = Guid.NewGuid(), LocationId = Location!.Value, BatchId = Batch ?? 0 };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.PharmAdjustStock) is { } deny) return deny;
        Location = Input.LocationId;
        await LoadAsync();
        var r = await Pharmacy.RequestAdjustmentAsync(Input, await RightsAsync());
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already saved." : CanApprove ? $"Adjustment {r.Value.Number} applied." : $"Adjustment {r.Value.Number} sent for approval.";
        return Redirect($"/Pharmacy/Adjustments?location={Input.LocationId}");
    }

    public async Task<IActionResult> OnPostDecideAsync(long id, bool approve, string? note)
    {
        if (await DenyUnlessAsync(Perm.PharmApproveAdjustments) is { } deny) return deny;
        var r = await Pharmacy.DecideAdjustmentAsync(id, approve, note);
        if (r.Succeeded) FlashSuccess = approve ? "Adjustment approved – stock updated." : "Adjustment rejected."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
