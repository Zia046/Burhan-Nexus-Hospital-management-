using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

// =====================================================================================================
// Phase 4 – Laboratory management.
// The catalogue (tests, parameters, reference ranges) is "live"; every booked test keeps its own snapshot of the
// definition and of the reference range that applied to the patient at booking, so later catalogue changes never
// alter earlier orders or reports. Verified results are never overwritten: a withdrawal stores a revision first.
// =====================================================================================================

public class LabSampleType
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(60)] public string Name { get; set; } = string.Empty;
    [MaxLength(100)] public string? Container { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>Laboratory section / category (Haematology, Biochemistry ...).</summary>
public class LabCategory
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LabTest
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public LabCategory? Category { get; set; }
    public int SampleTypeId { get; set; }
    public LabSampleType? SampleType { get; set; }
    /// <summary>Phase 3 service that carries the price and fee history (billing goes through the existing cashier system).</summary>
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    [MaxLength(500)] public string? CollectionInstructions { get; set; }
    [MaxLength(100)] public string? Method { get; set; }
    /// <summary>Default comment / interpretation printed with the result.</summary>
    [MaxLength(1000)] public string? Interpretation { get; set; }
    public int? TurnaroundHours { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    /// <summary>Increases with every definition change (shown on orders booked with it).</summary>
    public int DefinitionVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
    public List<LabTestParameter> Parameters { get; set; } = new();
}

public enum LabResultType { Numeric = 1, Text = 2, PositiveNegative = 3, Options = 4 }

public class LabTestParameter
{
    public int Id { get; set; }
    public int LabTestId { get; set; }
    public LabTest? LabTest { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Unit { get; set; }
    public LabResultType ResultType { get; set; } = LabResultType.Numeric;
    /// <summary>Choices for <see cref="LabResultType.Options"/>, separated by '|'.</summary>
    [MaxLength(500)] public string? Options { get; set; }
    public int Decimals { get; set; } = 1;
    public bool IsOptional { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    [MaxLength(60)] public string? DefaultValue { get; set; }
    public List<LabReferenceRange> Ranges { get; set; } = new();
}

/// <summary>Reference range; the most specific one (gender + age) that matches the patient is used.</summary>
public class LabReferenceRange
{
    public int Id { get; set; }
    public int ParameterId { get; set; }
    public LabTestParameter? Parameter { get; set; }
    /// <summary>Null = both genders.</summary>
    public Gender? Gender { get; set; }
    /// <summary>Age limits in days (null = no limit). Upper limit exclusive.</summary>
    public int? MinAgeDays { get; set; }
    public int? MaxAgeDays { get; set; }
    public decimal? Low { get; set; }
    public decimal? High { get; set; }
    public decimal? CriticalLow { get; set; }
    public decimal? CriticalHigh { get; set; }
    /// <summary>Expected / normal text (e.g. "Negative", "Not seen") or a free-text range shown on the report.</summary>
    [MaxLength(100)] public string? NormalText { get; set; }
}

public enum LabReferralType { None = 0, HospitalDoctor = 1, External = 2 }

public enum LabOrderItemStatus
{
    SamplePending = 1,
    SampleCollected = 2,
    SampleReceived = 3,
    InProgress = 4,
    /// <summary>Result entered, waiting for verification.</summary>
    AwaitingVerification = 5,
    Verified = 6,
    /// <summary>Verified report printed / handed over.</summary>
    Released = 7,
    Cancelled = 8,
}

public class LabOrder
{
    public long Id { get; set; }
    [MaxLength(30)] public string OrderNumber { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public long? EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    public LabReferralType ReferralType { get; set; }
    public int? ReferringDoctorId { get; set; }
    public Doctor? ReferringDoctor { get; set; }
    [MaxLength(150)] public string? ExternalDoctorName { get; set; }
    [MaxLength(1000)] public string? ClinicalNotes { get; set; }
    public bool IsUrgent { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime BookedUtc { get; set; }
    public int? BookedByUserId { get; set; }
    [MaxLength(64)] public string? BookedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
    public List<LabOrderItem> Items { get; set; } = new();
    public List<LabSample> Samples { get; set; } = new();

    public string ReferredBy => ReferralType switch
    {
        LabReferralType.HospitalDoctor => ReferringDoctor?.Name ?? "",
        LabReferralType.External => ExternalDoctorName ?? "",
        _ => "",
    };
}

public class LabOrderItem
{
    public long Id { get; set; }
    public long LabOrderId { get; set; }
    public LabOrder? Order { get; set; }
    public int LabTestId { get; set; }
    public LabTest? LabTest { get; set; }
    // ---- snapshot of the test definition at booking ----
    [MaxLength(30)] public string TestCode { get; set; } = string.Empty;
    [MaxLength(150)] public string TestName { get; set; } = string.Empty;
    [MaxLength(80)] public string CategoryName { get; set; } = string.Empty;
    [MaxLength(60)] public string SampleTypeName { get; set; } = string.Empty;
    [MaxLength(100)] public string? Method { get; set; }
    public int DefinitionVersion { get; set; }
    public int SortOrder { get; set; }
    // ---- workflow ----
    public LabOrderItemStatus Status { get; set; } = LabOrderItemStatus.SamplePending;
    public long? SampleId { get; set; }
    public LabSample? Sample { get; set; }
    /// <summary>Phase 3 charge (bill line) of this test.</summary>
    public long? ChargeId { get; set; }
    public Charge? Charge { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
    public DateTime? ResultEnteredUtc { get; set; }
    public int? ResultEnteredByUserId { get; set; }
    [MaxLength(64)] public string? ResultEnteredByUsername { get; set; }
    public DateTime? VerifiedUtc { get; set; }
    public int? VerifiedByUserId { get; set; }
    [MaxLength(64)] public string? VerifiedByUsername { get; set; }
    [MaxLength(150)] public string? VerifiedByName { get; set; }
    /// <summary>Reason the verifier sent the result back (shown to the technician until corrected).</summary>
    [MaxLength(300)] public string? CorrectionReason { get; set; }
    public DateTime? ReleasedUtc { get; set; }
    public int RevisionNo { get; set; }
    public DateTime? CancelledUtc { get; set; }
    [MaxLength(64)] public string? CancelledByUsername { get; set; }
    [MaxLength(300)] public string? CancelReason { get; set; }
    public uint RowVersion { get; set; }
    public List<LabResultValue> Values { get; set; } = new();
}

public enum LabFlag { None = 0, Low = 1, High = 2, Abnormal = 3, CriticalLow = 4, CriticalHigh = 5 }

/// <summary>One parameter of a booked test: definition snapshot (name, unit, reference range) + the patient's result.</summary>
public class LabResultValue
{
    public long Id { get; set; }
    public long OrderItemId { get; set; }
    public LabOrderItem? OrderItem { get; set; }
    public int? ParameterId { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Unit { get; set; }
    public LabResultType ResultType { get; set; }
    [MaxLength(500)] public string? Options { get; set; }
    public int Decimals { get; set; }
    public bool IsOptional { get; set; }
    public int SortOrder { get; set; }
    public decimal? RefLow { get; set; }
    public decimal? RefHigh { get; set; }
    public decimal? CriticalLow { get; set; }
    public decimal? CriticalHigh { get; set; }
    [MaxLength(100)] public string? RefText { get; set; }
    // ---- result ----
    [MaxLength(200)] public string? Value { get; set; }
    public decimal? NumericValue { get; set; }
    public LabFlag Flag { get; set; }

    public string ReferenceDisplay
    {
        get
        {
            string F(decimal v) => v.ToString(Decimals <= 0 ? "0" : "0." + new string('#', Decimals), System.Globalization.CultureInfo.InvariantCulture);
            if (RefLow is { } l && RefHigh is { } h) return $"{F(l)} – {F(h)}";
            if (RefLow is { } l2) return $"≥ {F(l2)}";
            if (RefHigh is { } h2) return $"≤ {F(h2)}";
            return RefText ?? "";
        }
    }
}

public enum LabSampleStatus { Pending = 1, Collected = 2, Received = 3, Rejected = 4 }

/// <summary>One physical sample of a visit (all tests needing the same sample type share it).</summary>
public class LabSample
{
    public long Id { get; set; }
    [MaxLength(30)] public string SampleNumber { get; set; } = string.Empty;
    public long LabOrderId { get; set; }
    public LabOrder? Order { get; set; }
    [MaxLength(60)] public string SampleTypeName { get; set; } = string.Empty;
    public LabSampleStatus Status { get; set; } = LabSampleStatus.Pending;
    public DateTime CreatedUtc { get; set; }
    public DateTime? CollectedUtc { get; set; }
    [MaxLength(64)] public string? CollectedByUsername { get; set; }
    public DateTime? ReceivedUtc { get; set; }
    [MaxLength(64)] public string? ReceivedByUsername { get; set; }
    public DateTime? RejectedUtc { get; set; }
    [MaxLength(64)] public string? RejectedByUsername { get; set; }
    [MaxLength(300)] public string? RejectReason { get; set; }
    /// <summary>The new sample requested after a rejection.</summary>
    public long? ReplacedBySampleId { get; set; }
    public DateOnly BusinessDate { get; set; }
}

public enum LabEventType
{
    Booked = 1, SampleCollected = 2, SampleReceived = 3, SampleRejected = 4, ResultSaved = 5, ResultSubmitted = 6, ResultEdited = 7,
    Verified = 8, VerificationRejected = 9, Released = 10, ResultWithdrawn = 11, Cancelled = 12, Printed = 13,
}

/// <summary>Insert-only history of every step of a booked test (also the basis of verification / rejection reports).</summary>
public class LabItemEvent
{
    public long Id { get; set; }
    public long OrderItemId { get; set; }
    public LabEventType EventType { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

/// <summary>Copy of a result as it was before it was withdrawn / corrected (verified reports are never overwritten silently).</summary>
public class LabResultRevision
{
    public long Id { get; set; }
    public long OrderItemId { get; set; }
    public int RevisionNo { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    /// <summary>JSON: values, flags, comment, entered / verified by and when.</summary>
    public string Snapshot { get; set; } = "{}";
    public LabOrderItemStatus PreviousStatus { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}

public class LabReportPrint
{
    public long Id { get; set; }
    public long LabOrderId { get; set; }
    /// <summary>Comma separated order item ids that were printed.</summary>
    [MaxLength(500)] public string ItemIds { get; set; } = string.Empty;
    public int CopyNumber { get; set; }
    [MaxLength(20)] public string Format { get; set; } = "A4";
    public DateTime PrintedUtc { get; set; }
    [MaxLength(64)] public string? PrintedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
}

public enum LabReportLayout { Standard = 1, Compact = 2 }

public class LabSettings
{
    public int Id { get; set; } = 1;
    [MaxLength(150)] public string? LaboratoryName { get; set; }
    [MaxLength(200)] public string? HospitalName { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(100)] public string? ContactNumber { get; set; }
    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(300)] public string? ReportHeader { get; set; }
    [MaxLength(500)] public string? ReportFooter { get; set; } = "This report is electronically generated. Please correlate clinically.";
    public LabReportLayout Layout { get; set; } = LabReportLayout.Standard;
    [MaxLength(150)] public string? SignatoryName { get; set; }
    [MaxLength(150)] public string? SignatoryDesignation { get; set; }
    public byte[]? Logo { get; set; }
    [MaxLength(50)] public string? LogoContentType { get; set; }
    public byte[]? Signature { get; set; }
    [MaxLength(50)] public string? SignatureContentType { get; set; }
    public bool ShowVerifiedBy { get; set; } = true;
    /// <summary>Hospital policy: may the person who entered a result also verify it (only with the extra permission)?</summary>
    public bool AllowSelfVerification { get; set; }
    /// <summary>Only release (print) reports of fully paid tests.</summary>
    public bool RequirePaymentBeforeRelease { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

public enum LabInventoryKind { Reagent = 1, Consumable = 2, TestKit = 3, Other = 9 }

/// <summary>Foundation for a later inventory phase: item master only (no stock accounting yet).</summary>
public class LabInventoryItem
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public LabInventoryKind Kind { get; set; }
    [MaxLength(30)] public string? Unit { get; set; }
    [MaxLength(150)] public string? Supplier { get; set; }
    public decimal? ReorderLevel { get; set; }
    public bool TracksExpiry { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
}
