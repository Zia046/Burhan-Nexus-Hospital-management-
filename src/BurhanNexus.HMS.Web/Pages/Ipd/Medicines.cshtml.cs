using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>Update 9: nursing medicine demand for one admitted patient – request, follow up, cancel.</summary>
[HasPermission(Perm.IpdRequestMedicines)]
public class MedicinesModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    private readonly IMedicineRequestService _req;
    public MedicinesModel(HmsDbContext db, IMedicineRequestService req) { _db = db; _req = req; }

    public const int Rows = 8;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public MedicineRequestInput Input { get; set; } = new();
    [BindProperty] public string? Reason { get; set; }

    public IpdAdmission A { get; private set; } = null!;
    public bool Active { get; private set; }
    public IReadOnlyList<(int Id, string Name)> Medicines { get; private set; } = Array.Empty<(int, string)>();
    public IReadOnlyList<MedicineRequestRow> Requests { get; private set; } = Array.Empty<MedicineRequestRow>();
    public Dictionary<long, MedicineRequest> Details { get; } = new();

    private async Task<IActionResult?> LoadAsync()
    {
        Nav("ipd-admissions", "admissions");
        var a = await Query.AdmissionAsync(Id);
        if (a is null) return NotFound();
        if (!await CanAccessAsync(a)) return Forbid();
        A = a;
        Active = a.Status is AdmissionStatus.Registered or AdmissionStatus.Admitted or AdmissionStatus.UnderTreatment or AdmissionStatus.Transferred or AdmissionStatus.DischargeRequested;
        var meds = await _db.Medicines.AsNoTracking().Include(m => m.DosageForm).Where(m => m.IsActive).OrderBy(m => m.Name).Take(3000).ToListAsync();
        Medicines = meds.Select(m => (m.Id, m.DisplayName)).ToList();
        Requests = await _req.ListAsync(false, Id, null, 100);
        foreach (var r in Requests.Take(30)) if (await _req.GetAsync(r.Id) is { } d) Details[r.Id] = d;
        if (Input.IdempotencyKey == Guid.Empty) Input.IdempotencyKey = Guid.NewGuid();
        return null;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ?? Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (await LoadAsync() is { } r0) return r0;
        Input.AdmissionId = Id;
        var r = await _req.CreateAsync(Input, await WardsAsync());
        if (!r.Succeeded)
        {
            AddErrors(r);
            return Page();
        }
        FlashSuccess = r.Value!.WasDuplicate ? $"{r.Value.Number} was already sent." : $"{r.Value.Number} sent to the pharmacy. Stock is deducted only when the pharmacy issues.";
        return Redirect($"/Ipd/Medicines/{Id}");
    }

    public async Task<IActionResult> OnPostCancelAsync(long requestId)
    {
        if (await LoadAsync() is { } r0) return r0;
        var r = await _req.CancelAsync(requestId, Reason, await WardsAsync());
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = "Request cancelled.";
        return Redirect($"/Ipd/Medicines/{Id}");
    }
}
