using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

public enum Gender { Male = 1, Female = 2, Other = 3, Unknown = 4 }

public enum AgeUnit { Years = 1, Months = 2, Days = 3 }

/// <summary>Relation printed before the father's/husband's name (S/O, D/O, W/O…).</summary>
public enum GuardianRelation { SonOf = 1, DaughterOf = 2, WifeOf = 3, Other = 4 }

public enum PatientStatus { Active = 1, Archived = 2, Merged = 3 }

public enum PatientIdentifierType
{
    /// <summary>The permanent hospital Patient ID (one per patient).</summary>
    PatientNumber = 1,
    /// <summary>Patient ID of a duplicate registration that was merged into this patient.</summary>
    MergedPatientNumber = 2,
    /// <summary>Other external identifiers (old register numbers etc.).</summary>
    External = 3
}

/// <summary>
/// Master patient record. One row per real person, identified by a permanent Patient ID
/// (<see cref="PatientNumber"/>). Every encounter in every department references this row.
/// </summary>
public class Patient
{
    public long Id { get; set; }

    /// <summary>Permanent, unique Patient ID, e.g. BNH-000025. Generated from a database sequence; never edited.</summary>
    [MaxLength(30)] public string PatientNumber { get; set; } = string.Empty;
    public long SequenceNumber { get; set; }

    [MaxLength(150)] public string FullName { get; set; } = string.Empty;
    public GuardianRelation? GuardianRelation { get; set; }
    [MaxLength(150)] public string? GuardianName { get; set; }
    public Gender Gender { get; set; }

    /// <summary>Exact or estimated date of birth (see <see cref="DateOfBirthEstimated"/>).</summary>
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>True when the age was entered manually and the date of birth was derived from it.</summary>
    public bool DateOfBirthEstimated { get; set; }
    /// <summary>Age as entered at registration (kept for reference).</summary>
    public int? AgeAtRegistration { get; set; }
    public AgeUnit? AgeUnitAtRegistration { get; set; }

    /// <summary>CNIC formatted as 12345-1234567-1.</summary>
    [MaxLength(15)] public string? Cnic { get; set; }
    [MaxLength(13)] public string? CnicDigits { get; set; }
    [MaxLength(20)] public string? MobileNumber { get; set; }
    [MaxLength(20)] public string? MobileDigits { get; set; }
    [MaxLength(20)] public string? AlternateContact { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(100)] public string? CityOrVillage { get; set; }
    [MaxLength(5)] public string? BloodGroup { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }

    /// <summary>Temporary registration of an unconscious / unidentified emergency patient.</summary>
    public bool IsUnidentified { get; set; }

    public PatientStatus Status { get; set; } = PatientStatus.Active;
    public DateTime? ArchivedUtc { get; set; }
    [MaxLength(500)] public string? StatusReason { get; set; }
    public long? MergedIntoPatientId { get; set; }
    public Patient? MergedIntoPatient { get; set; }

    public DateTime RegisteredUtc { get; set; }
    public int? RegisteredByUserId { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
    public uint RowVersion { get; set; }

    public List<PatientIdentifier> Identifiers { get; set; } = new();
    public List<PatientEncounter> Encounters { get; set; } = new();
}

public class PatientIdentifier
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public Patient? Patient { get; set; }
    public PatientIdentifierType Type { get; set; }
    [MaxLength(50)] public string Value { get; set; } = string.Empty;
    [MaxLength(300)] public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
}
