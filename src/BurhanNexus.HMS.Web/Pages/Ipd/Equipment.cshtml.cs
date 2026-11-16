using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>Update 10: NICU equipment on a newborn – start, stop, replace / transfer (separate periods), charged by usage.</summary>
[HasPermission(Perm.IpdRecordEquipment)]
public class EquipmentModel : IpdPageModel
{
    private readonly IIpdBillingService _billing;
    public EquipmentModel(IIpdBillingService billing) => _billing = billing;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public EquipmentStartInput Start { get; set; } = new();
    [BindProperty] public DateTime? EndLocal { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? NewIdentifier { get; set; }
    [BindProperty] public Guid ReplaceKey { get; set; }

    public IpdAdmission A { get; private set; } = null!;
    public IReadOnlyList<NicuEquipment> Catalogue { get; private set; } = Array.Empty<NicuEquipment>();
    public IReadOnlyList<EquipmentUsage> Usages { get; private set; } = Array.Empty<EquipmentUsage>();

    private async Task<IActionResult?> LoadAsync()
    {
        Nav("ipd-nicu", "nicu");
        var a = await Query.AdmissionAsync(Id);
        if (a is null) return NotFound();
        if (!await CanAccessAsync(a)) return Forbid();
        A = a;
        Catalogue = await _billing.EquipmentCatalogueAsync(activeOnly: true);
        Usages = (await _billing.LedgerAsync(Id))!.Equipment;
        if (Start.IdempotencyKey == Guid.Empty) Start.IdempotencyKey = Guid.NewGuid();
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    private async Task<IActionResult> Done(ServiceResult r, string ok)
    {
        if (!r.Succeeded) { if (await LoadAsync() is { } x) return x; AddErrors(r); return Page(); }
        FlashSuccess = ok;
        return Redirect($"/Ipd/Equipment/{Id}");
    }

    public async Task<IActionResult> OnPostStartAsync()
    {
        Start.AdmissionId = Id;
        return await Done(await _billing.StartEquipmentAsync(Start), "Equipment started – it is charged when it is stopped.");
    }

    public async Task<IActionResult> OnPostStopAsync(long usageId) =>
        await Done(await _billing.EndEquipmentAsync(usageId, EndLocal, Reason), "Equipment stopped and charged.");

    /// <summary>Replace the device: the current period ends now and a new period starts with the new device (no overlap).</summary>
    public async Task<IActionResult> OnPostReplaceAsync(long usageId)
    {
        var u = (await _billing.LedgerAsync(Id))?.Equipment.FirstOrDefault(x => x.Id == usageId);
        if (u is null) return NotFound();
        var ended = await _billing.EndEquipmentAsync(usageId, null, "Replaced" + (string.IsNullOrWhiteSpace(Reason) ? "" : ": " + Reason));
        if (!ended.Succeeded) return await Done(ended, "");
        var started = await _billing.StartEquipmentAsync(new EquipmentStartInput { IdempotencyKey = ReplaceKey == Guid.Empty ? Guid.NewGuid() : ReplaceKey, AdmissionId = Id, EquipmentId = u.EquipmentId, Identifier = NewIdentifier, Notes = $"Replaces {u.Identifier}" });
        return await Done(started, "Equipment replaced – the old period is closed and a new one started.");
    }
}
