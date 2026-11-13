using System.Globalization;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BurhanNexus.HMS.Infrastructure.Patients;

public sealed class PatientInput
{
    public string? FullName { get; set; }
    public GuardianRelation? GuardianRelation { get; set; }
    public string? GuardianName { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public AgeUnit? AgeUnit { get; set; }
    public string? Cnic { get; set; }
    public string? MobileNumber { get; set; }
    public string? AlternateContact { get; set; }
    public string? Address { get; set; }
    public string? CityOrVillage { get; set; }
    public string? BloodGroup { get; set; }
    public string? Notes { get; set; }
    public bool IsUnidentified { get; set; }
}

public sealed record DuplicateCandidate(
    long Id, string PatientNumber, string FullName, GuardianRelation? GuardianRelation, string? GuardianName, Gender Gender,
    string? AgeText, string? Cnic, string? MobileNumber, string? Address, string? CityOrVillage, DateTime RegisteredUtc,
    PatientStatus Status, IReadOnlyList<string> Reasons);

public sealed record PatientListItem(
    long Id, string PatientNumber, string FullName, GuardianRelation? GuardianRelation, string? GuardianName, Gender Gender,
    string? AgeText, string? MobileNumber, string? Cnic, string? CityOrVillage, DateTime RegisteredUtc, PatientStatus Status,
    bool IsUnidentified, int OpdVisits, int EmergencyVisits, DateTime? LastVisitUtc);

public sealed class PatientSearchQuery
{
    public string? Q { get; set; }
    public DateOnly? RegisteredFrom { get; set; }
    public DateOnly? RegisteredTo { get; set; }
    public Gender? Gender { get; set; }
    /// <summary>"opd" = patients with OPD visits, "emergency" = with emergency visits.</summary>
    public string? VisitType { get; set; }
    /// <summary>"active" (default), "archived" or "all".</summary>
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class RegistrationResult : ServiceResult
{
    public Patient? Patient { get; init; }
    public IReadOnlyList<DuplicateCandidate> Duplicates { get; init; } = Array.Empty<DuplicateCandidate>();
    public bool NeedsDuplicateConfirmation => Duplicates.Count > 0 && Patient is null && Errors.Count == 0;
}

public interface IPatientService
{
    Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(PatientInput input, long? excludeId = null, CancellationToken ct = default);
    Task<RegistrationResult> RegisterAsync(PatientInput input, bool duplicateWarningConfirmed, CancellationToken ct = default);
    /// <summary>Creates the patient inside the caller's transaction (used by emergency registration).</summary>
    Task<ServiceResult<Patient>> CreateWithinTransactionAsync(PatientInput input, bool minimal, CancellationToken ct = default);
    Task<ServiceResult> UpdateAsync(long id, PatientInput input, uint rowVersion, CancellationToken ct = default);
    Task<Patient?> GetAsync(long id, CancellationToken ct = default);
    Task<Patient?> FindByNumberAsync(string patientNumber, CancellationToken ct = default);
    Task<PagedResult<PatientListItem>> SearchAsync(PatientSearchQuery q, CancellationToken ct = default);
    Task<ServiceResult> SetArchivedAsync(long id, bool archived, string? reason, CancellationToken ct = default);
    Task<ServiceResult> MergeAsync(long duplicateId, long correctId, string? reason, CancellationToken ct = default);
    Task<(int Today, int Total)> RegistrationCountsAsync(CancellationToken ct = default);
    IReadOnlyList<string> Validate(PatientInput input, bool minimal, DateOnly today);
}

public sealed partial class PatientService : IPatientService
{
    public static readonly string[] BloodGroups = { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };

    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\p{L}")] private static partial Regex HasLetter();

