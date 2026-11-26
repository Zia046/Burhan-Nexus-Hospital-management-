using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

// =====================================================================================================
// Phase 6 – Radiology (X-Ray & Ultrasound).
// The catalogue and templates are "live"; every booked examination keeps a snapshot of the service it was booked
// with, and the report text lives on the examination itself. A verified report is never overwritten: an amendment
// first stores the previous text as a revision. Fees go through the Phase 3 cashier (one charge per examination).
// Attachments (images, PDFs, DICOM files) are stored as files in a server folder; only their metadata is in the DB.
// =====================================================================================================

/// <summary>Update 15: ECG (electrocardiography) is booked, charged and listed like the other radiology services.</summary>
public enum RadiologyModality { XRay = 1, Ultrasound = 2, Ecg = 3 }

public enum RadiologyPriority { Routine = 1, Urgent = 2 }

public enum RadiologyReferralType { None = 0, HospitalDoctor = 1, External = 2 }

public class RadiologyService
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public RadiologyModality Modality { get; set; }
    /// <summary>Body part / examination type (Chest, Abdomen, Obstetric …).</summary>
    [MaxLength(100)] public string? BodyPart { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    [MaxLength(1000)] public string? PreparationInstructions { get; set; }
    public int? DurationMinutes { get; set; }
    /// <summary>Report heading printed on the A4 report (default: service name).</summary>
    [MaxLength(150)] public string? ReportTitle { get; set; }
    /// <summary>Template loaded into a new report of this examination.</summary>
    public int? DefaultTemplateId { get; set; }
    public RadiologyTemplate? DefaultTemplate { get; set; }
    /// <summary>Phase 3 service that carries the fee and its history.</summary>
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Editable report template (normal findings etc.). Copied into a report when it is started – later edits never change existing reports.</summary>
public class RadiologyTemplate
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public RadiologyModality Modality { get; set; }
    [MaxLength(2000)] public string? Technique { get; set; }
    [MaxLength(8000)] public string? Findings { get; set; }
    [MaxLength(2000)] public string? Impression { get; set; }
    [MaxLength(2000)] public string? Recommendations { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedUtc { get; set; }
    [MaxLength(64)] public string? UpdatedByUsername { get; set; }
}

public enum RadiologyExamStatus
{
    Booked = 1,
    /// <summary>Patient has arrived and is waiting in the queue.</summary>
    Waiting = 2,
    InProgress = 3,
    ExamCompleted = 4,
    /// <summary>Report sent back by the verifier / withdrawn – needs correction.</summary>
    ReportPending = 5,
    /// <summary>Draft report saved, not yet submitted.</summary>
    ReportEntered = 6,
    AwaitingVerification = 7,
    Verified = 8,
    /// <summary>Verified report printed / handed over.</summary>
    Released = 9,
    Cancelled = 10,
}

public class RadiologyOrder
{
    public long Id { get; set; }
    [MaxLength(30)] public string OrderNumber { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public long? EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    /// <summary>Phase 7: ordered for an inpatient admission.</summary>
    public long? IpdAdmissionId { get; set; }
    public int? ReferringDepartmentId { get; set; }
    public Department? ReferringDepartment { get; set; }
    public RadiologyReferralType ReferralType { get; set; }
    public int? ReferringDoctorId { get; set; }
    public Doctor? ReferringDoctor { get; set; }
    [MaxLength(150)] public string? ExternalDoctorName { get; set; }
    [MaxLength(1000)] public string? ClinicalIndication { get; set; }
    public RadiologyPriority Priority { get; set; } = RadiologyPriority.Routine;
    public DateOnly BusinessDate { get; set; }
    public DateTime BookedUtc { get; set; }
    public int? BookedByUserId { get; set; }
    [MaxLength(64)] public string? BookedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
    public List<RadiologyExam> Exams { get; set; } = new();

    public string ReferredBy => ReferralType switch
    {
        RadiologyReferralType.HospitalDoctor => ReferringDoctor?.Name ?? "",
        RadiologyReferralType.External => ExternalDoctorName ?? "",
        _ => "",
    };
}

/// <summary>One booked examination (= one report).</summary>
public class RadiologyExam
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public RadiologyOrder? Order { get; set; }
    public int ServiceId { get; set; }
    public RadiologyService? Service { get; set; }
    // ---- snapshot of the service at booking ----
    [MaxLength(30)] public string ServiceCode { get; set; } = string.Empty;
    [MaxLength(150)] public string ServiceName { get; set; } = string.Empty;
    public RadiologyModality Modality { get; set; }
    [MaxLength(100)] public string? BodyPart { get; set; }
    [MaxLength(150)] public string? ReportTitle { get; set; }
    // ---- queue ----
    /// <summary>Hospital-local date and time the examination is booked for.</summary>
    public DateTime ScheduledUtc { get; set; }
    public DateOnly ScheduledDate { get; set; }
    /// <summary>Daily token per modality (shared by the examinations of one order and modality).</summary>
    public int TokenNumber { get; set; }
    public int SortOrder { get; set; }
    public RadiologyExamStatus Status { get; set; } = RadiologyExamStatus.Booked;
    /// <summary>Phase 3 charge (bill line) of this examination.</summary>
    public long? ChargeId { get; set; }
    public Charge? Charge { get; set; }
    // ---- examination ----
    public DateTime? ArrivedUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public int? PerformedByUserId { get; set; }
    [MaxLength(64)] public string? PerformedByUsername { get; set; }
    [MaxLength(150)] public string? PerformedByName { get; set; }
    [MaxLength(1000)] public string? TechnicalNotes { get; set; }
    // ---- report ----
    [MaxLength(1000)] public string? ClinicalIndication { get; set; }
    [MaxLength(2000)] public string? Technique { get; set; }
    [MaxLength(8000)] public string? Findings { get; set; }
    [MaxLength(2000)] public string? Impression { get; set; }
    [MaxLength(2000)] public string? Recommendations { get; set; }
    /// <summary>Radiologist / sonologist printed on the report (may differ from the user who typed it).</summary>
    [MaxLength(150)] public string? ReportingDoctorName { get; set; }
    [MaxLength(150)] public string? ReportingDoctorDesignation { get; set; }
    [MaxLength(120)] public string? TemplateName { get; set; }
    public DateTime? ReportSavedUtc { get; set; }
    public int? ReportedByUserId { get; set; }
    [MaxLength(64)] public string? ReportedByUsername { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? VerifiedUtc { get; set; }
    public int? VerifiedByUserId { get; set; }
    [MaxLength(64)] public string? VerifiedByUsername { get; set; }
    [MaxLength(150)] public string? VerifiedByName { get; set; }
    [MaxLength(300)] public string? CorrectionReason { get; set; }
    public DateTime? ReleasedUtc { get; set; }
    public int RevisionNo { get; set; }
    public DateTime? CancelledUtc { get; set; }
    [MaxLength(64)] public string? CancelledByUsername { get; set; }
    [MaxLength(300)] public string? CancelReason { get; set; }
    public uint RowVersion { get; set; }
}

public enum RadiologyEventType
{
    Booked = 1, Rescheduled = 2, Arrived = 3, Started = 4, Completed = 5, ReportSaved = 6, ReportSubmitted = 7,
    Verified = 8, VerificationRejected = 9, Released = 10, ReportAmended = 11, Cancelled = 12, Printed = 13,
    AttachmentAdded = 14, AttachmentRemoved = 15,
}

/// <summary>Insert-only history of every step of an examination.</summary>
public class RadiologyExamEvent
{
    public long Id { get; set; }
    public long ExamId { get; set; }
    public RadiologyEventType EventType { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

/// <summary>Copy of a report as it was before it was sent back or amended.</summary>
public class RadiologyReportRevision
{
    public long Id { get; set; }
    public long ExamId { get; set; }
    public int RevisionNo { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    /// <summary>JSON: report text, reporting doctor, entered / verified by and when.</summary>
    public string Snapshot { get; set; } = "{}";
    public RadiologyExamStatus PreviousStatus { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}

public class RadiologyReportPrint
{
    public long Id { get; set; }
    public long ExamId { get; set; }
    public int CopyNumber { get; set; }
    [MaxLength(20)] public string Format { get; set; } = "A4";
    public DateTime PrintedUtc { get; set; }
    [MaxLength(64)] public string? PrintedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
}

public enum RadiologyFileKind { Image = 1, Pdf = 2, Dicom = 3 }

/// <summary>An image / document of an examination. The file is in the radiology storage folder; never physically deleted.</summary>
public class RadiologyAttachment
{
    public long Id { get; set; }
    public long ExamId { get; set; }
    public long PatientId { get; set; }
    [MaxLength(200)] public string OriginalFileName { get; set; } = string.Empty;
    /// <summary>Path relative to the storage folder (yyyy/MM/guid.ext).</summary>
    [MaxLength(200)] public string StoredPath { get; set; } = string.Empty;
    [MaxLength(80)] public string ContentType { get; set; } = string.Empty;
    public RadiologyFileKind Kind { get; set; }
    public long SizeBytes { get; set; }
    [MaxLength(64)] public string Sha256 { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public DateTime UploadedUtc { get; set; }
    [MaxLength(64)] public string? UploadedByUsername { get; set; }
    public bool IsRemoved { get; set; }
    public DateTime? RemovedUtc { get; set; }
    [MaxLength(64)] public string? RemovedByUsername { get; set; }
    [MaxLength(300)] public string? RemoveReason { get; set; }
}

public class RadiologySettings
{
    public int Id { get; set; } = 1;
    [MaxLength(150)] public string? DepartmentName { get; set; } = "Department of Radiology";
    [MaxLength(200)] public string? HospitalName { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(100)] public string? ContactNumber { get; set; }
    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(300)] public string? ReportHeader { get; set; }
    [MaxLength(500)] public string? ReportFooter { get; set; } = "This report is based on the imaging findings and should be correlated clinically.";
    /// <summary>False = the hospital prints on its own pre-printed letterhead: no software header, space left at the top.</summary>
    public bool PrintHeader { get; set; } = true;
    public int LetterheadTopMarginMm { get; set; } = 45;
    [MaxLength(150)] public string? DefaultReportingDoctor { get; set; }
    [MaxLength(150)] public string? DefaultDesignation { get; set; }
    public byte[]? Logo { get; set; }
    [MaxLength(50)] public string? LogoContentType { get; set; }
    public byte[]? Signature { get; set; }
    [MaxLength(50)] public string? SignatureContentType { get; set; }
    public bool ShowVerifiedBy { get; set; } = true;
    /// <summary>Hospital policy: may the user who reported also verify (only with the extra permission)?</summary>
    public bool AllowSelfVerification { get; set; }
    public bool RequirePaymentBeforeRelease { get; set; }
    /// <summary>Allow bookings without an OPD / Emergency visit (default: Private yes, Government no).</summary>
    public bool AllowBookingWithoutVisit { get; set; } = true;
    /// <summary>How old an OPD / Emergency visit may be for a booking against it.</summary>
    public int VisitValidDays { get; set; } = 7;
    public int MaxUploadMb { get; set; } = 25;
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}
