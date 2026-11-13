using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Finance;

/// <summary>Doctor consultation account. Fees collected from patients and money paid to the doctor are kept apart.</summary>
public sealed record DoctorAccount(
    int DoctorId, string Name, string? Specialty, bool IsActive, DoctorShareType ShareType, decimal ShareValue, decimal? ConsultationFee,
    int Consultations, decimal FeesCollected, decimal HospitalShare, decimal DoctorEarned, decimal Adjustments, decimal PaidToDoctor)
{
    public decimal Payable => DoctorEarned + Adjustments - PaidToDoctor;
    public string Arrangement => FinanceMath.Describe(ShareType, ShareValue);
}

public sealed class SettlementInput
{
    public decimal Amount { get; set; }
    public int PaymentMethodId { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public DateOnly? PaidOn { get; set; }
}

public interface IDoctorAccountService
{
    Task<IReadOnlyList<DoctorAccount>> AccountsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<DoctorAccount?> AccountAsync(int doctorId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorLedgerEntry>> LedgerAsync(int doctorId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorSettlement>> SettlementsAsync(int? doctorId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<decimal> PayableAsync(int doctorId, CancellationToken ct = default);
    Task<ServiceResult<DoctorSettlement>> PayAsync(int doctorId, SettlementInput input, CancellationToken ct = default);
}

public sealed class DoctorAccountService : IDoctorAccountService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;

    public DoctorAccountService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
    }

    public async Task<decimal> PayableAsync(int doctorId, CancellationToken ct = default) =>
        await _db.DoctorLedger.Where(e => e.DoctorId == doctorId).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

    public async Task<IReadOnlyList<DoctorAccount>> AccountsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var doctors = await _db.Doctors.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        var ledger = _db.DoctorLedger.AsNoTracking().AsQueryable();
        if (from is { } f) ledger = ledger.Where(e => e.BusinessDate >= f);
        if (to is { } t) ledger = ledger.Where(e => e.BusinessDate <= t);
        var sums = await ledger.GroupBy(e => new { e.DoctorId, e.EntryType })
            .Select(g => new { g.Key.DoctorId, g.Key.EntryType, Amount = g.Sum(e => e.Amount), Gross = g.Sum(e => e.GrossAmount), Hospital = g.Sum(e => e.HospitalShare) })
            .ToListAsync(ct);
        var charges = _db.Charges.AsNoTracking().Where(c => c.DoctorId != null && c.Status != ChargeStatus.Cancelled);
        if (from is { } f2) charges = charges.Where(c => c.BusinessDate >= f2);
        if (to is { } t2) charges = charges.Where(c => c.BusinessDate <= t2);
        var counts = await charges.GroupBy(c => c.DoctorId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var fees = await _db.ServiceItems.AsNoTracking().Where(s => s.DoctorId != null && s.IsActive && s.Kind == ServiceKind.Consultation)
            .GroupBy(s => s.DoctorId!.Value).Select(g => new { g.Key, Fee = g.OrderBy(s => s.Id).First().DefaultFee }).ToDictionaryAsync(x => x.Key, x => x.Fee, ct);
        return doctors.Select(d =>
        {
            var mine = sums.Where(s => s.DoctorId == d.Id).ToList();
            var share = mine.Where(s => s.EntryType is DoctorEntryType.ConsultationShare or DoctorEntryType.ShareReversal).ToList();
            return new DoctorAccount(d.Id, d.Name, d.Specialty, d.IsActive, d.ShareType, d.ShareValue, fees.TryGetValue(d.Id, out var fee) ? fee : null,
                counts.GetValueOrDefault(d.Id), share.Sum(s => s.Gross), share.Sum(s => s.Hospital), share.Sum(s => s.Amount),
                mine.Where(s => s.EntryType == DoctorEntryType.Adjustment).Sum(s => s.Amount),
                -mine.Where(s => s.EntryType == DoctorEntryType.Settlement).Sum(s => s.Amount));
        }).ToList();
    }

    public async Task<DoctorAccount?> AccountAsync(int doctorId, DateOnly? from, DateOnly? to, CancellationToken ct = default) =>
        (await AccountsAsync(from, to, ct)).FirstOrDefault(a => a.DoctorId == doctorId);

    public async Task<IReadOnlyList<DoctorLedgerEntry>> LedgerAsync(int doctorId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.DoctorLedger.AsNoTracking().Where(e => e.DoctorId == doctorId);
        if (from is { } f) q = q.Where(e => e.BusinessDate >= f);
        if (to is { } t) q = q.Where(e => e.BusinessDate <= t);
        return await q.OrderByDescending(e => e.Id).Take(2000).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DoctorSettlement>> SettlementsAsync(int? doctorId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = _db.DoctorSettlements.AsNoTracking().Include(s => s.Doctor).Include(s => s.PaymentMethod).AsQueryable();
        if (doctorId is int d) q = q.Where(s => s.DoctorId == d);
        if (from is { } f) q = q.Where(s => s.BusinessDate >= f);
        if (to is { } t) q = q.Where(s => s.BusinessDate <= t);
        return await q.OrderByDescending(s => s.Id).Take(1000).ToListAsync(ct);
    }

    public async Task<ServiceResult<DoctorSettlement>> PayAsync(int doctorId, SettlementInput i, CancellationToken ct = default)
    {
        if (i.Amount <= 0 || !FinanceMath.IsMoney(i.Amount)) return ServiceResult<DoctorSettlement>.Fail("Enter a valid amount.");
        var method = await _db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == i.PaymentMethodId && m.IsActive, ct);
        if (method is null) return ServiceResult<DoctorSettlement>.Fail("Select the payment method.");
        var reference = string.IsNullOrWhiteSpace(i.PaymentReference) ? null : i.PaymentReference.Trim();
        if (method.RequiresReference && reference is null) return ServiceResult<DoctorSettlement>.Fail($"Enter the {method.Name} reference.");
        if ((reference?.Length ?? 0) > 100 || (i.Notes?.Length ?? 0) > 300) return ServiceResult<DoctorSettlement>.Fail("Reference max 100, notes max 300 characters.");
        var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctorId, ct);
        if (doctor is null) return ServiceResult<DoctorSettlement>.Fail("Doctor not found.");
        var today = await _clock.TodayAsync();
        var paidOn = i.PaidOn ?? today;
        if (paidOn > today) return ServiceResult<DoctorSettlement>.Fail("Payment date cannot be in the future.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"DOCTOR|{doctorId}", ct);
        var payable = await PayableAsync(doctorId, ct);
        if (i.Amount > payable)
            return ServiceResult<DoctorSettlement>.Fail(payable <= 0 ? $"Nothing is payable to {doctor.Name}." : $"At most {payable:N2} is payable to {doctor.Name}.");
        var now = _clock.UtcNow;
        var s = new DoctorSettlement
        {
            SettlementNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.SettlementNumberSequence, "DPY", paidOn, ct),
            DoctorId = doctorId, Amount = i.Amount, PaymentMethodId = method.Id, PaymentReference = reference,
            Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim(), PaidUtc = now, BusinessDate = paidOn,
            RecordedByUserId = _ctx.UserId, RecordedByUsername = _ctx.Username
        };
        _db.DoctorSettlements.Add(s);
        await _db.SaveChangesAsync(ct);
        _db.DoctorLedger.Add(new DoctorLedgerEntry
        {
            DoctorId = doctorId, EntryType = DoctorEntryType.Settlement, Amount = -i.Amount, SettlementId = s.Id, Reference = s.SettlementNumber,
            Description = $"Paid to doctor ({method.Name}{(reference is null ? "" : " " + reference)})", BusinessDate = paidOn, CreatedUtc = now,
            CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        });
        _audit.Add(AuditActions.DoctorPaid, "DoctorSettlement", s.SettlementNumber,
            $"{i.Amount:N2} paid to {doctor.Name} ({method.Name}); payable before {payable:N2}, after {payable - i.Amount:N2}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<DoctorSettlement>.Ok(s);
    }
}
