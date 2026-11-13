using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Patients;

public sealed class RegistrationSettingsInput
{
    public string PatientNumberPrefix { get; set; } = "BNH-";
    public int PatientNumberDigits { get; set; } = 6;
    public string OpdNumberPrefix { get; set; } = "OPD-";
    public string EmergencyNumberPrefix { get; set; } = "ER-";
    public bool EncounterNumberIncludesYear { get; set; } = true;
    public int EncounterNumberDigits { get; set; } = 6;
    public string OpdSlipPaper { get; set; } = "A5";
    public string EmergencySlipPaper { get; set; } = "A4";
    public bool AutoPrintAfterRegistration { get; set; } = true;
}

public sealed class OpdSettingsInput
{
    public bool OpdTokenEnabled { get; set; }
    public TokenScopeMode OpdTokenScope { get; set; }
    public int? DefaultOpdDepartmentId { get; set; }
    public bool RequireDoctorForPrivateOpd { get; set; }
    public int? DefaultOpdServiceItemId { get; set; }
    public int? DefaultEmergencyServiceItemId { get; set; }
}

public sealed class DoctorInput
{
    public string Name { get; set; } = string.Empty;
    public string? Qualification { get; set; }
    public string? Specialty { get; set; }
    public string? RegistrationNumber { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ServiceItemInput
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public EncounterType Category { get; set; }
    public decimal DefaultFee { get; set; }
    public int? DoctorId { get; set; }
    public bool IsActive { get; set; } = true;
}

public interface IRegistrationSetupService
{
    Task<RegistrationSettings> GetSettingsAsync(CancellationToken ct = default);
    Task<ServiceResult> UpdateNumberingAsync(RegistrationSettingsInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> UpdateOpdAsync(OpdSettingsInput input, uint rowVersion, CancellationToken ct = default);

    Task<IReadOnlyList<Doctor>> DoctorsAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveDoctorAsync(int? id, DoctorInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceItem>> ServiceItemsAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveServiceItemAsync(int? id, ServiceItemInput input, CancellationToken ct = default);
    Task<IReadOnlyList<EmergencyCategory>> CategoriesAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveCategoryAsync(int? id, string name, int sortOrder, bool isActive, CancellationToken ct = default);

    string FormatPatientNumber(RegistrationSettings s, long sequence);
    string FormatEncounterNumber(RegistrationSettings s, EncounterType type, long sequence, int year);
}

/// <summary>Numbering formats, OPD/Emergency configuration, doctors, fee references and emergency categories.</summary>
public sealed partial class RegistrationSetupService : IRegistrationSetupService
{
    public static readonly string[] SlipPapers = { "A5", "A4" };
    public static readonly string[] DefaultCategories =
    {
        "Road Traffic Accident", "Trauma / Injury", "Fall", "Burn", "Poisoning", "Dog / Animal Bite", "Snake Bite",
        "Chest Pain / Cardiac", "Breathing Difficulty", "Fever / Infection", "Diarrhoea / Vomiting", "Obstetric / Labour",
        "Fight / Assault", "Unconscious / Fits", "Other"
    };

    [GeneratedRegex(@"^[A-Za-z0-9\-/]{0,12}$")] private static partial Regex PrefixRegex();

    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public RegistrationSetupService(HmsDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>Creates default settings, emergency categories and fee references once. Never overwrites existing data.</summary>
    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        if (!await db.RegistrationSettings.AnyAsync(ct))
            db.RegistrationSettings.Add(new RegistrationSettings { Id = RegistrationSettings.SingletonId, UpdatedUtc = now });
        if (!await db.EmergencyCategories.AnyAsync(ct))
            for (var i = 0; i < DefaultCategories.Length; i++)
                db.EmergencyCategories.Add(new EmergencyCategory { Name = DefaultCategories[i], SortOrder = (i + 1) * 10, IsActive = true });
        if (!await db.ServiceItems.AnyAsync(ct))
        {
            db.ServiceItems.Add(new ServiceItem { Code = "OPD-REG", Name = "OPD Registration", Category = EncounterType.Opd, DefaultFee = 0, CreatedUtc = now, UpdatedUtc = now });
            db.ServiceItems.Add(new ServiceItem { Code = "ER-REG", Name = "Emergency Registration", Category = EncounterType.Emergency, DefaultFee = 0, CreatedUtc = now, UpdatedUtc = now });
        }
        await db.SaveChangesAsync(ct);

        var s = await db.RegistrationSettings.FirstAsync(ct);
        var changed = false;
        if (s.DefaultOpdServiceItemId is null)
        {
            s.DefaultOpdServiceItemId = await db.ServiceItems.Where(x => x.Code == "OPD-REG").Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            changed = true;
        }
        if (s.DefaultEmergencyServiceItemId is null)
        {
            s.DefaultEmergencyServiceItemId = await db.ServiceItems.Where(x => x.Code == "ER-REG").Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            changed = true;
        }
        if (s.DefaultOpdDepartmentId is null)
        {
            var opd = await db.Departments.Where(d => d.Name == "OPD").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
            if (opd is not null) { s.DefaultOpdDepartmentId = opd; changed = true; }
        }
        if (changed) await db.SaveChangesAsync(ct);
    }

    public async Task<RegistrationSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await _db.RegistrationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is not null) return s;
        await SeedAsync(_db, Now, ct);
        return await _db.RegistrationSettings.AsNoTracking().FirstAsync(ct);
    }

