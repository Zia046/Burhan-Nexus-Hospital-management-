using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Finance;

public sealed class CounterInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public int? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PaymentMethodInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public bool CountsAsCashInHand { get; set; }
    public bool RequiresReference { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class ServiceInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public ServiceKind Kind { get; set; } = ServiceKind.Other;
    public int? DepartmentId { get; set; }
    public int? DoctorId { get; set; }
    public bool IsFree { get; set; }
    /// <summary>Update 13: saved without a fee for now (a user without the fee permission) – "fee not set", NOT free.</summary>
    public bool FeePending { get; set; }
    public decimal Fee { get; set; }
    /// <summary>When the (changed) fee applies. Empty = today. Only today or a future date.</summary>
    public DateOnly? FeeEffectiveFrom { get; set; }
    public string? FeeNote { get; set; }
    public bool AllowPartialPayment { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public List<int> CounterIds { get; set; } = new();
    // ---- Update 10 ----
    public int? ServiceCategoryId { get; set; }
    /// <summary>Empty = keep the current one (forms that do not show it).</summary>
    public ChargeUnit? ChargeUnit { get; set; }
    public ServiceApplicability? Applicability { get; set; }
    /// <summary>Separate government rate (empty = same as the fee).</summary>
    public decimal? GovernmentFee { get; set; }
    /// <summary>The form shows the government rate (an empty value then means "same as the fee").</summary>
    public bool SetGovernmentFee { get; set; }
}

public sealed class ServiceCategoryInput
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed record ServiceCategoryRow(ServiceCategory Category, int Services, int ActiveServices);

public sealed class FinanceSettingsInput
{
    public string? ReceiptPrefix { get; set; }
    public bool IssueZeroReceiptForFreeServices { get; set; } = true;
    public bool ShowCashierOnReceipt { get; set; } = true;
    public string? ReceiptFooter { get; set; }
    public int DefaultReceiptWidthMm { get; set; } = 80;
}

public sealed record CounterRow(Counter Counter, IReadOnlyList<(int UserId, string Username, string FullName, DateTime AssignedUtc)> Staff);

public sealed record ServiceRow(ServiceItem Item, decimal CurrentFee, ServiceFee? NextChange, IReadOnlyList<string> CounterNames, decimal? CurrentGovernmentFee = null, bool HasTransactions = false);

public interface IFinanceSetupService
{
    Task<FinanceSettings> GetSettingsAsync(CancellationToken ct = default);
    Task<ServiceResult> UpdateSettingsAsync(FinanceSettingsInput input, uint rowVersion, CancellationToken ct = default);

    Task<IReadOnlyList<PaymentMethod>> PaymentMethodsAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SavePaymentMethodAsync(int? id, PaymentMethodInput input, CancellationToken ct = default);

    Task<IReadOnlyList<CounterRow>> CountersAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveCounterAsync(int? id, CounterInput input, CancellationToken ct = default);
    /// <summary>Assigns a user to a counter. With <paramref name="transferFromCounterId"/> the user's assignment there ends (transfer).</summary>
    Task<ServiceResult> AssignAsync(int counterId, int userId, int? transferFromCounterId, CancellationToken ct = default);
    Task<ServiceResult> UnassignAsync(int counterId, int userId, CancellationToken ct = default);
    Task<IReadOnlyList<Counter>> CountersOfUserAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<CounterAssignment>> AssignmentHistoryAsync(int counterId, CancellationToken ct = default);

    Task<IReadOnlyList<ServiceRow>> ServicesAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceRow?> ServiceAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceFee>> FeeHistoryAsync(int serviceId, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveServiceAsync(int? id, ServiceInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceCategoryRow>> CategoriesAsync(bool activeOnly, CancellationToken ct = default);
    Task<ServiceResult<int>> SaveCategoryAsync(int? id, ServiceCategoryInput input, CancellationToken ct = default);
    /// <summary>Fee that applies on <paramref name="date"/> (history aware).</summary>
    Task<decimal> FeeOnAsync(int serviceId, DateOnly date, CancellationToken ct = default);

    Task<ServiceResult> SetDoctorShareAsync(int doctorId, DoctorShareType type, decimal value, CancellationToken ct = default);
}

public sealed class FinanceSetupService : IFinanceSetupService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    public FinanceSetupService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    private static string? T(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    // ---------------- seed ----------------

    /// <summary>Default finance data, created once. Never overwrites what the hospital configured.</summary>
    public static async Task SeedAsync(HmsDbContext db, DateTime now, DateOnly today, CancellationToken ct = default)
    {
        if (!await db.FinanceSettings.AnyAsync(ct)) db.FinanceSettings.Add(new FinanceSettings { Id = 1, UpdatedUtc = now });
        if (!await db.PaymentMethods.AnyAsync(ct))
        {
            db.PaymentMethods.Add(new PaymentMethod { Code = "CASH", Name = "Cash", CountsAsCashInHand = true, SortOrder = 10, CreatedUtc = now, UpdatedUtc = now });
            db.PaymentMethods.Add(new PaymentMethod { Code = "BANK", Name = "Bank Transfer", RequiresReference = true, SortOrder = 20, CreatedUtc = now, UpdatedUtc = now });
            db.PaymentMethods.Add(new PaymentMethod { Code = "CARD", Name = "Card", RequiresReference = true, SortOrder = 30, CreatedUtc = now, UpdatedUtc = now });
        }
        // Update 10: bills settled from an advance deposit (inactive so that it is never chosen at the counter; not cash in hand).
        if (!await db.PaymentMethods.AnyAsync(m => m.Code == BillingService.AdvanceMethodCode, ct))
            db.PaymentMethods.Add(new PaymentMethod { Code = BillingService.AdvanceMethodCode, Name = "Advance deposit (adjusted)", CountsAsCashInHand = false, IsActive = false, SortOrder = 900, CreatedUtc = now, UpdatedUtc = now });
        if (!await db.Counters.AnyAsync(ct))
            db.Counters.Add(new Counter { Code = "MAIN", Name = "Main Reception Counter", CreatedUtc = now, UpdatedUtc = now });

        // Services that existed before Phase 3: classify once and start their fee history with the current fee.
        var withoutHistory = await db.ServiceItems.Where(s => !db.ServiceFees.Any(f => f.ServiceItemId == s.Id)).ToListAsync(ct);
        foreach (var s in withoutHistory)
        {
            if (s.Kind == ServiceKind.Other)
                s.Kind = s.DoctorId != null ? ServiceKind.Consultation
                    : s.Category == EncounterType.Emergency ? ServiceKind.EmergencyRegistration
                    : s.Category == EncounterType.Opd ? ServiceKind.OpdRegistration : ServiceKind.Other;
            db.ServiceFees.Add(new ServiceFee
            {
                ServiceItemId = s.Id, Fee = s.DefaultFee, EffectiveFrom = DateOnly.MinValue, CreatedUtc = now, CreatedByUsername = "SYSTEM",
                Note = "Fee at start of financial module"
            });
        }
        await db.SaveChangesAsync(ct);

        // Future-dated fee changes that have become effective: keep the reference fee shown in OPD/appointment screens current.
        var current = await db.ServiceFees.Where(f => f.EffectiveFrom <= today)
            .GroupBy(f => f.ServiceItemId)
            .Select(g => g.OrderByDescending(f => f.EffectiveFrom).ThenByDescending(f => f.Id).First())
            .ToListAsync(ct);
        var items = await db.ServiceItems.ToDictionaryAsync(s => s.Id, ct);
        foreach (var f in current)
            if (items.TryGetValue(f.ServiceItemId, out var item) && item.DefaultFee != f.Fee) { item.DefaultFee = f.Fee; item.UpdatedUtc = now; }
        await db.SaveChangesAsync(ct);
        await ServiceCatalogue.SeedAsync(db, now, ct);
    }

    // ---------------- settings ----------------

    public async Task<FinanceSettings> GetSettingsAsync(CancellationToken ct = default) =>
        await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new FinanceSettings();

    public async Task<ServiceResult> UpdateSettingsAsync(FinanceSettingsInput i, uint rowVersion, CancellationToken ct = default)
    {
        var prefix = T(i.ReceiptPrefix)?.ToUpperInvariant();
        if (prefix is null || prefix.Length > 10 || !prefix.All(c => char.IsAsciiLetterOrDigit(c)))
            return ServiceResult.Fail("Receipt prefix: 1–10 letters or digits.");
        if (i.DefaultReceiptWidthMm is not (58 or 80)) return ServiceResult.Fail("Receipt width must be 58 mm or 80 mm.");
        var s = await _db.FinanceSettings.FirstAsync(ct);
        var old = new { s.ReceiptPrefix, s.IssueZeroReceiptForFreeServices, s.ShowCashierOnReceipt, s.ReceiptFooter, s.DefaultReceiptWidthMm };
        _db.Entry(s).Property(x => x.RowVersion).OriginalValue = rowVersion;
        s.ReceiptPrefix = prefix;
        s.IssueZeroReceiptForFreeServices = i.IssueZeroReceiptForFreeServices;
        s.ShowCashierOnReceipt = i.ShowCashierOnReceipt;
        s.ReceiptFooter = T(i.ReceiptFooter) is { } f ? f[..Math.Min(200, f.Length)] : null;
        s.DefaultReceiptWidthMm = i.DefaultReceiptWidthMm;
        s.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.FinanceSettingsChanged, "FinanceSettings", "1", "Financial settings changed", old,
            new { s.ReceiptPrefix, s.IssueZeroReceiptForFreeServices, s.ShowCashierOnReceipt, s.ReceiptFooter, s.DefaultReceiptWidthMm });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("Settings were changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // ---------------- payment methods ----------------

    public async Task<IReadOnlyList<PaymentMethod>> PaymentMethodsAsync(bool activeOnly, CancellationToken ct = default) =>
        await _db.PaymentMethods.AsNoTracking().Where(m => !activeOnly || m.IsActive).OrderBy(m => m.SortOrder).ThenBy(m => m.Name).ToListAsync(ct);

    public async Task<ServiceResult<int>> SavePaymentMethodAsync(int? id, PaymentMethodInput i, CancellationToken ct = default)
    {
        var code = T(i.Code)?.ToUpperInvariant();
        var name = T(i.Name);
        if (code is null || code.Length > 20) return ServiceResult<int>.Fail("Code is required (max 20 characters).");
        if (name is null || name.Length > 60) return ServiceResult<int>.Fail("Name is required (max 60 characters).");
        if (await _db.PaymentMethods.AnyAsync(m => m.Id != id && (m.Code == code || m.Name == name), ct))
            return ServiceResult<int>.Fail("A payment method with this code or name already exists.");
        PaymentMethod m;
        object? old = null;
        if (id is null)
        {
            m = new PaymentMethod { CreatedUtc = _clock.UtcNow };
            _db.PaymentMethods.Add(m);
        }
        else
        {
            m = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
            old = new { m.Code, m.Name, m.CountsAsCashInHand, m.RequiresReference, m.IsActive };
        }
        m.Code = code; m.Name = name; m.CountsAsCashInHand = i.CountsAsCashInHand; m.RequiresReference = i.RequiresReference;
        m.IsActive = i.IsActive; m.SortOrder = i.SortOrder; m.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.PaymentMethodSaved, "PaymentMethod", code, $"Payment method {name} saved", old,
            new { m.Code, m.Name, m.CountsAsCashInHand, m.RequiresReference, m.IsActive });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(m.Id);
    }

    // ---------------- counters ----------------

    public async Task<IReadOnlyList<CounterRow>> CountersAsync(bool activeOnly, CancellationToken ct = default)
    {
        var counters = await _db.Counters.AsNoTracking().Include(c => c.Department)
            .Where(c => !activeOnly || c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);
        var staff = await _db.CounterAssignments.AsNoTracking().Where(a => a.EndedUtc == null)
            .Select(a => new { a.CounterId, a.UserId, a.User!.Username, a.User.FullName, a.AssignedUtc }).ToListAsync(ct);
        return counters.Select(c => new CounterRow(c, staff.Where(s => s.CounterId == c.Id).OrderBy(s => s.FullName)
            .Select(s => (s.UserId, s.Username, s.FullName, s.AssignedUtc)).ToList())).ToList();
    }

    public async Task<ServiceResult<int>> SaveCounterAsync(int? id, CounterInput i, CancellationToken ct = default)
    {
        var code = T(i.Code)?.ToUpperInvariant();
        var name = T(i.Name);
        if (code is null || code.Length > 20) return ServiceResult<int>.Fail("Counter code is required (max 20 characters).");
        if (name is null || name.Length > 100) return ServiceResult<int>.Fail("Counter name is required (max 100 characters).");
        if (await _db.Counters.AnyAsync(c => c.Id != id && (c.Code == code || c.Name == name), ct))
            return ServiceResult<int>.Fail("Another counter already uses this code or name.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) return ServiceResult<int>.Fail("Unknown department.");
        Counter c;
        object? old = null;
        if (id is null)
        {
            c = new Counter { CreatedUtc = _clock.UtcNow };
            _db.Counters.Add(c);
        }
        else
        {
            c = await _db.Counters.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
            old = new { c.Code, c.Name, c.DepartmentId, c.IsActive };
        }
        c.Code = code; c.Name = name; c.DepartmentId = i.DepartmentId; c.IsActive = i.IsActive; c.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.CounterSaved, "Counter", code, $"Counter {name} {(id is null ? "created" : "updated")}{(i.IsActive ? "" : " (inactive)")}",
            old, new { c.Code, c.Name, c.DepartmentId, c.IsActive });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(c.Id);
    }

    public async Task<ServiceResult> AssignAsync(int counterId, int userId, int? transferFromCounterId, CancellationToken ct = default)
    {
        var counter = await _db.Counters.FirstOrDefaultAsync(c => c.Id == counterId, ct);
        if (counter is null || !counter.IsActive) return ServiceResult.Fail("Select an active counter.");
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive) return ServiceResult.Fail("Select an active employee.");
        if (await _db.CounterAssignments.AnyAsync(a => a.CounterId == counterId && a.UserId == userId && a.EndedUtc == null, ct))
            return ServiceResult.Fail($"{user.FullName} is already assigned to {counter.Name}.");
        var now = _clock.UtcNow;
        string? from = null;
        if (transferFromCounterId is int fromId)
        {
            var current = await _db.CounterAssignments.Include(a => a.Counter)
                .FirstOrDefaultAsync(a => a.CounterId == fromId && a.UserId == userId && a.EndedUtc == null, ct);
            if (current is null) return ServiceResult.Fail("The employee is not assigned to the counter being transferred from.");
            current.EndedUtc = now;
            current.EndedBy = _ctx.Username;
            from = current.Counter!.Name;
        }
        _db.CounterAssignments.Add(new CounterAssignment { CounterId = counterId, UserId = userId, AssignedUtc = now, AssignedBy = _ctx.Username });
        _audit.Add(AuditActions.CounterAssigned, "Counter", counter.Code,
            from is null ? $"{user.Username} assigned to {counter.Name}" : $"{user.Username} transferred from {from} to {counter.Name}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex)) { return ServiceResult.Fail("This assignment already exists."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnassignAsync(int counterId, int userId, CancellationToken ct = default)
    {
        var a = await _db.CounterAssignments.Include(x => x.Counter).Include(x => x.User)
            .FirstOrDefaultAsync(x => x.CounterId == counterId && x.UserId == userId && x.EndedUtc == null, ct);
        if (a is null) return ServiceResult.Fail("Assignment not found.");
        a.EndedUtc = _clock.UtcNow;
        a.EndedBy = _ctx.Username;
        _audit.Add(AuditActions.CounterUnassigned, "Counter", a.Counter!.Code, $"{a.User!.Username} removed from {a.Counter.Name}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<Counter>> CountersOfUserAsync(int userId, CancellationToken ct = default) =>
        await _db.CounterAssignments.AsNoTracking().Where(a => a.UserId == userId && a.EndedUtc == null && a.Counter!.IsActive)
            .Select(a => a.Counter!).OrderBy(c => c.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<CounterAssignment>> AssignmentHistoryAsync(int counterId, CancellationToken ct = default) =>
        await _db.CounterAssignments.AsNoTracking().Include(a => a.User).Where(a => a.CounterId == counterId)
            .OrderByDescending(a => a.AssignedUtc).Take(200).ToListAsync(ct);

    // ---------------- services & fees ----------------

    public Task<decimal> FeeOnAsync(int serviceId, DateOnly date, CancellationToken ct = default) => ServiceCatalogue.FeeOnAsync(_db, serviceId, date, ct);

    public async Task<IReadOnlyList<ServiceRow>> ServicesAsync(bool activeOnly, CancellationToken ct = default)
    {
        var today = await _clock.TodayAsync();
        var items = await _db.ServiceItems.AsNoTracking().Include(s => s.Doctor).Include(s => s.Department)
            .Include(s => s.Counters).ThenInclude(c => c.Counter).Include(s => s.ServiceCategory)
            .Where(s => !activeOnly || s.IsActive).OrderBy(s => s.Kind).ThenBy(s => s.Name).ToListAsync(ct);
        var fees = await _db.ServiceFees.AsNoTracking().ToListAsync(ct);
        return items.Select(s => Row(s, fees.Where(f => f.ServiceItemId == s.Id).ToList(), today)).ToList();
    }

    private static ServiceRow Row(ServiceItem s, List<ServiceFee> fees, DateOnly today)
    {
        var current = fees.Where(f => f.EffectiveFrom <= today).OrderByDescending(f => f.EffectiveFrom).ThenByDescending(f => f.Id).FirstOrDefault();
        var next = fees.Where(f => f.EffectiveFrom > today).OrderBy(f => f.EffectiveFrom).ThenByDescending(f => f.Id).FirstOrDefault();
        return new ServiceRow(s, current?.Fee ?? s.DefaultFee, next, s.Counters.Select(c => c.Counter!.Name).OrderBy(n => n).ToList(), current?.GovernmentFee);
    }

    public async Task<ServiceRow?> ServiceAsync(int id, CancellationToken ct = default)
    {
        var s = await _db.ServiceItems.AsNoTracking().Include(x => x.Doctor).Include(x => x.Department).Include(x => x.ServiceCategory)
            .Include(x => x.Counters).ThenInclude(c => c.Counter).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        var fees = await _db.ServiceFees.AsNoTracking().Where(f => f.ServiceItemId == id).ToListAsync(ct);
        return Row(s, fees, await _clock.TodayAsync()) with { HasTransactions = await _db.Charges.AnyAsync(c => c.ServiceItemId == id, ct) };
    }

    public async Task<IReadOnlyList<ServiceFee>> FeeHistoryAsync(int serviceId, CancellationToken ct = default) =>
        await _db.ServiceFees.AsNoTracking().Where(f => f.ServiceItemId == serviceId).OrderByDescending(f => f.EffectiveFrom).ThenByDescending(f => f.Id).ToListAsync(ct);

    public async Task<ServiceResult<int>> SaveServiceAsync(int? id, ServiceInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code)?.ToUpperInvariant();
        var name = T(i.Name);
        if (code is null || code.Length > 30) errors.Add("Service code is required (max 30 characters).");
        if (name is null || name.Length > 150) errors.Add("Service name is required (max 150 characters).");
        if (!Enum.IsDefined(i.Kind)) errors.Add("Select the service type.");
        var fee = i.IsFree ? 0 : i.Fee;
        if (fee < 0 || fee > 10_000_000 || !FinanceMath.IsMoney(fee)) errors.Add("Fee must be a valid amount (0 or more, 2 decimals).");
        if (!i.IsFree && fee == 0 && !i.FeePending) errors.Add("Enter the fee, or mark the service as FREE.");
        if (i.DepartmentId is int d && !await _db.Departments.AnyAsync(x => x.Id == d, ct)) errors.Add("Unknown department.");
        if (i.DoctorId is int doc && !await _db.Doctors.AnyAsync(x => x.Id == doc, ct)) errors.Add("Unknown doctor.");
        if (i.Kind == ServiceKind.Consultation && i.DoctorId is null) errors.Add("A consultation service must be linked to a doctor.");
        var counterIds = i.CounterIds.Distinct().ToList();
        if (counterIds.Count > 0 && await _db.Counters.CountAsync(c => counterIds.Contains(c.Id), ct) != counterIds.Count) errors.Add("Unknown counter.");
        if (code is not null && await _db.ServiceItems.AnyAsync(s => s.Id != id && s.Code == code, ct)) errors.Add("Another service already uses this code.");
        var today = await _clock.TodayAsync();
        var effective = i.FeeEffectiveFrom ?? today;
        if (effective < today) errors.Add("A fee change cannot be back-dated – it only affects new transactions (today or later).");
        if ((i.Description?.Length ?? 0) > 300) errors.Add("Description must not exceed 300 characters.");
        if (i.ChargeUnit is { } cu && !Enum.IsDefined(cu)) errors.Add("Select the charging method.");
        if (i.Applicability is { } ap && !Enum.IsDefined(ap)) errors.Add("Select where the service applies (government / private).");
        // Forms without the government-rate field keep the current one.
        var keepGov = i.GovernmentFee is null && !i.SetGovernmentFee;
        var govFee = i.IsFree ? (decimal?)null : i.GovernmentFee;
        if (govFee is decimal gf && (gf < 0 || gf > 10_000_000 || !FinanceMath.IsMoney(gf))) errors.Add("Government rate must be a valid amount (0 or more, 2 decimals).");
        if (i.ServiceCategoryId is int sc && !await _db.ServiceCategories.AnyAsync(c => c.Id == sc, ct)) errors.Add("Unknown category.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());

        var now = _clock.UtcNow;
        ServiceItem s;
        object? old = null;
        decimal? oldFee = null;
        decimal? oldGov = null;
        if (id is null)
        {
            s = new ServiceItem { CreatedUtc = now };
            _db.ServiceItems.Add(s);
        }
        else
        {
            s = await _db.ServiceItems.Include(x => x.Counters).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
            var row = await _db.ServiceFees.AsNoTracking().Where(f => f.ServiceItemId == s.Id && f.EffectiveFrom <= effective)
                .OrderByDescending(f => f.EffectiveFrom).ThenByDescending(f => f.Id).Select(f => new { f.Fee, f.GovernmentFee }).FirstOrDefaultAsync(ct);
            oldFee = row?.Fee ?? s.DefaultFee;
            oldGov = row?.GovernmentFee;
            if (keepGov) govFee = i.IsFree ? null : oldGov;
            old = new { s.Code, s.Name, s.Kind, s.DepartmentId, s.DoctorId, s.AllowPartialPayment, s.IsActive, s.ServiceCategoryId, s.ChargeUnit, s.Applicability, Fee = oldFee, GovernmentFee = oldGov };
        }
        s.Code = code!; s.Name = name!; s.Kind = i.Kind; s.DepartmentId = i.DepartmentId; s.DoctorId = i.DoctorId;
        s.AllowPartialPayment = i.AllowPartialPayment; s.IsActive = i.IsActive; s.Description = T(i.Description); s.UpdatedUtc = now;
        s.IsFree = i.IsFree; // Update 13: free only when explicitly marked
        s.ServiceCategoryId = i.ServiceCategoryId ?? s.ServiceCategoryId ?? await ServiceCatalogue.CategoryIdAsync(_db, i.Kind, ct);
        s.ChargeUnit = i.ChargeUnit ?? s.ChargeUnit; s.Applicability = i.Applicability ?? s.Applicability;
        // Registration services keep their OPD/ER category; everything else is billed at the counter only.
        s.Category = i.Kind switch
        {
            ServiceKind.OpdRegistration or ServiceKind.Consultation => EncounterType.Opd,
            ServiceKind.EmergencyRegistration => EncounterType.Emergency,
            _ => id is null ? EncounterType.ServiceOnly : s.Category is EncounterType.Opd or EncounterType.Emergency ? EncounterType.ServiceOnly : s.Category,
        };
        if (effective <= today) s.DefaultFee = fee;
        s.Counters.RemoveAll(c => !counterIds.Contains(c.CounterId));
        foreach (var cid in counterIds.Where(cid => s.Counters.All(c => c.CounterId != cid)))
            s.Counters.Add(new ServiceCounter { CounterId = cid });
        await _db.SaveChangesAsync(ct);

        if (oldFee != fee || oldGov != govFee)
        {
            _db.ServiceFees.Add(new ServiceFee
            {
                ServiceItemId = s.Id, Fee = fee, GovernmentFee = govFee, EffectiveFrom = id is null ? DateOnly.MinValue : effective, CreatedUtc = now,
                CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username, Note = T(i.FeeNote)
            });
            if (id is not null)
                _audit.Add(AuditActions.ServiceFeeChanged, "ServiceItem", s.Code,
                    $"Fee of {s.Name} changed from {oldFee:N2} to {fee:N2}{(oldGov != govFee ? $" (government rate {oldGov?.ToString("N2") ?? "same"} → {govFee?.ToString("N2") ?? "same"})" : "")} effective {effective:yyyy-MM-dd}",
                    new { Fee = oldFee, GovernmentFee = oldGov }, new { Fee = fee, GovernmentFee = govFee, EffectiveFrom = effective });
        }
        _audit.Add(AuditActions.ServiceSaved, "ServiceItem", s.Code, $"Service {s.Name} {(id is null ? "created" : "updated")}", old,
            new { s.Code, s.Name, s.Kind, s.DepartmentId, s.DoctorId, s.AllowPartialPayment, s.IsActive, s.ServiceCategoryId, s.ChargeUnit, s.Applicability, Fee = fee, GovernmentFee = govFee, Counters = counterIds });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(s.Id);
    }

    // ---------------- service categories (update 10) ----------------

    public async Task<IReadOnlyList<ServiceCategoryRow>> CategoriesAsync(bool activeOnly, CancellationToken ct = default)
    {
        var cats = await _db.ServiceCategories.AsNoTracking().Where(c => !activeOnly || c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        var counts = await _db.ServiceItems.Where(s => s.ServiceCategoryId != null).GroupBy(s => s.ServiceCategoryId!.Value)
            .Select(g => new { g.Key, All = g.Count(), Active = g.Count(s => s.IsActive) }).ToDictionaryAsync(x => x.Key, ct);
        return cats.Select(c => new ServiceCategoryRow(c, counts.GetValueOrDefault(c.Id)?.All ?? 0, counts.GetValueOrDefault(c.Id)?.Active ?? 0)).ToList();
    }

    public async Task<ServiceResult<int>> SaveCategoryAsync(int? id, ServiceCategoryInput i, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var code = T(i.Code)?.ToUpperInvariant();
        var name = T(i.Name);
        if (code is null || code.Length > 20) errors.Add("Category code is required (max 20 characters).");
        if (name is null || name.Length > 100) errors.Add("Category name is required (max 100 characters).");
        if ((i.Description?.Length ?? 0) > 300) errors.Add("Description must not exceed 300 characters.");
        if (code is not null && await _db.ServiceCategories.AnyAsync(c => c.Id != id && c.Code == code, ct)) errors.Add("Another category already uses this code.");
        if (name is not null && await _db.ServiceCategories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower(), ct)) errors.Add("Another category already has this name.");
        if (errors.Count > 0) return ServiceResult<int>.Fail(errors.ToArray());
        var now = _clock.UtcNow;
        ServiceCategory c;
        object? old = null;
        if (id is null)
        {
            c = new ServiceCategory { CreatedUtc = now, SortOrder = (await _db.ServiceCategories.MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 10 };
            _db.ServiceCategories.Add(c);
        }
        else
        {
            c = await _db.ServiceCategories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
            old = new { c.Code, c.Name, c.Description, c.IsActive, c.SortOrder };
            if (i.SortOrder > 0) c.SortOrder = i.SortOrder;
        }
        c.Code = code!; c.Name = name!; c.Description = T(i.Description); c.IsActive = i.IsActive; c.UpdatedUtc = now;
        _audit.Add(AuditActions.ServiceCategorySaved, "ServiceCategory", c.Code, $"Service category {c.Name} {(id is null ? "created" : "updated")}{(c.IsActive ? "" : " (inactive)")}",
            old, new { c.Code, c.Name, c.Description, c.IsActive, c.SortOrder });
        await _db.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(c.Id);
    }

    // ---------------- doctors ----------------

    public async Task<ServiceResult> SetDoctorShareAsync(int doctorId, DoctorShareType type, decimal value, CancellationToken ct = default)
    {
        if (FinanceMath.ValidateShare(type, value) is { } err) return ServiceResult.Fail(err);
        if (type is DoctorShareType.HospitalKeepsAll or DoctorShareType.FullFeeToDoctor) value = 0;
        var d = await _db.Doctors.FirstOrDefaultAsync(x => x.Id == doctorId, ct);
        if (d is null) return ServiceResult.Fail("Doctor not found.");
        var old = new { d.ShareType, d.ShareValue };
        d.ShareType = type;
        d.ShareValue = value;
        d.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.DoctorShareChanged, "Doctor", d.Id.ToString(),
            $"Fee arrangement of {d.Name}: {FinanceMath.Describe(type, value)} (applies to new consultations)", old, new { d.ShareType, d.ShareValue });
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
