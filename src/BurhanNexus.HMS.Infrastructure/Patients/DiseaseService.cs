using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Patients;

public sealed class DiseaseInput
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record DiseaseRow(Disease Disease, int Records, int Patients);

public sealed class DiseaseAnalyticsFilter
{
    /// <summary>Null = all diseases (a disease-wise table is shown).</summary>
    public int? DiseaseId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    /// <summary>Null = all genders.</summary>
    public Gender? Gender { get; set; }
    /// <summary>Upper bounds of the age groups (default 5, 12, 18, 35, 50, 65 → 0–5, 6–12, … above 65).</summary>
    public int[] AgeBounds { get; set; } = DiseaseService.DefaultAgeBounds;
}

public sealed record AgeGroupCount(string Label, int Patients);
public sealed record DateCount(DateOnly Date, int Patients, int Visits);
public sealed record DiseaseCount(int DiseaseId, string Disease, int Patients, int Visits, int Male, int Female, int Other);

public sealed record DiseaseAnalytics(
    string? DiseaseName, int UniquePatients, int Visits, int Male, int Female, int Other, int UnknownGender, int AgeUnknown,
    IReadOnlyList<AgeGroupCount> AgeGroups, IReadOnlyList<DateCount> ByDate, IReadOnlyList<DiseaseCount> ByDisease);

public interface IDiseaseService
{
    Task<IReadOnlyList<DiseaseRow>> ListAsync(string? q, bool? active, CancellationToken ct = default);
    /// <summary>Active diseases for the searchable registration dropdown (name order).</summary>
    Task<IReadOnlyList<Disease>> ActiveAsync(CancellationToken ct = default);
    Task<Disease?> GetAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveAsync(int? id, DiseaseInput input, uint? rowVersion, CancellationToken ct = default);
    /// <summary>Deletes an unused disease; a disease used in patient records is only deactivated (soft delete).</summary>
    Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Finds an active disease by name (case-insensitive, normalized) or – when allowed – adds it to the Disease Master.</summary>
    Task<ServiceResult<Disease>> ResolveAsync(string name, bool canCreate, CancellationToken ct = default);
    /// <summary>Records a disease for a patient on a visit (never overwrites earlier records). Empty name = nothing recorded.</summary>
    Task<ServiceResult<PatientDisease?>> RecordAsync(long patientId, string? diseaseName, long? encounterId, long? appointmentId, bool canCreate,
        DateOnly? visitDate = null, string? notes = null, CancellationToken ct = default);
    Task<IReadOnlyList<PatientDisease>> HistoryAsync(long patientId, CancellationToken ct = default);
    Task<DiseaseAnalytics> AnalyticsAsync(DiseaseAnalyticsFilter f, CancellationToken ct = default);
}

