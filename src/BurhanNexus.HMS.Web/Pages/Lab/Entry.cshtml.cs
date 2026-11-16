using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>
/// Fast result entry: all tests of one patient visit on one screen; parameters, units and reference ranges come from the
/// booked definition, the technician only types the values. "Save &amp; next" jumps to the next open test.
/// </summary>
[HasPermission(Perm.LabEnterResults, Perm.LabEditResults)]
public class EntryModel : HmsPageModel
{
    private readonly ILabService _lab;
    public EntryModel(ILabService lab) => _lab = lab;

    [BindProperty(SupportsGet = true)] public long? Item { get; set; }
    [BindProperty] public Dictionary<long, string?> Values { get; set; } = new();
    [BindProperty] public string? Comment { get; set; }
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public string? Mode { get; set; }
    /// <summary>Update 9: antibiotic sensitivity lines of culture parameters.</summary>
    [BindProperty] public List<CultureRow> Culture { get; set; } = new();
    public Dictionary<long, List<LabCultureResult>> CultureLines { get; private set; } = new();
    /// <summary>Running index of the culture rows while the page renders.</summary>
    public int CultureIndex { get; set; }

    public sealed class CultureRow
    {
        public long ValueId { get; set; }
        public string? Organism { get; set; }
        public string? Antibiotic { get; set; }
        public SensitivityResult? Result { get; set; }
        public string? Mic { get; set; }
    }

    /// <summary>Common organisms and antibiotics offered as suggestions (free text is allowed).</summary>
    public static readonly string[] Organisms = { "No growth", "Escherichia coli", "Klebsiella pneumoniae", "Pseudomonas aeruginosa", "Staphylococcus aureus", "MRSA",
        "Coagulase-negative Staphylococcus", "Enterococcus faecalis", "Streptococcus pyogenes", "Streptococcus pneumoniae", "Proteus mirabilis", "Acinetobacter baumannii",
        "Enterobacter spp.", "Salmonella Typhi", "Salmonella Paratyphi A", "Shigella spp.", "Candida albicans", "Candida spp.", "Mixed growth (contamination)" };
    public static readonly string[] Antibiotics = { "Amikacin", "Amoxicillin-clavulanate", "Ampicillin", "Azithromycin", "Aztreonam", "Cefepime", "Cefixime", "Cefotaxime",
        "Ceftazidime", "Ceftriaxone", "Cefuroxime", "Ciprofloxacin", "Clindamycin", "Colistin", "Co-trimoxazole", "Doxycycline", "Erythromycin", "Fosfomycin", "Gentamicin",
        "Imipenem", "Levofloxacin", "Linezolid", "Meropenem", "Nitrofurantoin", "Oxacillin / Cefoxitin", "Penicillin", "Piperacillin-tazobactam", "Teicoplanin", "Tigecycline", "Vancomycin" };

