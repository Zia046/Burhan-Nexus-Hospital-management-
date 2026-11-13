using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

public enum AppointmentStatus { Booked = 1, Seen = 2, Cancelled = 3 }

/// <summary>
/// Private-hospital appointment with a doctor. Each appointment is linked 1:1 to the patient's OPD encounter
/// (visit history, future billing) and carries the doctor-specific daily token.
/// Unique (DoctorId, AppointmentDate, TokenNumber): every doctor has an independent counter that starts at 1 every day.
/// Cancelled appointments keep their token – it is never issued again.
/// </summary>
public class Appointment
{
    public long Id { get; set; }

    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public long EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }

    /// <summary>Hospital-local appointment date (token counter resets per date).</summary>
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly? AppointmentTime { get; set; }
    /// <summary>Doctor-specific daily token, 1, 2, 3… Never changes after booking.</summary>
    public int TokenNumber { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    [MaxLength(300)] public string? Complaint { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    /// <summary>Fee reference and the fee amount valid at booking time (no payment is recorded here).</summary>
    public int? ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }
    public decimal? ConsultationFee { get; set; }

    public DateTime BookedUtc { get; set; }
    public int? BookedByUserId { get; set; }
    [MaxLength(64)] public string? BookedByUsername { get; set; }
    [MaxLength(100)] public string? Workstation { get; set; }

    public DateTime? CancelledUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    [MaxLength(64)] public string? CancelledByUsername { get; set; }
    [MaxLength(500)] public string? CancelReason { get; set; }

    public DateTime? SeenUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

public enum PrintMode
{
    /// <summary>Browser print dialog – works with every Windows-installed printer.</summary>
    Browser = 1,
    /// <summary>Server sends ESC/POS directly to a network (LAN/Ethernet) printer, e.g. 192.168.1.50:9100.</summary>
    LanDirect = 2,
    /// <summary>Burhan Nexus Print Agent on this computer prints silently to a Windows/USB printer.</summary>
    LocalAgent = 3
}

public enum SlipAlignment { Left = 1, Center = 2 }
public enum SlipFontSize { Small = 1, Normal = 2, Large = 3 }

/// <summary>Thermal printer settings of one workstation ("*" = default for all computers).</summary>
public class PrinterProfile
{
    public const string DefaultWorkstation = "*";

    public int Id { get; set; }
    [MaxLength(50)] public string WorkstationName { get; set; } = DefaultWorkstation;
    public PrintMode Mode { get; set; } = PrintMode.Browser;
    public int PaperWidthMm { get; set; } = 80;
    [MaxLength(200)] public string? WindowsPrinterName { get; set; }
    [MaxLength(100)] public string? LanHost { get; set; }
    public int LanPort { get; set; } = 9100;
    [MaxLength(200)] public string AgentUrl { get; set; } = "http://127.0.0.1:9123";
    public SlipAlignment Alignment { get; set; } = SlipAlignment.Center;
    public SlipFontSize FontSize { get; set; } = SlipFontSize.Normal;
    public bool ShowLogo { get; set; } = true;
    public bool BoldText { get; set; } = true;
    /// <summary>Token number magnification 2–8 (ESC/POS character multiplier; also scales the browser slip).</summary>
    public int TokenSize { get; set; } = 4;
    public bool AutoCut { get; set; } = true;
    public int Copies { get; set; } = 1;
    public DateTime UpdatedUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
    public uint RowVersion { get; set; }

    public int CharsPerLine => PaperWidthMm <= 58 ? 32 : 48;
}
