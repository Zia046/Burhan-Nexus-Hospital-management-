using System.Text.Json;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Radiology;

public sealed class RadiologyBookingInput
{
    public Guid IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public long? EncounterId { get; set; }
    /// <summary>Phase 7: ordered for an inpatient admission (its fees go on the IPD bill).</summary>
    public long? IpdAdmissionId { get; set; }
    public List<int> ServiceIds { get; set; } = new();
    /// <summary>Hospital-local date / time of the examination (empty = now).</summary>
    public DateTime? ScheduledLocal { get; set; }
    public RadiologyReferralType ReferralType { get; set; }
    public int? ReferringDoctorId { get; set; }
    public string? ExternalDoctorName { get; set; }
    public int? ReferringDepartmentId { get; set; }
    public string? ClinicalIndication { get; set; }
    public RadiologyPriority Priority { get; set; } = RadiologyPriority.Routine;
    /// <summary>Optional discounts per service id (needs the Phase 3 "Give Discounts" permission).</summary>
    public Dictionary<int, decimal> Discounts { get; set; } = new();
    public string? DiscountReason { get; set; }
}

public sealed class RadiologyOrderDetailsInput
{
    public RadiologyReferralType ReferralType { get; set; }
    public int? ReferringDoctorId { get; set; }
    public string? ExternalDoctorName { get; set; }
    public int? ReferringDepartmentId { get; set; }
    public string? ClinicalIndication { get; set; }
    public RadiologyPriority Priority { get; set; } = RadiologyPriority.Routine;
}

public sealed class RadiologyReportInput
{
    public string? ClinicalIndication { get; set; }
    public string? Technique { get; set; }
    public string? Findings { get; set; }
    public string? Impression { get; set; }
    public string? Recommendations { get; set; }
    public string? ReportingDoctorName { get; set; }
    public string? ReportingDoctorDesignation { get; set; }
    public string? TemplateName { get; set; }
}

public sealed record RadiologyBookingResult(RadiologyOrder Order, bool WasDuplicate);

public enum ExamPaymentState { NotBilled, Free, Unpaid, PartiallyPaid, Paid, Cancelled }

public sealed record ExamPayment(ExamPaymentState State, decimal Net, decimal Paid, decimal Refunded)
{
    public decimal Balance => Net - Paid;
}

/// <summary>One examination in a worklist / history.</summary>
public sealed record RadiologyExamRow(
    long Id, long OrderId, string OrderNumber, RadiologyModality Modality, string ServiceCode, string ServiceName, int TokenNumber,
    DateTime ScheduledUtc, DateOnly ScheduledDate, RadiologyExamStatus Status, RadiologyPriority Priority,
    long PatientId, string PatientNumber, string PatientName, string AgeGender, string? Mobile,
    string? ReferringDepartment, string? ReferredBy, string? CorrectionReason, string? ReportedBy, string? VerifiedBy, int Attachments, int Prints);

public sealed class RadiologyWorklistQuery
{
    public RadiologyModality? Modality { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>queue | reporting | verification | verified | released | cancelled | all</summary>
    public string? Scope { get; set; }
    public RadiologyExamStatus? Status { get; set; }
    public string? Q { get; set; }
    public long? PatientId { get; set; }
    /// <summary>Include open work from earlier days (worklists).</summary>
    public bool IncludeOpenEarlier { get; set; }
    public int Take { get; set; } = 300;
}

public sealed record RadiologyDashboard(
    DateOnly From, DateOnly To, int XRayBooked, int UltrasoundBooked, int Pending, int Waiting, int InProgress, int ReportsPending,
    int AwaitingVerification, int Verified, int Released, int Cancelled, int Urgent, decimal? Revenue);

public interface IRadiologyService
{
    Task<ServiceResult<RadiologyBookingResult>> BookAsync(RadiologyBookingInput input, bool canDiscount, CancellationToken ct = default);
    Task<ServiceResult> AddExamsAsync(long orderId, IReadOnlyList<int> serviceIds, CancellationToken ct = default);
    Task<ServiceResult> UpdateOrderAsync(long orderId, RadiologyOrderDetailsInput input, CancellationToken ct = default);
    Task<ServiceResult> RescheduleAsync(long examId, DateTime local, CancellationToken ct = default);
    Task<ServiceResult> CancelAsync(long examId, string? reason, CancellationToken ct = default);

    Task<ServiceResult> ArriveAsync(long examId, CancellationToken ct = default);
    Task<ServiceResult> StartAsync(long examId, CancellationToken ct = default);
    Task<ServiceResult> CompleteAsync(long examId, string? technicalNotes, string? performedBy, CancellationToken ct = default);

    Task<ServiceResult> SaveReportAsync(long examId, RadiologyReportInput input, bool submit, uint rowVersion, CancellationToken ct = default);
    Task<ServiceResult> VerifyAsync(long examId, bool canVerifyOwn, CancellationToken ct = default);
    Task<ServiceResult> RejectAsync(long examId, string? reason, CancellationToken ct = default);
    Task<ServiceResult> ReleaseAsync(long examId, CancellationToken ct = default);
    Task<ServiceResult> AmendAsync(long examId, string? reason, CancellationToken ct = default);
    Task<ServiceResult<int>> RecordPrintAsync(long examId, string format, bool release, CancellationToken ct = default);

    Task<ServiceResult<long>> AddAttachmentAsync(long examId, Stream content, string fileName, string? description, CancellationToken ct = default);
    Task<ServiceResult> RemoveAttachmentAsync(long attachmentId, string? reason, CancellationToken ct = default);
    Task<(RadiologyAttachment Attachment, string Path)?> OpenAttachmentAsync(long attachmentId, CancellationToken ct = default);
    Task<IReadOnlyList<RadiologyAttachment>> AttachmentsAsync(long examId, CancellationToken ct = default);

    Task<RadiologyOrder?> OrderAsync(long id, CancellationToken ct = default);
    Task<RadiologyExam?> ExamAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<RadiologyExamRow>> ExamsAsync(RadiologyWorklistQuery q, CancellationToken ct = default);
    Task<IReadOnlyList<RadiologyExamEvent>> EventsAsync(IReadOnlyList<long> examIds, CancellationToken ct = default);
    Task<IReadOnlyList<RadiologyReportRevision>> RevisionsAsync(long examId, CancellationToken ct = default);
    Task<IReadOnlyList<RadiologyReportPrint>> PrintsAsync(long examId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, ExamPayment>> PaymentsAsync(long orderId, CancellationToken ct = default);
    Task<RadiologyDashboard> DashboardAsync(DateOnly from, DateOnly to, RadiologyModality? modality, bool includeFinance, CancellationToken ct = default);
}

public sealed class RadiologyWorkflowService : IRadiologyService
{
    private readonly HmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IRequestContext _ctx;
    private readonly HospitalClock _clock;
    private readonly IFinanceSetupService _finance;
    private readonly IRadiologyCatalogService _catalog;
    private readonly RadiologyFileStore _files;

