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

    public LabOrder Order { get; private set; } = null!;
    public LabOrderItem? Current { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanEnter { get; private set; }

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
        Current = Item is long iid ? o.Items.FirstOrDefault(i => i.Id == iid)
            : o.Items.FirstOrDefault(i => LabService.OpenStatuses.Contains(i.Status) && i.Status != LabOrderItemStatus.SamplePending)
              ?? o.Items.FirstOrDefault(i => LabService.OpenStatuses.Contains(i.Status))
              ?? o.Items.FirstOrDefault(i => i.Status == LabOrderItemStatus.AwaitingVerification)
              ?? o.Items.FirstOrDefault();
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
        var r = await _lab.SaveResultAsync(item.Id, Values, Comment, submit, RowVersion);
        if (!r.Succeeded)
        {
            await LoadAsync(id);
            AddErrors(r);
            return Page();
        }
        FlashSuccess = submit ? $"{item.TestName}: result saved – sent for verification." : $"{item.TestName}: draft saved.";
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

    public async Task<IActionResult> OnPostCollectAsync(long id, long sampleId)
    {
        if (await DenyUnlessAsync(Perm.LabCollectSamples) is { } deny) return deny;
        var r = await _lab.CollectSampleAsync(sampleId, alsoReceive: true);
        if (r.Succeeded) FlashSuccess = "Sample collected and received."; else FlashError = r.ErrorText;
        return Redirect($"/Lab/Entry/{id}" + (Item is long i ? $"?item={i}" : ""));
    }
}
