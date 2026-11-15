using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

// =====================================================================================================
// Phase 7 – IPD, wards & beds, Gynae, ANC, Labour Room, delivery, newborns, postnatal, NICU, nursing and MAR.
// Every admission is its own inpatient encounter linked to the permanent Patient ID. History tables are insert-only;
// clinical records are never overwritten – a correction is a new record that points to the one it corrects.
// Money goes only through Phase 3 charges (Charges.IpdAdmissionId). The system documents care – it never
// diagnoses, prescribes or decides: alert limits exist only when an authorised clinical administrator configures them.
// =====================================================================================================

public enum WardType { General = 1, MaleGeneral = 2, FemaleGeneral = 3, Children = 4, Gynae = 5, Maternity = 6, Postnatal = 7, LabourRoom = 8, Nicu = 9, Other = 99 }
public enum RoomType { General = 1, Private = 2, SemiPrivate = 3, Isolation = 4, LabourRoom = 5, Nicu = 6, Other = 99 }
public enum BedType { Standard = 1, Labour = 2, Incubator = 3, Cot = 4, RadiantWarmer = 5, Other = 99 }
public enum BedStatus { Available = 1, Occupied = 2, Reserved = 3, CleaningRequired = 4, UnderMaintenance = 5, TemporarilyUnavailable = 6 }

