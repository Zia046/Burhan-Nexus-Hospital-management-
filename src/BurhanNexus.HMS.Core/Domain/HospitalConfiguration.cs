using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

/// <summary>
/// Hospital operating configuration. Single row (Id = 1).
/// Hospital type selects defaults for operational rules; each rule stays individually editable.
/// </summary>
public class HospitalConfiguration
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public HospitalType HospitalType { get; set; } = HospitalType.Private;

    public bool SetupCompleted { get; set; }
    public DateTime? SetupCompletedUtc { get; set; }
    public int? SetupCompletedByUserId { get; set; }

    // Regional preferences
    [MaxLength(10)] public string CurrencyCode { get; set; } = "PKR";
    [MaxLength(10)] public string CurrencySymbol { get; set; } = "Rs.";
    [MaxLength(64)] public string TimeZoneId { get; set; } = "Asia/Karachi";
    [MaxLength(32)] public string DateFormat { get; set; } = "dd/MM/yyyy";
    [MaxLength(32)] public string TimeFormat { get; set; } = "hh:mm tt";
    [MaxLength(10)] public string DefaultLanguage { get; set; } = "en";

    // Printing
    [MaxLength(200)] public string? ReceiptPrinterName { get; set; }
    [MaxLength(20)] public string ReceiptPaperSize { get; set; } = "80mm";
    public int ReceiptCopies { get; set; } = 1;
    [MaxLength(200)] public string? ReportPrinterName { get; set; }
    [MaxLength(20)] public string ReportPaperSize { get; set; } = "A4";
    [MaxLength(20)] public string ReportOrientation { get; set; } = "Portrait";

    // Operational rules (defaults derived from HospitalType, editable later by Admin)
    /// <summary>Pharmacy keeps purchase and sale prices.</summary>
    public bool PharmacyPricingEnabled { get; set; }
    /// <summary>Pharmacy performs retail sales to patients.</summary>
    public bool PharmacyRetailSalesEnabled { get; set; }
    /// <summary>Pharmacy profit calculation and reports.</summary>
    public bool PharmacyProfitTrackingEnabled { get; set; }
    /// <summary>Medicines are issued from government supplied stock without prices.</summary>
    public bool GovernmentStockIssueEnabled { get; set; }
    /// <summary>Patients are charged fees for services (OPD, lab, imaging...).</summary>
    public bool PatientChargesEnabled { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    public void ApplyHospitalTypeDefaults()
    {
        var isPrivate = HospitalType == HospitalType.Private;
        PharmacyPricingEnabled = isPrivate;
        PharmacyRetailSalesEnabled = isPrivate;
        PharmacyProfitTrackingEnabled = isPrivate;
        GovernmentStockIssueEnabled = !isPrivate;
        PatientChargesEnabled = isPrivate;
    }
}
