using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

/// <summary>Kind of hospital encounter. Future phases add Laboratory, Radiology, Pharmacy, IPD…</summary>
public enum EncounterType { Opd = 1, Emergency = 2 }

public enum EmergencyStatus
{
    Registered = 1,
    UnderTreatment = 2,
    Referred = 3,
    Discharged = 4,
    /// <summary>Reserved for the future IPD module.</summary>
    Admitted = 5,
    Closed = 6
}

public enum PrintedDocumentType { OpdSlip = 1, PrivateVisitDocument = 2, EmergencySlip = 3, PatientRecord = 4 }

/// <summary>
/// One visit of a patient (OPD, Emergency, and later Lab/Radiology/IPD…). Holds the unique visit number and the
/// links to patient, department, doctor and (future) billing service. Patient master data is NOT copied here.
/// </summary>
public class PatientEncounter
{
    public long Id { get; set; }

    /// <summary>Unique visit / slip number, e.g. OPD-2026-000123 or ER-2026-000045.</summary>
    [MaxLength(30)] public string EncounterNumber { get; set; } = string.Empty;
    public long SequenceNumber { get; set; }
    public EncounterType Type { get; set; }

    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    /// <summary>Set when the encounter was moved to another patient by a duplicate correction.</summary>
    public long? OriginalPatientId { get; set; }

    public DateTime EncounterUtc { get; set; }
    /// <summary>Hospital-local date of the visit (used for daily tokens and reports).</summary>
    public DateOnly EncounterDate { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    /// <summary>Daily token (queue) number, when enabled.</summary>
    public int? TokenNumber { get; set; }
    [MaxLength(60)] public string? TokenScope { get; set; }

    /// <summary>Hospital type when the visit was registered (Government / Private).</summary>
    public HospitalType HospitalTypeAtVisit { get; set; }

    /// <summary>Reference to the configured service/fee item. Payments are recorded by the future billing module.</summary>
    public int? ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }

    [MaxLength(1000)] public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    public OpdVisit? Opd { get; set; }
    public EmergencyVisit? Emergency { get; set; }
    public List<PrintedDocument> PrintedDocuments { get; set; } = new();
}

public class OpdVisit
{
    public long EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    /// <summary>True when the patient had an earlier OPD visit.</summary>
    public bool IsRepeatVisit { get; set; }
    [MaxLength(300)] public string? ChiefComplaint { get; set; }
}

public class EmergencyVisit
{
    public long EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    public int? CategoryId { get; set; }
    public EmergencyCategory? Category { get; set; }
    [MaxLength(500)] public string? BriefComplaint { get; set; }
    [MaxLength(150)] public string? BroughtBy { get; set; }
    [MaxLength(150)] public string? AttendantName { get; set; }
    [MaxLength(20)] public string? AttendantContact { get; set; }
    [MaxLength(200)] public string? ReferredBy { get; set; }
    public bool IsMedicoLegal { get; set; }
    public EmergencyStatus Status { get; set; } = EmergencyStatus.Registered;
    public DateTime StatusChangedUtc { get; set; }
    public List<EmergencyStatusHistory> StatusHistory { get; set; } = new();
}

public class EmergencyStatusHistory
{
    public long Id { get; set; }
    public long EncounterId { get; set; }
    public EmergencyVisit? Visit { get; set; }
    public EmergencyStatus? FromStatus { get; set; }
    public EmergencyStatus ToStatus { get; set; }
    public DateTime ChangedUtc { get; set; }
    public int? ChangedByUserId { get; set; }
    [MaxLength(64)] public string? ChangedByUsername { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
}

/// <summary>Configurable emergency type/category (RTA, burn, poisoning…).</summary>
public class EmergencyCategory
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Doctor reference used for OPD visits (doctors do not need a login). The future Doctor Management module
/// extends this record with schedules, fees and shares.
/// </summary>
public class Doctor
{
    public int Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(150)] public string? Qualification { get; set; }
    [MaxLength(100)] public string? Specialty { get; set; }
    [MaxLength(50)] public string? RegistrationNumber { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    /// <summary>Optional link to a user account (not required).</summary>
    public int? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>
/// Configurable service / fee reference (e.g. "OPD Registration", "Emergency Registration", "Consultation – Dr X").
/// No payment is recorded in Phase 2; the future billing module creates receipts against encounters using this reference.
/// </summary>
public class ServiceItem
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public EncounterType Category { get; set; }
    public decimal DefaultFee { get; set; }
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public class PrintedDocument
{
    public long Id { get; set; }
    public long EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    public PrintedDocumentType DocumentType { get; set; }
    [MaxLength(10)] public string PaperSize { get; set; } = "A5";
    public int PrintNumber { get; set; }
    public DateTime PrintedUtc { get; set; }
    public int? PrintedByUserId { get; set; }
    [MaxLength(64)] public string? PrintedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
}

public enum TokenScopeMode { Hospital = 1, Department = 2, Doctor = 3 }

/// <summary>Patient-registration, numbering and slip settings (single row, Id = 1).</summary>
public class RegistrationSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;

    [MaxLength(12)] public string PatientNumberPrefix { get; set; } = "BNH-";
    public int PatientNumberDigits { get; set; } = 6;

    [MaxLength(12)] public string OpdNumberPrefix { get; set; } = "OPD-";
    [MaxLength(12)] public string EmergencyNumberPrefix { get; set; } = "ER-";
    public bool EncounterNumberIncludesYear { get; set; } = true;
    public int EncounterNumberDigits { get; set; } = 6;

    public bool OpdTokenEnabled { get; set; } = true;
    public TokenScopeMode OpdTokenScope { get; set; } = TokenScopeMode.Department;
    public int? DefaultOpdDepartmentId { get; set; }
    /// <summary>In Private hospital mode the visit must be recorded against a doctor.</summary>
    public bool RequireDoctorForPrivateOpd { get; set; } = true;

    [MaxLength(10)] public string OpdSlipPaper { get; set; } = "A5";
    [MaxLength(10)] public string EmergencySlipPaper { get; set; } = "A4";
    /// <summary>Open the print dialog automatically right after a visit is registered.</summary>
    public bool AutoPrintAfterRegistration { get; set; } = true;

    /// <summary>Fee reference shown for future billing; 0 = free OPD.</summary>
    public int? DefaultOpdServiceItemId { get; set; }
    public int? DefaultEmergencyServiceItemId { get; set; }

    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}
