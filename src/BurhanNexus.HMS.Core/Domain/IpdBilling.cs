using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

// =====================================================================================
// Update 10 – inpatient billing: ward / bed policy, NICU equipment, packages, advances, clearance.
// Every amount is still a Phase 3 charge, receipt or refund – these tables only add the IPD detail.
// =====================================================================================

/// <summary>How daily ward / bed charges are counted (hospital policy, IPD settings). Never hard-coded.</summary>
public enum BedChargePolicy
{
    /// <summary>One charge per night spent in a bed (earlier behaviour).</summary>
    PerNight = 0,
    /// <summary>Full day per billing day that starts at the cutoff time (e.g. 12:00); any part of a billing day counts.</summary>
    CutoffDay = 1,
    /// <summary>As <see cref="CutoffDay"/>, but a billing day used for less than the half-day hours is charged half.</summary>
    HalfDay = 2,
    /// <summary>Hours actually spent in the bed (service fee per hour).</summary>
    Hourly = 3,
}

/// <summary>NICU equipment type with its charging service (rate and unit come from Fees &amp; Services).</summary>
public class NicuEquipment
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public enum EquipmentUsageStatus { Active = 1, Ended = 2, Cancelled = 3 }

/// <summary>One period of a piece of equipment on one patient. Replacement or transfer ends the period and starts a new one.</summary>
public class EquipmentUsage
{
    public long Id { get; set; }
    public Guid IdempotencyKey { get; set; }
    public long AdmissionId { get; set; }
    public IpdAdmission? Admission { get; set; }
    public long PatientId { get; set; }
    public int EquipmentId { get; set; }
    public NicuEquipment? Equipment { get; set; }
    [MaxLength(100)] public string EquipmentName { get; set; } = string.Empty;
    /// <summary>Serial / asset number of the device (one device cannot be on two patients at once).</summary>
    [MaxLength(50)] public string? Identifier { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public ChargeUnit ChargeUnit { get; set; }
    /// <summary>Rate at the start of the period (later fee changes do not change it).</summary>
    public decimal UnitPrice { get; set; }
    public decimal? Quantity { get; set; }
    public long? ChargeId { get; set; }
    /// <summary>Included in a package of the admission – no separate charge.</summary>
    public long? CoveredByAdmissionPackageId { get; set; }
    public EquipmentUsageStatus Status { get; set; } = EquipmentUsageStatus.Active;
    [MaxLength(300)] public string? Notes { get; set; }
    [MaxLength(300)] public string? EndReason { get; set; }
    public int? StartedByUserId { get; set; }
    [MaxLength(64)] public string? StartedByUsername { get; set; }
    [MaxLength(64)] public string? EndedByUsername { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Optional package (e.g. Basic NICU, Phototherapy) priced as a service; the included services are not billed again.</summary>
public class ServicePackage
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    /// <summary>Service that carries the package price (Fees &amp; Services).</summary>
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    [MaxLength(500)] public string? Excluded { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<ServicePackageItem> Items { get; set; } = new();
}

public class ServicePackageItem
{
    public int PackageId { get; set; }
    public ServicePackage? Package { get; set; }
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
}

public class AdmissionPackage
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public int PackageId { get; set; }
    public ServicePackage? Package { get; set; }
    public long? ChargeId { get; set; }
    public DateTime AppliedUtc { get; set; }
    [MaxLength(64)] public string? AppliedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? RemovedUtc { get; set; }
    [MaxLength(64)] public string? RemovedBy { get; set; }
    [MaxLength(300)] public string? RemovedReason { get; set; }
}

public enum AdvanceStatus { Valid = 1, Cancelled = 2 }

/// <summary>Advance deposit of a patient (usually for an admission). Its use is recorded as normal Phase 3 receipts.</summary>
public class PatientAdvance
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public long? AdmissionId { get; set; }
    public decimal Amount { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public int CounterId { get; set; }
    public int CashierUserId { get; set; }
    [MaxLength(64)] public string CashierUsername { get; set; } = string.Empty;
    [MaxLength(100)] public string? Reference { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public AdvanceStatus Status { get; set; } = AdvanceStatus.Valid;
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public uint RowVersion { get; set; }
    public decimal Available => Status == AdvanceStatus.Valid ? Amount - AppliedAmount - RefundedAmount : 0;
}

public enum AdvanceEntryType { Received = 1, Applied = 2, Refunded = 3 }

/// <summary>Insert-only history of an advance deposit.</summary>
public class PatientAdvanceEntry
{
    public long Id { get; set; }
    public long AdvanceId { get; set; }
    public AdvanceEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public long? ReceiptId { get; set; }
    [MaxLength(30)] public string? Reference { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

/// <summary>Authorised discharge with an outstanding balance (reason required, insert-only, audited).</summary>
public class DischargeClearanceOverride
{
    public long Id { get; set; }
    public long AdmissionId { get; set; }
    public decimal Outstanding { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}
