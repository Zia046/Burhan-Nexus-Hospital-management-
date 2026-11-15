using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

// =================================================================================================
// Phase 8 – Operation Theatre & surgical management. Patients, IPD, Laboratory, Pharmacy stock, Phase 3 charges,
// doctors and users are the existing records – nothing here duplicates them.
// =================================================================================================

public enum OtType { General = 1, Gynaecology = 2, Orthopedic = 3, Emergency = 4, Minor = 5, Other = 99 }

public class OtTheatre
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public OtType Type { get; set; } = OtType.General;
    [MaxLength(100)] public string? Location { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    [MaxLength(1000)] public string? Equipment { get; set; }
    /// <summary>Operating hours (empty = 24 hours).</summary>
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    /// <summary>Preparation / cleaning interval kept free after each surgery.</summary>
    public int CleaningMinutes { get; set; } = 30;
    public bool IsAvailable { get; set; } = true;
    [MaxLength(200)] public string? UnavailableReason { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>OT stock = an existing Phase 5 department stock location.</summary>
    public int? StockLocationId { get; set; }
    public StockLocation? StockLocation { get; set; }
    /// <summary>Theatre charge (Phase 3 service) posted once per surgery.</summary>
    public int? ChargeServiceItemId { get; set; }
    public ServiceItem? ChargeServiceItem { get; set; }
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

public enum AnesthesiaType { General = 1, Spinal = 2, Epidural = 3, Local = 4, Regional = 5, Sedation = 6, Other = 99 }

public class OtProcedure
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    [MaxLength(60)] public string? Specialty { get; set; }
    [MaxLength(60)] public string? Category { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    /// <summary>Typical duration – an estimate used to propose the booking length.</summary>
    public int DurationMinutes { get; set; } = 60;
    /// <summary>Allowed anesthesia types (comma-separated enum names; empty = any).</summary>
    [MaxLength(200)] public string? AnesthesiaTypes { get; set; }
    public OtType? DefaultOtType { get; set; }
    /// <summary>Procedure / package charge (Phase 3 service).</summary>
    public int? ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

public enum SurgeryPriority { Elective = 1, Urgent = 2, Emergency = 3 }
public enum SurgeryStatus { Requested = 1, Scheduled = 2, PreOp = 3, Ready = 4, InProgress = 5, Completed = 6, Postponed = 7, Cancelled = 8 }

public class Surgery
{
    public long Id { get; set; }
    /// <summary>SRG-yyyy-nnnnnn (database sequence – never reused).</summary>
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public long? IpdAdmissionId { get; set; }
    public IpdAdmission? IpdAdmission { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? ProcedureId { get; set; }
    public OtProcedure? Procedure { get; set; }
    [MaxLength(200)] public string ProcedureName { get; set; } = string.Empty;
    [MaxLength(500)] public string? Diagnosis { get; set; }
    [MaxLength(150)] public string? Site { get; set; }
    public int TheatreId { get; set; }
    public OtTheatre? Theatre { get; set; }
    public DateTime PlannedStartUtc { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime PlannedEndUtc { get; set; }
    /// <summary>Cleaning / preparation interval after the surgery (blocks the theatre).</summary>
    public int CleaningMinutes { get; set; }
    public DateOnly PlannedDate { get; set; }
    public AnesthesiaType? AnesthesiaType { get; set; }
    [MaxLength(100)] public string? AnesthesiaDetail { get; set; }
    public SurgeryPriority Priority { get; set; } = SurgeryPriority.Elective;
    [MaxLength(500)] public string? EmergencyReason { get; set; }
    [MaxLength(2000)] public string? ClinicalNotes { get; set; }
    public SurgeryStatus Status { get; set; } = SurgeryStatus.Scheduled;
    [MaxLength(300)] public string? StatusReason { get; set; }
    public DateTime? ActualStartUtc { get; set; }
    public DateTime? ActualEndUtc { get; set; }
    public DateTime? AnesthesiaStartUtc { get; set; }
    public DateTime? AnesthesiaEndUtc { get; set; }
    [MaxLength(150)] public string? AnesthesiaProvider { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    /// <summary>Statuses that hold the theatre and the team in the schedule.</summary>
    public bool HoldsSchedule => Status is SurgeryStatus.Requested or SurgeryStatus.Scheduled or SurgeryStatus.PreOp or SurgeryStatus.Ready or SurgeryStatus.InProgress;
}

public enum OtRole { PrimarySurgeon = 1, AssistantSurgeon = 2, Anesthetist = 3, AnesthesiaAssistant = 4, ScrubNurse = 5, CirculatingNurse = 6, OtTechnician = 7, Other = 99 }

/// <summary>A team member linked to a surgery: an existing doctor or HMS user. Removal closes the row (history kept).</summary>
public class SurgeryTeamMember
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public OtRole Role { get; set; }
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public DateTime AddedUtc { get; set; }
    [MaxLength(64)] public string? AddedBy { get; set; }
    public DateTime? RemovedUtc { get; set; }
    [MaxLength(64)] public string? RemovedBy { get; set; }
    [MaxLength(300)] public string? RemovedReason { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Permanent history of a surgery (status changes, reschedules, exceptions …). Insert-only.</summary>
public class SurgeryEvent
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    [MaxLength(40)] public string Category { get; set; } = string.Empty;
    [MaxLength(60)] public string Event { get; set; } = string.Empty;
    [MaxLength(200)] public string? FromValue { get; set; }
    [MaxLength(200)] public string? ToValue { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

public class OtConsentType
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    /// <summary>Printable template text ({patient}, {procedure}, {hospital} are filled in).</summary>
    [MaxLength(4000)] public string? TemplateText { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum ConsentStatus { Pending = 1, Obtained = 2, Refused = 3, Withdrawn = 4 }

/// <summary>Documentation of the consent process (the signed paper / scan is the legal record).</summary>
public class SurgeryConsent
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public int ConsentTypeId { get; set; }
    public OtConsentType? ConsentType { get; set; }
    [MaxLength(100)] public string ConsentTypeName { get; set; } = string.Empty;
    [MaxLength(150)] public string GivenBy { get; set; } = string.Empty;
    [MaxLength(60)] public string? Relation { get; set; }
    public DateTime ConsentUtc { get; set; }
    public ConsentStatus Status { get; set; } = ConsentStatus.Pending;
    [MaxLength(150)] public string? ResponsibleStaff { get; set; }
    [MaxLength(150)] public string? Witness { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public DateTime UpdatedUtc { get; set; }
    [MaxLength(64)] public string? UpdatedBy { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Scanned consent / handwritten instruction / other document, stored as a file (insert-only).</summary>
public class OtDocument
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public long? ConsentId { get; set; }
    [MaxLength(30)] public string Kind { get; set; } = "Other";
    [MaxLength(150)] public string Title { get; set; } = string.Empty;
    [MaxLength(300)] public string StoredPath { get; set; } = string.Empty;
    [MaxLength(60)] public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    [MaxLength(64)] public string Sha256 { get; set; } = string.Empty;
    [MaxLength(200)] public string? OriginalName { get; set; }
    public DateTime UploadedUtc { get; set; }
    [MaxLength(64)] public string? UploadedBy { get; set; }
}

public enum ChecklistStage { PreOp = 1, SignIn = 2, TimeOut = 3, SignOut = 4 }
public enum ChecklistAnswer { Pending = 0, Done = 1, NotApplicable = 2 }

/// <summary>Configurable checklist item (pre-operative checklist and the three surgical safety stages).</summary>
public class OtChecklistItem
{
    public int Id { get; set; }
    public ChecklistStage Stage { get; set; }
    [MaxLength(200)] public string Label { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>An answer to one checklist item (insert-only – the latest answer counts, earlier ones stay as history).</summary>
public class SurgeryChecklistEntry
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public int ItemId { get; set; }
    public ChecklistStage Stage { get; set; }
    [MaxLength(200)] public string ItemLabel { get; set; } = string.Empty;
    public ChecklistAnswer Answer { get; set; }
    [MaxLength(300)] public string? Remarks { get; set; }
    public DateTime RecordedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

/// <summary>Completion of a checklist stage – normally with every mandatory item done; otherwise a documented, authorised exception.</summary>
public class SurgeryChecklistStage
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public ChecklistStage Stage { get; set; }
    public DateTime CompletedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
    [MaxLength(300)] public string? Remarks { get; set; }
    public bool IsException { get; set; }
    [MaxLength(500)] public string? ExceptionReason { get; set; }
    [MaxLength(500)] public string? PendingItems { get; set; }
}

public enum OtDrugKind { Drug = 1, Fluid = 2 }

/// <summary>Drug / IV fluid given in theatre or recovery – recorded as given (insert-only).</summary>
public class OtDrugEntry
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    [MaxLength(20)] public string Phase { get; set; } = "Anesthesia";
    public OtDrugKind Kind { get; set; } = OtDrugKind.Drug;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(60)] public string? Dose { get; set; }
    [MaxLength(40)] public string? Route { get; set; }
    public DateTime GivenUtc { get; set; }
    [MaxLength(150)] public string? GivenBy { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

public enum NoteStatus { Draft = 1, Final = 2 }

/// <summary>
/// Intra-operative record and operation note. A draft can be edited; a final version is immutable (database trigger).
/// An amendment is a new version that references the one it amends and carries a reason.
/// </summary>
public class OperativeNote
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public int Version { get; set; } = 1;
    public NoteStatus Status { get; set; } = NoteStatus.Draft;
    public long? AmendsNoteId { get; set; }
    [MaxLength(300)] public string? AmendReason { get; set; }
    // ---- intra-operative record ----
    [MaxLength(200)] public string? Positioning { get; set; }
    [MaxLength(150)] public string? Site { get; set; }
    [MaxLength(300)] public string? ProcedurePerformed { get; set; }
    [MaxLength(4000)] public string? OperativeFindings { get; set; }
    [MaxLength(4000)] public string? Technique { get; set; }
    public int? BloodLossMl { get; set; }
    [MaxLength(1000)] public string? IvFluids { get; set; }
    [MaxLength(1000)] public string? DrugsAdministered { get; set; }
    [MaxLength(1000)] public string? Specimens { get; set; }
    [MaxLength(1000)] public string? Implants { get; set; }
    [MaxLength(1000)] public string? Complications { get; set; }
    [MaxLength(1000)] public string? AdditionalProcedures { get; set; }
    [MaxLength(2000)] public string? Observations { get; set; }
    // ---- operation note ----
    [MaxLength(500)] public string? PreOpDiagnosis { get; set; }
    [MaxLength(500)] public string? PostOpDiagnosis { get; set; }
    [MaxLength(2000)] public string? PostOpInstructions { get; set; }
    [MaxLength(2000)] public string? SurgeonNotes { get; set; }
    [MaxLength(150)] public string? SurgeonName { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public DateTime UpdatedUtc { get; set; }
    [MaxLength(64)] public string? UpdatedBy { get; set; }
    public DateTime? FinalizedUtc { get; set; }
    [MaxLength(64)] public string? FinalizedBy { get; set; }
    public uint RowVersion { get; set; }
}

public enum SpecimenStatus { Collected = 1, Dispatched = 2, Received = 3 }

public class SurgerySpecimen
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public long PatientId { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(60)] public string? Type { get; set; }
    public DateTime CollectedUtc { get; set; }
    [MaxLength(150)] public string? CollectedBy { get; set; }
    [MaxLength(150)] public string? Destination { get; set; }
    /// <summary>Existing laboratory order the specimen belongs to (no duplicate order is created).</summary>
    public long? LabOrderId { get; set; }
    public LabOrder? LabOrder { get; set; }
    public SpecimenStatus Status { get; set; } = SpecimenStatus.Collected;
    public DateTime? DispatchedUtc { get; set; }
    [MaxLength(64)] public string? DispatchedBy { get; set; }
    public DateTime? ReceivedUtc { get; set; }
    [MaxLength(150)] public string? ReceivedBy { get; set; }
    [MaxLength(500)] public string? Remarks { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public uint RowVersion { get; set; }
}

public enum OtConsumptionKind { Use = 1, Return = 2 }

/// <summary>Major OT, patient-linked use (or return) of a medicine / consumable from the OT stock location. Insert-only.</summary>
public class OtConsumption
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public long PatientId { get; set; }
    public int LocationId { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    public long BatchId { get; set; }
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public OtConsumptionKind Kind { get; set; }
    public long? ReturnOfId { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

public enum EquipmentStatus { Available = 1, InUse = 2, UnderMaintenance = 3, OutOfService = 4 }

public class OtEquipment
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public int? TheatreId { get; set; }
    public OtTheatre? Theatre { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;
    public DateOnly? LastMaintenance { get; set; }
    public DateOnly? NextMaintenance { get; set; }
    [MaxLength(1000)] public string? ServiceNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

public class OtEquipmentLog
{
    public long Id { get; set; }
    public int EquipmentId { get; set; }
    [MaxLength(60)] public string Event { get; set; } = string.Empty;
    [MaxLength(60)] public string? FromValue { get; set; }
    [MaxLength(60)] public string? ToValue { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

public enum RecoveryStatus { Awaiting = 1, InRecovery = 2, UnderObservation = 3, ReadyForTransfer = 4, TransferredWard = 5, TransferredIcu = 6, Discharged = 7 }

/// <summary>Recovery room / PACU episode of a surgery (one per surgery). Disposition is documented by the responsible clinician.</summary>
public class RecoveryEpisode
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public Surgery? Surgery { get; set; }
    public long PatientId { get; set; }
    public RecoveryStatus Status { get; set; } = RecoveryStatus.Awaiting;
    public DateTime? ArrivalUtc { get; set; }
    [MaxLength(150)] public string? Nurse { get; set; }
    [MaxLength(500)] public string? AnesthesiaSummary { get; set; }
    [MaxLength(150)] public string? Destination { get; set; }
    [MaxLength(150)] public string? HandoverStaff { get; set; }
    public DateTime? TransferUtc { get; set; }
    [MaxLength(1000)] public string? HandoverNotes { get; set; }
    [MaxLength(150)] public string? DecisionBy { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    public bool IsOpen => Status is RecoveryStatus.Awaiting or RecoveryStatus.InRecovery or RecoveryStatus.UnderObservation or RecoveryStatus.ReadyForTransfer;
}

// -------------------------------------------------------------------------------------------------
// CSSD / sterilization
// -------------------------------------------------------------------------------------------------

public class CssdInstrument
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(60)] public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum CssdSetStatus { NotSterile = 1, InProcess = 2, Sterile = 3, Issued = 4, Quarantined = 5 }

public class CssdSet
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public int? TheatreId { get; set; }
    public CssdSetStatus Status { get; set; } = CssdSetStatus.NotSterile;
    public DateOnly? SterileUntil { get; set; }
    public long? LastBatchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
    public List<CssdSetItem> Items { get; set; } = new();
}

public class CssdSetItem
{
    public long Id { get; set; }
    public int SetId { get; set; }
    public int InstrumentId { get; set; }
    public CssdInstrument? Instrument { get; set; }
    public int Quantity { get; set; } = 1;
}

public enum CssdBatchStatus { InProgress = 1, Passed = 2, Failed = 3, Released = 4 }

public class CssdBatch
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    [MaxLength(60)] public string Method { get; set; } = string.Empty;
    [MaxLength(60)] public string? Steriliser { get; set; }
    [MaxLength(30)] public string? CycleNumber { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    [MaxLength(500)] public string? Parameters { get; set; }
    public CssdBatchStatus Status { get; set; } = CssdBatchStatus.InProgress;
    [MaxLength(300)] public string? FailReason { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    [MaxLength(150)] public string? ResponsibleStaff { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public DateTime? ResultUtc { get; set; }
    [MaxLength(64)] public string? ResultBy { get; set; }
    public DateTime? ReleasedUtc { get; set; }
    [MaxLength(64)] public string? ReleasedBy { get; set; }
    public uint RowVersion { get; set; }
    public List<CssdBatchSet> Sets { get; set; } = new();
}

public class CssdBatchSet
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public int SetId { get; set; }
    public CssdSet? Set { get; set; }
}

public class CssdIssue
{
    public long Id { get; set; }
    public int SetId { get; set; }
    public CssdSet? Set { get; set; }
    public long? SurgeryId { get; set; }
    public long? BatchId { get; set; }
    public DateTime IssuedUtc { get; set; }
    [MaxLength(64)] public string? IssuedBy { get; set; }
    [MaxLength(150)] public string? IssuedTo { get; set; }
    public DateTime? ReturnedUtc { get; set; }
    [MaxLength(64)] public string? ReturnedBy { get; set; }
    [MaxLength(300)] public string? ReturnNote { get; set; }
    public uint RowVersion { get; set; }
}

// -------------------------------------------------------------------------------------------------
// Billing (Phase 3 charges only) and settings
// -------------------------------------------------------------------------------------------------

public enum OtChargeKind { Theatre = 1, Procedure = 2, SurgeonFee = 3, AssistantFee = 4, AnesthesiaFee = 5, AnesthesiaSupplies = 6, Recovery = 7, Equipment = 8, Consumables = 9, Other = 99 }

/// <summary>Links a Phase 3 charge to a surgery (standard kinds are posted once per surgery).</summary>
public class SurgeryCharge
{
    public long Id { get; set; }
    public long SurgeryId { get; set; }
    public OtChargeKind Kind { get; set; }
    public long ChargeId { get; set; }
    public Charge? Charge { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
}

public class OtSettings
{
    public int Id { get; set; } = 1;
    public int? SurgeonFeeItemId { get; set; }
    public int? AssistantFeeItemId { get; set; }
    public int? AnesthesiaFeeItemId { get; set; }
    public int? AnesthesiaSuppliesItemId { get; set; }
    public int? RecoveryItemId { get; set; }
    public int? EquipmentItemId { get; set; }
    public int? ConsumablesItemId { get; set; }
    public int DefaultCleaningMinutes { get; set; } = 30;
    public bool PrintHeader { get; set; } = true;
    public int LetterheadTopMarginMm { get; set; } = 45;
    [MaxLength(300)] public string? FormFooter { get; set; }
    public int MaxUploadMb { get; set; } = 10;
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}
