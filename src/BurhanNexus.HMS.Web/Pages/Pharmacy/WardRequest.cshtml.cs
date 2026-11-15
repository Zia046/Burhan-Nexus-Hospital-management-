using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Update 9: one ward request – approve, reject or mark unavailable per medicine, and issue (fully or partly).</summary>
[HasPermission(Perm.PharmProcessWardRequests)]
public class WardRequestModel : PharmacyPageModel
{
    private readonly IMedicineRequestService _req;
    public WardRequestModel(IMedicineRequestService req) => _req = req;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public Guid IdempotencyKey { get; set; }
    [BindProperty] public string? Note { get; set; }

    public MedicineRequest R { get; private set; } = null!;
    public IReadOnlyList<MedicineRequestEvent> Events { get; private set; } = Array.Empty<MedicineRequestEvent>();
    public Dictionary<int, int> Stock { get; } = new();

    private async Task<bool> LoadAsync()
    {
        await InitAsync("ward");
        var r = await _req.GetAsync(Id);
        if (r is null) return false;
        R = r;
        Events = await _req.EventsAsync(Id);
        var rows = await Query.MedicineStockAsync(null, null, false, false);
        var ids = r.Lines.Select(l => l.MedicineId).ToHashSet();
        foreach (var s in rows.Where(s => ids.Contains(s.MedicineId))) Stock[s.MedicineId] = s.MainStock;
        if (IdempotencyKey == Guid.Empty) IdempotencyKey = Guid.NewGuid();
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    public async Task<IActionResult> OnPostIssueAsync()
    {
        var qty = new Dictionary<long, int>();
        foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("Issue[") && k.EndsWith("]")))
            if (long.TryParse(key[6..^1], out var lineId) && int.TryParse(Request.Form[key], out var q) && q != 0) qty[lineId] = q;
        var r = await _req.IssueAsync(Id, IdempotencyKey, qty, await RightsForIssueAsync());
        if (!r.Succeeded)
        {
            IdempotencyKey = Guid.Empty;
            if (!await LoadAsync()) return NotFound();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = r.Value!.WasDuplicate ? $"Already issued as {r.Value.Number} – nothing was deducted again." : $"Issued as {r.Value.Number}. Stock deducted against the admission.";
        return Redirect($"/Pharmacy/WardRequest/{Id}");
    }

    private async Task<PharmacyRights> RightsForIssueAsync()
    {
        await InitAsync("ward");
        return await RightsAsync();
    }

    public async Task<IActionResult> OnPostDecideAsync(long lineId, LineDecision decision)
    {
        var r = await _req.DecideAsync(lineId, decision, Note);
        if (!r.Succeeded)
        {
            if (!await LoadAsync()) return NotFound();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = "Saved.";
        return Redirect($"/Pharmacy/WardRequest/{Id}");
    }
}
