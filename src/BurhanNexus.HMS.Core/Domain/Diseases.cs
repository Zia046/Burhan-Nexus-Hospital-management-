using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

/// <summary>
/// Update 18: the central Disease Master – the one disease catalogue used by registration, patient disease history,
/// disease analytics and disease management. A used disease is never removed: it is marked inactive / deleted (soft).
/// </summary>
public class Disease
{
    public int Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    /// <summary>Upper-case name without spaces / punctuation – unique (duplicates are refused case-insensitively).</summary>
    [MaxLength(150)] public string NormalizedName { get; set; } = string.Empty;
    [MaxLength(30)] public string? Code { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Soft delete of a disease that is already used in patient records (hidden everywhere except old records and reports).</summary>
    public bool IsDeleted { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }
}

/// <summary>Update 18: a disease of a patient on a visit / registration date (visit-wise history – never overwritten).</summary>
public class PatientDisease
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int DiseaseId { get; set; }
    public Disease? Disease { get; set; }
    public long? EncounterId { get; set; }
    public PatientEncounter? Encounter { get; set; }
    public long? AppointmentId { get; set; }
    /// <summary>Hospital-local registration / visit date of this disease record.</summary>
    public DateOnly VisitDate { get; set; }
    /// <summary>Age in completed years and gender at that date (recorded age – used by the analytics).</summary>
    public int? AgeYears { get; set; }
    public Gender? Gender { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}

/// <summary>Update 18: "Pay to Hospital" – the administrator settles the outstanding cash of an account (one row per account settled).</summary>
public class HospitalSettlement
{
    public long Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User? User { get; set; }
    [MaxLength(64)] public string Username { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Outstanding before the settlement.</summary>
    public decimal OutstandingBefore { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    [MaxLength(64)] public string? CreatedByUsername { get; set; }
}
