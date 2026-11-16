using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>
/// Update 10: revenue, collections and outstanding report. Everything is read from the Phase 3 records (charges, receipts, refunds,
/// cashier ledger) and the IPD detail – department totals and cashier totals come from the same receipts, so they always agree.
/// </summary>
[HasPermission(Perm.FinanceDashboard)]
public class RevenueModel : HmsPageModel
{
    private readonly HmsDbContext _db;
    private readonly HospitalClock _clock;
    public RevenueModel(HmsDbContext db, HospitalClock clock) { _db = db; _clock = clock; }

    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? Department { get; set; }
    [BindProperty(SupportsGet = true)] public int? Cashier { get; set; }
    [BindProperty(SupportsGet = true)] public int? Service { get; set; }
    /// <summary>paid | unpaid | partial</summary>
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    public sealed record ChargeInfo(decimal Fee, decimal Discount, decimal NetAmount, decimal PaidAmount, bool Lab, bool Rad, bool Pharm, bool Ot, bool Ipd, ServiceKind ServiceKind,
        string? Cat, string? Dept, ServiceApplicability Applicability, bool Bed, bool Equip, bool AdmFee, bool Appt);
    public sealed record Row(string Label, int Count, decimal Billed, decimal Discount, decimal Paid, decimal Outstanding);
    public sealed record DayRow(DateOnly Day, decimal Collections, decimal Advances, decimal Refunds);
    public sealed record CashierRow(string Cashier, int Receipts, decimal Amount, decimal FromAdvance);
    public sealed record PatientRow(long PatientId, string PatientNumber, string Name, decimal Outstanding);
    public sealed record AdmissionRow(long Id, string Number, string Patient, DateTime Admitted, AdmissionStatus Status, decimal Net, decimal Paid);

    public IReadOnlyList<Row> BySource { get; private set; } = Array.Empty<Row>();
    public IReadOnlyList<Row> ByDepartment { get; private set; } = Array.Empty<Row>();
    public IReadOnlyList<Row> ByApplicability { get; private set; } = Array.Empty<Row>();
    public IReadOnlyList<DayRow> Days { get; private set; } = Array.Empty<DayRow>();
    public IReadOnlyList<CashierRow> Cashiers { get; private set; } = Array.Empty<CashierRow>();
    public IReadOnlyList<PatientRow> Outstanding { get; private set; } = Array.Empty<PatientRow>();
    public IReadOnlyList<AdmissionRow> Admissions { get; private set; } = Array.Empty<AdmissionRow>();
    public IReadOnlyList<Charge> Waived { get; private set; } = Array.Empty<Charge>();
    public decimal AdvancesReceived { get; private set; }
    public decimal AdvancesUnused { get; private set; }
    public decimal Refunds { get; private set; }
    public decimal Discounts { get; private set; }
    public decimal DepartmentCollected { get; private set; }
    public decimal CashierCollected { get; private set; }
    public bool Government { get; private set; }
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<(int Id, string Name)> Users { get; private set; } = Array.Empty<(int, string)>();
    public IReadOnlyList<(int Id, string Name)> Services { get; private set; } = Array.Empty<(int, string)>();

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "finance-revenue";
        ViewData["FinanceTab"] = "reports";
        var today = await _clock.TodayAsync();
        From ??= new DateOnly(today.Year, today.Month, 1);
        To ??= today;
        if (To < From) (From, To) = (To, From);
        var from = From.Value; var to = To.Value;
        Government = await ServiceCatalogue.IsGovernmentAsync(_db);
        Departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
        Users = (await _db.Users.AsNoTracking().OrderBy(u => u.Username).Select(u => new { u.Id, u.Username }).ToListAsync()).Select(u => (u.Id, u.Username)).ToList();
        Services = (await _db.ServiceItems.AsNoTracking().OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync()).Select(s => (s.Id, s.Name)).ToList();