    public string FormatPatientNumber(RegistrationSettings s, long sequence) =>
        s.PatientNumberPrefix + sequence.ToString("D" + Math.Clamp(s.PatientNumberDigits, 4, 10));

    public string FormatEncounterNumber(RegistrationSettings s, EncounterType type, long sequence, int year) =>
        (type == EncounterType.Opd ? s.OpdNumberPrefix : s.EmergencyNumberPrefix)
        + (s.EncounterNumberIncludesYear ? year + "-" : string.Empty)
        + sequence.ToString("D" + Math.Clamp(s.EncounterNumberDigits, 4, 10));

    public async Task<ServiceResult> UpdateNumberingAsync(RegistrationSettingsInput i, uint rowVersion, CancellationToken ct = default)
    {
        var e = new List<string>();
        foreach (var (v, n) in new[] { (i.PatientNumberPrefix, "Patient ID prefix"), (i.OpdNumberPrefix, "OPD number prefix"), (i.EmergencyNumberPrefix, "Emergency number prefix") })
            if (!PrefixRegex().IsMatch(v ?? string.Empty)) e.Add($"{n} may contain only letters, numbers, '-' and '/' (max 12).");
        if (i.PatientNumberDigits is < 4 or > 10 || i.EncounterNumberDigits is < 4 or > 10) e.Add("Number of digits must be between 4 and 10.");
        if (string.Equals(i.OpdNumberPrefix, i.EmergencyNumberPrefix, StringComparison.OrdinalIgnoreCase))
            e.Add("OPD and Emergency prefixes must be different so slip numbers can never collide.");
        if (!SlipPapers.Contains(i.OpdSlipPaper) || !SlipPapers.Contains(i.EmergencySlipPaper)) e.Add("Slip paper must be A5 or A4.");
        if (e.Count > 0) return ServiceResult.Fail(e.ToArray());

        var s = await _db.RegistrationSettings.FirstAsync(ct);
        var old = new { s.PatientNumberPrefix, s.PatientNumberDigits, s.OpdNumberPrefix, s.EmergencyNumberPrefix, s.EncounterNumberIncludesYear, s.EncounterNumberDigits, s.OpdSlipPaper, s.EmergencySlipPaper, s.AutoPrintAfterRegistration };
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = rowVersion;
        s.PatientNumberPrefix = i.PatientNumberPrefix ?? string.Empty;
        s.PatientNumberDigits = i.PatientNumberDigits;
        s.OpdNumberPrefix = i.OpdNumberPrefix ?? string.Empty;
        s.EmergencyNumberPrefix = i.EmergencyNumberPrefix ?? string.Empty;
        s.EncounterNumberIncludesYear = i.EncounterNumberIncludesYear;
        s.EncounterNumberDigits = i.EncounterNumberDigits;
        s.OpdSlipPaper = i.OpdSlipPaper;
        s.EmergencySlipPaper = i.EmergencySlipPaper;
        s.AutoPrintAfterRegistration = i.AutoPrintAfterRegistration;
        s.UpdatedUtc = Now;
        _audit.Add(AuditActions.RegistrationSettingsChanged, "RegistrationSettings", "1",
            "Patient ID / visit number / slip settings changed (existing numbers never change)", old,
            new { s.PatientNumberPrefix, s.PatientNumberDigits, s.OpdNumberPrefix, s.EmergencyNumberPrefix, s.EncounterNumberIncludesYear, s.EncounterNumberDigits, s.OpdSlipPaper, s.EmergencySlipPaper, s.AutoPrintAfterRegistration });
        return await SaveAsync(ct);
    }