    public RadiologyWorkflowService(HmsDbContext db, IAuditService audit, IRequestContext ctx, HospitalClock clock, IFinanceSetupService finance,
        IRadiologyCatalogService catalog, RadiologyFileStore files)
    {
        _db = db;
        _audit = audit;
        _ctx = ctx;
        _clock = clock;
        _finance = finance;
        _catalog = catalog;
        _files = files;
    }

    private static string? T(string? v, int max) => RadiologyCatalogService.T(v, max);

    private RadiologyExamEvent Event(long examId, RadiologyEventType type, string? note, DateOnly today) => new()
    {
        ExamId = examId, EventType = type, Note = note is null ? null : note[..Math.Min(500, note.Length)], BusinessDate = today,
        CreatedUtc = _clock.UtcNow, UserId = _ctx.UserId, Username = _ctx.Username
    };

    public static string Label(RadiologyExamStatus s) => s switch
    {
        RadiologyExamStatus.Booked => "Booked",
        RadiologyExamStatus.Waiting => "Waiting",
        RadiologyExamStatus.InProgress => "In progress",
        RadiologyExamStatus.ExamCompleted => "Examination completed",
        RadiologyExamStatus.ReportPending => "Report pending (correction)",
        RadiologyExamStatus.ReportEntered => "Report entered (draft)",
        RadiologyExamStatus.AwaitingVerification => "Awaiting verification",
        RadiologyExamStatus.Verified => "Verified",
        RadiologyExamStatus.Released => "Released",
        _ => "Cancelled",
    };

    /// <summary>Statuses are stored as text, so compare with lists, never with &lt;.</summary>
    public static readonly RadiologyExamStatus[] QueueStatuses = { RadiologyExamStatus.Booked, RadiologyExamStatus.Waiting, RadiologyExamStatus.InProgress };
    public static readonly RadiologyExamStatus[] ReportingStatuses = { RadiologyExamStatus.ExamCompleted, RadiologyExamStatus.ReportPending, RadiologyExamStatus.ReportEntered };
    public static readonly RadiologyExamStatus[] FinalStatuses = { RadiologyExamStatus.Verified, RadiologyExamStatus.Released };

    // =====================================================================================
    // Booking
    // =====================================================================================

    public async Task<ServiceResult<RadiologyBookingResult>> BookAsync(RadiologyBookingInput i, bool canDiscount, CancellationToken ct = default)
    {
        if (i.IdempotencyKey == Guid.Empty) return ServiceResult<RadiologyBookingResult>.Fail("The booking form has expired. Please reload the page.");
        if (await _db.RadiologyOrders.AsNoTracking().FirstOrDefaultAsync(o => o.IdempotencyKey == i.IdempotencyKey, ct) is { } dup)
        {
            await _audit.LogAsync(AuditActions.RadDuplicateBlocked, "RadiologyOrder", dup.OrderNumber, $"Repeated booking of {dup.OrderNumber} ignored (no second order created)", ct: ct);
            return ServiceResult<RadiologyBookingResult>.Ok(new RadiologyBookingResult(dup, true));
        }
        var errors = new List<string>();
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == i.PatientId, ct);
        if (patient is null) return ServiceResult<RadiologyBookingResult>.Fail("Select the patient.");
        if (patient.Status != PatientStatus.Active) errors.Add($"{patient.PatientNumber} is {patient.Status.ToString().ToLowerInvariant()} – examinations cannot be booked on this record.");
        var serviceIds = i.ServiceIds.Where(t => t > 0).ToList();
        if (serviceIds.Count == 0) errors.Add("Select at least one examination.");
        if (serviceIds.Count != serviceIds.Distinct().Count()) errors.Add("An examination was selected twice.");
        if (serviceIds.Count > 20) errors.Add("At most 20 examinations per booking.");
        if (!Enum.IsDefined(i.Priority)) errors.Add("Select the priority.");
        errors.AddRange(await ValidateDetailsAsync(i.ReferralType, i.ReferringDoctorId, i.ExternalDoctorName, i.ReferringDepartmentId, ct));
        if (i.IpdAdmissionId is long ipd)
        {
            if (!await _db.IpdAdmissions.AnyAsync(a => a.Id == ipd && a.PatientId == i.PatientId, ct)) errors.Add("The admission does not belong to this patient.");
        }
        else errors.AddRange(await ValidateVisitAsync(i.PatientId, i.EncounterId, ct));
        if (i.Discounts.Any(d => d.Value != 0) && !canDiscount) errors.Add("You are not allowed to give discounts.");
        if (i.Discounts.Any(d => d.Value < 0)) errors.Add("A discount cannot be negative.");
        if (i.Discounts.Any(d => d.Value > 0) && T(i.DiscountReason, 200) is null) errors.Add("Enter the reason for the discount.");
        var today = await _clock.TodayAsync();
        var localNow = await _clock.LocalNowAsync();
        var scheduledLocal = i.ScheduledLocal ?? localNow;
        var scheduledDate = DateOnly.FromDateTime(scheduledLocal);
        if (scheduledDate < today) errors.Add("The examination date cannot be in the past.");
        if (scheduledDate > today.AddDays(90)) errors.Add("Examinations can be booked at most 90 days ahead.");
        if (errors.Count > 0) return ServiceResult<RadiologyBookingResult>.Fail(errors.ToArray());