        var charges = _db.Charges.AsNoTracking().Where(c => c.Status != ChargeStatus.Cancelled && c.BusinessDate >= from && c.BusinessDate <= to);
        if (Department is int d) charges = charges.Where(c => c.DepartmentId == d);
        if (Service is int s) charges = charges.Where(c => c.ServiceItemId == s);
        if (Cashier is int cu) charges = charges.Where(c => c.CreatedByUserId == cu);
        charges = Status switch
        {
            "paid" => charges.Where(c => c.PaidAmount >= c.NetAmount),
            "unpaid" => charges.Where(c => c.PaidAmount == 0 && c.NetAmount > 0),
            "partial" => charges.Where(c => c.PaidAmount > 0 && c.PaidAmount < c.NetAmount),
            _ => charges,
        };
        var list = await charges.Select(c => new ChargeInfo(
            c.Fee, c.Discount, c.NetAmount, c.PaidAmount, c.LabOrderId != null, c.RadiologyOrderId != null, c.PharmacyIssueId != null || c.PharmacySaleId != null, c.SurgeryId != null,
            c.IpdAdmissionId != null, c.ServiceKind, c.ServiceItem!.ServiceCategory!.Code, c.Department!.Name, c.ServiceItem.Applicability,
            _db.IpdBedCharges.Any(b => b.ChargeId == c.Id), _db.EquipmentUsages.Any(u => u.ChargeId == c.Id),
            _db.IpdAdmissions.Any(a => a.AdmissionFeeChargeId == c.Id), _db.Appointments.Any(a => a.ChargeId == c.Id))).ToListAsync();
        static string Source(ChargeInfo c) =>
            c.Bed ? "Ward & bed charges" : c.Equip ? "NICU equipment" : c.AdmFee ? "Admission fees"
            : c.Lab || c.Cat == "LAB" ? "Laboratory" : c.Rad || c.Cat == "RAD" ? "Radiology"
            : c.Pharm || c.Cat == "PHARM" ? "Pharmacy" : c.Ot || c.Cat == "OT" ? "OT & procedures"
            : c.ServiceKind == ServiceKind.Consultation || c.Appt || c.Cat == "OPD" ? "OPD consultation" : c.Cat == "ER" || c.ServiceKind == ServiceKind.EmergencyRegistration ? "Emergency"
            : c.Ipd ? "Other inpatient services" : "Other services";
        static Row Sum(string label, IEnumerable<ChargeInfo> g)
        {
            var x = g.ToList();
            return new Row(label, x.Count, x.Sum(c => c.Fee), x.Sum(c => c.Discount), x.Sum(c => c.PaidAmount), x.Sum(c => c.NetAmount - c.PaidAmount));
        }
        BySource = list.GroupBy(c => Source(c)).Select(g => Sum(g.Key, g)).OrderByDescending(r => r.Billed).ToList();
        ByDepartment = list.GroupBy(c => c.Dept ?? "—").Select(g => Sum(g.Key, g)).OrderByDescending(r => r.Billed).ToList();
        ByApplicability = list.GroupBy(c => c.Applicability switch { ServiceApplicability.GovernmentOnly => "Government-only services", ServiceApplicability.PrivateOnly => "Private-only services", _ => "Government & private services" })
            .Select(g => Sum(g.Key, g)).ToList();
        Discounts = list.Sum(c => c.Discount);

