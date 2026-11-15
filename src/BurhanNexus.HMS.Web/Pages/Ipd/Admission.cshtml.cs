using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>
/// The inpatient chart: overview, progress notes, nursing, MAR, investigations, billing, labour / postnatal / NICU
/// documentation, transfers and discharge. Every action is checked on the server (permission + ward access).
/// </summary>
[HasPermission(Perm.IpdViewPatients)]
public class AdmissionModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    public AdmissionModel(HmsDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "tab")] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true, Name = "correct")] public long? CorrectId { get; set; }

    [BindProperty] public RecordInput Record { get; set; } = new();
    [BindProperty] public AdmitInput Edit { get; set; } = new();
    [BindProperty] public DischargeInput Discharge { get; set; } = new();
    [BindProperty] public HandoverInput Handover { get; set; } = new();
    [BindProperty] public MedicationOrderInput Order { get; set; } = new();
    [BindProperty] public DoseInput Dose { get; set; } = new();
    [BindProperty] public LabourInput Labour { get; set; } = new();
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? Note { get; set; }
    [BindProperty] public int? BedId { get; set; }
    [BindProperty] public int? DepartmentId { get; set; }
    [BindProperty] public string? Receiving { get; set; }
    [BindProperty] public long? ItemId { get; set; }
    [BindProperty] public bool Attention { get; set; }
    [BindProperty] public NursingTaskType TaskType { get; set; } = NursingTaskType.VitalSigns;
    [BindProperty] public DateTime? DueLocal { get; set; }
    [BindProperty] public int? ServiceItemId { get; set; }
    [BindProperty] public int Quantity { get; set; } = 1;
    [BindProperty] public decimal Discount { get; set; }
    [BindProperty] public LabourStatus? LabourStatusValue { get; set; }

    public IpdAdmission A { get; private set; } = null!;
    public Patient P => A.Patient!;
    public string AgeText { get; private set; } = "—";
    public bool Private { get; private set; }
    public IpdSettings Settings { get; private set; } = new();
    public IpdAdmission? Mother { get; private set; }
    public IReadOnlyList<IpdAdmission> Babies { get; private set; } = Array.Empty<IpdAdmission>();
    public LabourRecord? LabourRec { get; private set; }
    public IReadOnlyList<DeliveryRecord> Deliveries { get; private set; } = Array.Empty<DeliveryRecord>();
    public NicuDetails? Nicu { get; private set; }
    public NewbornRecord? Birth { get; private set; }
    public Pregnancy? Pregnancy { get; private set; }
    public IReadOnlyList<IpdBedAllocation> Allocations { get; private set; } = Array.Empty<IpdBedAllocation>();
    public IReadOnlyList<IpdTransfer> Transfers { get; private set; } = Array.Empty<IpdTransfer>();
    public IReadOnlyList<IpdEvent> Events { get; private set; } = Array.Empty<IpdEvent>();
    public Dictionary<RecordKind, IReadOnlyList<RecordView>> Records { get; } = new();
    public Dictionary<RecordKind, IReadOnlyList<ClinicalFormField>> Fields { get; } = new();
    public IReadOnlyList<NursingTask> Tasks { get; private set; } = Array.Empty<NursingTask>();
    public IReadOnlyList<ShiftHandover> Handovers { get; private set; } = Array.Empty<ShiftHandover>();
    public IReadOnlyList<MedicationOrder> Orders { get; private set; } = Array.Empty<MedicationOrder>();
    public IReadOnlyList<MedicationDose> Doses { get; private set; } = Array.Empty<MedicationDose>();
    public IReadOnlyList<Medicine> Medicines { get; private set; } = Array.Empty<Medicine>();
    public IReadOnlyList<BillLine> Bill { get; private set; } = Array.Empty<BillLine>();
    public IReadOnlyList<InvestigationRow> Investigations { get; private set; } = Array.Empty<InvestigationRow>();
    public IReadOnlyList<IpdBed> FreeBeds { get; private set; } = Array.Empty<IpdBed>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public bool CanSeeGynae { get; private set; }
    public bool CanSeeFinance { get; private set; }

    public bool IsMaternity => A.Kind is AdmissionKind.Labour or AdmissionKind.Gynae || LabourRec is not null || Deliveries.Count > 0;
    public bool IsBaby => A.Kind is AdmissionKind.Nicu or AdmissionKind.Newborn;
    public string Url(string tab) => $"/Ipd/Admission/{Id}?tab={tab}";

    /// <summary>Record kinds shown on each tab.</summary>
    public static RecordKind[] KindsOf(string tab) => tab switch
    {
        "notes" => new[] { RecordKind.ProgressNote, RecordKind.Vitals },
        "nursing" => new[] { RecordKind.NursingAssessment, RecordKind.NursingNote, RecordKind.Vitals },
        "gynae" => new[] { RecordKind.GynaeHistory, RecordKind.GynaeAssessment },
        "labour" => new[] { RecordKind.LabourAssessment, RecordKind.LabourObservation },
        "postnatal" => new[] { RecordKind.PostnatalAssessment },
        "nicu" => new[] { RecordKind.NicuAssessment, RecordKind.NicuObservation, RecordKind.NicuFeeding, RecordKind.NicuCare, RecordKind.NicuProgress },
        _ => Array.Empty<RecordKind>(),
    };

    private async Task<bool> LoadAsync()
    {
        Nav("ipd-patients", "patients");
        Tab ??= "overview";
        var a = await Query.AdmissionAsync(Id);
        if (a is null || !await CanAccessAsync(a)) return false;
        A = a;
        var today = await Clock.TodayAsync();
        AgeText = AgeCalculator.Of(P, today)?.ToString() ?? "—";
        Private = await Setup.IsPrivateAsync();
        Settings = await Setup.SettingsAsync();
        CanSeeGynae = await CanAsync(Perm.IpdViewGynae);
        CanSeeFinance = await CanAsync(Perm.IpdViewFinancial);
        if (A.MotherAdmissionId is long m) Mother = await _db.IpdAdmissions.AsNoTracking().Include(x => x.Patient).Include(x => x.CurrentBed).FirstOrDefaultAsync(x => x.Id == m);
        Babies = await _db.IpdAdmissions.AsNoTracking().Include(x => x.Patient).Include(x => x.CurrentBed).Include(x => x.CurrentWard)
            .Where(x => x.MotherAdmissionId == Id).OrderBy(x => x.Id).ToListAsync();
        LabourRec = await _db.LabourRecords.AsNoTracking().FirstOrDefaultAsync(l => l.AdmissionId == Id);
        Deliveries = await _db.DeliveryRecords.AsNoTracking().Include(d => d.Newborns).ThenInclude(n => n.NewbornPatient)
            .Where(d => d.AdmissionId == Id).OrderBy(d => d.Id).ToListAsync();
        Nicu = await _db.NicuDetails.AsNoTracking().FirstOrDefaultAsync(n => n.AdmissionId == Id);
        Birth = await _db.NewbornRecords.AsNoTracking().Include(n => n.Delivery).FirstOrDefaultAsync(n => n.NewbornPatientId == A.PatientId);
        var pregId = A.PregnancyId ?? LabourRec?.PregnancyId;
        if (pregId is long pid && CanSeeGynae) Pregnancy = await _db.Pregnancies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);

        Allocations = await Query.AllocationsAsync(Id);
        Transfers = await Query.TransfersAsync(Id);
        Events = await Query.EventsAsync(Id, null);
        if (Tab is "gynae" && !CanSeeGynae) Tab = "overview";
        if (Tab is "billing" && !CanSeeFinance) Tab = "overview";

        var kinds = KindsOf(Tab);
        if (kinds.Length > 0)
        {
            var recs = await Query.RecordsAsync(A.PatientId, Tab == "gynae" ? null : Id, null, kinds);
            foreach (var k in kinds)
            {
                Records[k] = recs.Where(r => r.Record.Kind == k && (Tab != "gynae" || r.Record.AdmissionId == Id || r.Record.AdmissionId == null)).ToList();
                Fields[k] = await Setup.FieldsAsync(k, activeOnly: true);
            }
        }
        if (Tab is "nursing" or "overview")
        {
            Tasks = await _db.NursingTasks.AsNoTracking().Where(t => t.AdmissionId == Id).OrderBy(t => t.Status).ThenBy(t => t.DueUtc).Take(200).ToListAsync();
            Handovers = await _db.ShiftHandovers.AsNoTracking().Where(h => h.AdmissionId == Id).OrderByDescending(h => h.CreatedUtc).Take(50).ToListAsync();
        }
        if (Tab is "mar" or "overview")
        {
            Orders = await _db.MedicationOrders.AsNoTracking().Where(o => o.AdmissionId == Id).OrderByDescending(o => o.Status == MedicationOrderStatus.Active).ThenByDescending(o => o.StartUtc).ToListAsync();
            Doses = await _db.MedicationDoses.AsNoTracking().Where(d => d.AdmissionId == Id).OrderBy(d => d.ScheduledUtc).ToListAsync();
        }
        if (Tab == "mar") Medicines = await _db.Medicines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Take(3000).ToListAsync();
        if (Tab is "billing" && CanSeeFinance)
        {
            Bill = await Query.BillAsync(Id);
            Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true))
                .Where(s => s.Item.Kind is ServiceKind.Inpatient or ServiceKind.Procedure or ServiceKind.Consultation or ServiceKind.Other).ToList();
        }
        if (Tab is "investigations" or "overview") Investigations = await Query.InvestigationsAsync(Id, A.PatientId);
        if (Tab is "transfer" or "labour" or "nicu")
        {
            FreeBeds = (await Setup.BedsAsync(null, activeOnly: true)).Where(b => b.Id != A.CurrentBedId && b.Status is BedStatus.Available or BedStatus.Reserved && b.Ward!.IsActive).ToList();
            Departments = await _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        }
        if (Tab == "overview" || Tab == "edit")
        {
            Departments = await _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Edit = new AdmitInput
        {
            DepartmentId = A.DepartmentId, ReferringDoctor = A.ReferringDoctor, AdmittingDoctorId = A.AdmittingDoctorId, AdmittingDoctorName = A.AdmittingDoctorName,
            AdmissionDiagnosis = A.AdmissionDiagnosis, InitialCondition = A.InitialCondition, MedicalHistory = A.MedicalHistory, Allergies = A.Allergies,
            AttendantName = A.AttendantName, AttendantRelation = A.AttendantRelation, AttendantPhone = A.AttendantPhone,
        };
        Discharge = new DischargeInput
        {
            Type = A.DischargeType, FinalDiagnosis = A.FinalDiagnosis ?? A.AdmissionDiagnosis, TreatmentSummary = A.TreatmentSummary, Procedures = A.Procedures,
            InvestigationSummary = A.InvestigationSummary, ConditionAtDischarge = A.ConditionAtDischarge, FollowUp = A.FollowUp,
            DischargeMedications = A.DischargeMedications, ReferredTo = A.ReferredTo, DischargeDoctor = A.DischargeDoctor ?? A.AdmittingDoctorName ?? A.AdmittingDoctor?.Name,
        };
        var zone = await Clock.ZoneAsync();
        DateTime? Local(DateTime? u) => u is DateTime x ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x, DateTimeKind.Utc), zone) : null;
        if (LabourRec is not null)
            Labour = new LabourInput
            {
                PregnancyId = LabourRec.PregnancyId, GestationalAge = LabourRec.GestationalAge, PresentingComplaints = LabourRec.PresentingComplaints, OnsetInfo = LabourRec.OnsetInfo,
                MembraneStatus = LabourRec.MembraneStatus, ClinicalHistory = LabourRec.ClinicalHistory, InitialAssessment = LabourRec.InitialAssessment, ResponsibleStaff = LabourRec.ResponsibleStaff,
                OnsetLocal = Local(LabourRec.OnsetUtc),
                MembranesRupturedLocal = Local(LabourRec.MembranesRupturedUtc),
            };
        return Page();
    }

    /// <summary>Common POST pipeline: permission → admission + ward access → action → back to the tab (or the page with errors).</summary>
    private async Task<IActionResult> RunAsync(string permission, string tab, Func<Task<ServiceResult>> action, string ok)
    {
        if (await DenyUnlessAsync(permission) is { } deny) return deny;
        var a = await Query.AdmissionAsync(Id);
        if (a is null || !await CanAccessAsync(a)) return NotFound();
        var r = await action();
        Tab = tab;
        if (r.Succeeded)
        {
            FlashSuccess = ok;
            return Redirect(Url(tab));
        }
        if (!await LoadAsync()) return NotFound();
        AddErrors(r);
        return Page();
    }

    public Task<IActionResult> OnPostEditAsync() => RunAsync(Perm.IpdEditAdmission, "overview", () => Ipd.EditAdmissionAsync(Id, Edit), "Admission details saved.");
    public Task<IActionResult> OnPostCancelAsync() => RunAsync(Perm.IpdEditAdmission, "overview", () => Ipd.CancelAdmissionAsync(Id, Reason), "Admission cancelled.");
    public Task<IActionResult> OnPostAttentionAsync() => RunAsync(Perm.IpdProgressNotes, "overview", () => Ipd.SetUnderTreatmentAsync(Id, Attention, Note), Attention ? "Patient flagged as needing attention." : "Attention flag cleared.");
    public Task<IActionResult> OnPostTransferAsync() => RunAsync(Perm.IpdTransferPatient, "transfer", () => Ipd.TransferAsync(Id, BedId, DepartmentId, Reason, Receiving), "Patient transferred.");
    public Task<IActionResult> OnPostAllocateAsync() => RunAsync(Perm.IpdAllocateBed, "transfer", () => Ipd.TransferAsync(Id, BedId, null, Reason ?? "Bed allocated", Receiving), "Bed allocated.");
    public Task<IActionResult> OnPostRequestDischargeAsync() => RunAsync(Perm.IpdRequestDischarge, "discharge", () => Ipd.RequestDischargeAsync(Id, Discharge), "Discharge requested.");
    public Task<IActionResult> OnPostDischargeAsync() => RunAsync(Perm.IpdDischargePatient, "discharge", () => Ipd.DischargeAsync(Id, Discharge), "Patient discharged. The discharge summary can be printed.");

    public async Task<IActionResult> OnPostRecordAsync()
    {
        var kind = Record.Kind;
        var tab = KindsOf("nicu").Contains(kind) ? "nicu" : KindsOf("labour").Contains(kind) ? "labour" : kind == RecordKind.PostnatalAssessment ? "postnatal"
            : KindsOf("gynae").Contains(kind) ? "gynae" : kind is RecordKind.NursingAssessment or RecordKind.NursingNote ? "nursing" : "notes";
        if (Record.CorrectsRecordId is not null && await DenyUnlessAsync(Perm.IpdCorrectRecords) is { } deny) return deny;
        if (IsSensitive(kind) && await DenyUnlessAsync(Perm.IpdViewGynae) is { } deny2) return deny2;
        return await RunAsync(WritePermission(kind), tab, async () =>
        {
            var a = (await Query.AdmissionAsync(Id))!;
            Record.PatientId = a.PatientId;
            Record.AdmissionId = Id;
            Record.PregnancyId ??= a.PregnancyId;
            if (!a.IsActive && Record.CorrectsRecordId is null) return ServiceResult.Fail("The admission is closed – new entries are not allowed (corrections only).");
            return await Ipd.AddRecordAsync(Record);
        }, Record.CorrectsRecordId is null ? "Saved." : "Correction saved (the original is kept).");
    }

    public Task<IActionResult> OnPostTaskAsync() => RunAsync(Perm.IpdManageNursing, "nursing", async () => await Ipd.AddTaskAsync(Id, TaskType, Note, DueLocal), "Task added.");
    public Task<IActionResult> OnPostCompleteTaskAsync() => RunAsync(Perm.IpdManageNursing, "nursing", () => Ipd.CompleteTaskAsync(ItemId ?? 0, Attention, Note), "Task updated.");
    public Task<IActionResult> OnPostHandoverAsync() => RunAsync(Perm.IpdHandover, "nursing", async () => await Ipd.HandoverAsync(Id, Handover), "Shift handover saved – the incoming nurse must acknowledge it.");
    public Task<IActionResult> OnPostAcknowledgeAsync() => RunAsync(Perm.IpdHandover, "nursing", () => Ipd.AcknowledgeHandoverAsync(ItemId ?? 0, Note), "Handover acknowledged.");

    public Task<IActionResult> OnPostOrderAsync() => RunAsync(Perm.IpdMedicationOrders, "mar", async () => await Ipd.OrderMedicationAsync(Id, Order), "Medication order entered and doses scheduled.");
    public Task<IActionResult> OnPostStopAsync() => RunAsync(Perm.IpdMedicationOrders, "mar", () => Ipd.StopMedicationAsync(ItemId ?? 0, Reason), "Medication stopped – future doses cancelled.");
    public Task<IActionResult> OnPostDoseAsync() => RunAsync(Perm.IpdRecordMedication, "mar", () => Ipd.RecordDoseAsync(ItemId ?? 0, Dose), "Dose recorded.");

    public Task<IActionResult> OnPostBedChargesAsync() => RunAsync(Perm.IpdManageBilling, "billing", async () => await Ipd.PostBedChargesAsync(Id, false), "Bed charges posted up to last night.");
    public async Task<IActionResult> OnPostChargeAsync()
    {
        var canDiscount = await CanAsync(Perm.FinanceDiscount);
        return await RunAsync(Perm.IpdManageBilling, "billing", () => Ipd.AddServiceChargeAsync(Id, ServiceItemId ?? 0, Quantity, Discount, Reason, canDiscount), "Charge added to the IPD bill.");
    }

    public Task<IActionResult> OnPostMoveToLabourAsync() => RunAsync(Perm.IpdManageLabour, "labour", () => Maternity.MoveToLabourAsync(Id, BedId ?? 0, Labour), "Moved to the Labour Room.");
    public async Task<IActionResult> OnPostLabourAsync()
    {
        var lr = await _db.LabourRecords.AsNoTracking().FirstOrDefaultAsync(l => l.AdmissionId == Id);
        return await RunAsync(Perm.IpdManageLabour, "labour", () => Maternity.UpdateLabourAsync(lr?.Id ?? 0, Labour), "Labour details saved.");
    }
    public async Task<IActionResult> OnPostLabourStatusAsync()
    {
        var lr = await _db.LabourRecords.AsNoTracking().FirstOrDefaultAsync(l => l.AdmissionId == Id);
        return await RunAsync(Perm.IpdManageLabour, "labour", () => Maternity.SetLabourStatusAsync(lr?.Id ?? 0, LabourStatusValue ?? LabourStatus.UnderObservation, Note), "Labour status changed.");
    }
    public Task<IActionResult> OnPostReturnToMotherAsync() => RunAsync(Perm.IpdManageNicu, "nicu", () => Maternity.ReturnToMotherAsync(Id, Reason), "Baby returned to the mother.");
}
