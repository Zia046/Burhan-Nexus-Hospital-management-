using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

// =====================================================================================================
// Phase 3 – Financial, cashier & receipt management.
// Money is always decimal (PostgreSQL numeric(14,2)). Financial rows are never deleted or overwritten:
// cancellations, refunds, deposits, settlements and adjustments are separate, linked records, and every
// balance is the sum of ledger entries.
// =====================================================================================================

/// <summary>A cash / collection counter (OPD reception, emergency, laboratory, ...).</summary>
public class Counter
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
    public List<CounterAssignment> Assignments { get; set; } = new();
}

/// <summary>Assignment of an employee to a counter. History is kept: a transfer ends one row and starts another.</summary>
public class CounterAssignment
{
    public int Id { get; set; }
    public int CounterId { get; set; }
    public Counter? Counter { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime AssignedUtc { get; set; }
    [MaxLength(64)] public string? AssignedBy { get; set; }
    public DateTime? EndedUtc { get; set; }
    [MaxLength(64)] public string? EndedBy { get; set; }
    public bool IsCurrent => EndedUtc is null;
}

public class PaymentMethod
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(60)] public string Name { get; set; } = string.Empty;
    /// <summary>Money stays with the cashier (cash) and must be deposited to Admin. Bank transfer / card go directly to the hospital.</summary>
    public bool CountsAsCashInHand { get; set; }
    public bool RequiresReference { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public enum ServiceKind
{
    OpdRegistration = 1, EmergencyRegistration = 2, Consultation = 3, Laboratory = 4, XRay = 5, Ultrasound = 6, Procedure = 7, Other = 99
}

/// <summary>Fee history of a service: a change only affects transactions on/after its effective date.</summary>
public class ServiceFee
{
    public int Id { get; set; }
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public decimal Fee { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

/// <summary>Counters allowed to collect a service (no rows = any counter).</summary>
public class ServiceCounter
{
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public int CounterId { get; set; }
    public Counter? Counter { get; set; }
}

public enum DoctorShareType
{
    /// <summary>Hospital keeps the whole fee (e.g. salaried doctor).</summary>
    HospitalKeepsAll = 0,
    /// <summary>Option A – hospital collects the full fee and later pays all of it to the doctor.</summary>
    FullFeeToDoctor = 1,
    /// <summary>Option B – hospital keeps a commission percentage, the rest is payable to the doctor.</summary>
    HospitalCommissionPercent = 2,
    /// <summary>Option C – doctor receives a percentage of the fee.</summary>
    DoctorPercent = 3,
    /// <summary>Option C – doctor receives a fixed amount per consultation (capped at the fee).</summary>
    DoctorFixedAmount = 4,
    /// <summary>Option C – hospital keeps a fixed amount per consultation, the rest is payable to the doctor.</summary>
    HospitalFixedAmount = 5,
}

public enum ChargeStatus { Unpaid = 1, PartiallyPaid = 2, Paid = 3, Cancelled = 4 }

/// <summary>
/// One billable service for a patient (the "bill line"). Holds the fee as charged at that time.
/// Payments against it are receipt lines; partial payments leave a balance that later receipts can settle.
/// </summary>
public class Charge
{
    public long Id { get; set; }
    [MaxLength(30)] public string ChargeNumber { get; set; } = string.Empty;
    public long? PatientId { get; set; }
    public Patient? Patient { get; set; }
    public long? EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    /// <summary>Phase 4: laboratory order that created this charge.</summary>
    public long? LabOrderId { get; set; }
    public int ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public ServiceKind ServiceKind { get; set; }
    [MaxLength(150)] public string ServiceName { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    [MaxLength(150)] public string? DoctorName { get; set; }
    public decimal Fee { get; set; }
    public decimal Discount { get; set; }
    public decimal NetAmount { get; set; }
    /// <summary>Sum of valid (not cancelled) receipt lines.</summary>
    public decimal PaidAmount { get; set; }
    /// <summary>Sum of completed refunds against this charge.</summary>
    public decimal RefundedAmount { get; set; }
    public bool AllowPartialPayment { get; set; }
    [MaxLength(200)] public string? DiscountReason { get; set; }
    // Doctor share snapshot at charge time (arrangement changes only affect new charges).
    public DoctorShareType DoctorShareType { get; set; }
    public decimal DoctorShareValue { get; set; }
    public decimal DoctorShareAmount { get; set; }
    public ChargeStatus Status { get; set; }
    /// <summary>The receipt in which this charge was created (cancelling that receipt voids the charge if nothing else was paid).</summary>
    public long? OriginReceiptId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public uint RowVersion { get; set; }
    public decimal Balance => NetAmount - PaidAmount;
}

public enum ReceiptStatus { Valid = 1, Cancelled = 2 }

public class Receipt
{
    public long Id { get; set; }
    [MaxLength(30)] public string ReceiptNumber { get; set; } = string.Empty;
    /// <summary>Client-generated key: a double-click or resubmission returns the same receipt instead of charging twice.</summary>
    public Guid IdempotencyKey { get; set; }
    public long? PatientId { get; set; }
    public Patient? Patient { get; set; }
    [MaxLength(150)] public string? PayerName { get; set; }
    public int CounterId { get; set; }
    public Counter? Counter { get; set; }
    public int CashierUserId { get; set; }
    public User? Cashier { get; set; }
    [MaxLength(64)] public string CashierUsername { get; set; } = string.Empty;
    [MaxLength(150)] public string CashierName { get; set; } = string.Empty;
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    [MaxLength(100)] public string? PaymentReference { get; set; }
    public decimal TotalFee { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalNet { get; set; }
    /// <summary>Amount received on this receipt.</summary>
    public decimal AmountPaid { get; set; }
    /// <summary>Balance still payable on the charges of this receipt after this payment.</summary>
    public decimal BalanceAfter { get; set; }
    public decimal RefundedAmount { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Valid;
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    [MaxLength(64)] public string? CancelledByUsername { get; set; }
    [MaxLength(300)] public string? CancelReason { get; set; }
    public uint RowVersion { get; set; }
    public List<ReceiptLine> Lines { get; set; } = new();
    public List<ReceiptPrint> Prints { get; set; } = new();
}

public class ReceiptLine
{
    public long Id { get; set; }
    public long ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
    public long ChargeId { get; set; }
    public Charge? Charge { get; set; }
    [MaxLength(150)] public string ServiceName { get; set; } = string.Empty;
    [MaxLength(150)] public string? DoctorName { get; set; }
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public decimal Fee { get; set; }
    public decimal Discount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PreviouslyPaid { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    /// <summary>Part of <see cref="Amount"/> payable to the doctor.</summary>
    public decimal DoctorShare { get; set; }
    public decimal RefundedAmount { get; set; }
}

public class ReceiptPrint
{
    public long Id { get; set; }
    public long ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
    public int CopyNumber { get; set; }
    [MaxLength(10)] public string Format { get; set; } = "80mm";
    [MaxLength(40)] public string Method { get; set; } = "browser";
    public DateTime PrintedUtc { get; set; }
    public int? PrintedByUserId { get; set; }
    [MaxLength(64)] public string? PrintedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
}

public enum RefundStatus { Pending = 1, Completed = 2, Rejected = 3 }

public class Refund
{
    public long Id { get; set; }
    [MaxLength(30)] public string RefundNumber { get; set; } = string.Empty;
    public long ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public DateTime RequestedUtc { get; set; }
    public int? RequestedByUserId { get; set; }
    [MaxLength(64)] public string? RequestedByUsername { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public int? DecidedByUserId { get; set; }
    [MaxLength(64)] public string? DecidedByUsername { get; set; }
    [MaxLength(300)] public string? DecisionNote { get; set; }
    public DateOnly? BusinessDate { get; set; }
    public uint RowVersion { get; set; }
}

public enum DepositStatus { Pending = 1, Confirmed = 2, Rejected = 3 }

/// <summary>Cash handed over by a cashier to Admin. Only a confirmed deposit reduces the cashier's balance.</summary>
public class CashDeposit
{
    public long Id { get; set; }
    [MaxLength(30)] public string DepositNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User? User { get; set; }
    [MaxLength(64)] public string Username { get; set; } = string.Empty;
    public int? CounterId { get; set; }
    public Counter? Counter { get; set; }
    public decimal Amount { get; set; }
    public decimal? ReceivedAmount { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DepositStatus Status { get; set; } = DepositStatus.Pending;
    public DateTime SubmittedUtc { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public int? ReviewedByUserId { get; set; }
    [MaxLength(64)] public string? ReviewedByUsername { get; set; }
    [MaxLength(300)] public string? ReviewRemarks { get; set; }
    public uint RowVersion { get; set; }
}

public enum CashierEntryType { Collection = 1, ReceiptCancellation = 2, Refund = 3, DepositConfirmed = 4, Adjustment = 5 }

/// <summary>
/// Individual cashier ledger. Positive = the cashier holds more hospital money; negative = less.
/// Outstanding (payable to Admin) = sum of entries that affect cash in hand.
/// </summary>
public class CashierLedgerEntry
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int? CounterId { get; set; }
    public CashierEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public int? PaymentMethodId { get; set; }
    /// <summary>True when the entry changes cash the cashier must hand over (cash payments, cash refunds, deposits, adjustments).</summary>
    public bool AffectsCashInHand { get; set; }
    public long? ReceiptId { get; set; }
    public long? RefundId { get; set; }
    public long? DepositId { get; set; }
    public long? AdjustmentId { get; set; }
    [MaxLength(30)] public string Reference { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}

public enum DoctorEntryType { ConsultationShare = 1, ShareReversal = 2, Settlement = 3, Adjustment = 4 }

/// <summary>Doctor ledger. Positive = payable to the doctor; settlements are negative.</summary>
public class DoctorLedgerEntry
{
    public long Id { get; set; }
    public int DoctorId { get; set; }
    public DoctorEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Patient money collected for the consultation (for share entries).</summary>
    public decimal GrossAmount { get; set; }
    public decimal HospitalShare { get; set; }
    public long? ReceiptId { get; set; }
    public long? ReceiptLineId { get; set; }
    public long? RefundId { get; set; }
    public long? SettlementId { get; set; }
    public long? AdjustmentId { get; set; }
    [MaxLength(30)] public string Reference { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}

/// <summary>Money actually paid to a doctor.</summary>
public class DoctorSettlement
{
    public long Id { get; set; }
    [MaxLength(30)] public string SettlementNumber { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    [MaxLength(100)] public string? PaymentReference { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateTime PaidUtc { get; set; }
    public DateOnly BusinessDate { get; set; }
    public int? RecordedByUserId { get; set; }
    [MaxLength(64)] public string? RecordedByUsername { get; set; }
}

public enum AdjustmentTarget { Cashier = 1, Doctor = 2 }

/// <summary>Authorised correction of a cashier or doctor balance (e.g. a cash-closing discrepancy). Never edits old rows.</summary>
public class FinancialAdjustment
{
    public long Id { get; set; }
    [MaxLength(30)] public string AdjustmentNumber { get; set; } = string.Empty;
    public AdjustmentTarget Target { get; set; }
    public int? UserId { get; set; }
    public int? DoctorId { get; set; }
    /// <summary>Change of the balance: negative reduces what the cashier owes / what is payable to the doctor.</summary>
    public decimal Amount { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}

public enum ClosingScope { Cashier = 1, Counter = 2, Hospital = 3 }

/// <summary>Dated snapshot of a day's figures. Creating it never resets or deletes transactions.</summary>
public class DailyCashClosing
{
    public long Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public ClosingScope Scope { get; set; }
    public int? UserId { get; set; }
    public int? CounterId { get; set; }
    [MaxLength(150)] public string ScopeName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal CashCollections { get; set; }
    public decimal NonCashCollections { get; set; }
    public int ReceiptCount { get; set; }
    public int CancelledCount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal Refunds { get; set; }
    public decimal DepositsSubmitted { get; set; }
    public decimal DepositsConfirmed { get; set; }
    public decimal Adjustments { get; set; }
    public decimal ClosingBalance { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateTime ClosedUtc { get; set; }
    public int? ClosedByUserId { get; set; }
    [MaxLength(64)] public string? ClosedByUsername { get; set; }
}

/// <summary>Hospital-wide financial settings (single row).</summary>
public class FinanceSettings
{
    public int Id { get; set; } = 1;
    [MaxLength(10)] public string ReceiptPrefix { get; set; } = "RCP";
    [MaxLength(10)] public string CurrencySymbol { get; set; } = "Rs.";
    public bool IssueZeroReceiptForFreeServices { get; set; } = true;
    public bool ShowCashierOnReceipt { get; set; } = true;
    [MaxLength(200)] public string? ReceiptFooter { get; set; } = "Thank you. Please keep this receipt.";
    public int DefaultReceiptWidthMm { get; set; } = 80;
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}