    public LabOrder Order { get; private set; } = null!;
    public LabOrderItem? Current { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanEnter { get; private set; }
    /// <summary>Update 10: simple workflow (save = verified) and the "Review report" correction mode.</summary>
    public bool Simple { get; private set; }
    public bool CanReview { get; private set; }
    [BindProperty(SupportsGet = true, Name = "review")] public bool Review { get; set; }
    [BindProperty] public string? Reason { get; set; }

    private static bool Editable(LabOrderItem i) => LabService.OpenStatuses.Contains(i.Status) || i.Status == LabOrderItemStatus.AwaitingVerification;

    private async Task<bool> LoadAsync(long id)
    {
        ViewData["Nav"] = "lab-pending";
        ViewData["LabTab"] = "pending";
        var o = await _lab.OrderAsync(id);
        if (o is null) return false;
        Order = o;
        CanEnter = await CanAsync(Perm.LabEnterResults);
        CanEdit = await CanAsync(Perm.LabEditResults);
        CanReview = await CanAsync(Perm.LabReviewResults);
        Simple = (await HttpContext.RequestServices.GetRequiredService<ILabCatalogService>().SettingsAsync()).SimpleWorkflow;
        Current = Item is long iid ? o.Items.FirstOrDefault(i => i.Id == iid)
            : o.Items.FirstOrDefault(i => LabService.OpenStatuses.Contains(i.Status) && i.Status != LabOrderItemStatus.SamplePending)
              ?? o.Items.FirstOrDefault(i => LabService.OpenStatuses.Contains(i.Status))
              ?? o.Items.FirstOrDefault(i => i.Status == LabOrderItemStatus.AwaitingVerification)
              ?? o.Items.FirstOrDefault();
        if (Current is not null)
        {
            var ids = Current.Values.Where(v => v.ResultType == LabResultType.Culture).Select(v => v.Id).ToList();
            if (ids.Count > 0)
            {
                var db = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Persistence.HmsDbContext>();
                CultureLines = (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.LabCultureResults.Where(c => ids.Contains(c.ResultValueId)).OrderBy(c => c.SortOrder)))
                    .GroupBy(c => c.ResultValueId).ToDictionary(g => g.Key, g => g.ToList());
            }
        }
        if (Current is not null && !Values.Any())
        {
            Values = Current.Values.ToDictionary(v => v.Id, v => v.Value);
            Comment = Current.Comment;
            RowVersion = Current.RowVersion;
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ? Page() : NotFound();

    /// <summary>First entry needs "Enter Test Results"; changing a submitted or sent-back result needs "Edit Unverified Results".</summary>
    public static string RequiredPermission(LabOrderItem i) =>
        i.Status == LabOrderItemStatus.AwaitingVerification || i.CorrectionReason is not null ? Perm.LabEditResults : Perm.LabEnterResults;

    public async Task<IActionResult> OnPostAsync(long id)
    {
        var item = Item is long iid ? await _lab.ItemAsync(iid) : null;
        if (item is null || item.LabOrderId != id) return NotFound();
        if (await DenyUnlessAsync(RequiredPermission(item)) is { } deny) return deny;
        var submit = Mode is "submit" or "next";
        var culture = Culture.Select(c => new CultureLineInput(c.ValueId, c.Organism, c.Antibiotic, c.Result, c.Mic)).ToList();
        var r = await _lab.SaveResultAsync(item.Id, Values, culture, Comment, submit, RowVersion);
        if (!r.Succeeded)
        {
            await LoadAsync(id);
            AddErrors(r);
            return Page();
        }
        var verified = submit && (await _lab.ItemAsync(item.Id))?.Status == LabOrderItemStatus.Verified;
        FlashSuccess = verified ? $"{item.TestName}: report saved and VERIFIED – it can be printed now." : submit ? $"{item.TestName}: result saved – sent for verification." : $"{item.TestName}: draft saved.";
        if (Mode == "next")
        {
            var order = (await _lab.OrderAsync(id))!;
            var next = order.Items.Where(i => i.Id != item.Id && LabService.OpenStatuses.Contains(i.Status) && i.Status != LabOrderItemStatus.SamplePending)
                .OrderBy(i => i.SortOrder < item.SortOrder ? 1 : 0).ThenBy(i => i.SortOrder).FirstOrDefault();
            if (next is not null) return Redirect($"/Lab/Entry/{id}?item={next.Id}");
            FlashSuccess += " All available tests of this visit are entered.";
            return Redirect("/Lab/Pending");
        }
        return Redirect($"/Lab/Entry/{id}?item={item.Id}");
    }

    /// <summary>Update 10: correct a saved report (permission "Review / Edit Saved Results"); the previous result stays in history.</summary>
    public async Task<IActionResult> OnPostReviewAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.LabReviewResults) is { } deny) return deny;
        var item = Item is long iid ? await _lab.ItemAsync(iid) : null;
        if (item is null || item.LabOrderId != id) return NotFound();
        var culture = Culture.Select(c => new CultureLineInput(c.ValueId, c.Organism, c.Antibiotic, c.Result, c.Mic)).ToList();
        var r = await _lab.ReviewResultAsync(item.Id, Values, culture, Comment, Reason);
        if (!r.Succeeded)
        {
            Review = true;
            await LoadAsync(id);
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"{item.TestName}: corrected report saved (still VERIFIED). The previous result is kept in the report history.";
        return Redirect($"/Lab/Entry/{id}?item={item.Id}");
    }

    public async Task<IActionResult> OnPostCollectAsync(long id, long sampleId)
    {
        if (await DenyUnlessAsync(Perm.LabCollectSamples) is { } deny) return deny;
        var r = await _lab.CollectSampleAsync(sampleId, alsoReceive: true);
        if (r.Succeeded) FlashSuccess = "Sample collected and received."; else FlashError = r.ErrorText;
        return Redirect($"/Lab/Entry/{id}" + (Item is long i ? $"?item={i}" : ""));
    }
}
