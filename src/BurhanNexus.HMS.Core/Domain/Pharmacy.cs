using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

// =====================================================================================================
// Phase 5 – Pharmacy management & medicine inventory.
// Stock is kept per (location, batch). A balance is never written directly: every change is an
// InventoryTransaction (insert-only) and the balance is updated atomically in the same database
// transaction (CHECK quantity >= 0, so stock can never go negative).
// Government mode stores no prices at all; Private mode adds purchase/sale prices, retail sales, supplier
// accounts and profit. Money goes through the Phase 3 charges/receipts/refunds – there is no pharmacy ledger.
// =====================================================================================================

/// <summary>Simple master lists used by the medicine catalogue.</summary>
public enum PharmacyLookupKind { Category = 1, Manufacturer = 2, DosageForm = 3, Strength = 4, PackSize = 5, Unit = 6 }

public class PharmacyLookup
{
    public int Id { get; set; }
    public PharmacyLookupKind Kind { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class Medicine
{
    public int Id { get; set; }
    /// <summary>Medicine ID, e.g. MED-000123.</summary>
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(150)] public string? GenericName { get; set; }
    [MaxLength(150)] public string? BrandName { get; set; }
    public int? ManufacturerId { get; set; }
    public PharmacyLookup? Manufacturer { get; set; }
    public int? CategoryId { get; set; }
    public PharmacyLookup? Category { get; set; }
    public int? DosageFormId { get; set; }
    public PharmacyLookup? DosageForm { get; set; }
    [MaxLength(50)] public string? Strength { get; set; }
    [MaxLength(50)] public string? PackSize { get; set; }
    /// <summary>Stock is counted in this unit (tablet, capsule, bottle, vial, tube, piece ...).</summary>
    [MaxLength(30)] public string Unit { get; set; } = "Unit";
    public int UnitsPerPack { get; set; } = 1;
    /// <summary>Low-stock alert level (in units, main pharmacy).</summary>
    public int MinimumStock { get; set; }
    [MaxLength(300)] public string? StorageInstructions { get; set; }
    public bool IsActive { get; set; } = true;

    // ---- Private hospital only (never shown or accepted in Government mode) ----
    /// <summary>Default purchase price per unit.</summary>
    public decimal? PurchasePrice { get; set; }
    /// <summary>Default sale price per unit (a batch may carry its own sale price).</summary>
    public decimal? SalePrice { get; set; }
    public bool AllowDiscount { get; set; } = true;
    /// <summary>Highest discount (percent of the line) a user with "Apply Discount" may give.</summary>
    public decimal MaxDiscountPercent { get; set; } = 10;

    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    public string DisplayName => string.Join(" ", new[] { Name, Strength, DosageForm?.Name }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Price change history (insert-only). Old sales keep the price they were sold at.</summary>
public class MedicinePriceChange
{
    public long Id { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public decimal? OldPurchasePrice { get; set; }
    public decimal? NewPurchasePrice { get; set; }
    public decimal? OldSalePrice { get; set; }
    public decimal? NewSalePrice { get; set; }
    public long? BatchId { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public DateTime ChangedUtc { get; set; }
    public int? ChangedByUserId { get; set; }
    [MaxLength(64)] public string? ChangedByUsername { get; set; }
}

public class Supplier
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(100)] public string? ContactPerson { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(60)] public string? City { get; set; }
    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

public enum StockLocationKind { MainPharmacy = 1, Department = 2 }

/// <summary>A place that holds its own stock: the main pharmacy, Minor OT, Emergency, Dressing Room, Ward ...</summary>
public class StockLocation
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public StockLocationKind Kind { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>A batch (lot) of a medicine with its own expiry date. Private mode keeps its cost and sale price.</summary>
public class MedicineBatch
{
    public long Id { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    /// <summary>Private only: purchase cost per unit.</summary>
    public decimal? UnitCost { get; set; }
    /// <summary>Private only: sale price per unit for this batch (falls back to the medicine's sale price).</summary>
    public decimal? SalePrice { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Current quantity of one batch at one location. Changed only together with an inventory transaction.</summary>
public class StockBalance
{
    public int LocationId { get; set; }
    public StockLocation? Location { get; set; }
    public long BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int Quantity { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public enum InventoryTxnType
{
    Receipt = 1, PatientIssue = 2, Sale = 3, TransferOut = 4, TransferIn = 5, AdjustmentIncrease = 6, AdjustmentDecrease = 7,
    SaleReturn = 8, IssueReturn = 9, SupplierReturn = 10
}

/// <summary>Permanent stock movement (insert-only). Quantity is signed: + into the location, − out of it.</summary>
public class InventoryTransaction
{
    public long Id { get; set; }
    public InventoryTxnType Type { get; set; }
    public int LocationId { get; set; }
    public StockLocation? Location { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public long BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? UnitPrice { get; set; }
    /// <summary>GRN, ISS, SAL, TRF, ADJ, RET, SRT</summary>
    [MaxLength(10)] public string DocumentType { get; set; } = string.Empty;
    public long DocumentId { get; set; }
    [MaxLength(30)] public string DocumentNumber { get; set; } = string.Empty;
    public int? CounterpartLocationId { get; set; }
    public long? PatientId { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? UserId { get; set; }
    [MaxLength(64)] public string? Username { get; set; }
}

/// <summary>Stock receiving / purchase (GRN). Government mode: no amounts.</summary>
public class StockReceipt
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public HospitalType Mode { get; set; }
    public int LocationId { get; set; }
    public StockLocation? Location { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    /// <summary>Source when there is no supplier record (e.g. district health office, donation).</summary>
    [MaxLength(150)] public string? SourceName { get; set; }
    /// <summary>Supplier invoice / delivery / reference document number.</summary>
    [MaxLength(60)] public string? ReferenceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? AdditionalCharges { get; set; }
    public decimal? NetAmount { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public List<StockReceiptItem> Items { get; set; } = new();
}

public class StockReceiptItem
{
    public long Id { get; set; }
    public long StockReceiptId { get; set; }
    public StockReceipt? StockReceipt { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public long BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public DateOnly? ManufactureDate { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? Discount { get; set; }
    public decimal? LineTotal { get; set; }
}

/// <summary>Medicines given to a registered patient (OPD / Emergency visit).</summary>
public class PatientMedicineIssue
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public HospitalType Mode { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public long? EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    public int LocationId { get; set; }
    public StockLocation? Location { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }
    public List<PatientMedicineIssueItem> Items { get; set; } = new();
}

public class PatientMedicineIssueItem
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public PatientMedicineIssue? Issue { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public long BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    [MaxLength(200)] public string? Instructions { get; set; }
    public bool FefoOverridden { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? Discount { get; set; }
    public decimal? LineTotal { get; set; }
    /// <summary>Private mode: the Phase 3 charge for this line (paid at the cash counter).</summary>
    public long? ChargeId { get; set; }
    public Charge? Charge { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Private mode only: walk-in retail sale, paid immediately through a Phase 3 receipt.</summary>
public class PharmacySale
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public int LocationId { get; set; }
    public StockLocation? Location { get; set; }
    [MaxLength(150)] public string? CustomerName { get; set; }
    [MaxLength(30)] public string? CustomerPhone { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public long? ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
    public int CounterId { get; set; }
    public Counter? Counter { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public List<PharmacySaleItem> Items { get; set; } = new();
}

public class PharmacySaleItem
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public PharmacySale? Sale { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public long BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public bool FefoOverridden { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public long? ChargeId { get; set; }
    public Charge? Charge { get; set; }
    public uint RowVersion { get; set; }
}

public enum StockTransferStatus { Completed = 1 }

/// <summary>Stock moved between locations (main pharmacy → Minor OT and other department stores, or back).</summary>
public class StockTransfer
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public int FromLocationId { get; set; }
    public StockLocation? FromLocation { get; set; }
    public int ToLocationId { get; set; }
    public StockLocation? ToLocation { get; set; }
    [MaxLength(60)] public string? Reference { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Completed;
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public List<StockTransferItem> Items { get; set; } = new();
}

public class StockTransferItem
{
    public long Id { get; set; }
    public long TransferId { get; set; }
    public StockTransfer? Transfer { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public long BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int Quantity { get; set; }
}

public enum StockAdjustmentReason { PhysicalCount = 1, Damaged = 2, BrokenPackaging = 3, Expired = 4, Lost = 5, DataEntryCorrection = 6, Other = 99 }

public enum StockAdjustmentStatus { Pending = 1, Approved = 2, Rejected = 3 }

/// <summary>Controlled stock correction with a mandatory reason. Applied only when approved.</summary>
public class StockAdjustment
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public int LocationId { get; set; }
    public StockLocation? Location { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public long BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }
    public bool Increase { get; set; }
    public int Quantity { get; set; }
    public int QuantityBefore { get; set; }
    public int? QuantityAfter { get; set; }
    public StockAdjustmentReason Reason { get; set; }
    [MaxLength(300)] public string Note { get; set; } = string.Empty;
    public StockAdjustmentStatus Status { get; set; } = StockAdjustmentStatus.Pending;
    public DateOnly BusinessDate { get; set; }
    public DateTime RequestedUtc { get; set; }
    public int? RequestedByUserId { get; set; }
    [MaxLength(64)] public string? RequestedByUsername { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public int? DecidedByUserId { get; set; }
    [MaxLength(64)] public string? DecidedByUsername { get; set; }
    [MaxLength(300)] public string? DecisionNote { get; set; }
    public uint RowVersion { get; set; }
}

public enum MedicineReturnKind { SaleReturn = 1, IssueReturn = 2 }

/// <summary>Customer / patient return against an original sale or issue.</summary>
public class MedicineReturn
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public MedicineReturnKind Kind { get; set; }
    public long? SaleId { get; set; }
    public PharmacySale? Sale { get; set; }
    public long? IssueId { get; set; }
    public PatientMedicineIssue? Issue { get; set; }
    public int LocationId { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    /// <summary>Phase 3 refund requested for the money (approved by an authorised user in Finance → Refunds).</summary>
    public long? RefundId { get; set; }
    public Refund? Refund { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public List<MedicineReturnItem> Items { get; set; } = new();
}

public class MedicineReturnItem
{
    public long Id { get; set; }
    public long ReturnId { get; set; }
    public MedicineReturn? Return { get; set; }
    public long? SaleItemId { get; set; }
    public long? IssueItemId { get; set; }
    public int MedicineId { get; set; }
    public long BatchId { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    /// <summary>True = undamaged and back in saleable stock; false = recorded only (damaged / opened / expired).</summary>
    public bool Restocked { get; set; }
    public decimal? Amount { get; set; }
}

/// <summary>Medicines sent back to the supplier.</summary>
public class SupplierReturn
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public HospitalType Mode { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    [MaxLength(150)] public string? SourceName { get; set; }
    public int LocationId { get; set; }
    public StockLocation? Location { get; set; }
    [MaxLength(60)] public string? ReferenceInvoice { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public List<SupplierReturnItem> Items { get; set; } = new();
}

public class SupplierReturnItem
{
    public long Id { get; set; }
    public long SupplierReturnId { get; set; }
    public SupplierReturn? SupplierReturn { get; set; }
    public int MedicineId { get; set; }
    public long BatchId { get; set; }
    [MaxLength(200)] public string MedicineName { get; set; } = string.Empty;
    [MaxLength(50)] public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? Amount { get; set; }
}

public enum SupplierEntryType { Purchase = 1, Payment = 2, PurchaseReturn = 3 }

/// <summary>Private mode: supplier account (insert-only). Positive = the hospital owes more to the supplier.</summary>
public class SupplierLedgerEntry
{
    public long Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public SupplierEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public long? StockReceiptId { get; set; }
    public long? SupplierPaymentId { get; set; }
    public long? SupplierReturnId { get; set; }
    [MaxLength(30)] public string Reference { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}

/// <summary>Private mode: money paid to a supplier by the hospital (like a doctor settlement – not from a cashier drawer).</summary>
public class SupplierPayment
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal Amount { get; set; }
    public int PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    [MaxLength(100)] public string? PaymentReference { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime PaidUtc { get; set; }
    public int? RecordedByUserId { get; set; }
    [MaxLength(64)] public string? RecordedByUsername { get; set; }
}

/// <summary>Pharmacy settings (single row).</summary>
public class PharmacySettings
{
    public int Id { get; set; } = 1;
    [MaxLength(150)] public string? PharmacyName { get; set; }
    /// <summary>Batches expiring within this many days are "near expiry".</summary>
    public int NearExpiryDays { get; set; } = 90;
    /// <summary>Government mode: an OPD/Emergency visit older than this cannot receive medicines.</summary>
    public int EncounterValidDays { get; set; } = 7;
    /// <summary>Phase 3 service used for pharmacy charges (medicine sales / patient issues).</summary>
    public int? MedicineServiceItemId { get; set; }
    public ServiceItem? MedicineServiceItem { get; set; }
    /// <summary>Government mode (optional): a fixed service fee charged per patient issue (e.g. "Pharmacy slip"), using the Phase 3 fee.</summary>
    public int? GovernmentIssueServiceItemId { get; set; }
    public ServiceItem? GovernmentIssueServiceItem { get; set; }
    [MaxLength(200)] public string? SlipFooter { get; set; } = "Please check your medicines before leaving the counter.";
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}