        // ---- collections (receipts) – the same receipts give the department and the cashier view ----
        var receipts = _db.Receipts.AsNoTracking().Where(r => r.Status == ReceiptStatus.Valid && r.BusinessDate >= from && r.BusinessDate <= to);
        if (Cashier is int cid) receipts = receipts.Where(r => r.CashierUserId == cid);
        Cashiers = (await receipts.GroupBy(r => r.CashierName).Select(g => new { g.Key, N = g.Count(), A = g.Sum(r => r.AmountPaid), Adv = g.Where(r => r.PaymentMethod!.Code == BillingService.AdvanceMethodCode).Sum(r => r.AmountPaid) }).ToListAsync())
            .Select(x => new CashierRow(x.Key, x.N, x.A, x.Adv)).OrderByDescending(x => x.Amount).ToList();
        CashierCollected = Cashiers.Sum(c => c.Amount);
        DepartmentCollected = await _db.ReceiptLines.Where(l => receipts.Any(r => r.Id == l.ReceiptId)).SumAsync(l => (decimal?)l.Amount) ?? 0;
        var ledger = await _db.CashierLedger.AsNoTracking().Where(e => e.BusinessDate >= from && e.BusinessDate <= to && (Cashier == null || e.UserId == Cashier))
            .GroupBy(e => new { e.BusinessDate, e.EntryType }).Select(g => new { g.Key.BusinessDate, g.Key.EntryType, A = g.Sum(e => e.Amount) }).ToListAsync();
        Days = ledger.GroupBy(x => x.BusinessDate).OrderBy(g => g.Key).Select(g => new DayRow(g.Key,
            g.Where(x => x.EntryType is CashierEntryType.Collection or CashierEntryType.ReceiptCancellation).Sum(x => x.A),
            g.Where(x => x.EntryType == CashierEntryType.AdvanceReceived).Sum(x => x.A),
            -g.Where(x => x.EntryType is CashierEntryType.Refund or CashierEntryType.AdvanceRefunded).Sum(x => x.A))).ToList();
        AdvancesReceived = await _db.PatientAdvances.Where(a => a.Status == AdvanceStatus.Valid && a.BusinessDate >= from && a.BusinessDate <= to).SumAsync(a => (decimal?)a.Amount) ?? 0;
        AdvancesUnused = await _db.PatientAdvances.Where(a => a.Status == AdvanceStatus.Valid).SumAsync(a => (decimal?)(a.Amount - a.AppliedAmount - a.RefundedAmount)) ?? 0;
        Refunds = await _db.Refunds.Where(r => r.Status == RefundStatus.Completed && r.BusinessDate >= from && r.BusinessDate <= to).SumAsync(r => (decimal?)r.Amount) ?? 0;

        // ---- outstanding, admissions, waivers ----
        Outstanding = (await _db.Charges.AsNoTracking().Where(c => c.Status != ChargeStatus.Cancelled && c.NetAmount > c.PaidAmount && c.PatientId != null)
                .GroupBy(c => new { c.PatientId, c.Patient!.PatientNumber, c.Patient.FullName }).Select(g => new { g.Key, Due = g.Sum(c => c.NetAmount - c.PaidAmount) })
                .OrderByDescending(x => x.Due).Take(200).ToListAsync())
            .Select(x => new PatientRow(x.Key.PatientId!.Value, x.Key.PatientNumber, x.Key.FullName, x.Due)).ToList();
        Admissions = (await _db.IpdAdmissions.AsNoTracking().Where(a => a.AdmissionDate >= from && a.AdmissionDate <= to || a.DischargeDate >= from && a.DischargeDate <= to)
                .OrderByDescending(a => a.AdmittedUtc).Take(300)
                .Select(a => new { a.Id, a.AdmissionNumber, a.Patient!.FullName, a.AdmittedUtc, a.Status,
                    Net = _db.Charges.Where(c => c.IpdAdmissionId == a.Id && c.Status != ChargeStatus.Cancelled).Sum(c => (decimal?)c.NetAmount) ?? 0,
                    Paid = _db.Charges.Where(c => c.IpdAdmissionId == a.Id && c.Status != ChargeStatus.Cancelled).Sum(c => (decimal?)c.PaidAmount) ?? 0 }).ToListAsync())
            .Select(a => new AdmissionRow(a.Id, a.AdmissionNumber, a.FullName, a.AdmittedUtc, a.Status, a.Net, a.Paid)).ToList();
        Waived = await charges.Where(c => c.Discount > 0 || (c.Fee == 0 && c.NetAmount == 0)).OrderByDescending(c => c.CreatedUtc).Take(300).ToListAsync();
    }
}