    private readonly HmsDbContext _db;
    private readonly IRegistrationSetupService _setup;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    public PatientService(HmsDbContext db, IRegistrationSetupService setup, IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
        _db = db;
        _setup = setup;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    // ---------------- Normalisation & validation ----------------

    public static string Digits(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    public static string? FormatCnic(string? s)
    {
        var d = Digits(s);
        return d.Length == 13 ? $"{d[..5]}-{d.Substring(5, 7)}-{d[12]}" : null;
    }

    public static string? FormatPhone(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var d = Digits(s);
        if (d.Length == 12 && d.StartsWith("92")) d = "0" + d[2..];
        if (d.Length == 11 && d.StartsWith("03")) return $"{d[..4]}-{d[4..]}";
        return s.Trim();
    }

    public static string? MobileKey(string? s)
    {
        var d = Digits(s);
        if (d.Length == 12 && d.StartsWith("92")) d = "0" + d[2..];
        return d.Length == 0 ? null : d;
    }

    public static string CleanName(string? s)
    {
        var v = Spaces().Replace((s ?? string.Empty).Trim(), " ");
        if (v.Length > 0 && v == v.ToLowerInvariant()) v = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(v);
        return v;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : Spaces().Replace(s.Trim(), " ");

    public IReadOnlyList<string> Validate(PatientInput i, bool minimal, DateOnly today)
    {
        var e = new List<string>();
        var name = CleanName(i.FullName);
        if (!i.IsUnidentified)
        {
            if (name.Length == 0) e.Add("Patient name is required.");
            else if (!HasLetter().IsMatch(name)) e.Add("Patient name must contain letters.");
        }
        if (name.Length > 150) e.Add("Patient name must not exceed 150 characters.");
        if ((i.GuardianName?.Trim().Length ?? 0) > 150) e.Add("Father's/Husband's name must not exceed 150 characters.");
        if (i.GuardianRelation is { } rel && !Enum.IsDefined(rel)) e.Add("Invalid relation.");
        if (i.Gender is null || !Enum.IsDefined(i.Gender.Value)) e.Add("Please select the gender.");
        else if (i.Gender == Gender.Unknown && !i.IsUnidentified && !minimal) e.Add("Please select the gender.");

        if (i.DateOfBirth is { } dob)
        {
            if (dob > today) e.Add("Date of birth cannot be in the future.");
            else if (dob < today.AddYears(-130)) e.Add("Date of birth is not valid.");
        }
        else if (i.Age is { } age)
        {
            if (i.AgeUnit is null || !Enum.IsDefined(i.AgeUnit.Value)) e.Add("Please select the age unit (years, months or days).");
            else
            {
                var max = i.AgeUnit switch { AgeUnit.Years => 130, AgeUnit.Months => 36, _ => 365 };
                if (age < 0 || age > max) e.Add($"Age must be between 0 and {max} {i.AgeUnit.ToString()!.ToLowerInvariant()}.");
            }
        }
        else if (!minimal && !i.IsUnidentified) e.Add("Enter the age or the date of birth.");

        if (!string.IsNullOrWhiteSpace(i.Cnic) && FormatCnic(i.Cnic) is null) e.Add("CNIC must have 13 digits (e.g. 17301-1234567-1).");
        foreach (var (v, n) in new[] { (i.MobileNumber, "Mobile number"), (i.AlternateContact, "Alternative contact number") })
            if (!string.IsNullOrWhiteSpace(v) && (Digits(v).Length is < 7 or > 15 || !Validation.IsValidPhone(v)))
                e.Add($"{n} is not valid.");

        if (!minimal && !i.IsUnidentified)
        {
            if (string.IsNullOrWhiteSpace(i.Address)) e.Add("Address is required.");
            if (string.IsNullOrWhiteSpace(i.CityOrVillage)) e.Add("City / village is required.");
        }
        if ((i.Address?.Trim().Length ?? 0) > 300) e.Add("Address must not exceed 300 characters.");
        if ((i.CityOrVillage?.Trim().Length ?? 0) > 100) e.Add("City / village must not exceed 100 characters.");
        if (!string.IsNullOrWhiteSpace(i.BloodGroup) && !BloodGroups.Contains(i.BloodGroup.Trim().ToUpperInvariant())) e.Add("Invalid blood group.");
        if ((i.Notes?.Length ?? 0) > 1000) e.Add("Notes must not exceed 1000 characters.");
        return e;
    }

    private void Apply(Patient p, PatientInput i, DateOnly today)
    {
        var name = CleanName(i.FullName);
        if (i.IsUnidentified && name.Length == 0)
            name = i.Gender switch { Gender.Male => "Unidentified Male", Gender.Female => "Unidentified Female", _ => "Unidentified Patient" };
        p.FullName = name;
        p.GuardianRelation = string.IsNullOrWhiteSpace(i.GuardianName) ? null : i.GuardianRelation;
        p.GuardianName = Clean(i.GuardianName) is { } g ? CleanName(g) : null;
        p.Gender = i.Gender ?? Gender.Unknown;
        if (i.DateOfBirth is { } dob)
        {
            p.DateOfBirth = dob;
            p.DateOfBirthEstimated = false;
            var age = AgeCalculator.FromDateOfBirth(dob, today);
            p.AgeAtRegistration ??= age.Value;
            p.AgeUnitAtRegistration ??= age.Unit;
        }
        else if (i.Age is { } a && i.AgeUnit is { } u)
        {
            // Keep an earlier estimate if the age did not change (avoids drifting dates on every edit).
            var current = p.DateOfBirth is { } existing ? AgeCalculator.FromDateOfBirth(existing, today) : (PatientAge?)null;
            if (current is null || current.Value.Value != a || current.Value.Unit != u || !p.DateOfBirthEstimated)
            {
                p.DateOfBirth = AgeCalculator.EstimateDateOfBirth(a, u, today);
                p.DateOfBirthEstimated = true;
            }
            p.AgeAtRegistration ??= a;
            p.AgeUnitAtRegistration ??= u;
        }
        else
        {
            p.DateOfBirth = null;
            p.DateOfBirthEstimated = false;
        }
        p.Cnic = FormatCnic(i.Cnic);
        p.CnicDigits = p.Cnic is null ? null : Digits(p.Cnic);
        p.MobileNumber = FormatPhone(i.MobileNumber);
        p.MobileDigits = MobileKey(i.MobileNumber);
        p.AlternateContact = FormatPhone(i.AlternateContact);
        p.Address = Clean(i.Address);
        p.CityOrVillage = Clean(i.CityOrVillage) is { } c ? CleanName(c) : null;
        p.BloodGroup = string.IsNullOrWhiteSpace(i.BloodGroup) ? null : i.BloodGroup.Trim().ToUpperInvariant();
        p.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
        p.IsUnidentified = i.IsUnidentified;
    }

    private static object Snapshot(Patient p) => new
    {
        p.PatientNumber, p.FullName, GuardianRelation = p.GuardianRelation?.ToString(), p.GuardianName, Gender = p.Gender.ToString(),
        p.DateOfBirth, p.DateOfBirthEstimated, p.Cnic, p.MobileNumber, p.AlternateContact, p.Address, p.CityOrVillage,
        p.BloodGroup, p.Notes, p.IsUnidentified, Status = p.Status.ToString()
    };

    // ---------------- Duplicate detection ----------------

    private sealed class DuplicateRow
    {
        public long Id { get; set; }
        public double NameScore { get; set; }
        public double GuardianScore { get; set; }
        public bool CnicMatch { get; set; }
        public bool MobileMatch { get; set; }
    }

    public async Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(PatientInput input, long? excludeId = null, CancellationToken ct = default)
    {
        var name = CleanName(input.FullName);
        var guardian = Clean(input.GuardianName);
        var cnic = FormatCnic(input.Cnic) is { } c ? Digits(c) : null;
        var mobile = MobileKey(input.MobileNumber);
        if (mobile is { Length: < 7 }) mobile = null;
        if (input.IsUnidentified && cnic is null && mobile is null) return Array.Empty<DuplicateCandidate>();
        if (name.Length < 3 && cnic is null && mobile is null) return Array.Empty<DuplicateCandidate>();

        const string sql = """
            SELECT "Id",
                   similarity("FullName", @name)::float8 AS "NameScore",
                   CASE WHEN @guardian IS NULL OR "GuardianName" IS NULL THEN 0 ELSE similarity("GuardianName", @guardian)::float8 END AS "GuardianScore",
                   (@cnic IS NOT NULL AND "CnicDigits" = @cnic) AS "CnicMatch",
                   (@mobile IS NOT NULL AND "MobileDigits" = @mobile) AS "MobileMatch"
            FROM "Patients"
            WHERE "Status" <> 'Merged' AND "Id" <> @exclude
              AND ((@cnic IS NOT NULL AND "CnicDigits" = @cnic)
                   OR (@mobile IS NOT NULL AND "MobileDigits" = @mobile)
                   OR (length(@name) >= 3 AND "FullName" % @name AND similarity("FullName", @name) >= 0.45))
            ORDER BY (@cnic IS NOT NULL AND "CnicDigits" = @cnic) DESC, similarity("FullName", @name) DESC
            LIMIT 40
            """;
        var rows = await _db.Database.SqlQueryRaw<DuplicateRow>(sql,
                new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = name },
                new NpgsqlParameter("guardian", NpgsqlDbType.Text) { Value = (object?)guardian ?? DBNull.Value },
                new NpgsqlParameter("cnic", NpgsqlDbType.Text) { Value = (object?)cnic ?? DBNull.Value },
                new NpgsqlParameter("mobile", NpgsqlDbType.Text) { Value = (object?)mobile ?? DBNull.Value },
                new NpgsqlParameter("exclude", NpgsqlDbType.Bigint) { Value = excludeId ?? -1 })
            .ToListAsync(ct);

        var scored = new List<(DuplicateRow Row, List<string> Reasons, int Score)>();
        foreach (var r in rows)
        {
            var reasons = new List<string>();
            var score = 0;
            if (r.CnicMatch) { reasons.Add("Same CNIC"); score += 100; }
            if (r.MobileMatch) { reasons.Add("Same mobile number"); score += 30; }
            if (r.NameScore >= 0.45) { reasons.Add(r.NameScore >= 0.85 ? "Same name" : "Similar name"); score += (int)(r.NameScore * 40); }
            if (guardian is not null && r.GuardianScore >= 0.5) { reasons.Add(r.GuardianScore >= 0.85 ? "Same father's/husband's name" : "Similar father's/husband's name"); score += (int)(r.GuardianScore * 30); }
            // A name-only match is shown only when the guardian also matches (or the guardian is unknown).
            var nameOnly = !r.CnicMatch && !r.MobileMatch;
            if (nameOnly && guardian is not null && r.GuardianScore < 0.5) continue;
            // A shared family mobile number alone is a weak signal; keep it when the name is somewhat similar.
            if (!r.CnicMatch && r.MobileMatch && r.NameScore < 0.3 && guardian is not null && r.GuardianScore < 0.5) continue;
            scored.Add((r, reasons, score));
        }
        var top = scored.OrderByDescending(x => x.Score).Take(10).ToList();
        if (top.Count == 0) return Array.Empty<DuplicateCandidate>();

        var ids = top.Select(t => t.Row.Id).ToList();
        var patients = await _db.Patients.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var today = await _clock.TodayAsync();
        return top.Where(t => patients.ContainsKey(t.Row.Id)).Select(t =>
        {
            var p = patients[t.Row.Id];
            return new DuplicateCandidate(p.Id, p.PatientNumber, p.FullName, p.GuardianRelation, p.GuardianName, p.Gender,
                AgeCalculator.Of(p, today)?.ToString(), p.Cnic, p.MobileNumber, p.Address, p.CityOrVillage, p.RegisteredUtc, p.Status, t.Reasons);
        }).ToList();
    }

    // ---------------- Registration ----------------

    public async Task<RegistrationResult> RegisterAsync(PatientInput input, bool duplicateWarningConfirmed, CancellationToken ct = default)
    {
        var today = await _clock.TodayAsync();
        var errors = Validate(input, minimal: false, today);
        if (errors.Count > 0) return new RegistrationResult { Succeeded = false, Errors = errors.ToList() };

        var duplicates = await FindDuplicatesAsync(input, null, ct);
        if (duplicates.Count > 0 && !duplicateWarningConfirmed)
            return new RegistrationResult { Succeeded = false, Duplicates = duplicates };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var created = await CreateWithinTransactionAsync(input, minimal: false, ct);
        if (!created.Succeeded) return new RegistrationResult { Succeeded = false, Errors = created.Errors };
        if (duplicates.Count > 0)
            _audit.Add(AuditActions.PatientDuplicateWarningOverridden, "Patient", created.Value!.PatientNumber,
                $"Registered despite possible duplicate(s): {string.Join(", ", duplicates.Select(d => d.PatientNumber))}",
                newValues: new { Candidates = duplicates.Select(d => new { d.PatientNumber, d.FullName, d.Reasons }) });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new RegistrationResult { Succeeded = true, Patient = created.Value, Duplicates = duplicates };
    }

    public async Task<ServiceResult<Patient>> CreateWithinTransactionAsync(PatientInput input, bool minimal, CancellationToken ct = default)
    {
        var today = await _clock.TodayAsync();
        var errors = Validate(input, minimal, today);
        if (errors.Count > 0) return ServiceResult<Patient>.Fail(errors.ToArray());

        var settings = await _setup.GetSettingsAsync(ct);
        // nextval() is atomic across all connections: two computers can never receive the same number.
        var seq = await _db.Database.SqlQueryRaw<long>($"SELECT nextval('{HmsDbContext.PatientNumberSequence}') AS \"Value\"").SingleAsync(ct);
        var now = _clock.UtcNow;
        var p = new Patient
        {
            SequenceNumber = seq,
            PatientNumber = _setup.FormatPatientNumber(settings, seq),
            RegisteredUtc = now,
            RegisteredByUserId = _ctx.UserId,
            UpdatedUtc = now,
            UpdatedByUserId = _ctx.UserId,
            Status = PatientStatus.Active
        };
        Apply(p, input, today);
        p.Identifiers.Add(new PatientIdentifier { Type = PatientIdentifierType.PatientNumber, Value = p.PatientNumber, CreatedUtc = now, CreatedByUserId = _ctx.UserId });
        _db.Patients.Add(p);
        await _db.SaveChangesAsync(ct);
        _audit.Add(AuditActions.PatientRegistered, "Patient", p.PatientNumber,
            $"Patient {p.PatientNumber} registered: {p.FullName}" + (p.IsUnidentified ? " (temporary / unidentified)" : ""),
            newValues: Snapshot(p));
        await _db.SaveChangesAsync(ct);
        return ServiceResult<Patient>.Ok(p);
    }

    public async Task<ServiceResult> UpdateAsync(long id, PatientInput input, uint rowVersion, CancellationToken ct = default)
    {
        var p = await _db.Patients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return ServiceResult.Fail("Patient not found.");
        if (p.Status == PatientStatus.Merged) return ServiceResult.Fail("This registration was merged into another patient and can no longer be edited.");
        var today = await _clock.TodayAsync();
        var errors = Validate(input, minimal: true, today);
        if (!input.IsUnidentified && string.IsNullOrWhiteSpace(input.FullName)) errors = errors.Append("Patient name is required.").Distinct().ToList();
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());

        var old = Snapshot(p);
        _db.Entry(p).Property(x => x.RowVersion).OriginalValue = rowVersion;
        Apply(p, input, today);
        p.UpdatedUtc = _clock.UtcNow;
        p.UpdatedByUserId = _ctx.UserId;
        _audit.Add(AuditActions.PatientUpdated, "Patient", p.PatientNumber, $"Patient {p.PatientNumber} information updated", old, Snapshot(p));
        try
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("This patient record was changed on another computer. Please reload and try again.");
        }
    }

