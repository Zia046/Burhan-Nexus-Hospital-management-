using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Laboratory;

/// <summary>One CBC parameter row of the configuration page.</summary>
public sealed class CbcRowInput
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; }
    public int Position { get; set; }
}

public interface ICbcConfigService
{
    /// <summary>The hospital's CBC test (standard code CBC-FULL, or the earlier starter code CBC) with ALL its parameters.</summary>
    Task<LabTest?> CbcTestAsync(CancellationToken ct = default);
    /// <summary>Saves names, active flags and the order. Units, reference ranges and booked results are never changed.</summary>
    Task<ServiceResult> SaveAsync(IReadOnlyList<CbcRowInput> rows, CancellationToken ct = default);
    /// <summary>The 14 default parameters active in the default order, every other parameter inactive (nothing is deleted).</summary>
    Task<ServiceResult<IReadOnlyList<string>>> RestoreDefaultAsync(CancellationToken ct = default);
}

/// <summary>
/// Update 15: CBC configuration. The report shows only active parameters; booked tests keep the snapshot taken at booking,
/// so changing the configuration never alters old patient reports.
/// </summary>
public sealed class CbcConfigService : ICbcConfigService
{
    /// <summary>Default CBC: 14 lines in this exact order. Aliases match the parameter of earlier versions (standard / starter catalogue).</summary>
    public static readonly (string Name, string[] Aliases, bool Heading)[] Default =
    {
        ("Total WBC", new[] { "Total WBC count", "WBC", "TLC", "Total leucocyte count", "WBC count" }, false),
        ("Total RBC", new[] { "Total RBC count", "RBC", "RBC count" }, false),
        ("Hemoglobin (HGB)", new[] { "Haemoglobin", "Hemoglobin", "Hb", "HGB", "Hemoglobin HBB", "Haemoglobin (Hb)" }, false),
        ("PCV (HCT)", new[] { "Haematocrit (HCT / PCV)", "Hematocrit", "Haematocrit", "HCT", "PCV", "PCV / HCT" }, false),
        ("MCV", Array.Empty<string>(), false),
        ("MCH", Array.Empty<string>(), false),
        ("MCHC", Array.Empty<string>(), false),
        ("DLC", new[] { "Differential leucocyte count", "Differential count" }, true),
        ("Neutrophils", new[] { "Neutrophil", "Polymorphs" }, false),
        ("Lymphocytes", new[] { "Lymphocyte" }, false),
        ("Monocytes", new[] { "Monocyte" }, false),
        ("Eosinophils", new[] { "Eosinophil" }, false),
        ("Basophils", new[] { "Basophil" }, false),
        ("Platelets Count", new[] { "Platelet count", "Platelets", "PLT", "Platelet" }, false),
    };

    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly HospitalClock _clock;
    public CbcConfigService(HmsDbContext db, IAuditService audit, HospitalClock clock) { _db = db; _audit = audit; _clock = clock; }

    public Task<LabTest?> CbcTestAsync(CancellationToken ct = default) => FindAsync(_db, ct);

    private static async Task<LabTest?> FindAsync(HmsDbContext db, CancellationToken ct) =>
        await db.LabTests.Include(t => t.Parameters).ThenInclude(p => p.Ranges).AsSplitQuery()
            .Where(t => t.Code == "CBC-FULL" || t.Code == "CBC").OrderBy(t => t.Code == "CBC-FULL" ? 0 : 1).FirstOrDefaultAsync(ct);

    public async Task<ServiceResult> SaveAsync(IReadOnlyList<CbcRowInput> rows, CancellationToken ct = default)
    {
        var test = await FindAsync(_db, ct);
        if (test is null) return ServiceResult.Fail("The CBC test was not found in the laboratory catalogue.");
        var errors = new List<string>();
        foreach (var r in rows)
        {
            var name = r.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 100) errors.Add("Every parameter needs a name (at most 100 characters).");
        }
        if (!rows.Any(r => r.IsActive)) errors.Add("At least one CBC parameter must stay active.");
        if (rows.Select(r => r.Name?.Trim()).Where(n => !string.IsNullOrEmpty(n)).GroupBy(n => n!, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            errors.Add("Two parameters have the same name.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.Distinct().ToArray());
        var pos = 0;
        foreach (var r in rows.OrderBy(r => r.Position).ThenBy(r => r.Id))
        {
            var p = test.Parameters.FirstOrDefault(x => x.Id == r.Id);
            if (p is null) continue;
            p.Name = r.Name!.Trim();
            p.IsActive = r.IsActive;
            p.SortOrder = ++pos * 10;
        }
        test.DefinitionVersion++;
        test.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabTestSaved, "LabTest", test.Code, $"CBC configuration saved: {string.Join(", ", test.Parameters.Where(p => p.IsActive).OrderBy(p => p.SortOrder).Select(p => p.Name))} (definition v{test.DefinitionVersion})");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> RestoreDefaultAsync(CancellationToken ct = default)
    {
        var test = await FindAsync(_db, ct);
        if (test is null) return ServiceResult<IReadOnlyList<string>>.Fail("The CBC test was not found in the laboratory catalogue.");
        var missing = Apply(_db, test);
        test.DefinitionVersion++;
        test.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabTestSaved, "LabTest", test.Code, $"CBC configuration restored to the default 14 parameters (definition v{test.DefinitionVersion})");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<IReadOnlyList<string>>.Ok(missing);
    }

    /// <summary>One-time update of existing installations (standard catalogue version 2).</summary>
    public static async Task ApplyDefaultOnceAsync(HmsDbContext db, CancellationToken ct)
    {
        var test = await FindAsync(db, ct);
        if (test is null) return;
        Apply(db, test);
        test.DefinitionVersion++;
    }

    /// <summary>Matches, renames, orders and (de)activates. Returns default names that had no parameter to match (except DLC, which is created).</summary>
    private static IReadOnlyList<string> Apply(HmsDbContext db, LabTest test)
    {
        static string Key(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var missing = new List<string>();
        var found = new LabTestParameter?[Default.Length];
        var taken = new HashSet<LabTestParameter>();
        // 1st pass: exact names / aliases; 2nd pass: a parameter renamed by the hospital that still starts with the default name ("MCV (fL)").
        for (var pass = 0; pass < 2; pass++)
            for (var i = 0; i < Default.Length; i++)
            {
                if (found[i] is not null) continue;
                var keys = Default[i].Aliases.Append(Default[i].Name).Select(Key).ToList();
                found[i] = test.Parameters.Where(x => !taken.Contains(x))
                    .FirstOrDefault(x => pass == 0 ? keys.Contains(Key(x.Name)) : keys.Any(k => Key(x.Name).StartsWith(k, StringComparison.Ordinal)));
                if (found[i] is { } f) taken.Add(f);
            }
        var placed = new List<LabTestParameter>();
        for (var i = 0; i < Default.Length; i++)
        {
            var (name, _, heading) = Default[i];
            var p = found[i];
            if (p is null && heading)
            {
                p = new LabTestParameter { Name = name, ResultType = LabResultType.Heading, IsOptional = true, Decimals = 0 };
                test.Parameters.Add(p);
            }
            if (p is null) { missing.Add(name); continue; }
            placed.Add(p);
            p.Name = name;
            p.IsActive = true;
            if (heading) { p.ResultType = LabResultType.Heading; p.IsOptional = true; }
        }
        var pos = 0;
        foreach (var p in placed) p.SortOrder = ++pos * 10;
        foreach (var p in test.Parameters.Where(x => !placed.Contains(x)).OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
        {
            p.IsActive = false;
            p.SortOrder = ++pos * 10;
        }
        return missing;
    }
}
