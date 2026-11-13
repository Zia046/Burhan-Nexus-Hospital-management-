using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

/// <summary>Hospital identity and branding. Single row (Id = 1).</summary>
public class HospitalSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    [MaxLength(200)] public string HospitalName { get; set; } = string.Empty;
    [MaxLength(100)] public string? RegistrationNumber { get; set; }
    [MaxLength(500)] public string Address { get; set; } = string.Empty;
    [MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(100)] public string Province { get; set; } = string.Empty;
    [MaxLength(50)] public string ContactNumber { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(300)] public string? Website { get; set; }

    public byte[]? LogoContent { get; set; }
    [MaxLength(100)] public string? LogoContentType { get; set; }
    public DateTime? LogoUpdatedUtc { get; set; }

    [MaxLength(500)] public string? ReceiptHeader { get; set; }
    [MaxLength(500)] public string? ReceiptFooter { get; set; }
    [MaxLength(1000)] public string? ReportHeader { get; set; }
    [MaxLength(1000)] public string? ReportFooter { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; set; }
}