    public Task<Patient?> GetAsync(long id, CancellationToken ct = default) =>
        _db.Patients.AsNoTracking().Include(p => p.Identifiers).Include(p => p.MergedIntoPatient).AsSplitQuery().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Patient?> FindByNumberAsync(string patientNumber, CancellationToken ct = default)
    {
        var value = patientNumber.Trim().ToUpperInvariant();
        var id = await _db.PatientIdentifiers.Where(i => i.Value.ToUpper() == value).Select(i => (long?)i.PatientId).FirstOrDefaultAsync(ct);
        return id is null ? null : await GetAsync(id.Value, ct);
    }

    // ---------------- Search ----------------

    public async Task<PagedResult<PatientListItem>> SearchAsync(PatientSearchQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 5, 200);
        var query = _db.Patients.AsNoTracking().AsQueryable();

        query = q.Status switch
        {
            "archived" => query.Where(p => p.Status == PatientStatus.Archived),
            "all" => query,
            "merged" => query.Where(p => p.Status == PatientStatus.Merged),
            _ => query.Where(p => p.Status == PatientStatus.Active)
        };

        var text = (q.Q ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            var upper = text.ToUpperInvariant();
            var digits = Digits(text);
            var exactPatientIds = await _db.PatientIdentifiers.Where(i => i.Value.ToUpper() == upper).Select(i => i.PatientId)
                .Union(_db.PatientEncounters.Where(e => e.EncounterNumber.ToUpper() == upper).Select(e => e.PatientId))
                .ToListAsync(ct);

            if (exactPatientIds.Count > 0 && (q.Status is null or "" or "active"))
                query = _db.Patients.AsNoTracking().Where(p => exactPatientIds.Contains(p.Id)); // exact ID always found, any status
            else if (exactPatientIds.Count > 0)
                query = query.Where(p => exactPatientIds.Contains(p.Id));
            else if (digits.Length > 0 && digits.Length == text.Replace("-", "").Replace(" ", "").Replace("+", "").Length)
            {
                // Numbers: CNIC, mobile, or the numeric part of a Patient ID ("25" finds BNH-000025).
                var mobileKey = MobileKey(text) ?? digits;
                long.TryParse(digits.Length <= 10 ? digits : "0", out var seqNo);
                query = query.Where(p => (p.CnicDigits != null && p.CnicDigits.StartsWith(digits))
                                         || (p.MobileDigits != null && p.MobileDigits.Contains(mobileKey))
                                         || (seqNo > 0 && p.SequenceNumber == seqNo));
            }
            else
            {
                var like = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                query = query.Where(p => EF.Functions.ILike(p.FullName, like)
                                         || (p.GuardianName != null && EF.Functions.ILike(p.GuardianName, like))
                                         || (p.CityOrVillage != null && EF.Functions.ILike(p.CityOrVillage, like)));
            }
        }