        var now = _clock.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var order = new RadiologyOrder
            {
                OrderNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.RadiologyOrderSequence, "RAD", today, ct),
                IdempotencyKey = i.IdempotencyKey, PatientId = patient.Id, EncounterId = i.EncounterId, IpdAdmissionId = i.IpdAdmissionId, ReferralType = i.ReferralType,
                ReferringDoctorId = i.ReferralType == RadiologyReferralType.HospitalDoctor ? i.ReferringDoctorId : null,
                ExternalDoctorName = i.ReferralType == RadiologyReferralType.External ? T(i.ExternalDoctorName, 150) : null,
                ReferringDepartmentId = i.ReferringDepartmentId, ClinicalIndication = T(i.ClinicalIndication, 1000), Priority = i.Priority,
                BusinessDate = today, BookedUtc = now, UpdatedUtc = now, BookedByUserId = _ctx.UserId, BookedByUsername = _ctx.Username, Workstation = T(_ctx.Workstation, 100)
            };
            _db.RadiologyOrders.Add(order);
            await _db.SaveChangesAsync(ct);
            var add = await AddItemsAsync(order, serviceIds, scheduledLocal, i.Discounts, T(i.DiscountReason, 200), today, ct);
            if (!add.Succeeded)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return ServiceResult<RadiologyBookingResult>.Fail(add.Errors.ToArray());
            }
            _audit.Add(AuditActions.RadOrderBooked, "RadiologyOrder", order.OrderNumber,
                $"Radiology order {order.OrderNumber} for {patient.PatientNumber}: {string.Join(", ", order.Exams.Select(x => $"{x.ServiceCode} (token {x.TokenNumber})"))}" +
                (order.ReferredBy is { Length: > 0 } r ? $", referred by {r}" : ""));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<RadiologyBookingResult>.Ok(new RadiologyBookingResult(order, false));
        }
        catch (DbUpdateException ex) when (FinanceDb.IsUniqueViolation(ex, "UX_RadiologyOrders_Idempotency"))
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            var existing = await _db.RadiologyOrders.AsNoTracking().FirstAsync(o => o.IdempotencyKey == i.IdempotencyKey, ct);
            return ServiceResult<RadiologyBookingResult>.Ok(new RadiologyBookingResult(existing, true));
        }
    }

    private async Task<List<string>> ValidateDetailsAsync(RadiologyReferralType type, int? doctorId, string? external, int? departmentId, CancellationToken ct)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(type)) errors.Add("Select the referral.");
        if (type == RadiologyReferralType.HospitalDoctor && (doctorId is null || !await _db.Doctors.AnyAsync(d => d.Id == doctorId, ct))) errors.Add("Select the referring hospital doctor.");
        if (type == RadiologyReferralType.External && T(external, 150) is null) errors.Add("Enter the external referring doctor's name.");
        if (departmentId is int dep && !await _db.Departments.AnyAsync(d => d.Id == dep, ct)) errors.Add("Select a valid referring department.");
        return errors;
    }

    /// <summary>OPD / Emergency visit rules: Government hospitals (by default) book only against a recent registered visit.</summary>
    private async Task<List<string>> ValidateVisitAsync(long patientId, long? encounterId, CancellationToken ct)
    {
        var errors = new List<string>();
        var settings = await _catalog.SettingsAsync(ct);
        if (encounterId is long eid)
        {
            var e = await _db.PatientEncounters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eid, ct);
            var today = await _clock.TodayAsync();
            if (e is null || e.PatientId != patientId) errors.Add("The visit does not belong to this patient.");
            else if (e.Type is not (EncounterType.Opd or EncounterType.Emergency)) errors.Add("Select an OPD or Emergency visit.");
            else if (e.EncounterDate < today.AddDays(-settings.VisitValidDays))
                errors.Add($"Visit {e.EncounterNumber} of {e.EncounterDate:dd MMM yyyy} is older than {settings.VisitValidDays} day(s). Register a new OPD / Emergency visit.");
        }
        else if (!settings.AllowBookingWithoutVisit)
            errors.Add("Select the patient's OPD or Emergency visit. The radiology settings only allow bookings against a registered visit.");
        return errors;
    }

    /// <summary>Daily token per modality – serialised with an advisory lock, so two counters never get the same number.</summary>
    private async Task<int> NextTokenAsync(RadiologyModality modality, DateOnly date, CancellationToken ct)
    {
        await FinanceDb.LockAsync(_db, $"RADTOKEN|{modality}|{date:yyyyMMdd}", ct);
        return (await _db.RadiologyExams.Where(e => e.Modality == modality && e.ScheduledDate == date).MaxAsync(e => (int?)e.TokenNumber, ct) ?? 0) + 1;
    }

    private async Task<ServiceResult> AddItemsAsync(RadiologyOrder order, IReadOnlyList<int> serviceIds, DateTime scheduledLocal, IReadOnlyDictionary<int, decimal> discounts,
        string? discountReason, DateOnly today, CancellationToken ct)
    {
        var services = await _db.RadiologyServices.AsNoTracking().Include(s => s.ServiceItem).Where(s => serviceIds.Contains(s.Id)).ToListAsync(ct);
        var errors = new List<string>();
        foreach (var id in serviceIds)
        {
            if (services.FirstOrDefault(x => x.Id == id) is not { IsActive: true } s || !s.ServiceItem!.IsActive) errors.Add("A selected examination is not available.");
            else if (await ServiceCatalogue.FeeNotSetErrorAsync(_db, s.ServiceItemId, today, ct) is { } feeMissing) errors.Add(feeMissing); // Update 13
        }
        if (await _db.RadiologyExams.AnyAsync(x => x.OrderId == order.Id && serviceIds.Contains(x.ServiceId) && x.Status != RadiologyExamStatus.Cancelled, ct))
            errors.Add("A selected examination is already booked in this order.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.Distinct().ToArray());

        var now = _clock.UtcNow;
        var scheduledUtc = await _clock.LocalToUtcAsync(scheduledLocal);
        var date = DateOnly.FromDateTime(scheduledLocal);
        var sort = await _db.RadiologyExams.Where(x => x.OrderId == order.Id).MaxAsync(x => (int?)x.SortOrder, ct) ?? 0;
        var newExams = new List<(RadiologyExam Exam, RadiologyService Service)>();
        foreach (var g in serviceIds.Select(id => services.First(x => x.Id == id)).GroupBy(s => s.Modality).OrderBy(g => g.Key))
        {
            // Examinations of one order and modality share one token (the patient queues once).
            var token = await _db.RadiologyExams.Where(x => x.OrderId == order.Id && x.Modality == g.Key && x.ScheduledDate == date && x.Status != RadiologyExamStatus.Cancelled)
                            .Select(x => (int?)x.TokenNumber).FirstOrDefaultAsync(ct)
                        ?? await NextTokenAsync(g.Key, date, ct);
            foreach (var s in g.OrderBy(x => x.SortOrder))
            {
                var exam = new RadiologyExam
                {
                    OrderId = order.Id, ServiceId = s.Id, ServiceCode = s.Code, ServiceName = s.Name, Modality = s.Modality, BodyPart = s.BodyPart,
                    ReportTitle = s.ReportTitle, ScheduledUtc = scheduledUtc, ScheduledDate = date, TokenNumber = token, SortOrder = sort += 10,
                    ClinicalIndication = order.ClinicalIndication, Status = RadiologyExamStatus.Booked
                };
                order.Exams.Add(exam);
                _db.RadiologyExams.Add(exam);
                newExams.Add((exam, s));
            }
            await _db.SaveChangesAsync(ct); // tokens must be visible to the next modality / booking inside the lock
        }

        // Phase 3 billing: one charge (bill line) per examination at today's fee; paid at any cash counter.
        foreach (var (exam, s) in newExams)
        {
            var fee = await _finance.FeeOnAsync(s.ServiceItemId, today, ct);
            var discount = discounts.GetValueOrDefault(s.Id);
            if (discount > fee) return ServiceResult.Fail($"The discount on {s.Name} cannot be more than its fee ({fee:N2}).");
            if (!FinanceMath.IsMoney(discount)) return ServiceResult.Fail("Discounts may have at most 2 decimals.");
            var charge = new Charge
            {
                ChargeNumber = await FinanceDb.NextNumberAsync(_db, HmsDbContext.ChargeNumberSequence, "CHG", today, ct),
                PatientId = order.PatientId, EncounterId = order.EncounterId, RadiologyOrderId = order.Id, IpdAdmissionId = order.IpdAdmissionId, ServiceItemId = s.ServiceItemId,
                ServiceKind = s.Modality == RadiologyModality.XRay ? ServiceKind.XRay : ServiceKind.Ultrasound, ServiceName = s.Name,
                DepartmentId = s.ServiceItem!.DepartmentId, Fee = fee, Discount = discount, NetAmount = fee - discount,
                AllowPartialPayment = s.ServiceItem.AllowPartialPayment, DiscountReason = discount > 0 ? discountReason : null,
                DoctorShareType = DoctorShareType.HospitalKeepsAll, Status = fee - discount == 0 ? ChargeStatus.Paid : ChargeStatus.Unpaid,
                BusinessDate = today, CreatedUtc = now, CreatedByUserId = _ctx.UserId, CreatedByUsername = _ctx.Username
            };
            _db.Charges.Add(charge);
            await _db.SaveChangesAsync(ct);
            exam.ChargeId = charge.Id;
            _db.RadiologyExamEvents.Add(Event(exam.Id, RadiologyEventType.Booked,
                $"{order.OrderNumber} · token {exam.TokenNumber} · {exam.ScheduledDate:dd MMM yyyy} · fee {fee:N2}{(discount > 0 ? $" − discount {discount:N2}" : "")}", today));
        }
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AddExamsAsync(long orderId, IReadOnlyList<int> serviceIds, CancellationToken ct = default)
    {
        if (serviceIds.Count == 0) return ServiceResult.Fail("Select at least one examination.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"RADORDER|{orderId}", ct);
        var order = await _db.RadiologyOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return ServiceResult.Fail("Order not found.");
        var today = await _clock.TodayAsync();
        // New examinations go on the order's open date (or today).
        var openDate = await _db.RadiologyExams.Where(x => x.OrderId == orderId && x.Status != RadiologyExamStatus.Cancelled && x.ScheduledDate >= today)
            .OrderBy(x => x.ScheduledUtc).Select(x => (DateTime?)x.ScheduledUtc).FirstOrDefaultAsync(ct);
        var local = openDate is DateTime u ? TimeZoneInfo.ConvertTimeFromUtc(u, await _clock.ZoneAsync()) : await _clock.LocalNowAsync();
        var r = await AddItemsAsync(order, serviceIds, local, new Dictionary<int, decimal>(), null, today, ct);
        if (!r.Succeeded) return r;
        order.UpdatedUtc = _clock.UtcNow;
        _audit.Add(AuditActions.RadOrderEdited, "RadiologyOrder", order.OrderNumber, $"Examinations added to {order.OrderNumber}: {string.Join(", ", order.Exams.Select(x => x.ServiceCode))}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateOrderAsync(long orderId, RadiologyOrderDetailsInput i, CancellationToken ct = default)
    {
        var errors = await ValidateDetailsAsync(i.ReferralType, i.ReferringDoctorId, i.ExternalDoctorName, i.ReferringDepartmentId, ct);
        if (!Enum.IsDefined(i.Priority)) errors.Add("Select the priority.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());
        var o = await _db.RadiologyOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null) return ServiceResult.Fail("Order not found.");
        var old = new { o.ReferralType, o.ReferringDoctorId, o.ExternalDoctorName, o.ReferringDepartmentId, o.ClinicalIndication, o.Priority };
        o.ReferralType = i.ReferralType;
        o.ReferringDoctorId = i.ReferralType == RadiologyReferralType.HospitalDoctor ? i.ReferringDoctorId : null;
        o.ExternalDoctorName = i.ReferralType == RadiologyReferralType.External ? T(i.ExternalDoctorName, 150) : null;
        o.ReferringDepartmentId = i.ReferringDepartmentId;
        o.ClinicalIndication = T(i.ClinicalIndication, 1000);
        o.Priority = i.Priority;
        o.UpdatedUtc = _clock.UtcNow;
        // The indication is copied to examinations whose report has not been started yet.
        foreach (var e in await _db.RadiologyExams.Where(x => x.OrderId == orderId && x.ReportSavedUtc == null && x.Status != RadiologyExamStatus.Cancelled).ToListAsync(ct))
            e.ClinicalIndication = o.ClinicalIndication;
        _audit.Add(AuditActions.RadOrderEdited, "RadiologyOrder", o.OrderNumber, $"Order {o.OrderNumber} details changed", old,
            new { o.ReferralType, o.ReferringDoctorId, o.ExternalDoctorName, o.ReferringDepartmentId, o.ClinicalIndication, o.Priority });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The order was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RescheduleAsync(long examId, DateTime local, CancellationToken ct = default)
    {
        var today = await _clock.TodayAsync();
        var date = DateOnly.FromDateTime(local);
        if (date < today) return ServiceResult.Fail("The examination date cannot be in the past.");
        if (date > today.AddDays(90)) return ServiceResult.Fail("Examinations can be booked at most 90 days ahead.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (e.Status is not (RadiologyExamStatus.Booked or RadiologyExamStatus.Waiting)) return ServiceResult.Fail("Only an examination that has not started can be rescheduled.");
        var oldText = $"{e.ScheduledDate:dd MMM yyyy}, token {e.TokenNumber}";
        if (date != e.ScheduledDate)
        {
            e.TokenNumber = await NextTokenAsync(e.Modality, date, ct);
            e.ScheduledDate = date;
            e.Status = RadiologyExamStatus.Booked;
            e.ArrivedUtc = null;
        }
        e.ScheduledUtc = await _clock.LocalToUtcAsync(local);
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.Rescheduled, $"{oldText} → {local:dd MMM yyyy HH:mm}, token {e.TokenNumber}", today));
        _audit.Add(AuditActions.RadOrderEdited, "RadiologyOrder", e.Order!.OrderNumber, $"{e.ServiceName} of {e.Order.OrderNumber} rescheduled: {oldText} → {local:dd MMM yyyy HH:mm}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The examination was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelAsync(long examId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for cancelling the examination.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (e.Status == RadiologyExamStatus.Cancelled) return ServiceResult.Fail("This examination is already cancelled.");
        if (FinalStatuses.Contains(e.Status)) return ServiceResult.Fail("An examination with a verified report cannot be cancelled. Amend the report if it is wrong.");
        var today = await _clock.TodayAsync();
        e.Status = RadiologyExamStatus.Cancelled;
        e.CancelledUtc = _clock.UtcNow;
        e.CancelledByUsername = _ctx.Username;
        e.CancelReason = reason;
        var note = $"Cancelled: {reason}";
        if (e.ChargeId is long cid)
        {
            var charge = await _db.Charges.FromSqlRaw("SELECT c.*, c.xmin FROM \"Charges\" c WHERE c.\"Id\" = {0} FOR UPDATE", cid).FirstAsync(ct);
            if (charge.PaidAmount == 0 && charge.Status != ChargeStatus.Cancelled) charge.Status = ChargeStatus.Cancelled;
            else if (charge.PaidAmount > 0) note += $" – {charge.PaidAmount:N2} was paid: refund it from the receipt (Admin refund)";
        }
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.Cancelled, note, today));
        _audit.Add(AuditActions.RadExamCancelled, "RadiologyOrder", e.Order!.OrderNumber, $"{e.ServiceName} in {e.Order.OrderNumber} cancelled: {reason}");
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The examination was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    // =====================================================================================
    // Examination workflow
    // =====================================================================================

    private async Task<ServiceResult> StepAsync(long examId, RadiologyExamStatus[] from, string notAllowed, Action<RadiologyExam> apply, RadiologyEventType ev, string? note, CancellationToken ct)
    {
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (!from.Contains(e.Status)) return ServiceResult.Fail($"{notAllowed} (status: {Label(e.Status)}).");
        var before = e.Status;
        apply(e);
        _db.RadiologyExamEvents.Add(Event(e.Id, ev, note, await _clock.TodayAsync()));
        _audit.Add(AuditActions.RadExamStatus, "RadiologyOrder", e.Order!.OrderNumber, $"{e.ServiceName} of {e.Order.OrderNumber}: {Label(before)} → {Label(e.Status)}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The examination was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public Task<ServiceResult> ArriveAsync(long examId, CancellationToken ct = default) =>
        StepAsync(examId, new[] { RadiologyExamStatus.Booked }, "Only a booked examination can be marked as waiting",
            e => { e.Status = RadiologyExamStatus.Waiting; e.ArrivedUtc = _clock.UtcNow; }, RadiologyEventType.Arrived, "Patient arrived – waiting", ct);

    public Task<ServiceResult> StartAsync(long examId, CancellationToken ct = default) =>
        StepAsync(examId, new[] { RadiologyExamStatus.Booked, RadiologyExamStatus.Waiting }, "The examination cannot be started",
            e =>
            {
                e.Status = RadiologyExamStatus.InProgress;
                e.ArrivedUtc ??= _clock.UtcNow;
                e.StartedUtc = _clock.UtcNow;
                e.PerformedByUserId = _ctx.UserId;
                e.PerformedByUsername = _ctx.Username;
            }, RadiologyEventType.Started, null, ct);

    public async Task<ServiceResult> CompleteAsync(long examId, string? technicalNotes, string? performedBy, CancellationToken ct = default)
    {
        var user = _ctx.UserId is int uid ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct) : null;
        var notes = T(technicalNotes, 1000);
        var performer = T(performedBy, 150) ?? user?.FullName ?? _ctx.Username;
        return await StepAsync(examId, new[] { RadiologyExamStatus.InProgress }, "Start the examination before completing it",
            e =>
            {
                e.Status = RadiologyExamStatus.ExamCompleted;
                e.CompletedUtc = _clock.UtcNow;
                e.PerformedByUserId = _ctx.UserId;
                e.PerformedByUsername = _ctx.Username;
                e.PerformedByName = performer;
                e.TechnicalNotes = notes;
            }, RadiologyEventType.Completed, $"Performed by {performer}{(notes is null ? "" : " – " + notes)}", ct);
    }

    // =====================================================================================
    // Reporting & verification
    // =====================================================================================

    private static readonly RadiologyExamStatus[] Reportable =
        { RadiologyExamStatus.ExamCompleted, RadiologyExamStatus.ReportPending, RadiologyExamStatus.ReportEntered, RadiologyExamStatus.AwaitingVerification };

    private static string Snapshot(RadiologyExam e) => JsonSerializer.Serialize(new
    {
        e.ServiceCode, e.ServiceName, Status = e.Status.ToString(), e.ClinicalIndication, e.Technique, e.Findings, e.Impression, e.Recommendations,
        e.ReportingDoctorName, e.ReportingDoctorDesignation, e.ReportedByUsername, e.SubmittedUtc, e.VerifiedByUsername, e.VerifiedByName, e.VerifiedUtc, e.ReleasedUtc
    });

    public async Task<ServiceResult> SaveReportAsync(long examId, RadiologyReportInput i, bool submit, uint rowVersion, CancellationToken ct = default)
    {
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (e.Status == RadiologyExamStatus.Cancelled) return ServiceResult.Fail("This examination was cancelled.");
        if (FinalStatuses.Contains(e.Status)) return ServiceResult.Fail("This report is verified and cannot be changed. Amend it with a reason if it is wrong.");
        if (!Reportable.Contains(e.Status)) return ServiceResult.Fail("The examination has not been completed yet. Complete the examination first.");
        _db.Entry(e).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var findings = T(i.Findings, 8000);
        var impression = T(i.Impression, 2000);
        var errors = new List<string>();
        var anyText = findings is not null || impression is not null || T(i.Technique, 2000) is not null || T(i.Recommendations, 2000) is not null;
        if (!anyText) errors.Add("Enter the report (findings and / or impression).");
        // Only what every report needs is mandatory: a conclusion and the reporting doctor. Other sections stay optional.
        if (submit && findings is null && impression is null) errors.Add("Enter the findings or the impression before submitting.");
        if (submit && T(i.ReportingDoctorName, 150) is null) errors.Add("Enter the reporting doctor / radiologist name.");
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());

        var today = await _clock.TodayAsync();
        var wasSubmitted = e.Status == RadiologyExamStatus.AwaitingVerification;
        var isNew = e.ReportSavedUtc is null;
        e.ClinicalIndication = T(i.ClinicalIndication, 1000);
        e.Technique = T(i.Technique, 2000);
        e.Findings = findings;
        e.Impression = impression;
        e.Recommendations = T(i.Recommendations, 2000);
        e.ReportingDoctorName = T(i.ReportingDoctorName, 150);
        e.ReportingDoctorDesignation = T(i.ReportingDoctorDesignation, 150);
        e.TemplateName = T(i.TemplateName, 120) ?? e.TemplateName;
        e.ReportSavedUtc = _clock.UtcNow;
        e.ReportedByUserId = _ctx.UserId;
        e.ReportedByUsername = _ctx.Username;
        if (submit)
        {
            e.Status = RadiologyExamStatus.AwaitingVerification;
            e.SubmittedUtc = _clock.UtcNow;
            e.CorrectionReason = null;
        }
        else e.Status = RadiologyExamStatus.ReportEntered; // a draft – also takes a submitted report back out of the verification queue
        _db.RadiologyExamEvents.Add(Event(e.Id, submit ? RadiologyEventType.ReportSubmitted : RadiologyEventType.ReportSaved,
            (submit ? "Submitted for verification" : wasSubmitted ? "Taken back as draft" : isNew ? "Draft started" : "Draft saved") + (impression is null ? "" : $" – {impression}"), today));
        _audit.Add(AuditActions.RadReportSaved, "RadiologyOrder", e.Order!.OrderNumber,
            $"{e.ServiceName} of {e.Order.OrderNumber}: report {(submit ? "submitted for verification" : "saved as draft")}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The report was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> VerifyAsync(long examId, bool canVerifyOwn, CancellationToken ct = default)
    {
        var settings = await _catalog.SettingsAsync(ct);
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (e.Status != RadiologyExamStatus.AwaitingVerification) return ServiceResult.Fail($"Only a report awaiting verification can be approved (status: {Label(e.Status)}).");
        if (e.ReportedByUserId == _ctx.UserId && !(settings.AllowSelfVerification && canVerifyOwn))
            return ServiceResult.Fail("You entered this report – another authorised person must verify it.");
        var user = _ctx.UserId is int uid ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct) : null;
        e.Status = RadiologyExamStatus.Verified;
        e.VerifiedUtc = _clock.UtcNow;
        e.VerifiedByUserId = _ctx.UserId;
        e.VerifiedByUsername = _ctx.Username;
        e.VerifiedByName = user?.FullName ?? _ctx.Username;
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.Verified, e.ReportedByUserId == _ctx.UserId ? "Self-verified (allowed by settings)" : null, await _clock.TodayAsync()));
        _audit.Add(AuditActions.RadReportVerified, "RadiologyOrder", e.Order!.OrderNumber, $"{e.ServiceName} report of {e.Order.OrderNumber} verified");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The report was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RejectAsync(long examId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for sending the report back.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (e.Status != RadiologyExamStatus.AwaitingVerification) return ServiceResult.Fail("Only a report awaiting verification can be rejected.");
        e.RevisionNo++;
        _db.RadiologyReportRevisions.Add(new RadiologyReportRevision
        {
            ExamId = e.Id, RevisionNo = e.RevisionNo, Reason = "Sent back: " + reason, Snapshot = Snapshot(e), PreviousStatus = e.Status,
            CreatedUtc = _clock.UtcNow, CreatedByUsername = _ctx.Username
        });
        e.Status = RadiologyExamStatus.ReportPending;
        e.CorrectionReason = reason;
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.VerificationRejected, reason, await _clock.TodayAsync()));
        _audit.Add(AuditActions.RadReportRejected, "RadiologyOrder", e.Order!.OrderNumber, $"{e.ServiceName} report of {e.Order.OrderNumber} sent back for correction: {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    private async Task<string?> PaymentBlockAsync(RadiologyExam e, CancellationToken ct)
    {
        var settings = await _catalog.SettingsAsync(ct);
        if (!settings.RequirePaymentBeforeRelease || e.ChargeId is null) return null;
        var c = await _db.Charges.AsNoTracking().FirstAsync(x => x.Id == e.ChargeId, ct);
        return c.Status != ChargeStatus.Cancelled && c.PaidAmount < c.NetAmount
            ? "Payment is outstanding. The radiology settings require full payment before the report is released."
            : null;
    }

    public async Task<ServiceResult> ReleaseAsync(long examId, CancellationToken ct = default)
    {
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (e.Status != RadiologyExamStatus.Verified) return ServiceResult.Fail("Only a verified report can be released as final.");
        if (await PaymentBlockAsync(e, ct) is { } block) return ServiceResult.Fail(block);
        e.Status = RadiologyExamStatus.Released;
        e.ReleasedUtc = _clock.UtcNow;
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.Released, null, await _clock.TodayAsync()));
        _audit.Add(AuditActions.RadReportReleased, "RadiologyOrder", e.Order!.OrderNumber, $"{e.ServiceName} report of {e.Order.OrderNumber} released as final");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return ServiceResult.Fail("The report was changed by someone else. Please reload."); }
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AmendAsync(long examId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for amending the verified report.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult.Fail("Examination not found.");
        if (!FinalStatuses.Contains(e.Status)) return ServiceResult.Fail("Only a verified or released report can be amended.");
        var previous = e.Status;
        e.RevisionNo++;
        _db.RadiologyReportRevisions.Add(new RadiologyReportRevision
        {
            ExamId = e.Id, RevisionNo = e.RevisionNo, Reason = "Amendment: " + reason, Snapshot = Snapshot(e), PreviousStatus = previous,
            CreatedUtc = _clock.UtcNow, CreatedByUsername = _ctx.Username
        });
        // Status leaves "verified" in the same statement – the database refuses text changes of a verified report.
        e.Status = RadiologyExamStatus.ReportPending;
        e.VerifiedUtc = null; e.VerifiedByUserId = null; e.VerifiedByUsername = null; e.VerifiedByName = null; e.ReleasedUtc = null; e.SubmittedUtc = null;
        e.CorrectionReason = "Amendment: " + reason;
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.ReportAmended, $"Revision {e.RevisionNo} kept: {reason}", await _clock.TodayAsync()));
        _audit.Add(AuditActions.RadReportAmended, "RadiologyOrder", e.Order!.OrderNumber,
            $"{e.ServiceName} report of {e.Order.OrderNumber} ({previous}) re-opened for amendment – revision {e.RevisionNo} kept: {reason}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<int>> RecordPrintAsync(long examId, string format, bool release, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await FinanceDb.LockAsync(_db, $"RADPRINT|{examId}", ct);
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult<int>.Fail("Examination not found.");
        if (e.Status == RadiologyExamStatus.Verified && release)
        {
            if (await PaymentBlockAsync(e, ct) is { } block) return ServiceResult<int>.Fail(block);
            e.Status = RadiologyExamStatus.Released;
            e.ReleasedUtc = _clock.UtcNow;
            _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.Released, "Released when printed", await _clock.TodayAsync()));
            _audit.Add(AuditActions.RadReportReleased, "RadiologyOrder", e.Order!.OrderNumber, $"{e.ServiceName} report of {e.Order.OrderNumber} released as final");
        }
        if (e.Status != RadiologyExamStatus.Released) return ServiceResult<int>.Fail("Only a verified and released report can be printed as final.");
        var copy = await _db.RadiologyReportPrints.CountAsync(p => p.ExamId == examId, ct) + 1;
        _db.RadiologyReportPrints.Add(new RadiologyReportPrint
        {
            ExamId = examId, CopyNumber = copy, Format = format, PrintedUtc = _clock.UtcNow, PrintedByUsername = _ctx.Username, Workstation = T(_ctx.Workstation, 100)
        });
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.Printed, $"Copy {copy}, {format}", await _clock.TodayAsync()));
        _audit.Add(copy == 1 ? AuditActions.RadReportPrinted : AuditActions.RadReportReprinted, "RadiologyOrder", e.Order!.OrderNumber,
            $"{e.ServiceName} report of {e.Order.OrderNumber} {(copy == 1 ? "printed" : $"reprinted – copy {copy}")} (no new order or report created)");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<int>.Ok(copy);
    }

    // =====================================================================================
    // Attachments
    // =====================================================================================

    public async Task<ServiceResult<long>> AddAttachmentAsync(long examId, Stream content, string fileName, string? description, CancellationToken ct = default)
    {
        var e = await _db.RadiologyExams.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == examId, ct);
        if (e is null) return ServiceResult<long>.Fail("Examination not found.");
        if (e.Status == RadiologyExamStatus.Cancelled) return ServiceResult<long>.Fail("Files cannot be attached to a cancelled examination.");
        var settings = await _catalog.SettingsAsync(ct);
        var now = _clock.UtcNow;
        var saved = await _files.SaveAsync(content, settings.MaxUploadMb * 1024L * 1024L, now, ct);
        if (saved is null) return ServiceResult<long>.Fail($"The file is larger than {settings.MaxUploadMb} MB.");
        if (saved.Value.Size == 0) return ServiceResult<long>.Fail("Only JPG, PNG, PDF or DICOM (.dcm) files can be attached.");
        var name = Path.GetFileName(fileName ?? "file");
        var a = new RadiologyAttachment
        {
            ExamId = e.Id, PatientId = e.Order!.PatientId, OriginalFileName = string.IsNullOrWhiteSpace(name) ? "file" : name.Length > 200 ? name[^200..] : name,
            StoredPath = saved.Value.StoredPath, ContentType = saved.Value.ContentType, Kind = RadiologyFileStore.KindOf(saved.Value.ContentType),
            SizeBytes = saved.Value.Size, Sha256 = saved.Value.Sha256, Description = T(description, 300), UploadedUtc = now, UploadedByUsername = _ctx.Username
        };
        _db.RadiologyAttachments.Add(a);
        _db.RadiologyExamEvents.Add(Event(e.Id, RadiologyEventType.AttachmentAdded, $"{a.OriginalFileName} ({a.Kind}, {a.SizeBytes / 1024.0:N0} KB)", await _clock.TodayAsync()));
        _audit.Add(AuditActions.RadAttachmentAdded, "RadiologyOrder", e.Order.OrderNumber, $"{a.Kind} file {a.OriginalFileName} attached to {e.ServiceName} of {e.Order.OrderNumber}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult<long>.Ok(a.Id);
    }

    public async Task<ServiceResult> RemoveAttachmentAsync(long attachmentId, string? reason, CancellationToken ct = default)
    {
        reason = T(reason, 300);
        if (reason is null) return ServiceResult.Fail("Enter the reason for removing the file.");
        var a = await _db.RadiologyAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, ct);
        if (a is null || a.IsRemoved) return ServiceResult.Fail("File not found.");
        var order = await _db.RadiologyExams.Where(x => x.Id == a.ExamId).Select(x => x.Order!.OrderNumber).FirstAsync(ct);
        // The file itself stays on the server (and in backups); it is only hidden from normal views.
        a.IsRemoved = true;
        a.RemovedUtc = _clock.UtcNow;
        a.RemovedByUsername = _ctx.Username;
        a.RemoveReason = reason;
        _db.RadiologyExamEvents.Add(Event(a.ExamId, RadiologyEventType.AttachmentRemoved, $"{a.OriginalFileName}: {reason}", await _clock.TodayAsync()));
        _audit.Add(AuditActions.RadAttachmentRemoved, "RadiologyOrder", order, $"File {a.OriginalFileName} of {order} removed from view: {reason}");
        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    public async Task<(RadiologyAttachment Attachment, string Path)?> OpenAttachmentAsync(long attachmentId, CancellationToken ct = default)
    {
        var a = await _db.RadiologyAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId, ct);
        if (a is null || a.IsRemoved) return null;
        var path = _files.PathOf(a.StoredPath);
        if (path is null || !File.Exists(path)) return null;
        await _audit.LogAsync(AuditActions.RadAttachmentViewed, "RadiologyAttachment", a.Id.ToString(), $"Radiology file {a.OriginalFileName} opened", ct: ct);
        return (a, path);
    }

    public async Task<IReadOnlyList<RadiologyAttachment>> AttachmentsAsync(long examId, CancellationToken ct = default) =>
        await _db.RadiologyAttachments.AsNoTracking().Where(a => a.ExamId == examId).OrderBy(a => a.Id).ToListAsync(ct);

    // =====================================================================================
    // Queries
    // =====================================================================================

    public async Task<RadiologyOrder?> OrderAsync(long id, CancellationToken ct = default) =>
        await _db.RadiologyOrders.AsNoTracking().Include(o => o.Patient).Include(o => o.ReferringDoctor).Include(o => o.ReferringDepartment).Include(o => o.Encounter)
            .Include(o => o.Exams.OrderBy(x => x.SortOrder)).AsSplitQuery().FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<RadiologyExam?> ExamAsync(long id, CancellationToken ct = default) =>
        await _db.RadiologyExams.AsNoTracking().Include(x => x.Service)
            .Include(x => x.Order).ThenInclude(o => o!.Patient)
            .Include(x => x.Order).ThenInclude(o => o!.ReferringDoctor)
            .Include(x => x.Order).ThenInclude(o => o!.ReferringDepartment)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<RadiologyExamEvent>> EventsAsync(IReadOnlyList<long> examIds, CancellationToken ct = default) =>
        await _db.RadiologyExamEvents.AsNoTracking().Where(e => examIds.Contains(e.ExamId)).OrderByDescending(e => e.Id).Take(500).ToListAsync(ct);

    public async Task<IReadOnlyList<RadiologyReportRevision>> RevisionsAsync(long examId, CancellationToken ct = default) =>
        await _db.RadiologyReportRevisions.AsNoTracking().Where(r => r.ExamId == examId).OrderBy(r => r.RevisionNo).ToListAsync(ct);

    public async Task<IReadOnlyList<RadiologyReportPrint>> PrintsAsync(long examId, CancellationToken ct = default) =>
        await _db.RadiologyReportPrints.AsNoTracking().Where(p => p.ExamId == examId).OrderBy(p => p.CopyNumber).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<long, ExamPayment>> PaymentsAsync(long orderId, CancellationToken ct = default)
    {
        var rows = await _db.RadiologyExams.AsNoTracking().Where(i => i.OrderId == orderId)
            .Select(i => new { i.Id, Net = (decimal?)i.Charge!.NetAmount, Paid = (decimal?)i.Charge.PaidAmount, Refunded = (decimal?)i.Charge.RefundedAmount, ChargeStatus = (ChargeStatus?)i.Charge.Status })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => new ExamPayment(
            r.Net is null ? ExamPaymentState.NotBilled
            : r.ChargeStatus == ChargeStatus.Cancelled ? ExamPaymentState.Cancelled
            : r.Net == 0 ? ExamPaymentState.Free
            : r.Paid >= r.Net ? ExamPaymentState.Paid
            : r.Paid > 0 ? ExamPaymentState.PartiallyPaid : ExamPaymentState.Unpaid,
            r.Net ?? 0, r.Paid ?? 0, r.Refunded ?? 0));
    }

    public async Task<IReadOnlyList<RadiologyExamRow>> ExamsAsync(RadiologyWorklistQuery q, CancellationToken ct = default)
    {
        var x = _db.RadiologyExams.AsNoTracking().AsQueryable();
        if (q.Modality is { } m) x = x.Where(e => e.Modality == m);
        if (q.PatientId is long pid) x = x.Where(e => e.Order!.PatientId == pid);
        if (q.Status is { } st) x = x.Where(e => e.Status == st);
        var open = q.Scope switch
        {
            "queue" => QueueStatuses,
            "reporting" => ReportingStatuses,
            "verification" => new[] { RadiologyExamStatus.AwaitingVerification },
            "verified" => new[] { RadiologyExamStatus.Verified },
            "released" => new[] { RadiologyExamStatus.Released },
            "final" => FinalStatuses,
            "cancelled" => new[] { RadiologyExamStatus.Cancelled },
            _ => null,
        };
        if (open is not null) x = x.Where(e => open.Contains(e.Status));
        if (q.From is { } f && q.To is { } t)
            x = q.IncludeOpenEarlier && q.Scope is "queue" or "reporting" or "verification" or "verified"
                ? x.Where(e => e.ScheduledDate <= t) // open work from earlier days still needs attention
                : x.Where(e => e.ScheduledDate >= f && e.ScheduledDate <= t);
        else
        {
            if (q.From is { } f2) x = x.Where(e => e.ScheduledDate >= f2);
            if (q.To is { } t2) x = x.Where(e => e.ScheduledDate <= t2);
        }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var s = q.Q.Trim();
            var like = "%" + s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var digits = new string(s.Where(char.IsDigit).ToArray());
            x = x.Where(e => e.Order!.OrderNumber == s.ToUpper() || e.Order.Patient!.PatientNumber == s.ToUpper() || EF.Functions.ILike(e.Order.Patient.FullName, like)
                             || (digits.Length >= 4 && e.Order.Patient.MobileNumber != null && e.Order.Patient.MobileNumber.Contains(digits))
                             || (digits.Length >= 5 && e.Order.Patient.CnicDigits != null && e.Order.Patient.CnicDigits.Contains(digits)));
        }
        var ordered = q.Scope is "queue" or "reporting" or "verification" or "verified"
            ? x.OrderBy(e => e.ScheduledDate).ThenByDescending(e => e.Order!.Priority == RadiologyPriority.Urgent).ThenBy(e => e.TokenNumber).ThenBy(e => e.Id)
            : x.OrderByDescending(e => e.ScheduledDate).ThenByDescending(e => e.Id);
        var rows = await ordered.Take(Math.Clamp(q.Take, 1, 2000)).Select(e => new
        {
            e.Id, e.OrderId, e.Order!.OrderNumber, e.Modality, e.ServiceCode, e.ServiceName, e.TokenNumber, e.ScheduledUtc, e.ScheduledDate, e.Status, e.Order.Priority,
            e.Order.PatientId, e.Order.Patient!.PatientNumber, e.Order.Patient.FullName, e.Order.Patient.Gender, e.Order.Patient.DateOfBirth, e.Order.Patient.MobileNumber,
            Department = e.Order.ReferringDepartment != null ? e.Order.ReferringDepartment.Name : null, e.Order.ReferralType,
            Doctor = e.Order.ReferringDoctor != null ? e.Order.ReferringDoctor.Name : null, e.Order.ExternalDoctorName,
            CorrectionReason = e.Status == RadiologyExamStatus.Cancelled ? e.CancelReason : e.CorrectionReason,
            e.ReportingDoctorName, e.VerifiedByName,
            Attachments = _db.RadiologyAttachments.Count(a => a.ExamId == e.Id && !a.IsRemoved),
            Prints = _db.RadiologyReportPrints.Count(p => p.ExamId == e.Id)
        }).ToListAsync(ct);
        var today = await _clock.TodayAsync();
        return rows.Select(r =>
        {
            var age = r.DateOfBirth is { } dob ? AgeCalculator.FromDateOfBirth(dob, today).Short : "-";
            var g = r.Gender switch { Gender.Male => "M", Gender.Female => "F", Gender.Other => "O", _ => "-" };
            return new RadiologyExamRow(r.Id, r.OrderId, r.OrderNumber, r.Modality, r.ServiceCode, r.ServiceName, r.TokenNumber, r.ScheduledUtc, r.ScheduledDate, r.Status, r.Priority,
                r.PatientId, r.PatientNumber, r.FullName, $"{age} / {g}", r.MobileNumber, r.Department,
                r.ReferralType == RadiologyReferralType.HospitalDoctor ? r.Doctor : r.ReferralType == RadiologyReferralType.External ? r.ExternalDoctorName : null,
                r.CorrectionReason, r.ReportingDoctorName, r.VerifiedByName, r.Attachments, r.Prints);
        }).ToList();
    }

    public async Task<RadiologyDashboard> DashboardAsync(DateOnly from, DateOnly to, RadiologyModality? modality, bool includeFinance, CancellationToken ct = default)
    {
        var exams = _db.RadiologyExams.AsNoTracking().Where(e => e.ScheduledDate >= from && e.ScheduledDate <= to && (modality == null || e.Modality == modality));
        var byStatus = await exams.GroupBy(e => new { e.Modality, e.Status }).Select(g => new { g.Key.Modality, g.Key.Status, C = g.Count() }).ToListAsync(ct);
        int S(params RadiologyExamStatus[] s) => byStatus.Where(r => s.Contains(r.Status)).Sum(r => r.C);
        int Booked(RadiologyModality m) => byStatus.Where(r => r.Modality == m && r.Status != RadiologyExamStatus.Cancelled).Sum(r => r.C);
        decimal? revenue = null;
        if (includeFinance)
        {
            var kinds = modality switch
            {
                RadiologyModality.XRay => new[] { ServiceKind.XRay },
                RadiologyModality.Ultrasound => new[] { ServiceKind.Ultrasound },
                _ => new[] { ServiceKind.XRay, ServiceKind.Ultrasound },
            };
            revenue = await _db.ReceiptLines.Where(l => l.Charge!.RadiologyOrderId != null && kinds.Contains(l.Charge.ServiceKind) && l.Receipt!.Status == ReceiptStatus.Valid
                                                          && l.Receipt.BusinessDate >= from && l.Receipt.BusinessDate <= to)
                .SumAsync(l => (decimal?)(l.Amount - l.RefundedAmount), ct) ?? 0;
        }
        var urgent = await exams.CountAsync(e => e.Order!.Priority == RadiologyPriority.Urgent && e.Status != RadiologyExamStatus.Cancelled && !FinalStatuses.Contains(e.Status), ct);
        return new RadiologyDashboard(from, to, Booked(RadiologyModality.XRay), Booked(RadiologyModality.Ultrasound),
            S(RadiologyExamStatus.Booked, RadiologyExamStatus.Waiting), S(RadiologyExamStatus.Waiting), S(RadiologyExamStatus.InProgress),
            S(ReportingStatuses), S(RadiologyExamStatus.AwaitingVerification), S(RadiologyExamStatus.Verified), S(RadiologyExamStatus.Released),
            S(RadiologyExamStatus.Cancelled), urgent, revenue);
    }
}
