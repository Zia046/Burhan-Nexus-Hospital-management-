using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Ot;

public sealed record OtBillLine(long ChargeId, string ChargeNumber, DateOnly Date, string Service, OtChargeKind Kind, decimal Fee, decimal Discount, decimal Net, decimal Paid, ChargeStatus Status)
{
    public decimal Balance => Status == ChargeStatus.Cancelled ? 0 : Net - Paid;
}

public interface IOtBillingService
{
    /// <summary>Standard charges that are configured for this surgery and not yet posted.</summary>
    Task<IReadOnlyList<(OtChargeKind Kind, ServiceItem Item)>> PendingStandardAsync(long surgeryId, CancellationToken ct = default);
    Task<ServiceResult<int>> PostStandardAsync(long surgeryId, IReadOnlyCollection<OtChargeKind> kinds, CancellationToken ct = default);
    Task<ServiceResult> AddChargeAsync(long surgeryId, int serviceItemId, int quantity, decimal discount, string? reason, bool canDiscount, CancellationToken ct = default);
    Task<IReadOnlyList<OtBillLine>> BillAsync(long surgeryId, CancellationToken ct = default);
}

/// <summary>
/// OT charges are ordinary Phase 3 charges (linked to the surgery and – for inpatients – to the IPD admission), paid at the existing cashier
/// with the existing receipts. Nothing here takes payments or keeps a ledger. Standard kinds are posted at most once per surgery.
/// </summary>
public sealed class OtBillingService : IOtBillingService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _finance;
    private readonly ISurgeryService _surgeries;

    public OtBillingService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IFinanceSetupService finance, ISurgeryService surgeries)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _finance = finance;
        _surgeries = surgeries;
    }

    public async Task<IReadOnlyList<(OtChargeKind Kind, ServiceItem Item)>> PendingStandardAsync(long surgeryId, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().Include(s => s.Theatre).Include(s => s.Procedure).FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return Array.Empty<(OtChargeKind, ServiceItem)>();
        var s = await _db.OtSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OtSettings();
        var team = await _db.SurgeryTeamMembers.AsNoTracking().Where(m => m.SurgeryId == surgeryId && m.RemovedUtc == null).Select(m => m.Role).ToListAsync(ct);
        var map = new List<(OtChargeKind, int?)>
        {
            (OtChargeKind.Theatre, g.Theatre?.ChargeServiceItemId),
            (OtChargeKind.Procedure, g.Procedure?.ServiceItemId),
            (OtChargeKind.SurgeonFee, team.Contains(OtRole.PrimarySurgeon) ? s.SurgeonFeeItemId : null),
            (OtChargeKind.AssistantFee, team.Contains(OtRole.AssistantSurgeon) ? s.AssistantFeeItemId : null),
            (OtChargeKind.AnesthesiaFee, g.AnesthesiaType is not null || team.Contains(OtRole.Anesthetist) ? s.AnesthesiaFeeItemId : null),
            (OtChargeKind.AnesthesiaSupplies, g.AnesthesiaType is not null ? s.AnesthesiaSuppliesItemId : null),
            (OtChargeKind.Recovery, await _db.RecoveryEpisodes.AnyAsync(r => r.SurgeryId == surgeryId, ct) ? s.RecoveryItemId : null),
            (OtChargeKind.Equipment, s.EquipmentItemId),
            (OtChargeKind.Consumables, s.ConsumablesItemId),
        };
        var posted = await _db.SurgeryCharges.AsNoTracking().Where(c => c.SurgeryId == surgeryId).Select(c => c.Kind).ToListAsync(ct);
        var ids = map.Where(m => m.Item2 is not null && !posted.Contains(m.Item1)).Select(m => m.Item2!.Value).ToList();
        var items = await _db.ServiceItems.AsNoTracking().Where(i => ids.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        return map.Where(m => m.Item2 is int id && items.ContainsKey(id) && !posted.Contains(m.Item1)).Select(m => (m.Item1, items[m.Item2!.Value])).ToList();
    }

    private async Task<Charge> ChargeAsync(Surgery g, ServiceItem svc, int quantity, decimal discount, string? reason, DateOnly date, CancellationToken ct)
    {
        var unit = await _finance.FeeOnAsync(svc.Id, date, ct);
        var fee = unit * quantity;
        var c = new Charge
        {
            ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", date, ct),
            PatientId = g.PatientId, IpdAdmissionId = g.IpdAdmissionId, SurgeryId = g.Id, ServiceItemId = svc.Id, ServiceKind = svc.Kind,
            ServiceName = quantity > 1 ? $"{svc.Name} × {quantity}" : svc.Name, DepartmentId = svc.DepartmentId, Fee = fee, Discount = discount, NetAmount = fee - discount,
            AllowPartialPayment = true, DiscountReason = discount > 0 ? reason : null, DoctorShareType = DoctorShareType.HospitalKeepsAll,
            Status = fee - discount == 0 ? ChargeStatus.Paid : ChargeStatus.Unpaid, BusinessDate = date, CreatedUtc = _clock.UtcNow, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
        };
        _db.Charges.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    public async Task<ServiceResult<int>> PostStandardAsync(long surgeryId, IReadOnlyCollection<OtChargeKind> kinds, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"OTBILL|{surgeryId}", ct);
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult<int>.Fail("Surgery not found.");
        if (g.Status == SurgeryStatus.Cancelled) return ServiceResult<int>.Fail("This surgery was cancelled – no new charges.");
        var pending = (await PendingStandardAsync(surgeryId, ct)).Where(p => kinds.Count == 0 || kinds.Contains(p.Kind)).ToList();
        if (pending.Count == 0) return ServiceResult<int>.Fail("Nothing to post – the configured OT charges are already on the bill.");
        var today = await _clock.TodayAsync();
        foreach (var (kind, item) in pending)
        {
            var c = await ChargeAsync(g, item, 1, 0, null, today, ct);
            _db.SurgeryCharges.Add(new SurgeryCharge { SurgeryId = g.Id, Kind = kind, ChargeId = c.Id, CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username });
        }
        _surgeries.Event(g.Id, "Billing", "Charges posted", null, string.Join(", ", pending.Select(p => p.Kind)), null);
        _audit.Add(AuditActions.OtChargePosted, "Surgery", g.Number, $"{pending.Count} OT charge(s) posted for {g.Number}: {string.Join(", ", pending.Select(p => p.Item.Name))}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_SurgeryCharges_Standard"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult<int>.Fail("These charges were just posted by someone else.");
        }
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(pending.Count);
    }

    public async Task<ServiceResult> AddChargeAsync(long surgeryId, int serviceItemId, int quantity, decimal discount, string? reason, bool canDiscount, CancellationToken ct = default)
    {
        var g = await _db.Surgeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == surgeryId, ct);
        if (g is null) return ServiceResult.Fail("Surgery not found.");
        if (g.Status == SurgeryStatus.Cancelled) return ServiceResult.Fail("This surgery was cancelled – no new charges.");
        var svc = await _db.ServiceItems.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceItemId && s.IsActive, ct);
        if (svc is null) return ServiceResult.Fail("Select an active service.");
        if (svc.Kind == ServiceKind.Pharmacy) return ServiceResult.Fail("Medicines are billed through the Pharmacy patient issue.");
        if (svc.Kind is ServiceKind.Laboratory or ServiceKind.XRay or ServiceKind.Ultrasound
            || await _db.RadiologyServices.AnyAsync(r => r.ServiceItemId == svc.Id, ct) || await _db.LabTests.AnyAsync(t => t.ServiceItemId == svc.Id, ct))
            return ServiceResult.Fail("Investigations are billed when they are booked in the Laboratory / Radiology module.");
        if (quantity is < 1 or > 100) return ServiceResult.Fail("Quantity must be between 1 and 100.");
        if (discount != 0 && !canDiscount) return ServiceResult.Fail("You are not allowed to give discounts.");
        var today = await _clock.TodayAsync();
        var fee = await _finance.FeeOnAsync(svc.Id, today, ct) * quantity;
        if (discount < 0 || discount > fee || !FinanceMath.IsMoney(discount)) return ServiceResult.Fail($"The discount must be between 0 and {fee:N2}.");
        if (discount > 0 && OtSetupService.T(reason, 200) is null) return ServiceResult.Fail("Enter the reason for the discount.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var c = await ChargeAsync(g, svc, quantity, discount, OtSetupService.T(reason, 200), today, ct);
        _db.SurgeryCharges.Add(new SurgeryCharge { SurgeryId = g.Id, Kind = OtChargeKind.Other, ChargeId = c.Id, CreatedUtc = _clock.UtcNow, CreatedBy = _ctx.Username });
        _surgeries.Event(g.Id, "Billing", "Charge added", null, $"{c.ServiceName} {c.NetAmount:N2}", null);
        _audit.Add(AuditActions.OtChargePosted, "Surgery", g.Number, $"{c.ServiceName} {c.NetAmount:N2} added to the OT bill of {g.Number}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<OtBillLine>> BillAsync(long surgeryId, CancellationToken ct = default) =>
        (await (from c in _db.Charges.AsNoTracking()
                join sc in _db.SurgeryCharges.AsNoTracking() on c.Id equals sc.ChargeId into scs
                from sc in scs.DefaultIfEmpty()
                where c.SurgeryId == surgeryId
                orderby c.Id
                select new { c.Id, c.ChargeNumber, c.BusinessDate, c.ServiceName, Kind = (OtChargeKind?)sc.Kind, c.Fee, c.Discount, c.NetAmount, c.PaidAmount, c.Status }).ToListAsync(ct))
        .Select(x => new OtBillLine(x.Id, x.ChargeNumber, x.BusinessDate, x.ServiceName, x.Kind ?? OtChargeKind.Other, x.Fee, x.Discount, x.NetAmount, x.PaidAmount, x.Status)).ToList();
}