public class IpdWard
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public WardType Type { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    [MaxLength(100)] public string? Location { get; set; }
    public int Capacity { get; set; }
    [MaxLength(200)] public string? ResponsibleStaff { get; set; }
    /// <summary>Phase 3 service charged per night in a bed of this ward (empty = no bed charge).</summary>
    public int? BedChargeServiceItemId { get; set; }
    public ServiceItem? BedChargeServiceItem { get; set; }
    /// <summary>Phase 5 department stock location used as ward stock (medicines issued to the ward).</summary>
    public int? StockLocationId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public class IpdRoom
{
    public int Id { get; set; }
    public int WardId { get; set; }
    public IpdWard? Ward { get; set; }
    [MaxLength(50)] public string Name { get; set; } = string.Empty;
    public RoomType Type { get; set; }
    public int BedCapacity { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(200)] public string? Notes { get; set; }
}

public class IpdBed
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public IpdRoom? Room { get; set; }
    public int WardId { get; set; }
    public IpdWard? Ward { get; set; }
    public BedType Type { get; set; }
    public BedStatus Status { get; set; } = BedStatus.Available;
    /// <summary>Incubator / cot / equipment identifier (NICU).</summary>
    [MaxLength(60)] public string? EquipmentId { get; set; }
    [MaxLength(200)] public string? StatusNote { get; set; }
    public long? CurrentAdmissionId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Insert-only history of bed status changes (cleaning, maintenance …) and equipment use.</summary>
public class IpdBedStatusChange
{
    public long Id { get; set; }
    public int BedId { get; set; }
    public BedStatus From { get; set; }
    public BedStatus To { get; set; }
    [MaxLength(200)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

public enum AdmissionKind { General = 1, Gynae = 2, Labour = 3, Nicu = 4, Newborn = 5 }
public enum AdmissionSource { Opd = 1, Emergency = 2, Gynae = 3, Maternity = 4, Direct = 5, DeliveryRoom = 6, LabourRoom = 7, PostnatalWard = 8, ExternalReferral = 9, Other = 99 }
public enum AdmissionStatus { Registered = 1, Admitted = 2, UnderTreatment = 3, Transferred = 4, DischargeRequested = 5, Discharged = 6, Referred = 7, Cancelled = 8 }
public enum DischargeType { Routine = 1, Referred = 2, Lama = 3, Transferred = 4, Death = 5, Other = 99 }

public class IpdAdmission
{
    public long Id { get; set; }
    [MaxLength(30)] public string AdmissionNumber { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public AdmissionKind Kind { get; set; } = AdmissionKind.General;
    public AdmissionSource Source { get; set; }
    /// <summary>OPD / Emergency visit the patient was admitted from.</summary>
    public long? SourceEncounterId { get; set; }
    public PatientEncounter? SourceEncounter { get; set; }
    /// <summary>Mother's admission (newborn rooming-in / NICU baby).</summary>
    public long? MotherAdmissionId { get; set; }
    public long? PregnancyId { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    [MaxLength(150)] public string? ReferringDoctor { get; set; }
    public int? AdmittingDoctorId { get; set; }
    public Doctor? AdmittingDoctor { get; set; }
    [MaxLength(150)] public string? AdmittingDoctorName { get; set; }
    [MaxLength(500)] public string? AdmissionDiagnosis { get; set; }
    [MaxLength(500)] public string? InitialCondition { get; set; }
    [MaxLength(2000)] public string? MedicalHistory { get; set; }
    [MaxLength(500)] public string? Allergies { get; set; }
    [MaxLength(150)] public string? AttendantName { get; set; }
    [MaxLength(50)] public string? AttendantRelation { get; set; }
    [MaxLength(30)] public string? AttendantPhone { get; set; }
    public AdmissionStatus Status { get; set; } = AdmissionStatus.Admitted;
    public DateTime AdmittedUtc { get; set; }
    public DateOnly AdmissionDate { get; set; }
    public int? CurrentWardId { get; set; }
    public IpdWard? CurrentWard { get; set; }
    public int? CurrentBedId { get; set; }
    public IpdBed? CurrentBed { get; set; }
    public bool NeedsAttention { get; set; }
    [MaxLength(200)] public string? AttentionNote { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    // ---- discharge ----
    public DischargeType? DischargeType { get; set; }
    [MaxLength(1000)] public string? FinalDiagnosis { get; set; }
    [MaxLength(4000)] public string? TreatmentSummary { get; set; }
    [MaxLength(2000)] public string? Procedures { get; set; }
    [MaxLength(2000)] public string? InvestigationSummary { get; set; }
    [MaxLength(500)] public string? ConditionAtDischarge { get; set; }
    [MaxLength(2000)] public string? FollowUp { get; set; }
    [MaxLength(2000)] public string? DischargeMedications { get; set; }
    [MaxLength(200)] public string? ReferredTo { get; set; }
    [MaxLength(150)] public string? DischargeDoctor { get; set; }
    public DateTime? DischargeRequestedUtc { get; set; }
    [MaxLength(64)] public string? DischargeRequestedBy { get; set; }
    public DateTime? DischargedUtc { get; set; }
    public DateOnly? DischargeDate { get; set; }
    [MaxLength(64)] public string? DischargedBy { get; set; }
    [MaxLength(300)] public string? CancelReason { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    public bool IsActive => Status is AdmissionStatus.Registered or AdmissionStatus.Admitted or AdmissionStatus.UnderTreatment
        or AdmissionStatus.Transferred or AdmissionStatus.DischargeRequested;
}

/// <summary>Bed occupancy (insert + close only). One open allocation per bed – except a newborn rooming-in with the mother (shared).</summary>
public class IpdBedAllocation
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public int BedId { get; set; }
    public IpdBed? Bed { get; set; }
    public int WardId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    /// <summary>Newborn staying in the mother's bed: not a second occupant of the bed.</summary>
    public bool IsShared { get; set; }
    public long? SharedWithAdmissionId { get; set; }
    [MaxLength(200)] public string? Reason { get; set; }
    [MaxLength(64)] public string? AllocatedBy { get; set; }
    [MaxLength(64)] public string? ReleasedBy { get; set; }
}

public enum TransferKind { Bed = 1, Room = 2, Ward = 3, Department = 4, External = 5 }

public class IpdTransfer
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public TransferKind Kind { get; set; }
    public int? FromWardId { get; set; }
    public int? FromBedId { get; set; }
    public int? FromDepartmentId { get; set; }
    public int? ToWardId { get; set; }
    public int? ToBedId { get; set; }
    public int? ToDepartmentId { get; set; }
    [MaxLength(200)] public string FromText { get; set; } = string.Empty;
    [MaxLength(200)] public string ToText { get; set; } = string.Empty;
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    [MaxLength(150)] public string? ReceivingDepartment { get; set; }
    public DateTime TransferUtc { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

/// <summary>Insert-only timeline of an admission (status changes, labour / NICU status, discharge …).</summary>
public class IpdEvent
{
    public long Id { get; set; }
    public long? AdmissionId { get; set; }
    public long? PregnancyId { get; set; }
    [MaxLength(40)] public string Category { get; set; } = string.Empty;
    [MaxLength(60)] public string Event { get; set; } = string.Empty;
    [MaxLength(60)] public string? FromValue { get; set; }
    [MaxLength(60)] public string? ToValue { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

// -------------------------------------------------------------------------------------------------
// Configurable clinical forms (one record table for every documentation kind).
// -------------------------------------------------------------------------------------------------

public enum RecordKind
{
    ProgressNote = 1, Vitals = 2, NursingAssessment = 3, NursingNote = 4,
    GynaeHistory = 10, GynaeAssessment = 11, AncVisit = 12,
    LabourAssessment = 20, LabourObservation = 21,
    PostnatalAssessment = 30,
    NicuAssessment = 40, NicuObservation = 41, NicuFeeding = 42, NicuCare = 43, NicuProgress = 44,
}

public enum FieldType { Number = 1, Text = 2, LongText = 3, Choice = 4, YesNo = 5 }

/// <summary>A field of a clinical form. Alert limits are empty unless set by an authorised clinical administrator.</summary>
public class ClinicalFormField
{
    public int Id { get; set; }
    public RecordKind Kind { get; set; }
    [MaxLength(40)] public string Key { get; set; } = string.Empty;
    [MaxLength(100)] public string Label { get; set; } = string.Empty;
    [MaxLength(60)] public string? Section { get; set; }
    public FieldType Type { get; set; }
    [MaxLength(20)] public string? Unit { get; set; }
    [MaxLength(500)] public string? Choices { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public decimal? AlertLow { get; set; }
    public decimal? AlertHigh { get; set; }
    [MaxLength(200)] public string? AlertSource { get; set; }
    public DateTime UpdatedUtc { get; set; }
    [MaxLength(64)] public string? UpdatedBy { get; set; }
}

/// <summary>One clinical documentation entry (insert-only). Values are a JSON object keyed by field key.</summary>
public class ClinicalRecord
{
    public long Id { get; set; }
    public RecordKind Kind { get; set; }
    public long PatientId { get; set; }
    public long? AdmissionId { get; set; }
    public long? PregnancyId { get; set; }
    /// <summary>Clinical time of the observation / note (may differ from the entry time).</summary>
    public DateTime RecordedUtc { get; set; }
    public DateTime EnteredUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
    /// <summary>Clinician / nurse responsible (may be named when staff document a handwritten note).</summary>
    [MaxLength(150)] public string? ResponsibleName { get; set; }
    public string Values { get; set; } = "{}";
    [MaxLength(4000)] public string? Notes { get; set; }
    /// <summary>This record corrects an earlier one (which is then shown as superseded).</summary>
    public long? CorrectsRecordId { get; set; }
    [MaxLength(300)] public string? CorrectionReason { get; set; }
}

// -------------------------------------------------------------------------------------------------
// Pregnancy, labour, delivery and newborns
// -------------------------------------------------------------------------------------------------

public enum PregnancyStatus { Active = 1, Delivered = 2, Ended = 3, TransferredOut = 4 }

public class Pregnancy
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public DateOnly RegisteredDate { get; set; }
    public DateOnly? Lmp { get; set; }
    public DateOnly? Edd { get; set; }
    public int? Gravida { get; set; }
    public int? Para { get; set; }
    public int? Abortions { get; set; }
    public int? LivingChildren { get; set; }
    [MaxLength(1000)] public string? PreviousComplications { get; set; }
    [MaxLength(2000)] public string? MedicalHistory { get; set; }
    [MaxLength(1000)] public string? RiskFactors { get; set; }
    public bool IsHighRisk { get; set; }
    [MaxLength(500)] public string? HighRiskReason { get; set; }
    [MaxLength(1000)] public string? HighRiskAssessment { get; set; }
    [MaxLength(300)] public string? HighRiskReferral { get; set; }
    [MaxLength(500)] public string? FollowUpPlan { get; set; }
    public PregnancyStatus Status { get; set; } = PregnancyStatus.Active;
    [MaxLength(300)] public string? StatusNote { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    /// <summary>Gestational age on a date from the recorded LMP (a calendar calculation, not a clinical assessment).</summary>
    public string? GestationOn(DateOnly date) =>
        Lmp is { } l && date >= l ? $"{(date.DayNumber - l.DayNumber) / 7}w {(date.DayNumber - l.DayNumber) % 7}d" : null;
}

public enum LabourStatus { Admitted = 1, UnderObservation = 2, EarlyLabour = 3, ActiveLabour = 4, DeliveryCompleted = 5, Referred = 6, Transferred = 7, Discharged = 8 }

/// <summary>Labour Room episode of an admission (1:1 with the labour admission).</summary>
public class LabourRecord
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public IpdAdmission? Admission { get; set; }
    public long? PregnancyId { get; set; }
    public Pregnancy? Pregnancy { get; set; }
    public LabourStatus Status { get; set; } = LabourStatus.Admitted;
    [MaxLength(20)] public string? GestationalAge { get; set; }
    [MaxLength(1000)] public string? PresentingComplaints { get; set; }
    public DateTime? OnsetUtc { get; set; }
    [MaxLength(200)] public string? OnsetInfo { get; set; }
    [MaxLength(100)] public string? MembraneStatus { get; set; }
    public DateTime? MembranesRupturedUtc { get; set; }
    [MaxLength(1000)] public string? ClinicalHistory { get; set; }
    [MaxLength(1000)] public string? InitialAssessment { get; set; }
    [MaxLength(150)] public string? ResponsibleStaff { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

public enum DeliveryType { Nvd = 1, AssistedVaginal = 2, CaesareanSection = 3, Other = 99 }

public class DeliveryRecord
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public long AdmissionId { get; set; }
    public IpdAdmission? Admission { get; set; }
    public long MotherPatientId { get; set; }
    public Patient? Mother { get; set; }
    public long? PregnancyId { get; set; }
    public long? LabourRecordId { get; set; }
    public DateTime DeliveredUtc { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public DeliveryType Type { get; set; }
    [MaxLength(100)] public string? TypeDetail { get; set; }
    [MaxLength(200)] public string? Outcome { get; set; }
    [MaxLength(300)] public string? AttendingStaff { get; set; }
    [MaxLength(150)] public string? DeliveringDoctor { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    [MaxLength(1000)] public string? Complications { get; set; }
    [MaxLength(500)] public string? Placenta { get; set; }
    public int? EstimatedBloodLossMl { get; set; }
    [MaxLength(500)] public string? Perineum { get; set; }
    [MaxLength(500)] public string? MaternalCondition { get; set; }
    [MaxLength(1000)] public string? ImmediateCare { get; set; }
    public int BabyCount { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public List<NewbornRecord> Newborns { get; set; } = new();
}

public enum BirthCondition { LiveBirth = 1, Stillbirth = 2 }
public enum NewbornPlacement { WithMother = 1, Nicu = 2, Other = 3 }

/// <summary>Birth record of one baby – the secure mother–baby link (mother, pregnancy, delivery → newborn Patient ID).</summary>
public class NewbornRecord
{
    public long Id { get; set; }
    public long DeliveryId { get; set; }
    public DeliveryRecord? Delivery { get; set; }
    public int BirthOrder { get; set; }
    public long MotherPatientId { get; set; }
    public long? PregnancyId { get; set; }
    /// <summary>The baby's own permanent Patient ID (live births). Never the mother's.</summary>
    public long? NewbornPatientId { get; set; }
    public Patient? NewbornPatient { get; set; }
    [MaxLength(150)] public string? Name { get; set; }
    public Gender Sex { get; set; }
    public DateTime BirthUtc { get; set; }
    public int? BirthWeightGrams { get; set; }
    [MaxLength(20)] public string? GestationalAge { get; set; }
    public int? Apgar1 { get; set; }
    public int? Apgar5 { get; set; }
    public int? Apgar10 { get; set; }
    public BirthCondition Condition { get; set; } = BirthCondition.LiveBirth;
    [MaxLength(500)] public string? Resuscitation { get; set; }
    [MaxLength(500)] public string? CongenitalObservations { get; set; }
    [MaxLength(1000)] public string? Examination { get; set; }
    [MaxLength(150)] public string? ResponsibleStaff { get; set; }
    public NewbornPlacement Placement { get; set; }
    /// <summary>Rooming-in / NICU admission of the baby.</summary>
    public long? NewbornAdmissionId { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
}

/// <summary>NICU-specific admission details (1:1 with a NICU admission).</summary>
public class NicuDetails
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public IpdAdmission? Admission { get; set; }
    public long? NewbornRecordId { get; set; }
    public NewbornRecord? NewbornRecord { get; set; }
    public long? MotherPatientId { get; set; }
    [MaxLength(20)] public string? GestationalAge { get; set; }
    public int? BirthWeightGrams { get; set; }
    public int? AdmissionWeightGrams { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    [MaxLength(500)] public string? ClinicalCondition { get; set; }
    [MaxLength(1000)] public string? BirthHistory { get; set; }
    [MaxLength(300)] public string? ReferralDetails { get; set; }
    [MaxLength(150)] public string? ResponsibleClinician { get; set; }
}

// -------------------------------------------------------------------------------------------------
// Nursing & medication administration
// -------------------------------------------------------------------------------------------------

public enum NursingTaskType { VitalSigns = 1, Medication = 2, Feeding = 3, IntakeOutput = 4, WoundCare = 5, Repositioning = 6, Other = 99 }
public enum NursingTaskStatus { Pending = 1, Done = 2, Cancelled = 3 }

public class NursingTask
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public NursingTaskType Type { get; set; }
    [MaxLength(300)] public string Description { get; set; } = string.Empty;
    public DateTime DueUtc { get; set; }
    public NursingTaskStatus Status { get; set; } = NursingTaskStatus.Pending;
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedBy { get; set; }
    public DateTime? CompletedUtc { get; set; }
    [MaxLength(64)] public string? CompletedBy { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public uint RowVersion { get; set; }
}

public class ShiftHandover
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public int? WardId { get; set; }
    [MaxLength(30)] public string? Shift { get; set; }
    public int? OutgoingUserId { get; set; }
    [MaxLength(64)] public string? OutgoingUsername { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(1000)] public string? PatientCondition { get; set; }
    [MaxLength(500)] public string? Diagnosis { get; set; }
    [MaxLength(500)] public string? PendingInvestigations { get; set; }
    [MaxLength(500)] public string? MedicationDue { get; set; }
    [MaxLength(500)] public string? IvFluids { get; set; }
    [MaxLength(1000)] public string? ImportantObservations { get; set; }
    [MaxLength(500)] public string? PendingProcedures { get; set; }
    [MaxLength(500)] public string? SpecialCare { get; set; }
    [MaxLength(500)] public string? OutstandingTasks { get; set; }
    public int? AcknowledgedByUserId { get; set; }
    [MaxLength(64)] public string? AcknowledgedByUsername { get; set; }
    public DateTime? AcknowledgedUtc { get; set; }
    [MaxLength(300)] public string? AcknowledgeNote { get; set; }
    public uint RowVersion { get; set; }
}

public enum OrderSource { HandwrittenPrescription = 1, VerbalOrder = 2, DirectEntry = 3 }
public enum MedicationOrderStatus { Active = 1, Stopped = 2, Cancelled = 3 }

public class MedicationOrder
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public long PatientId { get; set; }
    /// <summary>Medicine from the Phase 5 catalogue (no separate medicine list).</summary>
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    [MaxLength(60)] public string Dose { get; set; } = string.Empty;
    [MaxLength(40)] public string Route { get; set; } = string.Empty;
    [MaxLength(60)] public string Frequency { get; set; } = string.Empty;
    /// <summary>Administration times of day, e.g. "08:00,14:00,20:00".</summary>
    [MaxLength(100)] public string Times { get; set; } = string.Empty;
    public int? DurationDays { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime? StopUtc { get; set; }
    [MaxLength(150)] public string Prescriber { get; set; } = string.Empty;
    public OrderSource Source { get; set; }
    [MaxLength(500)] public string? Instructions { get; set; }
    /// <summary>Units taken from ward stock per administered dose (only when the ward keeps stock).</summary>
    public int StockUnitsPerDose { get; set; }
    public MedicationOrderStatus Status { get; set; } = MedicationOrderStatus.Active;
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? EnteredBy { get; set; }
    [MaxLength(300)] public string? StopReason { get; set; }
    [MaxLength(64)] public string? StoppedBy { get; set; }
    public uint RowVersion { get; set; }
}

public enum DoseStatus { Scheduled = 1, Pending = 2, Administered = 3, Missed = 4, Withheld = 5, Cancelled = 6 }

public class MedicationDose
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public MedicationOrder? Order { get; set; }
    public long AdmissionId { get; set; }
    public DateTime ScheduledUtc { get; set; }
    public DoseStatus Status { get; set; } = DoseStatus.Scheduled;
    public DateTime? GivenUtc { get; set; }
    [MaxLength(60)] public string? DoseGiven { get; set; }
    [MaxLength(40)] public string? RouteGiven { get; set; }
    [MaxLength(150)] public string? Nurse { get; set; }
    [MaxLength(300)] public string? Reason { get; set; }
    [MaxLength(300)] public string? Remarks { get; set; }
    public DateTime? RecordedUtc { get; set; }
    [MaxLength(64)] public string? RecordedBy { get; set; }
    /// <summary>Ward-stock movement (Phase 5 inventory transaction) of this dose, if stock was taken.</summary>
    public long? StockTransactionId { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Bed-night charge posted to Phase 3 (unique per admission and night: never billed twice).</summary>
public class IpdBedCharge
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public DateOnly Night { get; set; }
    public int WardId { get; set; }
    public long ChargeId { get; set; }
}

public class IpdSettings
{
    public int Id { get; set; } = 1;
    /// <summary>Phase 3 service charged once at admission (empty = none).</summary>
    public int? AdmissionServiceItemId { get; set; }
    public bool AllowDirectAdmission { get; set; } = true;
    public bool RequireBillClearanceForDischarge { get; set; }
    public bool PrintHeader { get; set; } = true;
    public int LetterheadTopMarginMm { get; set; } = 45;
    [MaxLength(300)] public string? FormFooter { get; set; }
    /// <summary>MAR safety confirmations (patient, medicine, dose, route, time) before recording a dose.</summary>
    public bool RequireMarConfirmations { get; set; } = true;
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}
