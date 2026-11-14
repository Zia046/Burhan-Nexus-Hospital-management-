using System.Globalization;
using System.Text.Json;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Laboratory;

public sealed class LabBookingInput
{
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public long? EncounterId { get; set; }
    public List<int> TestIds { get; set; } = new();
    public LabReferralType ReferralType { get; set; }
    public int? ReferringDoctorId { get; set; }
    public string? ExternalDoctorName { get; set; }
    public string? ClinicalNotes { get; set; }
    public bool IsUrgent { get; set; }
    /// <summary>Optional discounts per test id (needs the Phase 3 "Give Discounts" permission).</summary>
    public Dictionary<int, decimal> Discounts { get; set; } = new();
    public string? DiscountReason { get; set; }
}

public sealed record LabBookingResult(LabOrder Order, bool WasDuplicate);

public enum LabPaymentState { NotBilled, Free, Unpaid, PartiallyPaid, Paid, Cancelled }

public sealed record LabPayment(LabPaymentState State, decimal Net, decimal Paid, decimal Refunded)
{
    public decimal Balance => Net - Paid;
}

/// <summary>Order summary for work lists and the patient-based results page.</summary>
public sealed record LabOrderSummary(
    long Id, string OrderNumber, DateOnly BusinessDate, DateTime BookedUtc, bool IsUrgent,
    long PatientId, string PatientNumber, string PatientName, string? GuardianName, string AgeGender, string? Mobile, string? ReferredBy,
    int Total, int Cancelled, int Pending, int AwaitingVerification, int Verified, int Released, int Printed,
    IReadOnlyList<LabItemSummary> Items)
{
    public int Completed => AwaitingVerification + Verified + Released;
    public int Active => Total - Cancelled;
    public string VerificationStatus => Active == 0 ? "Cancelled" : Verified + Released == Active ? "All verified" : Verified + Released > 0 ? "Partly verified" : AwaitingVerification > 0 ? "Awaiting verification" : "Not entered";
    public string ReportStatus => Active == 0 ? "—" : Released == Active ? "Released" : Released > 0 ? "Partly released" : Verified > 0 ? "Ready to print" : "Not ready";
}

public sealed record LabItemSummary(long Id, string TestCode, string TestName, string SampleTypeName, LabOrderItemStatus Status, string? CorrectionReason, string? EnteredBy);

public sealed class LabOrderQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Q { get; set; }
    public long? PatientId { get; set; }
    /// <summary>pending | verification | verified | released | all</summary>
    public string? Scope { get; set; }
    public int Take { get; set; } = 300;
}

public sealed record LabDashboard(
    DateOnly From, DateOnly To, int Patients, int Orders, int Tests, int SamplePending, int SampleCollected, int InProgress,
    int AwaitingVerification, int Verified, int Released, int Cancelled, int SampleRejections, int Urgent, decimal? Collection);

public interface ILabService
{
    Task<ServiceResult<LabBookingResult>> BookAsync(LabBookingInput input, bool canDiscount, CancellationToken ct = default);
    Task<ServiceResult> AddTestsAsync(long orderId, IReadOnlyList<int> testIds, CancellationToken ct = default);
    Task<ServiceResult> UpdateOrderAsync(long orderId, LabReferralType referral, int? doctorId, string? externalDoctor, string? notes, bool urgent, CancellationToken ct = default);
    Task<ServiceResult> CancelItemAsync(long itemId, string? reason, CancellationToken ct = default);

    Task<ServiceResult> CollectSampleAsync(long sampleId, bool alsoReceive, CancellationToken ct = default);
    Task<ServiceResult> ReceiveSampleAsync(long sampleId, CancellationToken ct = default);
    Task<ServiceResult> RejectSampleAsync(long sampleId, string? reason, CancellationToken ct = default);