        if (q.RegisteredFrom is { } from) { var f = await _clock.StartOfDayUtcAsync(from); query = query.Where(p => p.RegisteredUtc >= f); }
        if (q.RegisteredTo is { } to) { var t = await _clock.StartOfDayUtcAsync(to.AddDays(1)); query = query.Where(p => p.RegisteredUtc < t); }
        if (q.Gender is { } g) query = query.Where(p => p.Gender == g);
        if (q.VisitType == "opd") query = query.Where(p => p.Encounters.Any(e => e.Type == EncounterType.Opd));
        if (q.VisitType == "emergency") query = query.Where(p => p.Encounters.Any(e => e.Type == EncounterType.Emergency));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(p => p.RegisteredUtc).ThenByDescending(p => p.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(p => new
            {
                p.Id, p.PatientNumber, p.FullName, p.GuardianRelation, p.GuardianName, p.Gender, p.DateOfBirth,
                p.AgeAtRegistration, p.AgeUnitAtRegistration, p.MobileNumber, p.Cnic, p.CityOrVillage, p.RegisteredUtc, p.Status, p.IsUnidentified,
                Opd = p.Encounters.Count(e => e.Type == EncounterType.Opd),
                Er = p.Encounters.Count(e => e.Type == EncounterType.Emergency),
                Last = p.Encounters.Max(e => (DateTime?)e.EncounterUtc)
            }).ToListAsync(ct);
        var today = await _clock.TodayAsync();
        var items = rows.Select(r =>
        {
            var age = r.DateOfBirth is { } d ? AgeCalculator.FromDateOfBirth(d, today)
                : r.AgeAtRegistration is { } a && r.AgeUnitAtRegistration is { } u ? new PatientAge(a, u) : (PatientAge?)null;
            return new PatientListItem(r.Id, r.PatientNumber, r.FullName, r.GuardianRelation, r.GuardianName, r.Gender, age?.ToString(),
                r.MobileNumber, r.Cnic, r.CityOrVillage, r.RegisteredUtc, r.Status, r.IsUnidentified, r.Opd, r.Er, r.Last);
        }).ToList();
        return new PagedResult<PatientListItem>(items, total, page, size);
    }

