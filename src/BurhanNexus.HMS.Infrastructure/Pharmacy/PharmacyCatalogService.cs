using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Pharmacy;

public sealed class MedicineInput
{
    public string? Name { get; set; }
    public string? GenericName { get; set; }
    public string? BrandName { get; set; }
    public int? ManufacturerId { get; set; }
    public int? CategoryId { get; set; }
    public int? DosageFormId { get; set; }
    public string? Strength { get; set; }
    public string? PackSize { get; set; }
    public string? Unit { get; set; }
    public int UnitsPerPack { get; set; } = 1;
    public int MinimumStock { get; set; }
    public string? StorageInstructions { get; set; }
    public bool IsActive { get; set; } = true;
    // Private hospital only – ignored in Government mode.
    public decimal? PurchasePrice { get; set; }
    public decimal? SalePrice { get; set; }
    public bool AllowDiscount { get; set; } = true;
    public decimal MaxDiscountPercent { get; set; } = 10;
    /// <summary>The user saw the "similar medicine exists" warning and wants to save anyway.</summary>
    public bool ConfirmDuplicate { get; set; }
}

public sealed class SupplierInput
{
    public string? Name { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class StockLocationInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
}

public sealed class PharmacySettingsInput
{
    public string? PharmacyName { get; set; }
    public int NearExpiryDays { get; set; } = 90;
    public int EncounterValidDays { get; set; } = 7;
    public int? GovernmentIssueServiceItemId { get; set; }
    public string? SlipFooter { get; set; }
}

/// <summary>Result of the duplicate check: medicines that look like the one being saved.</summary>
public sealed record SimilarMedicine(int Id, string Code, string DisplayName, string? GenericName, string? BrandName, bool IsActive, string Why);

public sealed class DuplicateMedicineException : Exception
{
    public IReadOnlyList<SimilarMedicine> Similar { get; }
    public DuplicateMedicineException(IReadOnlyList<SimilarMedicine> similar) : base("Similar medicine exists") => Similar = similar;
}

public interface IPharmacyCatalogService
{
    /// <summary>True in Private hospital mode. Every commercial operation checks this on the server.</summary>
    Task<bool> IsPrivateAsync(CancellationToken ct = default);
    Task<PharmacySettings> SettingsAsync(CancellationToken ct = default);
    Task<ServiceResult> SaveSettingsAsync(PharmacySettingsInput input, uint rowVersion, CancellationToken ct = default);

    Task<IReadOnlyList<PharmacyLookup>> LookupsAsync(PharmacyLookupKind? kind = null, bool activeOnly = true, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveLookupAsync(int? id, PharmacyLookupKind kind, string? name, bool isActive, int sortOrder, CancellationToken ct = default);

    Task<IReadOnlyList<Medicine>> MedicinesAsync(string? q, bool activeOnly, int? categoryId = null, int take = 500, CancellationToken ct = default);
    Task<Medicine?> MedicineAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<SimilarMedicine>> SimilarAsync(MedicineInput input, int? excludeId, CancellationToken ct = default);
    /// <summary>Fails with the list of similar medicines unless <see cref="MedicineInput.ConfirmDuplicate"/> is set.</summary>
    Task<ServiceResult<int>> SaveMedicineAsync(int? id, MedicineInput input, bool canManagePrices, CancellationToken ct = default);
    Task<IReadOnlyList<MedicinePriceChange>> PriceHistoryAsync(int medicineId, CancellationToken ct = default);
    Task<ServiceResult> SetBatchSalePriceAsync(long batchId, decimal? salePrice, CancellationToken ct = default);

    Task<IReadOnlyList<Supplier>> SuppliersAsync(string? q, bool activeOnly, CancellationToken ct = default);
    Task<Supplier?> SupplierAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveSupplierAsync(int? id, SupplierInput input, CancellationToken ct = default);

    Task<IReadOnlyList<StockLocation>> LocationsAsync(bool activeOnly, CancellationToken ct = default);
    Task<StockLocation> MainLocationAsync(CancellationToken ct = default);
    Task<ServiceResult<int>> SaveLocationAsync(int? id, StockLocationInput input, CancellationToken ct = default);
}

public sealed class PharmacyCatalogService : IPharmacyCatalogService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    public PharmacyCatalogService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    internal static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    public async Task<bool> IsPrivateAsync(CancellationToken ct = default) =>
        await _db.HospitalConfigurations.AsNoTracking().Select(c => c.HospitalType).FirstOrDefaultAsync(ct) == HospitalType.Private;