    public async Task<ServiceResult> UpdateOpdAsync(OpdSettingsInput i, uint rowVersion, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(i.OpdTokenScope)) return ServiceResult.Fail("Unknown token scope.");
        if (i.DefaultOpdDepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d && x.IsActive, ct))
            return ServiceResult.Fail("The default OPD department does not exist or is inactive.");
        foreach (var id in new[] { i.DefaultOpdServiceItemId, i.DefaultEmergencyServiceItemId }.OfType<int>())
            if (!await _db.ServiceItems.AnyAsync(x => x.Id == id && x.IsActive, ct)) return ServiceResult.Fail("Selected fee reference is not available.");

        var s = await _db.RegistrationSettings.FirstAsync(ct);
        var old = new { s.OpdTokenEnabled, s.OpdTokenScope, s.DefaultOpdDepartmentId, s.RequireDoctorForPrivateOpd, s.DefaultOpdServiceItemId, s.DefaultEmergencyServiceItemId };
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = rowVersion;
        s.OpdTokenEnabled = i.OpdTokenEnabled;
        s.OpdTokenScope = i.OpdTokenScope;
        s.DefaultOpdDepartmentId = i.DefaultOpdDepartmentId;
        s.RequireDoctorForPrivateOpd = i.RequireDoctorForPrivateOpd;
        s.DefaultOpdServiceItemId = i.DefaultOpdServiceItemId;
        s.DefaultEmergencyServiceItemId = i.DefaultEmergencyServiceItemId;
        s.UpdatedUtc = Now;
        _audit.Add(AuditActions.RegistrationSettingsChanged, "RegistrationSettings", "1", "OPD settings changed", old,
            new { s.OpdTokenEnabled, s.OpdTokenScope, s.DefaultOpdDepartmentId, s.RequireDoctorForPrivateOpd, s.DefaultOpdServiceItemId, s.DefaultEmergencyServiceItemId });
        return await SaveAsync(ct);
    }

    public async Task<IReadOnlyList<Doctor>> DoctorsAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.Doctors.AsNoTracking().Include(d => d.Department).Where(d => !activeOnly || d.IsActive).OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveDoctorAsync(int? id, DoctorInput i, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > 150) return ServiceResult<int>.Fail("Doctor name is required (max 150 characters).");
        if ((i.Qualification?.Length ?? 0) > 150 || (i.Specialty?.Length ?? 0) > 100 || (i.RegistrationNumber?.Length ?? 0) > 50)
            return ServiceResult<int>.Fail("Qualification, specialty or registration number is too long.");
        if (i.DepartmentId is int dep && !await _db.Departments.AnyAsync(x => x.Id == dep, ct)) return ServiceResult<int>.Fail("Department not found.");
        Doctor doc;
        object? old = null;
        if (id is null)
        {
            doc = new Doctor { CreatedUtc = Now };
            _db.Doctors.Add(doc);
        }
        else
        {
            var found = await _db.Doctors.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null) return ServiceResult<int>.Fail("Doctor not found.");
            doc = found;
            old = new { doc.Name, doc.Qualification, doc.Specialty, doc.RegistrationNumber, doc.DepartmentId, doc.IsActive };
        }
        doc.Name = i.Name.Trim();
        doc.Qualification = Clean(i.Qualification);
        doc.Specialty = Clean(i.Specialty);
        doc.RegistrationNumber = Clean(i.RegistrationNumber);
        doc.DepartmentId = i.DepartmentId;
        doc.IsActive = i.IsActive;
        doc.UpdatedUtc = Now;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditActions.DoctorSaved, "Doctor", doc.Id.ToString(), $"Doctor '{doc.Name}' {(id is null ? "added" : "updated")}", old,
            new { doc.Name, doc.Qualification, doc.Specialty, doc.RegistrationNumber, doc.DepartmentId, doc.IsActive }, ct: ct);
        return ServiceResult<int>.Ok(doc.Id);
    }

    public async Task<IReadOnlyList<ServiceItem>> ServiceItemsAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.ServiceItems.AsNoTracking().Include(s => s.Doctor).Where(s => !activeOnly || s.IsActive).OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveServiceItemAsync(int? id, ServiceItemInput i, CancellationToken ct = default)
    {
        var code = (i.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, "^[A-Z0-9\\-]{2,30}$")) return ServiceResult<int>.Fail("Code must be 2–30 letters, numbers or '-'.");
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 150) return ServiceResult<int>.Fail("Name is required (max 150 characters).");
        if (i.DefaultFee is < 0 or > 10_000_000) return ServiceResult<int>.Fail("Fee must be between 0 and 10,000,000.");
        if (!Enum.IsDefined(i.Category)) return ServiceResult<int>.Fail("Unknown category.");
        if (i.DoctorId is int doc && !await _db.Doctors.AnyAsync(d => d.Id == doc, ct)) return ServiceResult<int>.Fail("Doctor not found.");
        if (await _db.ServiceItems.AnyAsync(s => s.Code == code && s.Id != (id ?? 0), ct)) return ServiceResult<int>.Fail($"Code '{code}' is already used.");

        ServiceItem item;
        object? old = null;
        if (id is null)
        {
            item = new ServiceItem { CreatedUtc = Now };
            _db.ServiceItems.Add(item);
        }
        else
        {
            var found = await _db.ServiceItems.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null) return ServiceResult<int>.Fail("Fee reference not found.");
            item = found;
            old = new { item.Code, item.Name, item.Category, item.DefaultFee, item.DoctorId, item.IsActive };
        }
        item.Code = code;
        item.Name = i.Name.Trim();
        item.Category = i.Category;
        item.DefaultFee = decimal.Round(i.DefaultFee, 2);
        item.DoctorId = i.DoctorId;
        item.IsActive = i.IsActive;
        item.UpdatedUtc = Now;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditActions.ServiceItemSaved, "ServiceItem", item.Id.ToString(), $"Fee reference '{item.Name}' saved", old,
            new { item.Code, item.Name, item.Category, item.DefaultFee, item.DoctorId, item.IsActive }, ct: ct);
        return ServiceResult<int>.Ok(item.Id);
    }

    public async Task<IReadOnlyList<EmergencyCategory>> CategoriesAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.EmergencyCategories.AsNoTracking().Where(c => !activeOnly || c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveCategoryAsync(int? id, string name, int sortOrder, bool isActive, CancellationToken ct = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > 100) return ServiceResult<int>.Fail("Category name is required (max 100 characters).");
        if (await _db.EmergencyCategories.AnyAsync(c => c.Id != (id ?? 0) && c.Name.ToLower() == name.ToLower(), ct))
            return ServiceResult<int>.Fail($"Category '{name}' already exists.");
        EmergencyCategory c;
        if (id is null)
        {
            c = new EmergencyCategory();
            _db.EmergencyCategories.Add(c);
        }
        else
        {
            var found = await _db.EmergencyCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (found is null) return ServiceResult<int>.Fail("Category not found.");
            c = found;
        }
        c.Name = name;
        c.SortOrder = sortOrder;
        c.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditActions.EmergencyCategorySaved, "EmergencyCategory", c.Id.ToString(), $"Emergency category '{name}' saved", ct: ct);
        return ServiceResult<int>.Ok(c.Id);
    }

    private async Task<ServiceResult> SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("These settings were changed on another computer. Please reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("A record with the same value already exists.");
        }
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