    Task<ServiceResult> SaveResultAsync(long itemId, IReadOnlyDictionary<long, string?> values, string? comment, bool submit, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult<int>> VerifyAsync(IReadOnlyList<long> itemIds, bool canVerifyOwn, CancellationToken ct = default);
    Task<ServiceResult> RejectResultAsync(long itemId, string? reason, CancellationToken ct = default);
    Task<ServiceResult> WithdrawResultAsync(long itemId, string? reason, CancellationToken ct = default);
    Task<ServiceResult<int>> RecordPrintAsync(long orderId, IReadOnlyList<long> itemIds, string format, CancellationToken ct = default);

    Task<LabOrder?> OrderAsync(long id, CancellationToken ct = default);
    Task<LabOrderItem?> ItemAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<LabOrderSummary>> OrdersAsync(LabOrderQuery q, CancellationToken ct = default);
    Task<IReadOnlyList<LabItemEvent>> EventsAsync(long orderId, CancellationToken ct = default);
    Task<IReadOnlyList<LabResultRevision>> RevisionsAsync(long orderId, CancellationToken ct = default);
    Task<IReadOnlyList<LabReportPrint>> PrintsAsync(long orderId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, LabPayment>> PaymentsAsync(long orderId, CancellationToken ct = default);
    Task<LabPayment> OrderPaymentAsync(long orderId, CancellationToken ct = default);
    Task<LabDashboard> DashboardAsync(DateOnly from, DateOnly to, bool includeFinance, CancellationToken ct = default);
}

public sealed class LabService : ILabService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _finance;
    private readonly ILabCatalogService _catalog;

    public LabService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IFinanceSetupService finance, ILabCatalogService catalog)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _finance = finance;
        _catalog = catalog;
    }

    private static string? T(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Length > max ? v.Trim()[..max] : v.Trim();

    private LabItemEvent Event(long itemId, LabEventType type, string? note, DateOnly today) => new()
    {
        OrderItemId = itemId, EventType = type, Note = note is null ? null : note[..Math.Min(500, note.Length)], BusinessDate = today,
        CreatedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username
    };

    // =====================================================================================
    // Booking
    // =====================================================================================

    public async Task<ServiceResult<LabBookingResult>> BookAsync(LabBookingInput i, bool canDiscount, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<LabBookingResult>.Fail("The booking form has expired. Please reload the page.");
        if (await _db.LabOrders.AsNoTracking().FirstOrDefaultAsync(o => o.IdempotencyKey == i.IdempotencyKey, ct) is { } dup)
        {
            await _audit.LogAsync(AuditActions.LabDuplicateOrderBlocked, "LabOrder", dup.OrderNumber, $"Repeated booking of {dup.OrderNumber} ignored (no second order created)", ct: ct);
            return ServiceResult<LabBookingResult>.Ok(new LabBookingResult(dup, true));
        }
        var errors = new List<string>();
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == i.PatientId, ct);
        if (patient is null) return ServiceResult<LabBookingResult>.Fail("Select the patient.");
        if (patient.Status != PatientStatus.Active) errors.Add($"{patient.PatientNumber} is {patient.Status.ToString().ToLowerInvariant()} – tests cannot be booked on this record.");
        var testIds = i.TestIds.Where(t => t > 0).ToList();
        if (testIds.Count == 0) errors.Add("Select at least one test.");
        if (testIds.Count != testIds.Distinct().Count()) errors.Add("A test was selected twice.");
        if (testIds.Count > 40) errors.Add("At most 40 tests per visit.");
        errors.AddRange(await ValidateReferralAsync(i.ReferralType, i.ReferringDoctorId, i.ExternalDoctorName, ct));
        if (i.EncounterId is long eid && !await _db.PatientEncounters.AnyAsync(e => e.Id == eid && e.PatientId == i.PatientId, ct))
            errors.Add("The visit does not belong to this patient.");
        if (i.Discounts.Any(d => d.Value != 0) && !canDiscount) errors.Add("You are not allowed to give discounts.");
        if (i.Discounts.Any(d => d.Value > 0) && T(i.DiscountReason, 200) is null) errors.Add("Enter the reason for the discount.");
        if (errors.Count > 0) return ServiceResult<LabBookingResult>.Fail(errors.ToArray());

        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = new LabOrder
            {
                OrderNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.LabOrderNumberSequence, "LAB", today, ct),
                IdempotencyKey = i.IdempotencyKey, PatientId = patient.Id, EncounterId = i.EncounterId, ReferralType = i.ReferralType,
                ReferringDoctorId = i.ReferralType == LabReferralType.HospitalDoctor ? i.ReferringDoctorId : null,
                ExternalDoctorName = i.ReferralType == LabReferralType.External ? T(i.ExternalDoctorName, 150) : null,
                ClinicalNotes = T(i.ClinicalNotes, 1000), IsUrgent = i.IsUrgent, BusinessDate = today, BookedUtc = now, UpdatedUtc = now,
                BookedByUserId = _ctx.UserId, BookedByUsername = _ctx.Username, Workstation = T(_ctx.Workstation, 100)
            };
            _db.LabOrders.Add(order);
            await _db.SaveChangesAsync(ct);
            var add = await AddItemsAsync(order, patient, testIds, i.Discounts, T(i.DiscountReason, 200), today, ct);
            if (!add.Succeeded)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return ServiceResult<LabBookingResult>.Fail(add.Errors.ToArray());
            }
            _audit.Add(AuditActions.LabOrderBooked, "LabOrder", order.OrderNumber,
                $"Laboratory order {order.OrderNumber} for {patient.PatientNumber}: {string.Join(", ", order.Items.Select(x => x.TestCode))}" +
                (order.ReferredBy is { Length: > 0 } r ? $", referred by {r}" : ""));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<LabBookingResult>.Ok(new LabBookingResult(order, false));
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_LabOrders_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            var existing = await _db.LabOrders.AsNoTracking().FirstAsync(o => o.IdempotencyKey == i.IdempotencyKey, ct);
            return ServiceResult<LabBookingResult>.Ok(new LabBookingResult(existing, true));
        }
    }

    private async Task<List<string>> ValidateReferralAsync(LabReferralType type, int? doctorId, string? external, CancellationToken ct)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(type)) errors.Add("Select the referral.");
        if (type == LabReferralType.HospitalDoctor && (doctorId is null || !await _db.Doctors.AnyAsync(d => d.Id == doctorId, ct))) errors.Add("Select the referring hospital doctor.");
        if (type == LabReferralType.External && T(external, 150) is null) errors.Add("Enter the external referring doctor's name.");
        return errors;
    }

    /// <summary>Adds tests to an order: definition + reference-range snapshot, shared samples per sample type and a Phase 3 charge per test.</summary>
    private async Task<ServiceResult> AddItemsAsync(LabOrder order, Patient patient, IReadOnlyList<int> testIds, IReadOnlyDictionary<int, decimal> discounts,
        string? discountReason, DateOnly today, CancellationToken ct)
    {
        var tests = await _db.LabTests.AsNoTracking().Include(t => t.Category).Include(t => t.SampleType).Include(t => t.ServiceItem)
            .Include(t => t.Parameters.Where(p => p.IsActive).OrderBy(p => p.SortOrder).ThenBy(p => p.Id)).ThenInclude(p => p.Ranges)
            .AsSplitQuery().Where(t => testIds.Contains(t.Id)).ToListAsync(ct);
        var errors = new List<string>();
        foreach (var id in testIds)
        {
            var t = tests.FirstOrDefault(x => x.Id == id);
            if (t is null || !t.IsActive) errors.Add("A selected test is not available.");
            else if (t.Parameters.Count == 0) errors.Add($"{t.Name} has no result parameters yet – complete its definition first.");
        }
        if (await _db.LabOrderItems.AnyAsync(x => x.LabOrderId == order.Id && testIds.Contains(x.LabTestId) && x.Status != LabOrderItemStatus.Cancelled, ct))
            errors.Add("A selected test is already booked in this visit.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.Distinct().ToArray());

        var now = _clock.UtcNow;
        var ageDays = patient.DateOfBirth is { } dob ? today.DayNumber - dob.DayNumber : (int?)null;
        var samples = await _db.LabSamples.Where(s => s.LabOrderId == order.Id && s.Status != LabSampleStatus.Rejected).ToListAsync(ct);
        var sort = await _db.LabOrderItems.Where(x => x.LabOrderId == order.Id).MaxAsync(x => (int?)x.SortOrder, ct) ?? 0;
        var newItems = new List<(LabOrderItem Item, LabTest Test)>();
        foreach (var t in testIds.Select(id => tests.First(x => x.Id == id)).OrderBy(x => x.Category!.SortOrder).ThenBy(x => x.SortOrder))
        {
            // One sample per sample type and visit (unless it was rejected).
            var sample = samples.FirstOrDefault(s => s.SampleTypeName == t.SampleType!.Name && s.Status == LabSampleStatus.Pending)
                         ?? samples.FirstOrDefault(s => s.SampleTypeName == t.SampleType!.Name);
            if (sample is null)
            {
                sample = new LabSample
                {
                    SampleNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.LabSampleNumberSequence, "SMP", today, ct),
                    LabOrderId = order.Id, SampleTypeName = t.SampleType!.Name, CreatedUtc = now, BusinessDate = today
                };
                _db.LabSamples.Add(sample);
                samples.Add(sample);
            }
            var item = new LabOrderItem
            {
                LabOrderId = order.Id, LabTestId = t.Id, TestCode = t.Code, TestName = t.Name, CategoryName = t.Category!.Name, SampleTypeName = t.SampleType!.Name,
                Method = t.Method, DefinitionVersion = t.DefinitionVersion, SortOrder = sort += 10, Sample = sample, Comment = t.Interpretation,
                Status = sample.Status switch
                {
                    LabSampleStatus.Received => LabOrderItemStatus.SampleReceived,
                    LabSampleStatus.Collected => LabOrderItemStatus.SampleCollected,
                    _ => LabOrderItemStatus.SamplePending
                }
            };
            foreach (var p in t.Parameters)
            {
                var range = ResolveRange(p.Ranges, patient.Gender, ageDays);
                item.Values.Add(new LabResultValue
                {
                    ParameterId = p.Id, Name = p.Name, Unit = p.Unit, ResultType = p.ResultType, Options = p.Options, Decimals = p.Decimals,
                    IsOptional = p.IsOptional, SortOrder = p.SortOrder, RefLow = range?.Low, RefHigh = range?.High, CriticalLow = range?.CriticalLow,
                    CriticalHigh = range?.CriticalHigh, RefText = range?.NormalText, Value = p.DefaultValue
                });
            }
            order.Items.Add(item);
            _db.LabOrderItems.Add(item);
            newItems.Add((item, t));
        }
        await _db.SaveChangesAsync(ct);

        // Phase 3 billing: one charge (bill line) per test at today's price; paid later at any cash counter.
        foreach (var (item, t) in newItems)
        {
            var fee = await _finance.FeeOnAsync(t.ServiceItemId, today, ct);
            var discount = Math.Clamp(discounts.GetValueOrDefault(t.Id), 0, fee);
            if (!FinanceMath.IsMoney(discount)) return ServiceResult.Fail("Discounts may have at most 2 decimals.");
            var charge = new Charge
            {
                ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", today, ct),
                PatientId = patient.Id, EncounterId = order.EncounterId, LabOrderId = order.Id, ServiceItemId = t.ServiceItemId, ServiceKind = ServiceKind.Laboratory,
                ServiceName = t.Name, DepartmentId = t.ServiceItem!.DepartmentId, Fee = fee, Discount = discount, NetAmount = fee - discount,
                AllowPartialPayment = t.ServiceItem.AllowPartialPayment, DiscountReason = discount > 0 ? discountReason : null,
                DoctorShareType = DoctorShareType.HospitalKeepsAll, Status = fee - discount == 0 ? ChargeStatus.Paid : ChargeStatus.Unpaid,
                BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
            };
            _db.Charges.Add(charge);
            await _db.SaveChangesAsync(ct);
            item.ChargeId = charge.Id;
            _db.LabItemEvents.Add(Event(item.Id, LabEventType.Booked, $"{order.OrderNumber} · fee {fee:N2}{(discount > 0 ? $" − discount {discount:N2}" : "")}", today));
        }
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    /// <summary>Most specific matching range: gender-specific beats "both", narrower age band beats wider.</summary>
    public static LabReferenceRange? ResolveRange(IEnumerable<LabReferenceRange> ranges, Gender gender, int? ageDays) =>
        ranges.Where(r => (r.Gender is null || r.Gender == gender)
                          && (r.MinAgeDays is null || (ageDays is int a && a >= r.MinAgeDays))
                          && (r.MaxAgeDays is null || (ageDays is int b && b < r.MaxAgeDays)))
            .OrderByDescending(r => r.Gender is null ? 0 : 1)
            .ThenBy(r => (r.MaxAgeDays ?? 100_000) - (r.MinAgeDays ?? 0))
            .FirstOrDefault();

    public async Task<ServiceResult> AddTestsAsync(long orderId, IReadOnlyList<int> testIds, CancellationToken ct = default)
    {
        if (testIds.Count == 0) return ServiceResult.Fail("Select at least one test.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"LABORDER|{orderId}", ct);
        var order = await _db.LabOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return ServiceResult.Fail("Order not found.");
        var patient = await _db.Patients.AsNoTracking().FirstAsync(p => p.Id == order.PatientId, ct);
        var today = await _clock.TodayAsync();
        var r = await AddItemsAsync(order, patient, testIds, new Dictionary<int, decimal>(), null, today, ct);
        if (!r.Succeeded) return r;
        order.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabOrderEdited, "LabOrder", order.OrderNumber, $"Tests added to {order.OrderNumber}: {string.Join(", ", order.Items.Select(x => x.TestCode))}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateOrderAsync(long orderId, LabReferralType referral, int? doctorId, string? externalDoctor, string? notes, bool urgent, CancellationToken ct = default)
    {
        var errors = await ValidateReferralAsync(referral, doctorId, externalDoctor, ct);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var o = await _db.LabOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null) return ServiceResult.Fail("Order not found.");
        var old = new { o.ReferralType, o.ReferringDoctorId, o.ExternalDoctorName, o.ClinicalNotes, o.IsUrgent };
        o.ReferralType = referral;
        o.ReferringDoctorId = referral == LabReferralType.HospitalDoctor ? doctorId : null;
        o.ExternalDoctorName = referral == LabReferralType.External ? T(externalDoctor, 150) : null;
        o.ClinicalNotes = T(notes, 1000);
        o.IsUrgent = urgent;
        o.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.LabOrderEdited, "LabOrder", o.OrderNumber, $"Order {o.OrderNumber} details changed", old,
            new { o.ReferralType, o.ReferringDoctorId, o.ExternalDoctorName, o.ClinicalNotes, o.IsUrgent });
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelItemAsync(long itemId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for cancelling the test.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var item = await _db.LabOrderItems.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return ServiceResult.Fail("Test not found.");
        if (item.Status == LabOrderItemStatus.Cancelled) return ServiceResult.Fail("This test is already cancelled.");
        if (item.Status is LabOrderItemStatus.Verified or LabOrderItemStatus.Released)
            return ServiceResult.Fail("A verified result cannot be cancelled. Withdraw the result first (with a reason) if it is wrong.");
        var today = await _clock.TodayAsync();
        item.Status = LabOrderItemStatus.Cancelled;
        item.CancelledUtc = _clock.UtcNow;
        item.CancelledByUsername = _ctx.Username;
        item.CancelReason = reason;
        var note = $"Cancelled: {reason}";
        if (item.ChargeId is long cid)
        {
            var charge = await _db.Charges.FromSqlRaw("SELECT c.*, c.xmin FROM \"Charges\" c WHERE c.\"Id\" = {0} FOR UPDATE", cid).FirstAsync(ct);
            if (charge.PaidAmount == 0 && charge.Status != ChargeStatus.Cancelled) charge.Status = ChargeStatus.Cancelled;
            else if (charge.PaidAmount > 0) note += $" – {charge.PaidAmount:N2} was paid: refund it from the receipt (Admin refund)";
        }
        _db.LabItemEvents.Add(Event(item.Id, LabEventType.Cancelled, note, today));
        _audit.Add(AuditActions.LabTestCancelled, "LabOrder", item.Order!.OrderNumber, $"{item.TestName} in {item.Order.OrderNumber} cancelled: {reason}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The test was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Samples
    // =====================================================================================

    public async Task<ServiceResult> CollectSampleAsync(long sampleId, bool alsoReceive, CancellationToken ct = default)
    {
        var s = await _db.LabSamples.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == sampleId, ct);
        if (s is null) return ServiceResult.Fail("Sample not found.");
        if (s.Status != LabSampleStatus.Pending) return ServiceResult.Fail($"Sample {s.SampleNumber} is already {s.Status.ToString().ToLowerInvariant()}.");
        var now = _clock.UtcNow;
        var today = await _clock.TodayAsync();
        s.Status = alsoReceive ? LabSampleStatus.Received : LabSampleStatus.Collected;
        s.CollectedUtc = now;
        s.CollectedByUsername = _ctx.Username;
        if (alsoReceive) { s.ReceivedUtc = now; s.ReceivedByUsername = _ctx.Username; }
        var items = await _db.LabOrderItems.Where(x => x.SampleId == s.Id && x.Status == LabOrderItemStatus.SamplePending).ToListAsync(ct);
        foreach (var i in items)
        {
            i.Status = alsoReceive ? LabOrderItemStatus.SampleReceived : LabOrderItemStatus.SampleCollected;
            _db.LabItemEvents.Add(Event(i.Id, LabEventType.SampleCollected, $"{s.SampleNumber} ({s.SampleTypeName}) collected" + (alsoReceive ? " and received" : ""), today));
        }
        _audit.Add(AuditActions.LabSampleCollected, "LabSample", s.SampleNumber, $"Sample {s.SampleNumber} ({s.SampleTypeName}) of {s.Order!.OrderNumber} collected{(alsoReceive ? " and received" : "")}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReceiveSampleAsync(long sampleId, CancellationToken ct = default)
    {
        var s = await _db.LabSamples.FirstOrDefaultAsync(x => x.Id == sampleId, ct);
        if (s is null) return ServiceResult.Fail("Sample not found.");
        if (s.Status != LabSampleStatus.Collected) return ServiceResult.Fail("Only a collected sample can be received in the laboratory.");
        var today = await _clock.TodayAsync();
        s.Status = LabSampleStatus.Received;
        s.ReceivedUtc = _clock.UtcNow;
        s.ReceivedByUsername = _ctx.Username;
        foreach (var i in await _db.LabOrderItems.Where(x => x.SampleId == s.Id && x.Status == LabOrderItemStatus.SampleCollected).ToListAsync(ct))
        {
            i.Status = LabOrderItemStatus.SampleReceived;
            _db.LabItemEvents.Add(Event(i.Id, LabEventType.SampleReceived, $"{s.SampleNumber} received in laboratory", today));
        }
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RejectSampleAsync(long sampleId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for rejecting the sample (e.g. haemolysed, clotted, insufficient).");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var s = await _db.LabSamples.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == sampleId, ct);
        if (s is null) return ServiceResult.Fail("Sample not found.");
        if (s.Status is LabSampleStatus.Rejected or LabSampleStatus.Pending) return ServiceResult.Fail("Only a collected sample can be rejected.");
        var items = await _db.LabOrderItems.Where(x => x.SampleId == s.Id && x.Status != LabOrderItemStatus.Cancelled).ToListAsync(ct);
        if (items.Any(x => x.Status >= LabOrderItemStatus.AwaitingVerification))
            return ServiceResult.Fail("Results were already entered from this sample. Withdraw those results first.");
        var now = _clock.UtcNow;
        var today = await _clock.TodayAsync();
        var replacement = new LabSample
        {
            SampleNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.LabSampleNumberSequence, "SMP", today, ct),
            LabOrderId = s.LabOrderId, SampleTypeName = s.SampleTypeName, CreatedUtc = now, BusinessDate = today
        };
        _db.LabSamples.Add(replacement);
        await _db.SaveChangesAsync(ct);
        s.Status = LabSampleStatus.Rejected;
        s.RejectedUtc = now;
        s.RejectedByUsername = _ctx.Username;
        s.RejectReason = reason;
        s.ReplacedBySampleId = replacement.Id;
        foreach (var i in items)
        {
            i.SampleId = replacement.Id;
            i.Status = LabOrderItemStatus.SamplePending;
            _db.LabItemEvents.Add(Event(i.Id, LabEventType.SampleRejected, $"{s.SampleNumber} rejected: {reason}. New sample {replacement.SampleNumber} requested.", today));
        }
        _audit.Add(AuditActions.LabSampleRejected, "LabSample", s.SampleNumber, $"Sample {s.SampleNumber} of {s.Order!.OrderNumber} rejected: {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Results
    // =====================================================================================

    public static (LabFlag Flag, decimal? Number, string? Error) Evaluate(LabResultValue v, string? raw)
    {
        raw = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (raw is null) return (LabFlag.None, null, null);
        switch (v.ResultType)
        {
            case LabResultType.Numeric:
                var normalized = raw.Replace(',', '.');
                var prefix = normalized.StartsWith('<') || normalized.StartsWith('>') ? normalized[0].ToString() : "";
                if (!decimal.TryParse(normalized.TrimStart('<', '>').Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) || Math.Abs(n) > 1_000_000_000m)
                    return (LabFlag.None, null, $"{v.Name}: enter a number (e.g. 7.3).");
                var flag = LabFlag.None;
                if (v.CriticalLow is { } cl && n < cl) flag = LabFlag.CriticalLow;
                else if (v.CriticalHigh is { } ch && n > ch) flag = LabFlag.CriticalHigh;
                else if (v.RefLow is { } l && (n < l || (prefix == "<" && n <= l))) flag = LabFlag.Low;
                else if (v.RefHigh is { } h && (n > h || (prefix == ">" && n >= h))) flag = LabFlag.High;
                return (flag, n, null);
            case LabResultType.PositiveNegative:
                var pn = raw.Equals("positive", StringComparison.OrdinalIgnoreCase) || raw == "+" ? "Positive"
                    : raw.Equals("negative", StringComparison.OrdinalIgnoreCase) || raw == "-" ? "Negative" : null;
                if (pn is null) return (LabFlag.None, null, $"{v.Name}: choose Positive or Negative.");
                return (v.RefText is { } exp && !exp.Equals(pn, StringComparison.OrdinalIgnoreCase) ? LabFlag.Abnormal : LabFlag.None, null, null);
            case LabResultType.Options:
                var choices = (v.Options ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!choices.Contains(raw, StringComparer.OrdinalIgnoreCase)) return (LabFlag.None, null, $"{v.Name}: choose one of {string.Join(", ", choices)}.");
                return (v.RefText is { } e2 && !e2.Equals(raw, StringComparison.OrdinalIgnoreCase) ? LabFlag.Abnormal : LabFlag.None, null, null);
            default:
                return (LabFlag.None, null, raw.Length > 200 ? $"{v.Name}: at most 200 characters." : null);
        }
    }

    private static string Normalise(LabResultValue v, string raw) => v.ResultType switch
    {
        LabResultType.PositiveNegative => raw.Equals("positive", StringComparison.OrdinalIgnoreCase) || raw == "+" ? "Positive" : "Negative",
        LabResultType.Options => (v.Options ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First(o => o.Equals(raw, StringComparison.OrdinalIgnoreCase)),
        LabResultType.Numeric => raw.Replace(',', '.'),
        _ => raw,
    };

    public async Task<ServiceResult> SaveResultAsync(long itemId, IReadOnlyDictionary<long, string?> values, string? comment, bool submit, uint rowVersion, CancellationToken ct = default)
    {
        var item = await _db.LabOrderItems.Include(x => x.Values).Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return ServiceResult.Fail("Test not found.");
        switch (item.Status)
        {
            case LabOrderItemStatus.SamplePending: return ServiceResult.Fail("The sample has not been collected yet. Collect the sample first.");
            case LabOrderItemStatus.Cancelled: return ServiceResult.Fail("This test was cancelled.");
            case LabOrderItemStatus.Verified or LabOrderItemStatus.Released: return ServiceResult.Fail("This result is verified and cannot be changed. Withdraw it with a reason if it is wrong.");
        }
        _db.Entry(item).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var errors = new List<string>();
        var anyValue = false;
        foreach (var v in item.Values)
        {
            values.TryGetValue(v.Id, out var raw);
            raw = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            var (flag, number, error) = Evaluate(v, raw);
            if (error is not null) { errors.Add(error); continue; }
            if (raw is null && submit && !v.IsOptional) { errors.Add($"{v.Name}: enter the result."); continue; }
            anyValue |= raw is not null;
            v.Value = raw is null ? null : Normalise(v, raw);
            v.NumericValue = number;
            v.Flag = flag;
        }
        if (!anyValue && errors.Count == 0) errors.Add("Enter at least one result.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var today = await _clock.TodayAsync();
        var wasSubmitted = item.Status == LabOrderItemStatus.AwaitingVerification;
        item.Comment = T(comment, 1000);
        if (submit)
        {
            item.Status = LabOrderItemStatus.AwaitingVerification;
            item.ResultEnteredUtc = _clock.UtcNow;
            item.ResultEnteredByUserId = _ctx.UserId;
            item.ResultEnteredByUsername = _ctx.Username;
            item.CorrectionReason = null;
        }
        else if (!wasSubmitted) item.Status = LabOrderItemStatus.InProgress;
        var summary = string.Join("; ", item.Values.Where(v => v.Value is not null).Select(v => $"{v.Name} {v.Value}{(v.Flag == LabFlag.None ? "" : " " + v.Flag)}"));
        _db.LabItemEvents.Add(Event(item.Id, wasSubmitted ? LabEventType.ResultEdited : submit ? LabEventType.ResultSubmitted : LabEventType.ResultSaved, summary, today));
        _audit.Add(AuditActions.LabResultSaved, "LabOrder", item.Order!.OrderNumber,
            $"{item.TestName} of {item.Order.OrderNumber}: result {(wasSubmitted ? "edited" : submit ? "entered (awaiting verification)" : "saved as draft")}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The result was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<int>> VerifyAsync(IReadOnlyList<long> itemIds, bool canVerifyOwn, CancellationToken ct = default)
    {
        if (itemIds.Count == 0) return ServiceResult<int>.Fail("Select the results to verify.");
        var settings = await _catalog.SettingsAsync(ct);
        var me = _ctx.UserId;
        var user = me is int uid ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct) : null;
        var items = await _db.LabOrderItems.Include(x => x.Order).Where(x => itemIds.Contains(x.Id)).ToListAsync(ct);
        var today = await _clock.TodayAsync();
        var verified = 0;
        var skipped = new List<string>();
        foreach (var item in items)
        {
            // Only completed results can be approved; anything else is left untouched.
            if (item.Status != LabOrderItemStatus.AwaitingVerification) { skipped.Add($"{item.TestName} ({Label(item.Status)})"); continue; }
            if (item.ResultEnteredByUserId == me && !(settings.AllowSelfVerification && canVerifyOwn))
            {
                skipped.Add($"{item.TestName} (you entered this result – another person must verify it)");
                continue;
            }
            item.Status = LabOrderItemStatus.Verified;
            item.VerifiedUtc = _clock.UtcNow;
            item.VerifiedByUserId = me;
            item.VerifiedByUsername = _ctx.Username;
            item.VerifiedByName = user?.FullName ?? _ctx.Username;
            _db.LabItemEvents.Add(Event(item.Id, LabEventType.Verified, null, today));
            _audit.Add(AuditActions.LabResultVerified, "LabOrder", item.Order!.OrderNumber, $"{item.TestName} of {item.Order.OrderNumber} verified");
            verified++;
        }
        if (verified == 0)
            return ServiceResult<int>.Fail(skipped.Count == 0 ? "Nothing to verify." : "Not verified: " + string.Join(", ", skipped) + ".");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult<int>.Fail("A result was changed by someone else. Please reload."); }
        return ServiceResult<int>.Ok(verified);
    }

    public async Task<ServiceResult> RejectResultAsync(long itemId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for sending the result back.");
        var item = await _db.LabOrderItems.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return ServiceResult.Fail("Test not found.");
        if (item.Status != LabOrderItemStatus.AwaitingVerification) return ServiceResult.Fail("Only results awaiting verification can be rejected.");
        item.Status = LabOrderItemStatus.InProgress;
        item.CorrectionReason = reason;
        _db.LabItemEvents.Add(Event(item.Id, LabEventType.VerificationRejected, reason, await _clock.TodayAsync()));
        _audit.Add(AuditActions.LabResultRejected, "LabOrder", item.Order!.OrderNumber, $"{item.TestName} of {item.Order.OrderNumber} sent back for correction: {reason}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> WithdrawResultAsync(long itemId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for withdrawing the result.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var item = await _db.LabOrderItems.Include(x => x.Values).Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return ServiceResult.Fail("Test not found.");
        if (item.Status is not (LabOrderItemStatus.AwaitingVerification or LabOrderItemStatus.Verified or LabOrderItemStatus.Released))
            return ServiceResult.Fail("Only an entered or verified result can be withdrawn.");
        var previous = item.Status;
        var snapshot = JsonSerializer.Serialize(new
        {
            item.TestCode, item.TestName, Status = previous.ToString(), item.Comment, item.ResultEnteredUtc, item.ResultEnteredByUsername,
            item.VerifiedUtc, item.VerifiedByUsername, item.ReleasedUtc,
            Values = item.Values.OrderBy(v => v.SortOrder).Select(v => new { v.Name, v.Value, v.Unit, Reference = v.ReferenceDisplay, Flag = v.Flag.ToString() })
        });
        item.RevisionNo++;
        _db.LabResultRevisions.Add(new LabResultRevision
        {
            OrderItemId = item.Id, RevisionNo = item.RevisionNo, Reason = reason, Snapshot = snapshot, PreviousStatus = previous,
            CreatedUtc = _clock.UtcNow, CreatedByUsername = _ctx.Username
        });
        item.Status = LabOrderItemStatus.InProgress;
        item.VerifiedUtc = null; item.VerifiedByUserId = null; item.VerifiedByUsername = null; item.VerifiedByName = null; item.ReleasedUtc = null;
        item.CorrectionReason = "Withdrawn: " + reason;
        await _db.SaveChangesAsync(ct); // status first – the database only allows value changes of unverified results
        // Values stay (pre-filled for correction) – the technician changes what was wrong and submits again.
        _db.LabItemEvents.Add(Event(item.Id, LabEventType.ResultWithdrawn, $"Revision {item.RevisionNo}: {reason}", await _clock.TodayAsync()));
        _audit.Add(AuditActions.LabResultWithdrawn, "LabOrder", item.Order!.OrderNumber,
            $"{item.TestName} of {item.Order.OrderNumber} ({previous}) withdrawn – revision {item.RevisionNo} kept: {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<int>> RecordPrintAsync(long orderId, IReadOnlyList<long> itemIds, string format, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"LABPRINT|{orderId}", ct);
        var order = await _db.LabOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return ServiceResult<int>.Fail("Order not found.");
        var items = order.Items.Where(i => itemIds.Contains(i.Id)).ToList();
        if (items.Count == 0 || items.Any(i => i.Status is not (LabOrderItemStatus.Verified or LabOrderItemStatus.Released)))
            return ServiceResult<int>.Fail("Only verified results can be printed as a final report.");
        var settings = await _catalog.SettingsAsync(ct);
        if (settings.RequirePaymentBeforeRelease)
        {
            var pay = await PaymentsAsync(orderId, ct);
            if (items.Any(i => pay.TryGetValue(i.Id, out var p) && p.State is LabPaymentState.Unpaid or LabPaymentState.PartiallyPaid))
                return ServiceResult<int>.Fail("Payment is outstanding. The laboratory settings require full payment before the report is released.");
        }
        var today = await _clock.TodayAsync();
        var now = _clock.UtcNow;
        var copy = await _db.LabReportPrints.CountAsync(p => p.LabOrderId == orderId, ct) + 1;
        foreach (var i in items.Where(i => i.Status == LabOrderItemStatus.Verified))
        {
            i.Status = LabOrderItemStatus.Released;
            i.ReleasedUtc = now;
            _db.LabItemEvents.Add(Event(i.Id, LabEventType.Released, $"Report released (copy {copy})", today));
        }
        foreach (var i in items) _db.LabItemEvents.Add(Event(i.Id, LabEventType.Printed, $"Copy {copy}, {format}", today));
        _db.LabReportPrints.Add(new LabReportPrint
        {
            LabOrderId = orderId, ItemIds = string.Join(",", items.Select(i => i.Id)), CopyNumber = copy, Format = format, PrintedUtc = now,
            PrintedByUsername = _ctx.Username, Workstation = T(_ctx.Workstation, 100)
        });
        _audit.Add(copy == 1 ? AuditActions.LabReportPrinted : AuditActions.LabReportReprinted, "LabOrder", order.OrderNumber,
            $"Laboratory report {order.OrderNumber} {(copy == 1 ? "printed" : $"reprinted – copy {copy}")}: {string.Join(", ", items.Select(i => i.TestCode))}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(copy);
    }

    /// <summary>Tests not yet performed (statuses are stored as text, so compare with a list, not with &lt;).</summary>
    public static readonly LabOrderItemStatus[] OpenStatuses =
        { LabOrderItemStatus.SamplePending, LabOrderItemStatus.SampleCollected, LabOrderItemStatus.SampleReceived, LabOrderItemStatus.InProgress };

    public static string Label(LabOrderItemStatus s) => s switch
    {
        LabOrderItemStatus.SamplePending => "Sample pending",
        LabOrderItemStatus.SampleCollected => "Sample collected",
        LabOrderItemStatus.SampleReceived => "Sample received",
        LabOrderItemStatus.InProgress => "In progress",
        LabOrderItemStatus.AwaitingVerification => "Awaiting verification",
        LabOrderItemStatus.Verified => "Verified",
        LabOrderItemStatus.Released => "Report released",
        _ => "Cancelled",
    };

    // =====================================================================================
    // Queries
    // =====================================================================================

    public async Task<LabOrder?> OrderAsync(long id, CancellationToken ct = default) =>
        await _db.LabOrders.AsNoTracking().Include(o => o.Patient).Include(o => o.ReferringDoctor)
            .Include(o => o.Items.OrderBy(i => i.SortOrder)).ThenInclude(i => i.Values.OrderBy(v => v.SortOrder))
            .Include(o => o.Items).ThenInclude(i => i.Sample)
            .Include(o => o.Samples.OrderBy(s => s.Id))
            .AsSplitQuery().FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<LabOrderItem?> ItemAsync(long id, CancellationToken ct = default) =>
        await _db.LabOrderItems.AsNoTracking().Include(i => i.Values.OrderBy(v => v.SortOrder)).Include(i => i.Sample).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<LabItemEvent>> EventsAsync(long orderId, CancellationToken ct = default)
    {
        var ids = await _db.LabOrderItems.Where(i => i.LabOrderId == orderId).Select(i => i.Id).ToListAsync(ct);
        return await _db.LabItemEvents.AsNoTracking().Where(e => ids.Contains(e.OrderItemId)).OrderByDescending(e => e.Id).Take(500).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LabResultRevision>> RevisionsAsync(long orderId, CancellationToken ct = default)
    {
        var ids = await _db.LabOrderItems.Where(i => i.LabOrderId == orderId).Select(i => i.Id).ToListAsync(ct);
        return await _db.LabResultRevisions.AsNoTracking().Where(r => ids.Contains(r.OrderItemId)).OrderBy(r => r.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LabReportPrint>> PrintsAsync(long orderId, CancellationToken ct = default) =>
        await _db.LabReportPrints.AsNoTracking().Where(p => p.LabOrderId == orderId).OrderBy(p => p.CopyNumber).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<long, LabPayment>> PaymentsAsync(long orderId, CancellationToken ct = default)
    {
        var rows = await _db.LabOrderItems.AsNoTracking().Where(i => i.LabOrderId == orderId)
            .Select(i => new { i.Id, i.Status, Net = (decimal?)i.Charge!.NetAmount, Paid = (decimal?)i.Charge.PaidAmount, Refunded = (decimal?)i.Charge.RefundedAmount, ChargeStatus = (ChargeStatus?)i.Charge.Status })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => new LabPayment(
            r.Net is null ? LabPaymentState.NotBilled
            : r.ChargeStatus == ChargeStatus.Cancelled ? LabPaymentState.Cancelled
            : r.Net == 0 ? LabPaymentState.Free
            : r.Paid >= r.Net ? LabPaymentState.Paid
            : r.Paid > 0 ? LabPaymentState.PartiallyPaid : LabPaymentState.Unpaid,
            r.Net ?? 0, r.Paid ?? 0, r.Refunded ?? 0));
    }

    public async Task<LabPayment> OrderPaymentAsync(long orderId, CancellationToken ct = default)
    {
        var p = (await PaymentsAsync(orderId, ct)).Values.Where(x => x.State != LabPaymentState.Cancelled).ToList();
        var net = p.Sum(x => x.Net);
        var paid = p.Sum(x => x.Paid);
        var state = p.Count == 0 ? LabPaymentState.Cancelled : net == 0 ? LabPaymentState.Free : paid >= net ? LabPaymentState.Paid : paid > 0 ? LabPaymentState.PartiallyPaid : LabPaymentState.Unpaid;
        return new LabPayment(state, net, paid, p.Sum(x => x.Refunded));
    }

    public async Task<IReadOnlyList<LabOrderSummary>> OrdersAsync(LabOrderQuery q, CancellationToken ct = default)
    {
        var x = _db.LabOrders.AsNoTracking().AsQueryable();
        if (q.From is { } f) x = x.Where(o => o.BusinessDate >= f);
        if (q.To is { } t) x = x.Where(o => o.BusinessDate <= t);
        if (q.PatientId is long pid) x = x.Where(o => o.PatientId == pid);
        x = q.Scope switch
        {
            "pending" => x.Where(o => o.Items.Any(i => OpenStatuses.Contains(i.Status))),
            "samples" => x.Where(o => o.Items.Any(i => i.Status == LabOrderItemStatus.SamplePending)),
            "verification" => x.Where(o => o.Items.Any(i => i.Status == LabOrderItemStatus.AwaitingVerification)),
            "verified" => x.Where(o => o.Items.Any(i => i.Status == LabOrderItemStatus.Verified)),
            "released" => x.Where(o => o.Items.Any(i => i.Status == LabOrderItemStatus.Released)),
            _ => x,
        };
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var s = q.Q.Trim();
            var like = "%" + s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var digits = new string(s.Where(char.IsDigit).ToArray());
            x = x.Where(o => o.OrderNumber == s.ToUpper() || o.Patient!.PatientNumber == s.ToUpper() || EF.Functions.ILike(o.Patient.FullName, like)
                             || (digits.Length >= 4 && o.Patient.MobileNumber != null && o.Patient.MobileNumber.Contains(digits)));
        }
        var orderBy = q.Scope is "pending" or "samples" or "verification" ? x.OrderByDescending(o => o.IsUrgent).ThenBy(o => o.Id) : x.OrderByDescending(o => o.Id);
        var rows = await orderBy.Take(Math.Clamp(q.Take, 1, 2000)).Select(o => new
        {
            o.Id, o.OrderNumber, o.BusinessDate, o.BookedUtc, o.IsUrgent, o.PatientId, o.Patient!.PatientNumber, o.Patient.FullName, o.Patient.GuardianName,
            o.Patient.Gender, o.Patient.DateOfBirth, o.Patient.MobileNumber, o.ReferralType, Doctor = o.ReferringDoctor != null ? o.ReferringDoctor.Name : null, o.ExternalDoctorName,
            Printed = _db.LabReportPrints.Count(p => p.LabOrderId == o.Id),
            Items = o.Items.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.TestCode, i.TestName, i.SampleTypeName, i.Status, i.CorrectionReason, i.ResultEnteredByUsername }).ToList()
        }).ToListAsync(ct);
        var today = await _clock.TodayAsync();
        return rows.Select(r =>
        {
            var age = r.DateOfBirth is { } dob ? AgeCalculator.FromDateOfBirth(dob, today).Short : "-";
            var g = r.Gender switch { Gender.Male => "M", Gender.Female => "F", Gender.Other => "O", _ => "-" };
            var items = r.Items.Select(i => new LabItemSummary(i.Id, i.TestCode, i.TestName, i.SampleTypeName, i.Status, i.CorrectionReason, i.ResultEnteredByUsername)).ToList();
            return new LabOrderSummary(r.Id, r.OrderNumber, r.BusinessDate, r.BookedUtc, r.IsUrgent, r.PatientId, r.PatientNumber, r.FullName, r.GuardianName,
                $"{age} / {g}", r.MobileNumber, r.ReferralType == LabReferralType.HospitalDoctor ? r.Doctor : r.ReferralType == LabReferralType.External ? r.ExternalDoctorName : null,
                items.Count, items.Count(i => i.Status == LabOrderItemStatus.Cancelled), items.Count(i => i.Status <= LabOrderItemStatus.InProgress),
                items.Count(i => i.Status == LabOrderItemStatus.AwaitingVerification), items.Count(i => i.Status == LabOrderItemStatus.Verified),
                items.Count(i => i.Status == LabOrderItemStatus.Released), r.Printed, items);
        }).ToList();
    }

    public async Task<LabDashboard> DashboardAsync(DateOnly from, DateOnly to, bool includeFinance, CancellationToken ct = default)
    {
        var orders = _db.LabOrders.AsNoTracking().Where(o => o.BusinessDate >= from && o.BusinessDate <= to);
        var items = _db.LabOrderItems.AsNoTracking().Where(i => i.Order!.BusinessDate >= from && i.Order.BusinessDate <= to);
        var byStatus = await items.GroupBy(i => i.Status).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        int S(LabOrderItemStatus s) => byStatus.GetValueOrDefault(s);
        decimal? collection = null;
        if (includeFinance)
            collection = await _db.ReceiptLines.Where(l => l.Charge!.LabOrderId != null && l.Receipt!.Status == ReceiptStatus.Valid
                                                          && l.Receipt.BusinessDate >= from && l.Receipt.BusinessDate <= to)
                .SumAsync(l => (decimal?)(l.Amount - l.RefundedAmount), ct) ?? 0;
        return new LabDashboard(from, to,
            await orders.Select(o => o.PatientId).Distinct().CountAsync(ct), await orders.CountAsync(ct), byStatus.Values.Sum() - S(LabOrderItemStatus.Cancelled),
            S(LabOrderItemStatus.SamplePending), S(LabOrderItemStatus.SampleCollected) + S(LabOrderItemStatus.SampleReceived), S(LabOrderItemStatus.InProgress),
            S(LabOrderItemStatus.AwaitingVerification), S(LabOrderItemStatus.Verified), S(LabOrderItemStatus.Released), S(LabOrderItemStatus.Cancelled),
            await _db.LabSamples.CountAsync(s => s.Status == LabSampleStatus.Rejected && s.BusinessDate >= from && s.BusinessDate <= to, ct),
            await orders.CountAsync(o => o.IsUrgent && o.Items.Any(i => OpenStatuses.Contains(i.Status) || i.Status == LabOrderItemStatus.AwaitingVerification), ct),
            collection);
    }
}
