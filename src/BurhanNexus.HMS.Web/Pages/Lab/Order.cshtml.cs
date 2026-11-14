using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>One laboratory visit: tests, samples, payment status, history and every action allowed for the user.</summary>
[HasPermission(Perm.LabViewResults, Perm.LabBook, Perm.LabCollectSamples, Perm.LabEnterResults, Perm.LabVerifyResults)]
public class OrderModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly ILabCatalogService _catalog;
    private readonly IRegistrationSetupService _registration;

    public OrderModel(ILabService lab, ILabCatalogService catalog, IRegistrationSetupService registration)
    {
        _lab = lab;
        _catalog = catalog;
        _registration = registration;
    }

    public LabOrder Order { get; private set; } = null!;
    public IReadOnlyDictionary<long, LabPayment> Payments { get; private set; } = new Dictionary<long, LabPayment>();
    public LabPayment Payment { get; private set; } = null!;
    public IReadOnlyList<LabItemEvent> Events { get; private set; } = Array.Empty<LabItemEvent>();
    public IReadOnlyList<LabResultRevision> Revisions { get; private set; } = Array.Empty<LabResultRevision>();
    public IReadOnlyList<LabReportPrint> Prints { get; private set; } = Array.Empty<LabReportPrint>();
    public IReadOnlyList<LabTestRow> Tests { get; private set; } = Array.Empty<LabTestRow>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public bool ShowFees { get; private set; }

    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public List<int> AddTestIds { get; set; } = new();
    [BindProperty] public LabReferralType ReferralType { get; set; }
    [BindProperty] public int? ReferringDoctorId { get; set; }
    [BindProperty] public string? ExternalDoctorName { get; set; }
    [BindProperty] public string? ClinicalNotes { get; set; }
    [BindProperty] public bool IsUrgent { get; set; }

    private async Task<bool> LoadAsync(long id)
    {
        ViewData["Nav"] = "lab-results";
        ViewData["LabTab"] = "results";
        var o = await _lab.OrderAsync(id);
        if (o is null) return false;
        Order = o;
        ShowFees = await CanAsync(Perm.LabViewFinancial);
        Payments = await _lab.PaymentsAsync(id);
        Payment = await _lab.OrderPaymentAsync(id);
        Events = await _lab.EventsAsync(id);
        Revisions = await _lab.RevisionsAsync(id);
        Prints = await _lab.PrintsAsync(id);
        if (await CanAsync(Perm.LabEditOrders))
        {
            Tests = (await _catalog.TestsAsync(activeOnly: true)).Where(t => o.Items.All(i => i.LabTestId != t.Test.Id || i.Status == LabOrderItemStatus.Cancelled)).ToList();
            Doctors = await _registration.DoctorsAsync(activeOnly: true);
        }
        ReferralType = o.ReferralType; ReferringDoctorId = o.ReferringDoctorId; ExternalDoctorName = o.ExternalDoctorName; ClinicalNotes = o.ClinicalNotes; IsUrgent = o.IsUrgent;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ? Page() : NotFound();

    private async Task<IActionResult> DoneAsync(long id, Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return RedirectToPage(new { id }); }
        var keep = (ReferralType, ReferringDoctorId, ExternalDoctorName, ClinicalNotes, IsUrgent);
        if (!await LoadAsync(id)) return NotFound();
        (ReferralType, ReferringDoctorId, ExternalDoctorName, ClinicalNotes, IsUrgent) = keep;
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostCollectAsync(long id, long sampleId, bool receive)
    {
        if (await DenyUnlessAsync(Perm.LabCollectSamples) is { } deny) return deny;
        return await DoneAsync(id, await _lab.CollectSampleAsync(sampleId, receive), receive ? "Sample collected and received." : "Sample collected.");
    }

    public async Task<IActionResult> OnPostReceiveAsync(long id, long sampleId)
    {
        if (await DenyUnlessAsync(Perm.LabCollectSamples) is { } deny) return deny;
        return await DoneAsync(id, await _lab.ReceiveSampleAsync(sampleId), "Sample received in the laboratory.");
    }

    public async Task<IActionResult> OnPostRejectSampleAsync(long id, long sampleId)
    {
        if (await DenyUnlessAsync(Perm.LabRejectSamples) is { } deny) return deny;
        return await DoneAsync(id, await _lab.RejectSampleAsync(sampleId, Reason), "Sample rejected – a new sample has been requested.");
    }

    public async Task<IActionResult> OnPostCancelItemAsync(long id, long itemId)
    {
        if (await DenyUnlessAsync(Perm.LabCancelOrders) is { } deny) return deny;
        return await DoneAsync(id, await _lab.CancelItemAsync(itemId, Reason), "Test cancelled.");
    }

    public async Task<IActionResult> OnPostAddTestsAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.LabEditOrders) is { } deny) return deny;
        return await DoneAsync(id, await _lab.AddTestsAsync(id, AddTestIds), "Tests added. Their fees are payable at the cash counter.");
    }

    public async Task<IActionResult> OnPostDetailsAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.LabEditOrders) is { } deny) return deny;
        return await DoneAsync(id, await _lab.UpdateOrderAsync(id, ReferralType, ReferringDoctorId, ExternalDoctorName, ClinicalNotes, IsUrgent), "Visit details saved.");
    }

    public async Task<IActionResult> OnPostWithdrawAsync(long id, long itemId)
    {
        if (await DenyUnlessAsync(Perm.LabDeleteResults) is { } deny) return deny;
        return await DoneAsync(id, await _lab.WithdrawResultAsync(itemId, Reason), "Result withdrawn. The previous version is kept as a revision; the test is back in progress for correction.");
    }
}