    // ---------------- Archive & duplicate correction ----------------

    public async Task<ServiceResult> SetArchivedAsync(long id, bool archived, string? reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3) return ServiceResult.Fail("Please give a reason.");
        var p = await _db.Patients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return ServiceResult.Fail("Patient not found.");
        if (p.Status == PatientStatus.Merged) return ServiceResult.Fail("A merged registration cannot be archived or restored.");
        if (archived == (p.Status == PatientStatus.Archived)) return ServiceResult.Ok();
        p.Status = archived ? PatientStatus.Archived : PatientStatus.Active;
        p.ArchivedUtc = archived ? _clock.UtcNow : null;
        p.StatusReason = reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        p.UpdatedUtc = _clock.UtcNow;
        p.UpdatedByUserId = _ctx.UserId;
        _audit.Add(archived ? AuditActions.PatientArchived : AuditActions.PatientRestored, "Patient", p.PatientNumber,
            $"Patient {p.PatientNumber} {(archived ? "archived" : "restored")}: {p.StatusReason}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <summary>
    /// Controlled duplicate correction: visits of the duplicate move to the correct patient, the duplicate's
    /// Patient ID keeps working as an alias, the duplicate row is kept (status Merged) and everything is audited.
    /// </summary>
    public async Task<ServiceResult> MergeAsync(long duplicateId, long correctId, string? reason, CancellationToken ct = default)
    {
        if (duplicateId == correctId) return ServiceResult.Fail("Choose two different patients.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) return ServiceResult.Fail("Please describe why these records are duplicates.");

        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var dup = await _db.Patients.Include(p => p.Identifiers).FirstOrDefaultAsync(p => p.Id == duplicateId, ct);
        var target = await _db.Patients.FirstOrDefaultAsync(p => p.Id == correctId, ct);
        if (dup is null || target is null) return ServiceResult.Fail("Patient not found.");
        if (dup.Status == PatientStatus.Merged || target.Status == PatientStatus.Merged) return ServiceResult.Fail("One of the records was already merged.");

        var encounters = await _db.PatientEncounters.Where(e => e.PatientId == dup.Id).ToListAsync(ct);
        foreach (var e in encounters)
        {
            e.OriginalPatientId ??= dup.Id;
            e.PatientId = target.Id;
            e.UpdatedUtc = _clock.UtcNow;
        }
        foreach (var ident in dup.Identifiers.Where(i => i.Type != PatientIdentifierType.PatientNumber))
            ident.PatientId = target.Id;
        _db.PatientIdentifiers.Add(new PatientIdentifier
        {
            PatientId = target.Id, Type = PatientIdentifierType.MergedPatientNumber, Value = dup.PatientNumber,
            Note = "Duplicate registration merged: " + reason.Trim(), CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId
        });
        dup.Status = PatientStatus.Merged;
        dup.MergedIntoPatientId = target.Id;
        dup.StatusReason = reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
        dup.UpdatedUtc = _clock.UtcNow;
        dup.UpdatedByUserId = _ctx.UserId;
        _audit.Add(AuditActions.PatientMerged, "Patient", dup.PatientNumber,
            $"Duplicate {dup.PatientNumber} merged into {target.PatientNumber}; {encounters.Count} visit(s) moved. Reason: {dup.StatusReason}",
            Snapshot(dup), new { MergedInto = target.PatientNumber, Visits = encounters.Select(e => e.EncounterNumber) });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<(int Today, int Total)> RegistrationCountsAsync(CancellationToken ct = default)
    {
        var start = await _clock.StartOfDayUtcAsync(await _clock.TodayAsync());
        var today = await _db.Patients.CountAsync(p => p.RegisteredUtc >= start && p.Status != PatientStatus.Merged, ct);
        var total = await _db.Patients.CountAsync(p => p.Status == PatientStatus.Active, ct);
        return (today, total);
    }
}