    // =====================================================================================
    // Settings
    // =====================================================================================

    public async Task<PharmacySettings> SettingsAsync(CancellationToken ct = default) =>
        await _db.PharmacySettings.AsNoTracking().Include(s => s.GovernmentIssueServiceItem).FirstOrDefaultAsync(ct)
        ?? new PharmacySettings();

    public async Task<ServiceResult> SaveSettingsAsync(PharmacySettingsInput input, uint rowVersion, CancellationToken ct = default)
    {
        if (input.NearExpiryDays is < 1 or > 730) return ServiceResult.Fail("The near-expiry warning period must be between 1 and 730 days.");
        if (input.EncounterValidDays is < 0 or > 365) return ServiceResult.Fail("The visit validity must be between 0 and 365 days.");
        if (input.GovernmentIssueServiceItemId is int sid && !await _db.ServiceItems.AnyAsync(s => s.Id == sid && s.IsActive && s.Kind != ServiceKind.Pharmacy, ct))
            return ServiceResult.Fail("Select an active service for the pharmacy issue fee.");
        var s = await _db.PharmacySettings.FirstAsync(ct);
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var old = new { s.PharmacyName, s.NearExpiryDays, s.EncounterValidDays, s.GovernmentIssueServiceItemId, s.SlipFooter };
        s.PharmacyName = T(input.PharmacyName, 150);
        s.NearExpiryDays = input.NearExpiryDays;
        s.EncounterValidDays = input.EncounterValidDays;
        s.GovernmentIssueServiceItemId = input.GovernmentIssueServiceItemId;
        s.SlipFooter = T(input.SlipFooter, 200);
        s.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.PharmSettingsChanged, "PharmacySettings", "1", "Pharmacy settings changed", old,
            new { s.PharmacyName, s.NearExpiryDays, s.EncounterValidDays, s.GovernmentIssueServiceItemId, s.SlipFooter });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("Someone else changed the settings. Reload and try again."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Lookups (categories, manufacturers, dosage forms, strengths, pack sizes, units)
    // =====================================================================================

    public async Task<IReadOnlyList<PharmacyLookup>> LookupsAsync(PharmacyLookupKind? kind = null, bool activeOnly = true, CancellationToken ct = default) =>
        await _db.PharmacyLookups.AsNoTracking()
            .Where(l => (kind == null || l.Kind == kind) && (!activeOnly || l.IsActive))
            .OrderBy(l => l.Kind).ThenBy(l => l.SortOrder).ThenBy(l => l.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveLookupAsync(int? id, PharmacyLookupKind kind, string? name, bool isActive, int sortOrder, CancellationToken ct = default)
    {
        name = T(name, 100);
        if (name is null) return ServiceResult<int>.Fail("Enter the name.");
        if (!Enum.IsDefined(kind)) return ServiceResult<int>.Fail("Unknown list.");
        if (await _db.PharmacyLookups.AnyAsync(l => l.Kind == kind && l.Name.ToLower() == name.ToLower() && l.Id != id, ct))
            return ServiceResult<int>.Fail($"“{name}” already exists.");
        PharmacyLookup l;
        if (id is int lid)
        {
            l = await _db.PharmacyLookups.FirstOrDefaultAsync(x => x.Id == lid && x.Kind == kind, ct) ?? throw new KeyNotFoundException();
        }
        else
        {
            l = new PharmacyLookup { Kind = kind, CreatedUtc = _clock.UtcNow };
            _db.PharmacyLookups.Add(l);
        }
        l.Name = name;
        l.IsActive = isActive;
        l.SortOrder = sortOrder;
        _audit.Add(AuditActions.PharmLookupSaved, "PharmacyLookup", name, $"{kind} “{name}” saved{(isActive ? "" : " (inactive)")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(l.Id);
    }

    // =====================================================================================
    // Medicines
    // =====================================================================================

    private IQueryable<Medicine> MedicineQuery() =>
        _db.Medicines.AsNoTracking().Include(m => m.Category).Include(m => m.Manufacturer).Include(m => m.DosageForm);

    public async Task<IReadOnlyList<Medicine>> MedicinesAsync(string? q, bool activeOnly, int? categoryId = null, int take = 500, CancellationToken ct = default)
    {
        var query = MedicineQuery().Where(m => !activeOnly || m.IsActive);
        if (categoryId is int cid) query = query.Where(m => m.CategoryId == cid);
        if (T(q, 100) is { } term)
        {
            var like = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(m => EF.Functions.ILike(m.Name, like) || EF.Functions.ILike(m.GenericName ?? "", like)
                                     || EF.Functions.ILike(m.BrandName ?? "", like) || EF.Functions.ILike(m.Code, like));
        }
        return await query.OrderBy(m => m.Name).ThenBy(m => m.Strength).Take(Math.Clamp(take, 1, 2000)).ToListAsync(ct);
    }

    public async Task<Medicine?> MedicineAsync(int id, CancellationToken ct = default) => await MedicineQuery().FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <summary>Lower-case letters and digits only: "Panadol 500 mg" ≈ "panadol500mg".</summary>
    internal static string Norm(string? s) => s is null ? "" : Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]", "");

    public async Task<IReadOnlyList<SimilarMedicine>> SimilarAsync(MedicineInput input, int? excludeId, CancellationToken ct = default)
    {
        var name = Norm(input.Name);
        var generic = Norm(input.GenericName);
        var brand = Norm(input.BrandName);
        var strength = Norm(input.Strength);
        if (name.Length == 0 && generic.Length == 0 && brand.Length == 0) return Array.Empty<SimilarMedicine>();
        var all = await MedicineQuery().Where(m => m.Id != excludeId).ToListAsync(ct);
        var list = new List<SimilarMedicine>();
        foreach (var m in all)
        {
            var sameForm = input.DosageFormId is null || m.DosageFormId is null || m.DosageFormId == input.DosageFormId;
            var sameStrength = strength.Length == 0 || Norm(m.Strength).Length == 0 || Norm(m.Strength) == strength;
            string? why = null;
            if (name.Length > 0 && Norm(m.Name) == name && sameStrength && sameForm) why = "same name, strength and form";
            else if (generic.Length > 0 && Norm(m.GenericName) == generic && sameStrength && sameForm) why = "same generic name, strength and form";
            else if (brand.Length > 0 && Norm(m.BrandName) == brand && sameStrength) why = "same brand and strength";
            else if (name.Length >= 4 && Norm(m.Name).Length >= 4 && (Norm(m.Name).StartsWith(name) || name.StartsWith(Norm(m.Name))) && sameStrength && sameForm)
                why = "similar name";
            if (why is not null) list.Add(new SimilarMedicine(m.Id, m.Code, m.DisplayName, m.GenericName, m.BrandName, m.IsActive, why));
        }
        return list.Take(10).ToList();
    }

    public async Task<ServiceResult<int>> SaveMedicineAsync(int? id, MedicineInput i, bool canManagePrices, CancellationToken ct = default)
    {
        var isPrivate = await IsPrivateAsync(ct);
        var errors = new List<string>();
        var name = T(i.Name, 150);
        if (name is null) errors.Add("Enter the medicine name.");
        var unit = T(i.Unit, 30) ?? "Unit";
        if (i.UnitsPerPack is < 1 or > 10000) errors.Add("Units per pack must be between 1 and 10,000.");
        if (i.MinimumStock is < 0 or > 10_000_000) errors.Add("Minimum stock must be 0 or more.");
        foreach (var (lid, kind, label) in new[] { (i.CategoryId, PharmacyLookupKind.Category, "category"), (i.ManufacturerId, PharmacyLookupKind.Manufacturer, "manufacturer"), (i.DosageFormId, PharmacyLookupKind.DosageForm, "dosage form") })
            if (lid is int l && !await _db.PharmacyLookups.AnyAsync(x => x.Id == l && x.Kind == kind, ct)) errors.Add($"Select a valid {label}.");
        if (isPrivate)
        {
            if (i.PurchasePrice is < 0 || i.SalePrice is < 0) errors.Add("Prices cannot be negative.");
            if (i.SalePrice is decimal sp && !FinanceMath.IsMoney(sp)) errors.Add("Sale price may have at most 2 decimals.");
            if (i.PurchasePrice is decimal pp && decimal.Round(pp, 4) != pp) errors.Add("Purchase price may have at most 4 decimals.");
            if (i.MaxDiscountPercent is < 0 or > 100) errors.Add("Maximum discount must be between 0 and 100 %.");
        }
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());

        if (!i.ConfirmDuplicate)
        {
            var similar = await SimilarAsync(i, id, ct);
            if (similar.Count > 0) throw new DuplicateMedicineException(similar);
        }

        var now = _clock.UtcNow;
        Medicine m;
        if (id is int mid)
        {
            m = await _db.Medicines.FirstOrDefaultAsync(x => x.Id == mid, ct) ?? throw new KeyNotFoundException();
        }
        else
        {
            m = new Medicine
            {
                Code = await NextCodeAsync(HmsDbContext.MedicineCodeSequence, "MED", ct),
                CreatedUtc = now, CreatedByUsername = _ctx.Username
            };
            _db.Medicines.Add(m);
        }
        var oldPrices = (m.PurchasePrice, m.SalePrice);
        m.Name = name!;
        m.GenericName = T(i.GenericName, 150);
        m.BrandName = T(i.BrandName, 150);
        m.ManufacturerId = i.ManufacturerId;
        m.CategoryId = i.CategoryId;
        m.DosageFormId = i.DosageFormId;
        m.Strength = T(i.Strength, 50);
        m.PackSize = T(i.PackSize, 50);
        m.Unit = unit;
        m.UnitsPerPack = i.UnitsPerPack;
        m.MinimumStock = i.MinimumStock;
        m.StorageInstructions = T(i.StorageInstructions, 300);
        m.IsActive = i.IsActive;
        m.UpdatedUtc = now;
        // Prices exist only in Private mode and only users with "Manage Medicine Prices" may change them.
        var priceChanged = false;
        if (isPrivate && canManagePrices)
        {
            m.AllowDiscount = i.AllowDiscount;
            m.MaxDiscountPercent = i.MaxDiscountPercent;
            if (m.PurchasePrice != i.PurchasePrice || m.SalePrice != i.SalePrice)
            {
                priceChanged = id is not null || i.PurchasePrice is not null || i.SalePrice is not null;
                m.PurchasePrice = i.PurchasePrice;
                m.SalePrice = i.SalePrice;
            }
        }
        await _db.SaveChangesAsync(ct);
        if (priceChanged)
        {
            _db.MedicinePriceChanges.Add(new MedicinePriceChange
            {
                MedicineId = m.Id, OldPurchasePrice = oldPrices.PurchasePrice, NewPurchasePrice = m.PurchasePrice, OldSalePrice = oldPrices.SalePrice,
                NewSalePrice = m.SalePrice, ChangedUtc = now, ChangedByUserId = _ctx.UserId, ChangedByUsername = _ctx.Username,
                Note = id is null ? "Initial price" : null
            });
            _audit.Add(AuditActions.PharmMedicinePriceChanged, "Medicine", m.Code,
                $"{m.Name}: purchase {oldPrices.PurchasePrice?.ToString("N2") ?? "—"} → {m.PurchasePrice?.ToString("N2") ?? "—"}, sale {oldPrices.SalePrice?.ToString("N2") ?? "—"} → {m.SalePrice?.ToString("N2") ?? "—"}");
        }
        _audit.Add(AuditActions.PharmMedicineSaved, "Medicine", m.Code, $"Medicine {m.Code} {m.Name} {(id is null ? "added" : "updated")}{(m.IsActive ? "" : " (inactive)")}" +
                                                                  (i.ConfirmDuplicate ? " – saved despite similar-medicine warning" : ""));
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(m.Id);
    }

    public async Task<IReadOnlyList<MedicinePriceChange>> PriceHistoryAsync(int medicineId, CancellationToken ct = default) =>
        await _db.MedicinePriceChanges.AsNoTracking().Where(p => p.MedicineId == medicineId).OrderByDescending(p => p.Id).ToListAsync(ct);

    public async Task<ServiceResult> SetBatchSalePriceAsync(long batchId, decimal? salePrice, CancellationToken ct = default)
    {
        if (!await IsPrivateAsync(ct)) return ServiceResult.Fail("Prices are not used in a Government hospital.");
        if (salePrice is < 0 || salePrice is decimal sp && !FinanceMath.IsMoney(sp)) return ServiceResult.Fail("Enter a valid sale price.");
        var b = await _db.MedicineBatches.Include(x => x.Medicine).FirstOrDefaultAsync(x => x.Id == batchId, ct);
        if (b is null) return ServiceResult.Fail("Batch not found.");
        if (b.SalePrice == salePrice) return ServiceResult.Ok();
        _db.MedicinePriceChanges.Add(new MedicinePriceChange
        {
            MedicineId = b.MedicineId, BatchId = b.Id, OldSalePrice = b.SalePrice, NewSalePrice = salePrice, OldPurchasePrice = b.UnitCost, NewPurchasePrice = b.UnitCost,
            ChangedUtc = _clock.UtcNow, ChangedByUserId = _ctx.UserId, ChangedByUsername = _ctx.Username, Note = $"Batch {b.BatchNumber}"
        });
        _audit.Add(AuditActions.PharmMedicinePriceChanged, "MedicineBatch", b.BatchNumber,
            $"{b.Medicine!.Name} batch {b.BatchNumber}: sale price {b.SalePrice?.ToString("N2") ?? "(medicine price)"} → {salePrice?.ToString("N2") ?? "(medicine price)"}");
        b.SalePrice = salePrice;
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private async Task<string> NextCodeAsync(string sequence, string prefix, CancellationToken ct)
    {
        var n = await _db.Database.SqlQueryRaw<long>($"SELECT nextval('{sequence}') AS \"Value\"").SingleAsync(ct);
        return $"{prefix}-{n:000000}";
    }

    // =====================================================================================
    // Suppliers
    // =====================================================================================

    public async Task<IReadOnlyList<Supplier>> SuppliersAsync(string? q, bool activeOnly, CancellationToken ct = default)
    {
        var query = _db.Suppliers.AsNoTracking().Where(s => !activeOnly || s.IsActive);
        if (T(q, 100) is { } term)
        {
            var like = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(s => EF.Functions.ILike(s.Name, like) || EF.Functions.ILike(s.ContactPerson ?? "", like) || EF.Functions.ILike(s.Phone ?? "", like)
                                     || EF.Functions.ILike(s.City ?? "", like) || EF.Functions.ILike(s.Code, like));
        }
        return await query.OrderBy(s => s.Name).Take(1000).ToListAsync(ct);
    }

    public async Task<Supplier?> SupplierAsync(int id, CancellationToken ct = default) => await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ServiceResult<int>> SaveSupplierAsync(int? id, SupplierInput i, CancellationToken ct = default)
    {
        var name = T(i.Name, 150);
        if (name is null) return ServiceResult<int>.Fail("Enter the supplier name.");
        if (T(i.Email, 150) is { } email && !email.Contains('@')) return ServiceResult<int>.Fail("Enter a valid email address.");
        if (await _db.Suppliers.AnyAsync(s => s.Name.ToLower() == name.ToLower() && s.Id != id, ct))
            return ServiceResult<int>.Fail($"A supplier named “{name}” already exists.");
        var now = _clock.UtcNow;
        Supplier s;
        if (id is int sid) s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == sid, ct) ?? throw new KeyNotFoundException();
        else
        {
            s = new Supplier { Code = await NextCodeAsync(HmsDbContext.SupplierCodeSequence, "SUP", ct), CreatedUtc = now };
            _db.Suppliers.Add(s);
        }
        s.Name = name;
        s.ContactPerson = T(i.ContactPerson, 100);
        s.Phone = T(i.Phone, 30);
        s.Address = T(i.Address, 300);
        s.City = T(i.City, 60);
        s.Email = T(i.Email, 150);
        s.Notes = T(i.Notes, 500);
        s.IsActive = i.IsActive;
        s.UpdatedUtc = now;
        _audit.Add(AuditActions.PharmSupplierSaved, "Supplier", s.Code, $"Supplier {s.Code} {name} {(id is null ? "added" : "updated")}{(i.IsActive ? "" : " (inactive)")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(s.Id);
    }

    // =====================================================================================
    // Stock locations (main pharmacy, Minor OT and other department stores)
    // =====================================================================================

    public async Task<IReadOnlyList<StockLocation>> LocationsAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.StockLocations.AsNoTracking().Include(l => l.Department).Where(l => !activeOnly || l.IsActive)
            .OrderBy(l => l.Kind).ThenBy(l => l.SortOrder).ThenBy(l => l.Name).ToListAsync(ct);

    public async Task<StockLocation> MainLocationAsync(CancellationToken ct = default) =>
        await _db.StockLocations.AsNoTracking().OrderBy(l => l.Id).FirstAsync(l => l.Kind == StockLocationKind.MainPharmacy, ct);

    public async Task<ServiceResult<int>> SaveLocationAsync(int? id, StockLocationInput i, CancellationToken ct = default)
    {
        var name = T(i.Name, 100);
        var code = T(i.Code, 20)?.ToUpperInvariant();
        if (name is null || code is null) return ServiceResult<int>.Fail("Enter the code and name of the department store.");
        if (await _db.StockLocations.AnyAsync(l => (l.Code == code || l.Name.ToLower() == name.ToLower()) && l.Id != id, ct))
            return ServiceResult<int>.Fail("Another stock location already uses this code or name.");
        if (i.DepartmentId is int did && !await _db.Departments.AnyAsync(d => d.Id == did, ct)) return ServiceResult<int>.Fail("Select a valid department.");
        StockLocation l;
        if (id is int lid)
        {
            l = await _db.StockLocations.FirstOrDefaultAsync(x => x.Id == lid, ct) ?? throw new KeyNotFoundException();
            if (l.Kind == StockLocationKind.MainPharmacy && !i.IsActive) return ServiceResult<int>.Fail("The main pharmacy cannot be deactivated.");
            if (!i.IsActive && l.IsActive && await _db.StockBalances.AnyAsync(b => b.LocationId == l.Id && b.Quantity > 0, ct))
                return ServiceResult<int>.Fail($"{l.Name} still holds stock. Transfer it back or adjust it before deactivating.");
        }
        else
        {
            l = new StockLocation { Kind = StockLocationKind.Department, CreatedUtc = _clock.UtcNow };
            _db.StockLocations.Add(l);
        }
        l.Code = code;
        l.Name = name;
        l.DepartmentId = i.DepartmentId;
        l.IsActive = i.IsActive;
        l.SortOrder = i.SortOrder;
        l.Notes = T(i.Notes, 300);
        _audit.Add(AuditActions.PharmLocationSaved, "StockLocation", code, $"Stock location {code} {name} {(id is null ? "created" : "updated")}{(i.IsActive ? "" : " (inactive)")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(l.Id);
    }

    // =====================================================================================
    // Seed (start-up and after the setup wizard)
    // =====================================================================================

    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        if (!await db.PharmacySettings.AnyAsync(ct)) db.PharmacySettings.Add(new PharmacySettings { Id = 1, UpdatedUtc = now });
        if (!await db.PharmacyLookups.AnyAsync(ct))
        {
            void Add(PharmacyLookupKind kind, params string[] names)
            {
                var i = 0;
                foreach (var n in names) db.PharmacyLookups.Add(new PharmacyLookup { Kind = kind, Name = n, SortOrder = ++i * 10, CreatedUtc = now });
            }
            Add(PharmacyLookupKind.Category, "Analgesics & Antipyretics", "Antibiotics", "Antiseptics & Disinfectants", "Cardiovascular", "Gastrointestinal",
                "Respiratory", "Antidiabetics", "Vitamins & Supplements", "IV Fluids", "Vaccines & Sera", "Anaesthetics", "Surgical Supplies", "Dressing Material", "Other");
            Add(PharmacyLookupKind.DosageForm, "Tablet", "Capsule", "Syrup", "Suspension", "Injection", "Infusion", "Ointment", "Cream", "Gel", "Drops",
                "Inhaler", "Sachet", "Suppository", "Solution", "Surgical item");
            Add(PharmacyLookupKind.Strength, "5 mg", "10 mg", "20 mg", "50 mg", "100 mg", "250 mg", "500 mg", "1 g", "125 mg/5 ml", "250 mg/5 ml", "0.9 %", "5 %");
            Add(PharmacyLookupKind.PackSize, "10 tablets", "10 x 10", "20 x 10", "30 capsules", "60 ml", "120 ml", "1 vial", "5 ampoules", "500 ml bag", "1000 ml bag", "1 piece");
            Add(PharmacyLookupKind.Unit, "Tablet", "Capsule", "Bottle", "Vial", "Ampoule", "Bag", "Tube", "Sachet", "Piece", "Pack", "Roll", "Pair");
        }
        await db.SaveChangesAsync(ct);

        // Phase 3 service used for every pharmacy charge (the line carries the medicine and its price).
        var settings = await db.PharmacySettings.FirstAsync(ct);
        var pharmDept = await db.Departments.Where(d => d.Name == "Pharmacy").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (settings.MedicineServiceItemId is null)
        {
            var svc = await db.ServiceItems.FirstOrDefaultAsync(s => s.Code == "PHM-MED", ct);
            if (svc is null)
            {
                svc = new ServiceItem
                {
                    Code = "PHM-MED", Name = "Medicines (pharmacy)", Category = EncounterType.ServiceOnly, Kind = ServiceKind.Pharmacy, DepartmentId = pharmDept,
                    DefaultFee = 0, IsActive = true, CreatedUtc = now, UpdatedUtc = now,
                    Description = "Used by the Pharmacy module for medicine sales and patient issues. The price comes from the medicine."
                };
                db.ServiceItems.Add(svc);
                await db.SaveChangesAsync(ct);
                db.ServiceFees.Add(new ServiceFee { ServiceItemId = svc.Id, Fee = 0, EffectiveFrom = DateOnly.MinValue, CreatedUtc = now, CreatedByUsername = "SYSTEM", Note = "Price per medicine" });
            }
            settings.MedicineServiceItemId = svc.Id;
            await db.SaveChangesAsync(ct);
        }
        else if (pharmDept is int pd)
        {
            var svc = await db.ServiceItems.FirstAsync(s => s.Id == settings.MedicineServiceItemId, ct);
            if (svc.DepartmentId is null) { svc.DepartmentId = pd; await db.SaveChangesAsync(ct); }
        }

        // Stock locations: the main pharmacy and the Minor OT store (more department stores can be added by the administrator).
        var otDept = await db.Departments.Where(d => d.Name == "Minor OT").Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        foreach (var (code, name, kind, dept, sort) in new[]
                 {
                     ("MAIN", "Main Pharmacy", StockLocationKind.MainPharmacy, pharmDept, 10),
                     ("MOT", "Minor OT", StockLocationKind.Department, otDept, 20),
                 })
        {
            var loc = await db.StockLocations.FirstOrDefaultAsync(l => l.Code == code, ct);
            if (loc is null)
                db.StockLocations.Add(new StockLocation { Code = code, Name = name, Kind = kind, DepartmentId = dept, IsActive = true, SortOrder = sort, CreatedUtc = now });
            else if (loc.DepartmentId is null && dept is not null) loc.DepartmentId = dept;
        }
        await db.SaveChangesAsync(ct);
    }
}
