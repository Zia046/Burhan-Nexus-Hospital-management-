using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab.Verify;

/// <summary>
/// Patient-based verification: every result of the visit with flags and reference ranges.
/// Single test → "Verify Results"; all completed tests at once → "Approve Final Reports" (bulk).
/// The person who entered a result cannot verify it unless the hospital allows it and the user has "Verify Own Results".
/// </summary>
[HasPermission(Perm.LabVerifyResults, Perm.LabApproveReports)]
public class OrderModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly ILabCatalogService _catalog;
    public OrderModel(ILabService lab, ILabCatalogService catalog) { _lab = lab; _catalog = catalog; }

    public LabOrder Order { get; private set; } = null!;
    public bool CanSingle { get; private set; }
    public bool CanBulk { get; private set; }
    public bool CanOwn { get; private set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public List<long> ItemIds { get; set; } = new();

    public bool IsOwn(LabOrderItem i) => i.ResultEnteredByUserId == CurrentUserId && !CanOwn;

    private async Task<bool> LoadAsync(long id)
    {
        ViewData["Nav"] = "lab-verify";
        ViewData["LabTab"] = "verify";
        var o = await _lab.OrderAsync(id);
        if (o is null) return false;
        Order = o;
        CanSingle = await CanAsync(Perm.LabVerifyResults);
        CanBulk = await CanAsync(Perm.LabApproveReports);
        CanOwn = (await _catalog.SettingsAsync()).AllowSelfVerification && await CanAsync(Perm.LabVerifyOwnResults);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ? Page() : NotFound();

    private async Task<IActionResult> AfterAsync(long id, Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded)
        {
            FlashSuccess = ok;
            var o = await _lab.OrderAsync(id);
            return o is not null && o.Items.Any(i => i.Status == LabOrderItemStatus.AwaitingVerification) ? RedirectToPage(new { id }) : Redirect("/Lab/Verify");
        }
        if (!await LoadAsync(id)) return NotFound();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostVerifyAsync(long id, long itemId)
    {
        if (await DenyUnlessAsync(Perm.LabVerifyResults) is { } deny) return deny;
        var item = await _lab.ItemAsync(itemId);
        if (item is null || item.LabOrderId != id) return NotFound();
        var r = await _lab.VerifyAsync(new[] { itemId }, await OwnAllowedAsync());
        return await AfterAsync(id, r, $"{item.TestName} verified.");
    }

    public async Task<IActionResult> OnPostVerifyAllAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.LabApproveReports) is { } deny) return deny;
        var o = await _lab.OrderAsync(id);
        if (o is null) return NotFound();
        var ids = (ItemIds.Count > 0 ? o.Items.Where(i => ItemIds.Contains(i.Id)) : o.Items)
            .Where(i => i.Status == LabOrderItemStatus.AwaitingVerification).Select(i => i.Id).ToList();
        var r = await _lab.VerifyAsync(ids, await OwnAllowedAsync());
        return await AfterAsync(id, r, r.Succeeded ? $"{r.Value} result(s) verified." : "");
    }

    public async Task<IActionResult> OnPostRejectAsync(long id, long itemId)
    {
        if (await DenyUnlessAsync(Perm.LabVerifyResults) is { } deny) return deny;
        var item = await _lab.ItemAsync(itemId);
        if (item is null || item.LabOrderId != id) return NotFound();
        return await AfterAsync(id, await _lab.RejectResultAsync(itemId, Reason), $"{item.TestName} sent back to the technician for correction.");
    }

    private async Task<bool> OwnAllowedAsync() => await CanAsync(Perm.LabVerifyOwnResults);
}
