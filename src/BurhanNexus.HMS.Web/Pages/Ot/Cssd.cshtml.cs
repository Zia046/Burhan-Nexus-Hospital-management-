using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>CSSD: instruments, instrument sets, sterilisation batches (failed cycles are never released), issue and return of sets.</summary>
[HasPermission(Perm.OtManageCssd)]
public class CssdModel : OtPageModel
{
    [BindProperty(SupportsGet = true, Name = "inst")] public int? EditInstrument { get; set; }
    [BindProperty(SupportsGet = true, Name = "set")] public int? EditSet { get; set; }
    [BindProperty] public string? Code { get; set; }
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Category { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public int? TheatreId { get; set; }
    [BindProperty] public bool Active { get; set; } = true;
    /// <summary>Set contents – read from the "Qty[instrumentId]" form fields (not model-bound, so other forms are not affected).</summary>
    public Dictionary<int, int> Qty { get; set; } = new();
    [BindProperty] public CssdBatchInput Batch { get; set; } = new();
    [BindProperty] public long ItemId { get; set; }
    [BindProperty] public bool Passed { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public DateOnly? Expiry { get; set; }
    [BindProperty] public DateTime? EndLocal { get; set; }
    [BindProperty] public string? IssuedTo { get; set; }

    public IReadOnlyList<CssdInstrument> Instruments { get; private set; } = Array.Empty<CssdInstrument>();
    public IReadOnlyList<CssdSet> Sets { get; private set; } = Array.Empty<CssdSet>();
    public IReadOnlyList<CssdBatch> Batches { get; private set; } = Array.Empty<CssdBatch>();
    public IReadOnlyList<CssdIssue> Issues { get; private set; } = Array.Empty<CssdIssue>();
    public IReadOnlyList<OtTheatre> Theatres { get; private set; } = Array.Empty<OtTheatre>();
    public DateOnly Today { get; private set; }

    private async Task LoadAsync()
    {
        Nav("ot-cssd", "cssd");
        Today = await Clock.TodayAsync();
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        Instruments = await db.CssdInstruments.AsNoTracking().OrderBy(i => i.Name).ToListAsync();
        Sets = await db.CssdSets.AsNoTracking().Include(s => s.Items).ThenInclude(i => i.Instrument).OrderBy(s => s.Code).ToListAsync();
        Batches = await db.CssdBatches.AsNoTracking().Include(b => b.Sets).ThenInclude(s => s.Set).OrderByDescending(b => b.Id).Take(40).ToListAsync();
        Issues = await db.CssdIssues.AsNoTracking().Include(i => i.Set).Where(i => i.ReturnedUtc == null).OrderBy(i => i.IssuedUtc).ToListAsync();
        Theatres = await Setup.TheatresAsync(activeOnly: true);
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Instruments.FirstOrDefault(i => i.Id == EditInstrument) is { } ins) { Code = ins.Code; Name = ins.Name; Category = ins.Category; Active = ins.IsActive; }
        if (Sets.FirstOrDefault(s => s.Id == EditSet) is { } set)
        {
            Code = set.Code; Name = set.Name; Description = set.Description; TheatreId = set.TheatreId; Active = set.IsActive;
            Qty = set.Items.ToDictionary(i => i.InstrumentId, i => i.Quantity);
        }
    }

    private async Task<IActionResult> DoneAsync(Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostInstrumentAsync() => await DoneAsync(await Cssd.SaveInstrumentAsync(EditInstrument, Code, Name, Category, Active), "Instrument saved.");
    public async Task<IActionResult> OnPostSetAsync() => await DoneAsync(await Cssd.SaveSetAsync(EditSet, Code, Name, Description, TheatreId, Active, FormDictionary("Qty").Where(q => int.TryParse(q.Value, out var n) && n > 0).ToDictionary(q => q.Key, q => int.Parse(q.Value))), "Instrument set saved.");
    public async Task<IActionResult> OnPostBatchAsync() => await DoneAsync(await Cssd.StartBatchAsync(Batch), "Sterilisation batch started.");
    public async Task<IActionResult> OnPostResultAsync() => await DoneAsync(await Cssd.BatchResultAsync(ItemId, Passed, Reason, Expiry, EndLocal), Passed ? "Cycle recorded as passed – release it to make the sets available." : "Cycle recorded as FAILED – the sets stay non-sterile.");
    public async Task<IActionResult> OnPostReleaseAsync() => await DoneAsync(await Cssd.ReleaseAsync(ItemId), "Batch released – sets are sterile.");
    public async Task<IActionResult> OnPostIssueAsync() => await DoneAsync(await Cssd.IssueAsync((int)ItemId, null, IssuedTo), "Set issued.");
    public async Task<IActionResult> OnPostReturnAsync() => await DoneAsync(await Cssd.ReturnAsync(ItemId, Reason), "Set returned – it must be reprocessed before use.");
    public async Task<IActionResult> OnPostQuarantineAsync() => await DoneAsync(await Cssd.QuarantineAsync((int)ItemId, Reason), "Set quarantined.");

    private Dictionary<int, string> FormDictionary(string name) =>
        Request.Form.Keys.Where(k => k.StartsWith(name + "[") && k.EndsWith("]"))
            .Select(k => (Ok: int.TryParse(k[(name.Length + 1)..^1], out var id), Id: id, Value: Request.Form[k].ToString()))
            .Where(x => x.Ok).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Last().Value);
}
