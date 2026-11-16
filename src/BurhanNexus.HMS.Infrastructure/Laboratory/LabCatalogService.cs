using System.Globalization;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Laboratory;

public sealed class LabTestInput
{
    /// <summary>Update 9: the laboratory has checked the standard-catalogue ranges and price.</summary>
    public bool MarkReviewed { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int CategoryId { get; set; }
    public int SampleTypeId { get; set; }
    public string? CollectionInstructions { get; set; }
    public string? Method { get; set; }
    public string? Interpretation { get; set; }
    public int? TurnaroundHours { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    // Price (Phase 3 service). Only applied when the user may manage laboratory fees.
    public bool IsFree { get; set; }
    public decimal Price { get; set; }
    public DateOnly? PriceEffectiveFrom { get; set; }
    public bool AllowPartialPayment { get; set; }
}

public sealed class LabParameterInput
{
    public string? Name { get; set; }
    public string? Unit { get; set; }
    public LabResultType ResultType { get; set; } = LabResultType.Numeric;
    public string? Options { get; set; }
    public int Decimals { get; set; } = 1;
    public bool IsOptional { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class LabRangeInput
{
    public Gender? Gender { get; set; }
    public int? MinAge { get; set; }
    public AgeUnit MinAgeUnit { get; set; } = AgeUnit.Years;
    public int? MaxAge { get; set; }
    public AgeUnit MaxAgeUnit { get; set; } = AgeUnit.Years;
    public decimal? Low { get; set; }
    public decimal? High { get; set; }
    public decimal? CriticalLow { get; set; }
    public decimal? CriticalHigh { get; set; }
    public string? NormalText { get; set; }
    /// <summary>Update 9: range used only for pregnant patients.</summary>
    public bool IsPregnancy { get; set; }
}

public sealed class LabSettingsInput
{
    public string? LaboratoryName { get; set; }
    public string? HospitalName { get; set; }
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? ReportHeader { get; set; }
    public string? ReportFooter { get; set; }
    public LabReportLayout Layout { get; set; } = LabReportLayout.Standard;
    public string? SignatoryName { get; set; }
    public string? SignatoryDesignation { get; set; }
    public bool ShowVerifiedBy { get; set; } = true;
    public bool AllowSelfVerification { get; set; }
    public bool RequirePaymentBeforeRelease { get; set; }
}

public sealed record LabTestRow(LabTest Test, decimal Price, bool AllowPartial, int ParameterCount);

public interface ILabCatalogService
{
    Task<IReadOnlyList<LabCategory>> CategoriesAsync(bool activeOnly, CancellationToken ct = default);
    Task<IReadOnlyList<LabSampleType>> SampleTypesAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveCategoryAsync(int? id, string? name, int sortOrder, bool isActive, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveSampleTypeAsync(int? id, string? code, string? name, string? container, bool isActive, CancellationToken ct = default);

    Task<IReadOnlyList<LabTestRow>> TestsAsync(bool activeOnly, CancellationToken ct = default);
    Task<LabTest?> TestAsync(int id, CancellationToken ct = default);
    Task<LabTestRow?> TestRowAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveTestAsync(int? id, LabTestInput input, bool canManageFees, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveParameterAsync(int testId, int? parameterId, LabParameterInput input, CancellationToken ct = default);
    Task<ServiceResult> AddRangeAsync(int parameterId, LabRangeInput input, CancellationToken ct = default);
    Task<ServiceResult> DeleteRangeAsync(int rangeId, CancellationToken ct = default);

    Task<LabSettings> SettingsAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveSettingsAsync(LabSettingsInput input, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> SetImageAsync(string kind, byte[]? data, string? contentType, CancellationToken ct = default);

    Task<IReadOnlyList<LabInventoryItem>> InventoryAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveInventoryItemAsync(int? id, LabInventoryItem input, CancellationToken ct = default);
}

public sealed class LabCatalogService : ILabCatalogService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _finance;

    public LabCatalogService(HmsDbContext db, IAuditService audit, HospitalClock clock, IFinanceSetupService finance)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _finance = finance;
    }

    private static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    // =====================================================================================
    // Categories & sample types
    // =====================================================================================

    public async Task<IReadOnlyList<LabCategory>> CategoriesAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.LabCategories.AsNoTracking().Where(c => !activeOnly || c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<LabSampleType>> SampleTypesAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.LabSampleTypes.AsNoTracking().Where(c => !activeOnly || c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveCategoryAsync(int? id, string? name, int sortOrder, bool isActive, CancellationToken ct = default)
    {
        name = T(name, 80);
        if (name is null) return ServiceResult<int>.Fail("Category name is required.");
        if (await _db.LabCategories.AnyAsync(c => c.Id != id && c.Name == name, ct)) return ServiceResult<int>.Fail("This category already exists.");
        var c = id is null ? _db.LabCategories.Add(new LabCategory()).Entity : await _db.LabCategories.FirstAsync(x => x.Id == id, ct);
        c.Name = name; c.SortOrder = sortOrder; c.IsActive = isActive;
        _audit.Add(AuditActions.LabCatalogueSaved, "LabCategory", name, $"Laboratory category {name} saved");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(c.Id);
    }

    public async Task<ServiceResult<int>> SaveSampleTypeAsync(int? id, string? code, string? name, string? container, bool isActive, CancellationToken ct = default)
    {
        code = T(code, 20)?.ToUpperInvariant();
        name = T(name, 60);
        if (code is null || name is null) return ServiceResult<int>.Fail("Sample code and name are required.");
        if (await _db.LabSampleTypes.AnyAsync(c => c.Id != id && (c.Code == code || c.Name == name), ct)) return ServiceResult<int>.Fail("This sample type already exists.");
        var s = id is null ? _db.LabSampleTypes.Add(new LabSampleType()).Entity : await _db.LabSampleTypes.FirstAsync(x => x.Id == id, ct);
        s.Code = code; s.Name = name; s.Container = T(container, 100); s.IsActive = isActive;
        _audit.Add(AuditActions.LabCatalogueSaved, "LabSampleType", code, $"Sample type {name} saved");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(s.Id);
    }

    // =====================================================================================
    // Tests
    // =====================================================================================

    public async Task<IReadOnlyList<LabTestRow>> TestsAsync(bool activeOnly, CancellationToken ct = default)
    {
        var tests = await _db.LabTests.AsNoTracking().Include(t => t.Category).Include(t => t.SampleType).Include(t => t.ServiceItem)
            .Where(t => !activeOnly || t.IsActive)
            .OrderBy(t => t.Category!.SortOrder).ThenBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct);
        var counts = await _db.LabTestParameters.Where(p => p.IsActive).GroupBy(p => p.LabTestId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var today = await _clock.TodayAsync();
        var list = new List<LabTestRow>();
        foreach (var t in tests)
            list.Add(new LabTestRow(t, await _finance.FeeOnAsync(t.ServiceItemId, today, ct), t.ServiceItem!.AllowPartialPayment, counts.GetValueOrDefault(t.Id)));
        return list;
    }

    public async Task<LabTest?> TestAsync(int id, CancellationToken ct = default) =>
        await _db.LabTests.AsNoTracking().Include(t => t.Category).Include(t => t.SampleType).Include(t => t.ServiceItem)
            .Include(t => t.Parameters.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)).ThenInclude(p => p.Ranges)
            .AsSplitQuery().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<LabTestRow?> TestRowAsync(int id, CancellationToken ct = default)
    {
        var t = await TestAsync(id, ct);
        return t is null ? null : new LabTestRow(t, await _finance.FeeOnAsync(t.ServiceItemId, await _clock.TodayAsync(), ct), t.ServiceItem!.AllowPartialPayment, t.Parameters.Count(p => p.IsActive));
    }

    public async Task<ServiceResult<int>> SaveTestAsync(int? id, LabTestInput i, bool canManageFees, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code, 30)?.ToUpperInvariant();
        var name = T(i.Name, 150);
        if (code is null) errors.Add("Test code is required.");
        if (name is null) errors.Add("Test name is required.");
        if (!await _db.LabCategories.AnyAsync(c => c.Id == i.CategoryId, ct)) errors.Add("Select the category.");
        if (!await _db.LabSampleTypes.AnyAsync(c => c.Id == i.SampleTypeId, ct)) errors.Add("Select the sample type.");
        if (code is not null && await _db.LabTests.AnyAsync(t => t.Id != id && t.Code == code, ct)) errors.Add("Another test already uses this code.");
        if (i.TurnaroundHours is < 0 or > 2000) errors.Add("Turnaround time is not valid.");
        if (id is null && !canManageFees) { i.IsFree = true; i.Price = 0; } // price is set later by a user with "Manage Laboratory Fees"
        if (canManageFees && !i.IsFree && i.Price <= 0) errors.Add("Enter the test price, or mark the test as free.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());

        var now = _clock.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        LabTest t;
        object? old = null;
        if (id is null)
        {
            var dept = await _db.Departments.Where(d => d.Name == "Laboratory").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
            var svc = await _finance.SaveServiceAsync(null, new ServiceInput
            {
                Code = "LT-" + code, Name = name, Kind = ServiceKind.Laboratory, DepartmentId = dept, IsFree = i.IsFree, Fee = i.IsFree ? 0 : i.Price,
                AllowPartialPayment = i.AllowPartialPayment, IsActive = i.IsActive, Description = "Laboratory test"
            }, ct);
            if (!svc.Succeeded) return ServiceResult<int>.Fail(svc.Errors.ToArray());
            t = new LabTest { CreatedUtc = now, ServiceItemId = svc.Value };
            _db.LabTests.Add(t);
        }
        else
        {
            t = await _db.LabTests.Include(x => x.ServiceItem).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
            old = new { t.Code, t.Name, t.CategoryId, t.SampleTypeId, t.Method, t.IsActive, t.DefinitionVersion };
            var s = t.ServiceItem!;
            var currentFee = await _finance.FeeOnAsync(s.Id, await _clock.TodayAsync(), ct);
            var svc = await _finance.SaveServiceAsync(s.Id, new ServiceInput
            {
                Code = s.Code, Name = name, Kind = ServiceKind.Laboratory, DepartmentId = s.DepartmentId,
                IsFree = canManageFees ? i.IsFree : currentFee == 0, Fee = canManageFees ? (i.IsFree ? 0 : i.Price) : currentFee,
                FeeEffectiveFrom = canManageFees ? i.PriceEffectiveFrom : null, FeeNote = "Laboratory test price",
                AllowPartialPayment = canManageFees ? i.AllowPartialPayment : s.AllowPartialPayment, IsActive = i.IsActive,
                Description = s.Description, CounterIds = await _db.ServiceCounters.Where(c => c.ServiceItemId == s.Id).Select(c => c.CounterId).ToListAsync(ct)
            }, ct);
            if (!svc.Succeeded) return ServiceResult<int>.Fail(svc.Errors.ToArray());
            if (t.Code != code || t.Name != name || t.CategoryId != i.CategoryId || t.SampleTypeId != i.SampleTypeId || t.Method != T(i.Method, 100))
                t.DefinitionVersion++;
        }
        t.Code = code!; t.Name = name!; t.CategoryId = i.CategoryId; t.SampleTypeId = i.SampleTypeId;
        t.CollectionInstructions = T(i.CollectionInstructions, 500); t.Method = T(i.Method, 100); t.Interpretation = T(i.Interpretation, 1000);
        t.TurnaroundHours = i.TurnaroundHours; t.IsActive = i.IsActive; t.SortOrder = i.SortOrder; t.UpdatedUtc = now;
        // Update 9: saving a standard-catalogue test after checking it clears the "review required" mark.
        if (id is not null && t.RequiresReview && i.MarkReviewed) t.RequiresReview = false;
        await _db.SaveChangesAsync(ct);
        _audit.Add(AuditActions.LabTestSaved, "LabTest", t.Code, $"Laboratory test {t.Name} {(id is null ? "created" : "updated")} (definition v{t.DefinitionVersion})",
            old, new { t.Code, t.Name, t.CategoryId, t.SampleTypeId, t.Method, t.IsActive, t.DefinitionVersion });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(t.Id);
    }

    public async Task<ServiceResult<int>> SaveParameterAsync(int testId, int? parameterId, LabParameterInput i, CancellationToken ct = default)
    {
        var name = T(i.Name, 100);
        if (name is null) return ServiceResult<int>.Fail("Parameter name is required.");
        if (!Enum.IsDefined(i.ResultType)) return ServiceResult<int>.Fail("Select the result type.");
        var options = T(i.Options, 500);
        if (i.ResultType == LabResultType.Options && (options is null || options.Split('|', StringSplitOptions.RemoveEmptyEntries).Length < 2))
            return ServiceResult<int>.Fail("Enter at least two choices separated by '|' (e.g. Reactive|Non-reactive).");
        if (i.Decimals is < 0 or > 4) return ServiceResult<int>.Fail("Decimals must be between 0 and 4.");
        var test = await _db.LabTests.FirstOrDefaultAsync(t => t.Id == testId, ct);
        if (test is null) return ServiceResult<int>.Fail("Test not found.");
        LabTestParameter p;
        if (parameterId is null)
        {
            p = new LabTestParameter { LabTestId = testId };
            _db.LabTestParameters.Add(p);
        }
        else p = await _db.LabTestParameters.FirstOrDefaultAsync(x => x.Id == parameterId && x.LabTestId == testId, ct) ?? throw new KeyNotFoundException();
        p.Name = name; p.Unit = T(i.Unit, 30); p.ResultType = i.ResultType; p.Options = i.ResultType == LabResultType.Options ? options : null;
        p.Decimals = i.Decimals; p.IsOptional = i.IsOptional; p.IsActive = i.IsActive; p.SortOrder = i.SortOrder; p.DefaultValue = T(i.DefaultValue, 60);
        test.DefinitionVersion++;
        test.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabTestSaved, "LabTest", test.Code, $"Parameter {name} of {test.Name} {(parameterId is null ? "added" : "updated")} (definition v{test.DefinitionVersion})");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(p.Id);
    }

    private static int? ToDays(int? value, AgeUnit unit) => value is null ? null : unit switch
    {
        AgeUnit.Years => value * 365 + value / 4,
        AgeUnit.Months => (int)Math.Round(value.Value * 30.44),
        _ => value
    };

    public async Task<ServiceResult> AddRangeAsync(int parameterId, LabRangeInput i, CancellationToken ct = default)
    {
        var p = await _db.LabTestParameters.Include(x => x.LabTest).FirstOrDefaultAsync(x => x.Id == parameterId, ct);
        if (p is null) return ServiceResult.Fail("Parameter not found.");
        var text = T(i.NormalText, 100);
        if (i.Low is null && i.High is null && text is null) return ServiceResult.Fail("Enter a low and/or high limit, or the normal text.");
        if (i.Low is { } l && i.High is { } h && l > h) return ServiceResult.Fail("The low limit must not be above the high limit.");
        var min = ToDays(i.MinAge, i.MinAgeUnit);
        var max = ToDays(i.MaxAge, i.MaxAgeUnit);
        if (min is < 0 || max is < 0 || (min is { } a && max is { } b && a >= b)) return ServiceResult.Fail("Age range is not valid (from must be below to).");
        if (i.Gender is Gender.Other or Gender.Unknown) return ServiceResult.Fail("Gender-specific ranges are for male or female; leave empty for both.");
        _db.LabReferenceRanges.Add(new LabReferenceRange
        {
            ParameterId = parameterId, Gender = i.Gender, MinAgeDays = min, MaxAgeDays = max, Low = i.Low, High = i.High,
            CriticalLow = i.CriticalLow, CriticalHigh = i.CriticalHigh, NormalText = text, IsPregnancy = i.IsPregnancy && i.Gender != Gender.Male
        });
        p.LabTest!.DefinitionVersion++;
        p.LabTest.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabTestSaved, "LabTest", p.LabTest.Code,
            $"Reference range added to {p.LabTest.Name} / {p.Name}: {i.Gender?.ToString() ?? "all"}{(i.IsPregnancy ? " (pregnancy)" : "")}, {i.Low}–{i.High} {text} (definition v{p.LabTest.DefinitionVersion})");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteRangeAsync(int rangeId, CancellationToken ct = default)
    {
        var r = await _db.LabReferenceRanges.Include(x => x.Parameter).ThenInclude(p => p!.LabTest).FirstOrDefaultAsync(x => x.Id == rangeId, ct);
        if (r is null) return ServiceResult.Fail("Range not found.");
        var test = r.Parameter!.LabTest!;
        test.DefinitionVersion++;
        _audit.Add(AuditActions.LabTestSaved, "LabTest", test.Code,
            $"Reference range removed from {test.Name} / {r.Parameter.Name} ({r.Gender?.ToString() ?? "all"}, {r.Low}–{r.High} {r.NormalText}); booked orders keep their own copy");
        _db.LabReferenceRanges.Remove(r);
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Settings & inventory foundation
    // =====================================================================================

    public async Task<LabSettings> SettingsAsync(CancellationToken ct = default) =>
        await _db.LabSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new LabSettings();

    public async Task<ServiceResult> SaveSettingsAsync(LabSettingsInput i, uint rowVersion, CancellationToken ct = default)
    {
        if (T(i.Email, 150) is { } mail && !mail.Contains('@')) return ServiceResult.Fail("Email address is not valid.");
        var s = await _db.LabSettings.FirstAsync(ct);
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var old = new { s.LaboratoryName, s.HospitalName, s.SignatoryName, s.AllowSelfVerification, s.RequirePaymentBeforeRelease, s.Layout };
        s.LaboratoryName = T(i.LaboratoryName, 150); s.HospitalName = T(i.HospitalName, 200); s.Address = T(i.Address, 300);
        s.ContactNumber = T(i.ContactNumber, 100); s.Email = T(i.Email, 150); s.ReportHeader = T(i.ReportHeader, 300); s.ReportFooter = T(i.ReportFooter, 500);
        s.Layout = Enum.IsDefined(i.Layout) ? i.Layout : LabReportLayout.Standard; s.SignatoryName = T(i.SignatoryName, 150); s.SignatoryDesignation = T(i.SignatoryDesignation, 150);
        s.ShowVerifiedBy = i.ShowVerifiedBy; s.AllowSelfVerification = i.AllowSelfVerification; s.RequirePaymentBeforeRelease = i.RequirePaymentBeforeRelease;
        s.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabSettingsChanged, "LabSettings", "1", "Laboratory settings changed", old,
            new { s.LaboratoryName, s.HospitalName, s.SignatoryName, s.AllowSelfVerification, s.RequirePaymentBeforeRelease, s.Layout });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("Settings were changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetImageAsync(string kind, byte[]? data, string? contentType, CancellationToken ct = default)
    {
        if (data is { Length: > 1_000_000 }) return ServiceResult.Fail("Image is too large (max 1 MB).");
        if (data is not null && contentType is not ("image/png" or "image/jpeg")) return ServiceResult.Fail("Use a PNG or JPEG image.");
        if (data is not null && !LooksLikeImage(data)) return ServiceResult.Fail("The file is not a valid PNG or JPEG image.");
        var s = await _db.LabSettings.FirstAsync(ct);
        if (kind == "signature") { s.Signature = data; s.SignatureContentType = data is null ? null : contentType; }
        else { s.Logo = data; s.LogoContentType = data is null ? null : contentType; }
        s.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabSettingsChanged, "LabSettings", "1", $"Laboratory {kind} {(data is null ? "removed" : "uploaded")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private static bool LooksLikeImage(byte[] d) =>
        (d.Length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) || (d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF);

    public async Task<IReadOnlyList<LabInventoryItem>> InventoryAsync(CancellationToken ct = default) =>
        await _db.LabInventoryItems.AsNoTracking().OrderBy(i => i.Kind).ThenBy(i => i.Name).ToListAsync(ct);

    public async Task<ServiceResult> SaveInventoryItemAsync(int? id, LabInventoryItem i, CancellationToken ct = default)
    {
        var code = T(i.Code, 30)?.ToUpperInvariant();
        var name = T(i.Name, 150);
        if (code is null || name is null) return ServiceResult.Fail("Code and name are required.");
        if (await _db.LabInventoryItems.AnyAsync(x => x.Id != id && x.Code == code, ct)) return ServiceResult.Fail("This code already exists.");
        var e = id is null ? _db.LabInventoryItems.Add(new LabInventoryItem { CreatedUtc = _clock.UtcNow }).Entity : await _db.LabInventoryItems.FirstAsync(x => x.Id == id, ct);
        e.Code = code; e.Name = name; e.Kind = Enum.IsDefined(i.Kind) ? i.Kind : LabInventoryKind.Other; e.Unit = T(i.Unit, 30); e.Supplier = T(i.Supplier, 150);
        e.ReorderLevel = i.ReorderLevel; e.TracksExpiry = i.TracksExpiry; e.IsActive = i.IsActive;
        _audit.Add(AuditActions.LabCatalogueSaved, "LabInventoryItem", code, $"Laboratory inventory item {name} saved");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Seed: sample types, categories and a starter catalogue (inactive, free) the laboratory reviews and prices.
    // =====================================================================================

    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        if (!await db.LabSettings.AnyAsync(ct)) db.LabSettings.Add(new LabSettings { Id = 1, UpdatedUtc = now });
        if (!await db.LabSampleTypes.AnyAsync(ct))
        {
            var i = 0;
            foreach (var (code, name, container) in new[]
                     {
                         ("BLD", "Blood (EDTA)", "Purple top (EDTA)"), ("SER", "Serum", "Red / yellow top"), ("PLS", "Plasma (fluoride)", "Grey top"),
                         ("URN", "Urine", "Sterile urine container"), ("STL", "Stool", "Stool container"), ("OTH", "Other", null),
                     })
                db.LabSampleTypes.Add(new LabSampleType { Code = code, Name = name, Container = container, SortOrder = ++i * 10 });
        }
        if (!await db.LabCategories.AnyAsync(ct))
        {
            var i = 0;
            foreach (var n in new[] { "Haematology", "Biochemistry", "Endocrinology", "Clinical Pathology", "Serology", "Microbiology" })
                db.LabCategories.Add(new LabCategory { Name = n, SortOrder = ++i * 10 });
        }
        await db.SaveChangesAsync(ct);
        // Laboratory services created before the setup wizard made the departments: link them to "Laboratory".
        var labDept = await db.Departments.Where(d => d.Name == "Laboratory").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (labDept is int ld)
        {
            var unlinked = await db.ServiceItems.Where(s => s.Kind == ServiceKind.Laboratory && s.DepartmentId == null && db.LabTests.Any(t => t.ServiceItemId == s.Id)).ToListAsync(ct);
            foreach (var u in unlinked) u.DepartmentId = ld;
            await db.SaveChangesAsync(ct);
        }
        await SeedStandardCatalogueAsync(db, now, ct);
    }

    /// <summary>
    /// Update 9: adds the standard catalogue once (version 1) – missing disciplines, sample types and tests. Tests are ACTIVE with price 0
    /// (the hospital sets the prices in Services &amp; Fees) and marked "review required" for the method-dependent ranges. Existing tests,
    /// matched by code, by their earlier starter code or by name, are never changed.
    /// </summary>
    public static async Task SeedStandardCatalogueAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        var settings = await db.LabSettings.FirstOrDefaultAsync(ct);
        if (settings is null || settings.StandardCatalogueVersion >= 1) return;

        var categories = await db.LabCategories.ToListAsync(ct);
        var sort = categories.Count == 0 ? 0 : categories.Max(c => c.SortOrder);
        var catId = new Dictionary<string, LabCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, synonyms) in LabStandardCatalogue.Categories)
        {
            var existing = categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) || synonyms.Any(s => string.Equals(c.Name, s, StringComparison.OrdinalIgnoreCase)));
            if (existing is null) { existing = new LabCategory { Name = name, SortOrder = sort += 10 }; db.LabCategories.Add(existing); }
            catId[name] = existing;
        }
        var samples = await db.LabSampleTypes.ToListAsync(ct);
        var ssort = samples.Count == 0 ? 0 : samples.Max(s => s.SortOrder);
        var smp = new Dictionary<string, LabSampleType>();
        foreach (var (code, name, container) in LabStandardCatalogue.SampleTypes)
        {
            var s = samples.FirstOrDefault(x => x.Code == code);
            if (s is null) { s = new LabSampleType { Code = code, Name = name, Container = container, SortOrder = ssort += 10 }; db.LabSampleTypes.Add(s); }
            smp[code] = s;
        }
        var codes = (await db.LabTests.Select(t => t.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = (await db.LabTests.Select(t => t.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var serviceCodes = (await db.ServiceItems.Select(s => s.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dept = await db.Departments.Where(d => d.Name == "Laboratory").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        var tsort = await db.LabTests.MaxAsync(t => (int?)t.SortOrder, ct) ?? 0;
        foreach (var t in LabStandardCatalogue.Tests())
        {
            if (codes.Contains(t.Code) || names.Contains(t.Name) || EarlierStarter.TryGetValue(t.Code, out var old) && codes.Contains(old)) continue;
            var svcCode = "LT-" + t.Code;
            if (serviceCodes.Contains(svcCode)) continue;
            var svc = new ServiceItem
            {
                Code = svcCode, Name = t.Name, Category = EncounterType.ServiceOnly, Kind = ServiceKind.Laboratory, DepartmentId = dept,
                DefaultFee = 0, IsActive = true, CreatedUtc = now, UpdatedUtc = now, Description = "Laboratory test (standard catalogue) – set the price"
            };
            db.ServiceItems.Add(svc);
            db.ServiceFees.Add(new ServiceFee { ServiceItem = svc, Fee = 0, EffectiveFrom = DateOnly.MinValue, CreatedUtc = now, CreatedByUsername = "SYSTEM", Note = "Standard catalogue – set the price" });
            var test = new LabTest
            {
                Code = t.Code, Name = t.Name, Category = catId[t.Category], SampleType = smp[t.Sample], ServiceItem = svc, IsActive = true, RequiresReview = true,
                SortOrder = tsort += 10, CreatedUtc = now, UpdatedUtc = now, CollectionInstructions = t.Instructions, Interpretation = t.Interpretation, Method = t.Method,
                TurnaroundHours = t.Hours
            };
            var ps = 0;
            foreach (var p in t.Parameters)
            {
                var param = new LabTestParameter { Name = p.Name, Unit = p.Unit, ResultType = p.Type, Options = p.Options, Decimals = p.Decimals, IsOptional = p.Optional, SortOrder = ++ps * 10 };
                foreach (var r in p.Ranges) param.Ranges.Add(new LabReferenceRange
                {
                    Gender = r.Gender, MinAgeDays = r.MinAgeDays, MaxAgeDays = r.MaxAgeDays, Low = r.Low, High = r.High, NormalText = r.NormalText, IsPregnancy = r.IsPregnancy
                });
                test.Parameters.Add(param);
            }
            db.LabTests.Add(test);
            codes.Add(t.Code);
            names.Add(t.Name);
        }
        settings.StandardCatalogueVersion = 1;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Codes of the earlier (Phase 4) starter tests that the standard catalogue would duplicate.</summary>
    private static readonly Dictionary<string, string> EarlierStarter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CBC-FULL"] = "CBC", ["RBS"] = "BSR", ["FBS"] = "BSF", ["HBA1C-H"] = "HBA1C", ["LFT-FULL"] = "LFT", ["RFT-FULL"] = "RFT", ["LIPID-FULL"] = "LIPID",
        ["UCE"] = "URE", ["SCE"] = "SRE", ["TFT-FULL"] = "TFT",
    };


}
