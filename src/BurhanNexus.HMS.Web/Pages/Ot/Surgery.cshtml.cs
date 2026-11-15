using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Pages.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>
/// The surgery record: booking &amp; team, pre-operative assessment &amp; checklist, consent, anesthesia, surgical safety checklist,
/// intra-operative record &amp; operation note, specimens, OT stock consumption, recovery, post-operative care, billing and history.
/// Every action checks its own permission on the server.
/// </summary>
[HasPermission(Perm.OtDashboard, Perm.OtCreateBooking, Perm.OtEditBooking, Perm.OtViewPreOp, Perm.OtEnterPreOp, Perm.OtManageConsent, Perm.OtManageTeam,
    Perm.OtEnterAnesthesia, Perm.OtEnterIntraOp, Perm.OtCompleteChecklist, Perm.OtRecordConsumption, Perm.OtManageRecovery, Perm.OtEnterPostOp, Perm.OtViewBilling)]
public class SurgeryModel : OtPageModel
{
    private readonly HmsDbContext _db;
    public SurgeryModel(HmsDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "tab")] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true, Name = "correct")] public long? CorrectId { get; set; }

    [BindProperty] public BookingInput Edit { get; set; } = new();
    [BindProperty] public TeamInput Team { get; set; } = new();
    [BindProperty] public ConsentInput Consent { get; set; } = new();
    [BindProperty] public DrugInput Drug { get; set; } = new();
    [BindProperty] public NoteInput Note { get; set; } = new();
    [BindProperty] public SpecimenInput Specimen { get; set; } = new();
    [BindProperty] public RecoveryInput Recovery { get; set; } = new();
    [BindProperty] public RecordInput Record { get; set; } = new();
    [BindProperty] public Dictionary<int, ChecklistAnswer> Answers { get; set; } = new();
    [BindProperty] public Dictionary<int, string?> Remarks { get; set; } = new();
    [BindProperty] public ChecklistStage Stage { get; set; }
    [BindProperty] public bool Exception { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public long? ItemId { get; set; }
    [BindProperty] public SurgeryStatus NewStatus { get; set; }
    [BindProperty] public ConsentStatus NewConsentStatus { get; set; }
    [BindProperty] public SpecimenStatus NewSpecimenStatus { get; set; }
    [BindProperty] public int TheatreId { get; set; }
    [BindProperty] public DateTime? StartLocal { get; set; }
    [BindProperty] public DateTime? EndLocal { get; set; }
    [BindProperty] public int Duration { get; set; }
    [BindProperty] public string? Provider { get; set; }
    [BindProperty] public string? By { get; set; }
    [BindProperty] public int MedicineId { get; set; }
    [BindProperty] public int Quantity { get; set; } = 1;
    [BindProperty] public long? BatchId { get; set; }
    [BindProperty] public int? ServiceItemId { get; set; }
    [BindProperty] public decimal Discount { get; set; }
    [BindProperty] public List<OtChargeKind> Kinds { get; set; } = new();
    [BindProperty] public string DocKind { get; set; } = "Consent";
    [BindProperty] public string? DocTitle { get; set; }
    [BindProperty] public long? ConsentId { get; set; }
    [BindProperty] public IFormFile? Upload { get; set; }

    public Surgery S { get; private set; } = null!;
    public Patient P => S.Patient!;
    public string AgeText { get; private set; } = "—";
    public bool IsPrivate { get; private set; }
    public IReadOnlyList<SurgeryTeamMember> TeamMembers { get; private set; } = Array.Empty<SurgeryTeamMember>();
    public IReadOnlyList<SurgeryEvent> Events { get; private set; } = Array.Empty<SurgeryEvent>();
    public IReadOnlyList<SurgeryChecklistStage> Stages { get; private set; } = Array.Empty<SurgeryChecklistStage>();
    public Dictionary<ChecklistStage, IReadOnlyList<OtChecklistItem>> Items { get; } = new();
    public Dictionary<int, SurgeryChecklistEntry> Latest { get; private set; } = new();
    public IReadOnlyList<SurgeryConsent> Consents { get; private set; } = Array.Empty<SurgeryConsent>();
    public IReadOnlyList<OtConsentType> ConsentTypes { get; private set; } = Array.Empty<OtConsentType>();
    public IReadOnlyList<OtDocument> Documents { get; private set; } = Array.Empty<OtDocument>();
    public IReadOnlyList<OtDrugEntry> Drugs { get; private set; } = Array.Empty<OtDrugEntry>();
    public IReadOnlyList<OperativeNote> Notes { get; private set; } = Array.Empty<OperativeNote>();
    public IReadOnlyList<SurgerySpecimen> Specimens { get; private set; } = Array.Empty<SurgerySpecimen>();
    public IReadOnlyList<OtConsumption> Consumption { get; private set; } = Array.Empty<OtConsumption>();
    public RecoveryEpisode? RecoveryEpisode { get; private set; }
    public Dictionary<RecordKind, IReadOnlyList<RecordView>> Records { get; } = new();
    public Dictionary<RecordKind, IReadOnlyList<ClinicalFormField>> Fields { get; } = new();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<User> Users { get; private set; } = Array.Empty<User>();
    public IReadOnlyList<OtTheatre> Theatres { get; private set; } = Array.Empty<OtTheatre>();
    public IReadOnlyList<(StockBalance Balance, string Name)> Stock { get; private set; } = Array.Empty<(StockBalance, string)>();
    public IReadOnlyList<OtBillLine> Bill { get; private set; } = Array.Empty<OtBillLine>();
    public IReadOnlyList<(OtChargeKind Kind, ServiceItem Item)> Pending { get; private set; } = Array.Empty<(OtChargeKind, ServiceItem)>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public IReadOnlyList<(DateTime Utc, string Kind, string Number, string Name, string Status, string Link)> Investigations { get; private set; } = Array.Empty<(DateTime, string, string, string, string, string)>();
    public IReadOnlyList<LabOrder> LabOrders { get; private set; } = Array.Empty<LabOrder>();
    public IReadOnlyList<CssdIssue> CssdIssues { get; private set; } = Array.Empty<CssdIssue>();
    public IReadOnlyList<CssdSet> SterileSets { get; private set; } = Array.Empty<CssdSet>();
    public string Url(string tab) => $"/Ot/Surgery/{Id}?tab={tab}";
    public bool Closed => S.Status is SurgeryStatus.Cancelled;

    public static RecordKind[] KindsOf(string? tab) => tab switch
    {
        "preop" => new[] { RecordKind.PreOpAssessment },
        "anesthesia" => new[] { RecordKind.AnesthesiaAssessment, RecordKind.IntraOpMonitoring },
        "recovery" => new[] { RecordKind.PacuObservation },
        "postop" => new[] { RecordKind.PostOpRecord },
        _ => Array.Empty<RecordKind>(),
    };

    private async Task<bool> LoadAsync()
    {
        Nav("ot-dashboard", "dashboard");
        Tab ??= "overview";
        var s = await Query.SurgeryAsync(Id);
        if (s is null) return false;
        S = s;
        var today = await Clock.TodayAsync();
        AgeText = AgeCalculator.Of(P, today)?.ToString() ?? "—";
        IsPrivate = await Setup.IsPrivateAsync();
        TeamMembers = await Query.TeamAsync(Id, includeRemoved: Tab == "history" || Tab == "overview");
        Stages = await _db.SurgeryChecklistStages.AsNoTracking().Where(x => x.SurgeryId == Id).ToListAsync();
        RecoveryEpisode = await _db.RecoveryEpisodes.AsNoTracking().FirstOrDefaultAsync(r => r.SurgeryId == Id);
        Consents = await _db.SurgeryConsents.AsNoTracking().Where(c => c.SurgeryId == Id).OrderBy(c => c.Id).ToListAsync();
        Notes = await _db.OperativeNotes.AsNoTracking().Where(n => n.SurgeryId == Id).OrderBy(n => n.Version).ToListAsync();
        if (Tab is "overview" or "history") Events = await _db.SurgeryEvents.AsNoTracking().Where(e => e.SurgeryId == Id).OrderByDescending(e => e.Id).Take(500).ToListAsync();
        if (Tab is "overview")
        {
            Doctors = await _db.Doctors.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
            Users = await _db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            Theatres = await Setup.TheatresAsync(activeOnly: true);
            CssdIssues = await _db.CssdIssues.AsNoTracking().Include(i => i.Set).Where(i => i.SurgeryId == Id).ToListAsync();
            SterileSets = await _db.CssdSets.AsNoTracking().Where(x => x.Status == CssdSetStatus.Sterile && x.IsActive).OrderBy(x => x.Code).ToListAsync();
        }
        if (Tab is "preop" or "safety")
        {
            foreach (var st in Tab == "preop" ? new[] { ChecklistStage.PreOp } : new[] { ChecklistStage.SignIn, ChecklistStage.TimeOut, ChecklistStage.SignOut })
                Items[st] = await Setup.ChecklistAsync(st, activeOnly: true);
            Latest = (await _db.SurgeryChecklistEntries.AsNoTracking().Where(e => e.SurgeryId == Id).OrderBy(e => e.Id).ToListAsync())
                .GroupBy(e => e.ItemId).ToDictionary(g => g.Key, g => g.Last());
        }
        if (Tab == "preop")
        {
            var lab = await _db.LabOrders.AsNoTracking().Where(o => o.PatientId == S.PatientId).OrderByDescending(o => o.Id).Take(30).ToListAsync();
            var rad = await _db.RadiologyOrders.AsNoTracking().Where(o => o.PatientId == S.PatientId).OrderByDescending(o => o.Id).Take(30).ToListAsync();
            Investigations = lab.Select(o => (o.BookedUtc, "Laboratory", o.OrderNumber, "Laboratory order", "", $"/Lab/Order/{o.Id}"))
                .Concat(rad.Select(o => (o.BookedUtc, "Radiology", o.OrderNumber, "X-ray / ultrasound order", "", $"/Radiology/Order/{o.Id}")))
                .OrderByDescending(x => x.Item1).ToList();
        }
        if (Tab is "consent" or "postop" or "documents") Documents = await _db.OtDocuments.AsNoTracking().Where(d => d.SurgeryId == Id).OrderBy(d => d.Id).ToListAsync();
        if (Tab == "consent") ConsentTypes = await Setup.ConsentTypesAsync(activeOnly: true);
        if (Tab is "anesthesia" or "intraop" or "recovery") Drugs = await _db.OtDrugEntries.AsNoTracking().Where(d => d.SurgeryId == Id).OrderBy(d => d.GivenUtc).ToListAsync();
        if (Tab == "intraop")
        {
            Specimens = await _db.SurgerySpecimens.AsNoTracking().Include(x => x.LabOrder).Where(x => x.SurgeryId == Id).OrderBy(x => x.Id).ToListAsync();
            LabOrders = await _db.LabOrders.AsNoTracking().Where(o => o.PatientId == S.PatientId).OrderByDescending(o => o.Id).Take(20).ToListAsync();
        }
        if (Tab == "stock")
        {
            Consumption = await _db.OtConsumptions.AsNoTracking().Where(c => c.SurgeryId == Id).OrderBy(c => c.Id).ToListAsync();
            if (S.Theatre!.StockLocationId is int loc)
            {
                var bal = await _db.StockBalances.AsNoTracking().Include(b => b.Batch).ThenInclude(b => b!.Medicine)
                    .Where(b => b.LocationId == loc && b.Quantity > 0 && b.Batch!.ExpiryDate >= today).OrderBy(b => b.Batch!.Medicine!.Name).ThenBy(b => b.Batch!.ExpiryDate).ToListAsync();
                Stock = bal.Select(b => (b, b.Batch!.Medicine!.Name)).ToList();
            }
        }
        if (Tab == "billing" && await CanAsync(Perm.OtViewBilling))
        {
            Bill = await Billing.BillAsync(Id);
            Pending = await Billing.PendingStandardAsync(Id);
            Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true))
                .Where(x => x.Item.Kind is ServiceKind.Procedure or ServiceKind.Inpatient or ServiceKind.Consultation or ServiceKind.Other).ToList();
        }
        foreach (var k in KindsOf(Tab))
        {
            Records[k] = (await Query.RecordsAsync(Id, new[] { k })).ToList();
            Fields[k] = await HttpContext.RequestServices.GetRequiredService<IIpdSetupService>().FieldsAsync(k, activeOnly: true);
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Edit = new BookingInput { ProcedureName = S.ProcedureName, Diagnosis = S.Diagnosis, Site = S.Site, AnesthesiaType = S.AnesthesiaType, AnesthesiaDetail = S.AnesthesiaDetail, ClinicalNotes = S.ClinicalNotes, DepartmentId = S.DepartmentId };
        TheatreId = S.TheatreId;
        Duration = S.DurationMinutes;
        var current = Notes.LastOrDefault();
        if (current is not null)
            Note = new NoteInput
            {
                Positioning = current.Positioning, Site = current.Site, ProcedurePerformed = current.ProcedurePerformed, OperativeFindings = current.OperativeFindings, Technique = current.Technique,
                BloodLossMl = current.BloodLossMl, IvFluids = current.IvFluids, DrugsAdministered = current.DrugsAdministered, Specimens = current.Specimens, Implants = current.Implants,
                Complications = current.Complications, AdditionalProcedures = current.AdditionalProcedures, Observations = current.Observations, PreOpDiagnosis = current.PreOpDiagnosis,
                PostOpDiagnosis = current.PostOpDiagnosis, PostOpInstructions = current.PostOpInstructions, SurgeonNotes = current.SurgeonNotes, SurgeonName = current.SurgeonName,
            };
        else
            Note = new NoteInput { PreOpDiagnosis = S.Diagnosis, ProcedurePerformed = S.ProcedureName, Site = S.Site, SurgeonName = TeamMembers.FirstOrDefault(m => m.Role == OtRole.PrimarySurgeon && m.RemovedUtc == null)?.Name };
        return Page();
    }

    /// <summary>Common POST pipeline: permission → surgery exists → action → back to the tab (or the page with errors).</summary>
    private async Task<IActionResult> RunAsync(string permission, string tab, Func<Task<ServiceResult>> action, string ok)
    {
        if (await DenyUnlessAsync(permission) is { } deny) return deny;
        if (!await _db.Surgeries.AnyAsync(s => s.Id == Id)) return NotFound();
        var r = await action();
        Tab = tab;
        if (r.Succeeded) { FlashSuccess = ok; return Redirect(Url(tab)); }
        if (!await LoadAsync()) return NotFound();
        AddErrors(r);
        return Page();
    }

    private Task<bool> CanAuthorizeAsync() => CanAsync(Perm.OtAuthorizeException);

    // ---- booking / team
    public Task<IActionResult> OnPostEditAsync() => RunAsync(Perm.OtEditBooking, "overview", () => Surgeries.EditAsync(Id, Edit), "Booking details saved.");
    public Task<IActionResult> OnPostRescheduleAsync() => RunAsync(Perm.OtEditBooking, "overview", () => StartLocal is DateTime s ? Surgeries.RescheduleAsync(Id, TheatreId, s, Duration, Reason) : Task.FromResult(ServiceResult.Fail("Enter the new start time.")), "Surgery rescheduled.");
    public async Task<IActionResult> OnPostStatusAsync()
    {
        var perm = NewStatus is SurgeryStatus.Cancelled or SurgeryStatus.Postponed ? Perm.OtCancelSurgery : NewStatus == SurgeryStatus.Ready ? Perm.OtEnterPreOp : Perm.OtEditBooking;
        return await RunAsync(perm, "overview", () => Surgeries.SetStatusAsync(Id, NewStatus, Reason), $"Status changed to {SurgeryService.Label(NewStatus)}.");
    }
    public Task<IActionResult> OnPostTeamAsync() => RunAsync(Perm.OtManageTeam, "overview", () => Surgeries.AddTeamMemberAsync(Id, Team), "Team member added.");
    public Task<IActionResult> OnPostRemoveMemberAsync() => RunAsync(Perm.OtManageTeam, "overview", () => Surgeries.RemoveTeamMemberAsync(ItemId ?? 0, Reason), "Team member removed (kept in history).");
    public Task<IActionResult> OnPostMemberTimesAsync() => RunAsync(Perm.OtManageTeam, "overview", () => Surgeries.TeamTimesAsync(ItemId ?? 0, StartLocal, EndLocal), "Participation times saved.");
    public Task<IActionResult> OnPostCssdIssueAsync() => RunAsync(Perm.OtManageCssd, "overview", () => Cssd.IssueAsync((int)(ItemId ?? 0), Id, By), "Instrument set issued to this surgery.");

    // ---- records (pre-op, anesthesia, monitoring, PACU, post-op)
    public async Task<IActionResult> OnPostRecordAsync()
    {
        var kind = Record.Kind;
        var tab = kind switch { RecordKind.PreOpAssessment => "preop", RecordKind.PacuObservation => "recovery", RecordKind.PostOpRecord => "postop", _ => "anesthesia" };
        if (KindsOf(tab).Length == 0 || !KindsOf(tab).Contains(kind)) return BadRequest();
        if (Record.CorrectsRecordId is not null && await DenyUnlessAsync(Perm.IpdCorrectRecords) is { } deny) return deny;
        return await RunAsync(IpdPageModel.WritePermission(kind), tab, async () =>
        {
            var s = await _db.Surgeries.AsNoTracking().FirstAsync(x => x.Id == Id);
            if (s.Status == SurgeryStatus.Cancelled && Record.CorrectsRecordId is null) return ServiceResult.Fail("This surgery was cancelled.");
            Record.PatientId = s.PatientId;
            Record.SurgeryId = Id;
            Record.AdmissionId = s.IpdAdmissionId;
            Record.PregnancyId = null;
            return await HttpContext.RequestServices.GetRequiredService<IIpdService>().AddRecordAsync(Record);
        }, Record.CorrectsRecordId is null ? "Saved." : "Correction saved (the original is kept).");
    }

    // ---- checklists
    public async Task<IActionResult> OnPostChecklistAsync()
    {
        var perm = Stage == ChecklistStage.PreOp ? Perm.OtEnterPreOp : Perm.OtCompleteChecklist;
        var tab = Stage == ChecklistStage.PreOp ? "preop" : "safety";
        return await RunAsync(perm, tab, () => Clinical.AnswerChecklistAsync(Id, Stage, Answers, Remarks), "Checklist saved.");
    }
    public async Task<IActionResult> OnPostCompleteStageAsync()
    {
        var perm = Stage == ChecklistStage.PreOp ? Perm.OtEnterPreOp : Perm.OtCompleteChecklist;
        var tab = Stage == ChecklistStage.PreOp ? "preop" : "safety";
        var canAuth = await CanAuthorizeAsync();
        return await RunAsync(perm, tab, async () =>
        {
            var a = await Clinical.AnswerChecklistAsync(Id, Stage, Answers, Remarks);
            return a.Succeeded ? await Clinical.CompleteStageAsync(Id, Stage, Reason, Exception, Reason, canAuth) : a;
        }, $"{Stage} completed.");
    }

    // ---- consent & documents
    public Task<IActionResult> OnPostConsentAsync() => RunAsync(Perm.OtManageConsent, "consent", async () => await Clinical.AddConsentAsync(Id, Consent), "Consent documented.");
    public Task<IActionResult> OnPostConsentStatusAsync() => RunAsync(Perm.OtManageConsent, "consent", () => Clinical.SetConsentStatusAsync(ItemId ?? 0, NewConsentStatus, Reason), "Consent status changed.");
    public async Task<IActionResult> OnPostUploadAsync()
    {
        var perm = DocKind == "Consent" ? Perm.OtManageConsent : DocKind == "PostOpInstruction" ? Perm.OtEnterPostOp : Perm.OtEnterIntraOp;
        var tab = DocKind == "Consent" ? "consent" : "postop";
        return await RunAsync(perm, tab, async () =>
        {
            if (Upload is null || Upload.Length == 0) return ServiceResult.Fail("Choose the scanned file.");
            await using var stream = Upload.OpenReadStream();
            return await Clinical.UploadAsync(Id, ConsentId, DocKind, DocTitle, Upload.FileName, stream);
        }, "Document uploaded.");
    }

    // ---- anesthesia & intra-op
    public Task<IActionResult> OnPostAnesthesiaTimesAsync() => RunAsync(Perm.OtEnterAnesthesia, "anesthesia", () => Clinical.AnesthesiaTimesAsync(Id, StartLocal, EndLocal, Provider), "Anesthesia times saved.");
    public Task<IActionResult> OnPostDrugAsync() => RunAsync(Drug.Phase == "Recovery" ? Perm.OtManageRecovery : Drug.Phase == "IntraOp" ? Perm.OtEnterIntraOp : Perm.OtEnterAnesthesia,
        Drug.Phase == "Recovery" ? "recovery" : Drug.Phase == "IntraOp" ? "intraop" : "anesthesia", async () => await Clinical.AddDrugAsync(Id, Drug), "Recorded.");
    public async Task<IActionResult> OnPostStartAsync()
    {
        var canAuth = await CanAuthorizeAsync();
        return await RunAsync(Perm.OtEnterIntraOp, "intraop", () => Clinical.StartAsync(Id, StartLocal, Exception, Reason, canAuth), "Surgery started.");
    }
    public async Task<IActionResult> OnPostEndAsync()
    {
        var canAuth = await CanAuthorizeAsync();
        return await RunAsync(Perm.OtEnterIntraOp, "intraop", () => Clinical.EndAsync(Id, EndLocal, Exception, Reason, canAuth), "Surgery ended – patient handed to recovery.");
    }
    public Task<IActionResult> OnPostNoteAsync() => RunAsync(Perm.OtEnterIntraOp, "intraop", async () => await Clinical.SaveNoteAsync(Id, Note), "Draft saved.");
    public Task<IActionResult> OnPostFinalizeAsync() => RunAsync(Perm.OtFinalizeNotes, "intraop", () => Clinical.FinalizeNoteAsync(Id), "Operation note finalized.");
    public Task<IActionResult> OnPostAmendAsync() => RunAsync(Perm.OtFinalizeNotes, "intraop", async () => await Clinical.AmendNoteAsync(Id, Note, Reason), "Amendment draft created – finalize it when complete.");
    public Task<IActionResult> OnPostSpecimenAsync() => RunAsync(Perm.OtEnterIntraOp, "intraop", async () => await Clinical.AddSpecimenAsync(Id, Specimen), "Specimen recorded.");
    public Task<IActionResult> OnPostSpecimenStatusAsync() => RunAsync(Perm.OtEnterIntraOp, "intraop", () => Clinical.SpecimenStatusAsync(ItemId ?? 0, NewSpecimenStatus, By, Reason), "Specimen updated.");

    // ---- stock
    public async Task<IActionResult> OnPostConsumeAsync()
    {
        var canBatch = await CanAsync(Perm.PharmSelectBatch);
        return await RunAsync(Perm.OtRecordConsumption, "stock", () => Clinical.ConsumeAsync(Id, MedicineId, Quantity, BatchId, Reason, canBatch), "Consumption recorded – OT stock updated.");
    }
    public Task<IActionResult> OnPostReturnAsync() => RunAsync(Perm.OtRecordConsumption, "stock", () => Clinical.ReturnAsync(ItemId ?? 0, Quantity, Reason), "Unused quantity returned to OT stock.");

    // ---- recovery
    public Task<IActionResult> OnPostRecoveryAsync() => RunAsync(Perm.OtManageRecovery, "recovery", () => Clinical.RecoveryAsync(Id, Recovery), "Recovery updated.");

    // ---- billing
    public Task<IActionResult> OnPostPostChargesAsync() => RunAsync(Perm.OtManageCharges, "billing", async () => await Billing.PostStandardAsync(Id, Kinds), "OT charges posted to the bill.");
    public async Task<IActionResult> OnPostChargeAsync()
    {
        var canDiscount = await CanAsync(Perm.FinanceDiscount);
        return await RunAsync(Perm.OtManageCharges, "billing", () => Billing.AddChargeAsync(Id, ServiceItemId ?? 0, Quantity, Discount, Reason, canDiscount), "Charge added.");
    }
}
