using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

/// <summary>Update 9 – nursing medicine demand for an admitted patient (ward → pharmacy).</summary>
public enum MedicineRequestStatus { Pending = 1, Approved = 2, PartlyIssued = 3, Completed = 4, Rejected = 5, Cancelled = 6 }

public enum MedicineRequestLineStatus { Pending = 1, Approved = 2, PartlyIssued = 3, Issued = 4, Rejected = 5, Unavailable = 6, Cancelled = 7 }

public enum MedicineRequestPriority { Routine = 1, Urgent = 2 }

/// <summary>
/// A nurse's request to the pharmacy for medicines of one admitted patient. Nothing leaves stock when it is created:
/// stock is deducted only when the pharmacy issues (through the normal patient issue, linked to the admission).
/// </summary>
public class MedicineRequest
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public long AdmissionId { get; set; }
    public IpdAdmission? Admission { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int? WardId { get; set; }
    [MaxLength(100)] public string? WardName { get; set; }
    [MaxLength(30)] public string? BedCode { get; set; }
    public MedicineRequestPriority Priority { get; set; } = MedicineRequestPriority.Routine;
    public MedicineRequestStatus Status { get; set; } = MedicineRequestStatus.Pending;
    [MaxLength(500)] public string? Notes { get; set; }
    public DateTime RequestedUtc { get; set; }
    public int? RequestedByUserId { get; set; }
    [MaxLength(64)] public string? RequestedByUsername { get; set; }
    [MaxLength(150)] public string? RequestedByName { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
    public List<MedicineRequestLine> Lines { get; set; } = new();
}

public class MedicineRequestLine
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public MedicineRequest? Request { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int IssuedQuantity { get; set; }
    [MaxLength(300)] public string? Instructions { get; set; }
    public MedicineRequestLineStatus Status { get; set; } = MedicineRequestLineStatus.Pending;
    [MaxLength(300)] public string? DecisionNote { get; set; }
    public DateTime? DecidedUtc { get; set; }
    [MaxLength(64)] public string? DecidedBy { get; set; }
    public uint RowVersion { get; set; }
    public int Remaining => Status is MedicineRequestLineStatus.Rejected or MedicineRequestLineStatus.Unavailable or MedicineRequestLineStatus.Cancelled ? 0 : Math.Max(0, Quantity - IssuedQuantity);
}

/// <summary>Insert-only history of a request: created, approved, issued (with the pharmacy issue number), rejected, unavailable, cancelled.</summary>
public class MedicineRequestEvent
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public long? LineId { get; set; }
    [MaxLength(30)] public string Event { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public long? PharmacyIssueId { get; set; }
    [MaxLength(30)] public string? PharmacyIssueNumber { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}