/// <summary>Update 18: the central Disease Master (registration, patient disease history, analytics and management use this one catalogue).</summary>
public sealed class DiseaseService : IDiseaseService
{
    public static readonly int[] DefaultAgeBounds = { 5, 12, 18, 35, 50, 65 };

    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    public DiseaseService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    /// <summary>Upper case, only letters and digits ("R.T.I." = "rti" = "RTI").</summary>
    public static string Normalize(string? name) => Regex.Replace((name ?? "").ToUpperInvariant(), @"[^\p{L}\p{N}]", "");

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = Regex.Replace(v.Trim(), @"\s+", " ");
        return t.Length > max ? t[..max] : t;
    }

    public async Task<IReadOnlyList<DiseaseRow>> ListAsync(string? q, bool? active, CancellationToken ct = default)
    {
        var query = _db.Diseases.AsNoTracking().Where(d => !d.IsDeleted);
        if (active is bool a) query = query.Where(d => d.IsActive == a);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim().ToLower();
            var n = Normalize(q);
            query = query.Where(d => d.Name.ToLower().Contains(t) || (d.Code != null && d.Code.ToLower().Contains(t)) || (n != "" && d.NormalizedName.Contains(n)));
        }
        var list = await query.OrderBy(d => d.Name).Take(1000).ToListAsync(ct);
        var ids = list.Select(d => d.Id).ToList();
        var usage = await _db.PatientDiseases.AsNoTracking().Where(p => ids.Contains(p.DiseaseId)).GroupBy(p => p.DiseaseId)
            .Select(g => new { g.Key, Records = g.Count(), Patients = g.Select(x => x.PatientId).Distinct().Count() }).ToDictionaryAsync(x => x.Key, ct);
        return list.Select(d => new DiseaseRow(d, usage.TryGetValue(d.Id, out var u) ? u.Records : 0, usage.TryGetValue(d.Id, out var u2) ? u2.Patients : 0)).ToList();
    }

    public async Task<IReadOnlyList<Disease>> ActiveAsync(CancellationToken ct = default) =>
        await _db.Diseases.AsNoTracking().Where(d => d.IsActive && !d.IsDeleted).OrderBy(d => d.Name).Take(5000).ToListAsync(ct);

    public Task<Disease?> GetAsync(int id, CancellationToken ct = default) => _db.Diseases.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public async Task<ServiceResult<int>> SaveAsync(int? id, DiseaseInput i, uint? rowVersion, CancellationToken ct = default)
    {
        var name = Clean(i.Name, 150);
        var errors = new List<string>();
        if (name is null) errors.Add("Disease name is required.");
        var norm = Normalize(name);
        if (name is not null && norm.Length == 0) errors.Add("Enter a disease name with letters or digits.");
        var code = Clean(i.Code, 30);
        var description = Clean(i.Description, 500);
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var dup = await _db.Diseases.AsNoTracking().FirstOrDefaultAsync(d => d.NormalizedName == norm && d.Id != (id ?? 0), ct);
        if (dup is not null)
            return ServiceResult<int>.Fail(dup.IsDeleted
                ? $"'{dup.Name}' already exists (deleted earlier because it is used in patient records). Ask the administrator to restore it."
                : $"'{dup.Name}' already exists in the Disease Master.");
        if (code is not null && await _db.Diseases.AnyAsync(d => d.Code != null && d.Code.ToLower() == code.ToLower() && d.Id != (id ?? 0) && !d.IsDeleted, ct))
            return ServiceResult<int>.Fail($"Disease code '{code}' is already used.");
        var now = _clock.UtcNow;
        Disease d;
        object? old = null;
        if (id is int existing)
        {
            d = await _db.Diseases.FirstOrDefaultAsync(x => x.Id == existing && !x.IsDeleted, ct) ?? throw new KeyNotFoundException();
            if (rowVersion is uint rv) _db.Entry(d).Property(x => x.RowVersion).OriginalValue = rv;
            old = new { d.Name, d.Code, d.Description, d.IsActive };
        }
        else
        {
            d = new Disease { CreatedUtc = now, CreatedByUsername = _ctx.Username };
            _db.Diseases.Add(d);
        }
        d.Name = name!;
        d.NormalizedName = norm;
        d.Code = code;
        d.Description = description;
        d.IsActive = i.IsActive;
        d.UpdatedUtc = now;
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult<int>.Fail("The disease was changed by someone else. Reload and try again."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult<int>.Fail($"'{name}' was just added by someone else.");
        }
        await _audit.LogAsync(id is null ? AuditActions.DiseaseAdded : AuditActions.DiseaseChanged, "Disease", d.Id.ToString(),
            id is null ? $"Disease '{d.Name}' added to the Disease Master" : $"Disease '{d.Name}' changed", old, new { d.Name, d.Code, d.Description, d.IsActive }, ct: ct);
        return ServiceResult<int>.Ok(d.Id);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var d = await _db.Diseases.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (d is null) return ServiceResult<bool>.Fail("Disease not found.");
        var used = await _db.PatientDiseases.AnyAsync(p => p.DiseaseId == id, ct);
        if (used)
        {
            // Used in patient records: kept for the history and reports, hidden from new registrations.
            d.IsActive = false;
            d.IsDeleted = true;
            d.UpdatedUtc = _clock.UtcNow;
        }
        else _db.Diseases.Remove(d);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditActions.DiseaseDeleted, "Disease", id.ToString(),
            used ? $"Disease '{d.Name}' is used in patient records – deactivated and hidden (history kept)" : $"Disease '{d.Name}' deleted (not used)", ct: ct);
        return ServiceResult<bool>.Ok(used);
    }

    public async Task<ServiceResult<Disease>> ResolveAsync(string name, bool canCreate, CancellationToken ct = default)
    {
        var clean = Clean(name, 150);
        var norm = Normalize(clean);
        if (clean is null || norm.Length == 0) return ServiceResult<Disease>.Fail("Enter the disease name.");
        var d = await _db.Diseases.AsNoTracking().FirstOrDefaultAsync(x => x.NormalizedName == norm, ct);
        if (d is not null)
        {
            if (d.IsDeleted || !d.IsActive) return ServiceResult<Disease>.Fail($"'{d.Name}' is inactive in the Disease Master. Select another disease.");
            return ServiceResult<Disease>.Ok(d);
        }
        if (!canCreate) return ServiceResult<Disease>.Fail($"'{clean}' is not in the Disease Master. Select a disease from the list (only authorised users can add new diseases).");
        var saved = await SaveAsync(null, new DiseaseInput { Name = clean, IsActive = true }, null, ct);
        if (!saved.Succeeded)
        {
            // Added by someone else at the same moment: use it.
            var again = await _db.Diseases.AsNoTracking().FirstOrDefaultAsync(x => x.NormalizedName == norm && x.IsActive && !x.IsDeleted, ct);
            return again is not null ? ServiceResult<Disease>.Ok(again) : ServiceResult<Disease>.Fail(saved.Errors.ToArray());
        }
        return ServiceResult<Disease>.Ok((await _db.Diseases.AsNoTracking().FirstAsync(x => x.Id == saved.Value, ct)));
    }

    public async Task<ServiceResult<PatientDisease?>> RecordAsync(long patientId, string? diseaseName, long? encounterId, long? appointmentId, bool canCreate,
        DateOnly? visitDate = null, string? notes = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(diseaseName)) return ServiceResult<PatientDisease?>.Ok(null); // optional
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct);
        if (patient is null) return ServiceResult<PatientDisease?>.Fail("Patient not found.");
        var disease = await ResolveAsync(diseaseName, canCreate, ct);
        if (!disease.Succeeded) return ServiceResult<PatientDisease?>.Fail(disease.Errors.ToArray());
        var today = await _clock.TodayAsync();
        var date = visitDate ?? today;
        if (date > today) return ServiceResult<PatientDisease?>.Fail("The visit date cannot be in the future.");
        if (encounterId is long eid)
        {
            var enc = await _db.PatientEncounters.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eid && e.PatientId == patientId, ct);
            if (enc is null) return ServiceResult<PatientDisease?>.Fail("The visit does not belong to this patient.");
            date = enc.EncounterDate;
        }
        // The same disease on the same visit is recorded once (no duplicate records).
        var existing = await _db.PatientDiseases.AsNoTracking().FirstOrDefaultAsync(p => p.PatientId == patientId && p.DiseaseId == disease.Value!.Id
            && (encounterId != null ? p.EncounterId == encounterId : p.EncounterId == null && p.VisitDate == date), ct);
        if (existing is not null) return ServiceResult<PatientDisease?>.Ok(existing);
        if (encounterId is not null)
        {
            // Recorded at registration the same day: that record now belongs to this visit (one visit – one record).
            var atRegistration = await _db.PatientDiseases.FirstOrDefaultAsync(p => p.PatientId == patientId && p.DiseaseId == disease.Value!.Id
                && p.EncounterId == null && p.VisitDate == date, ct);
            if (atRegistration is not null)
            {
                atRegistration.EncounterId = encounterId;
                atRegistration.AppointmentId ??= appointmentId;
                await _db.SaveChangesAsync(ct);
                return ServiceResult<PatientDisease?>.Ok(atRegistration);
            }
        }
        int? age = null;
        if (patient.DateOfBirth is { } dob)
        {
            var a = AgeCalculator.FromDateOfBirth(dob, date);
            age = a.Unit == AgeUnit.Years ? a.Value : 0; // months / days old = 0 years
        }
        var row = new PatientDisease
        {
            PatientId = patientId, DiseaseId = disease.Value!.Id, EncounterId = encounterId, AppointmentId = appointmentId, VisitDate = date,
            AgeYears = age, Gender = patient.Gender, Notes = Clean(notes, 300),
            CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username,
        };
        _db.PatientDiseases.Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditActions.PatientDiseaseRecorded, "Patient", patient.PatientNumber,
            $"Disease '{disease.Value.Name}' recorded for {patient.PatientNumber} on {date:dd MMM yyyy}", ct: ct);
        return ServiceResult<PatientDisease?>.Ok(row);
    }

    public async Task<IReadOnlyList<PatientDisease>> HistoryAsync(long patientId, CancellationToken ct = default) =>
        await _db.PatientDiseases.AsNoTracking().Include(p => p.Disease).Include(p => p.Encounter)
            .Where(p => p.PatientId == patientId).OrderByDescending(p => p.VisitDate).ThenByDescending(p => p.Id).ToListAsync(ct);

    public static IReadOnlyList<(string Label, int Min, int? Max)> AgeGroups(int[] bounds)
    {
        var b = bounds.Where(x => x >= 0 && x < 130).Distinct().OrderBy(x => x).ToArray();
        if (b.Length == 0) b = DefaultAgeBounds;
        var groups = new List<(string, int, int?)>();
        var start = 0;
        foreach (var upper in b)
        {
            groups.Add(($"{start}–{upper} years", start, upper));
            start = upper + 1;
        }
        groups.Add(($"Above {b[^1]} years", start, null));
        return groups;
    }

    public async Task<DiseaseAnalytics> AnalyticsAsync(DiseaseAnalyticsFilter f, CancellationToken ct = default)
    {
        var q = _db.PatientDiseases.AsNoTracking().AsQueryable();
        if (f.DiseaseId is int did) q = q.Where(p => p.DiseaseId == did);
        if (f.From is { } from) q = q.Where(p => p.VisitDate >= from);
        if (f.To is { } to) q = q.Where(p => p.VisitDate <= to);
        if (f.Gender is { } g) q = q.Where(p => p.Gender == g);
        if (f.MinAge is int min) q = q.Where(p => p.AgeYears != null && p.AgeYears >= min);
        if (f.MaxAge is int max) q = q.Where(p => p.AgeYears != null && p.AgeYears <= max);
        var rows = await q.Select(p => new { p.PatientId, p.DiseaseId, Disease = p.Disease!.Name, p.VisitDate, p.EncounterId, p.AgeYears, p.Gender }).ToListAsync(ct);
        // Visits: one per patient, disease and visit (encounter, or registration date) – repeat visits count again, duplicates never.
        var visits = rows.GroupBy(r => new { r.PatientId, r.DiseaseId, Visit = r.EncounterId?.ToString() ?? r.VisitDate.ToString("yyyyMMdd") }).Select(x => x.First()).ToList();
        // Unique patients: a patient once, with the age / gender recorded at the first visit in the period.
        var patients = visits.OrderBy(r => r.VisitDate).GroupBy(r => r.PatientId).Select(x => x.First()).ToList();
        var groups = AgeGroups(f.AgeBounds).Select(gr => new AgeGroupCount(gr.Label,
            patients.Count(p => p.AgeYears is int a && a >= gr.Min && (gr.Max is null || a <= gr.Max)))).ToList();
        var byDate = visits.GroupBy(r => r.VisitDate).OrderBy(x => x.Key)
            .Select(x => new DateCount(x.Key, x.Select(r => r.PatientId).Distinct().Count(), x.Count())).ToList();
        var byDisease = visits.GroupBy(r => new { r.DiseaseId, r.Disease }).Select(x =>
            {
                var pts = x.OrderBy(r => r.VisitDate).GroupBy(r => r.PatientId).Select(y => y.First()).ToList();
                return new DiseaseCount(x.Key.DiseaseId, x.Key.Disease, pts.Count, x.Count(), pts.Count(p => p.Gender == Gender.Male),
                    pts.Count(p => p.Gender == Gender.Female), pts.Count(p => p.Gender == Gender.Other));
            })
            .OrderByDescending(x => x.Patients).ThenBy(x => x.Disease).ToList();
        var name = f.DiseaseId is int id ? await _db.Diseases.AsNoTracking().Where(d => d.Id == id).Select(d => d.Name).FirstOrDefaultAsync(ct) : null;
        return new DiseaseAnalytics(name, patients.Count, visits.Count,
            patients.Count(p => p.Gender == Gender.Male), patients.Count(p => p.Gender == Gender.Female), patients.Count(p => p.Gender == Gender.Other),
            patients.Count(p => p.Gender is null or Gender.Unknown), patients.Count(p => p.AgeYears is null), groups, byDate, byDisease);
    }
}
