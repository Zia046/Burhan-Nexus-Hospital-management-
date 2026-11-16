using System.Globalization;
using System.Net;
using System.Text;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Pages.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Print.Ipd;

/// <summary>
/// The 22 printable A4 IPD / maternity / NICU forms. Printing and reprinting only read the stored records – nothing is
/// created or changed (the view is audited). Header on plain paper, or blank space for the hospital's pre-printed letterhead.
/// </summary>
[HasPermission(Perm.IpdPrintForms, Perm.IpdExportPdf)]
public class FormModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;
    public FormModel(HmsDbContext db, ISettingsService settings, IAuditService audit) { _db = db; _settings = settings; _audit = audit; }

    /// <summary>Form key → title. Keys are used in the URL: /Print/Ipd/{form}/{id}.</summary>
    public static readonly IReadOnlyDictionary<string, string> Forms = new Dictionary<string, string>
    {
        ["admission"] = "IPD Admission Form",
        ["progress"] = "Daily Progress Notes",
        ["vitals"] = "Vital Signs Chart",
        ["nursing"] = "Nursing Assessment & Notes",
        ["mar"] = "Medication Administration Record",
        ["transfer"] = "Patient Transfer Form",
        ["handover"] = "Nursing Shift Handover",
        ["discharge"] = "Discharge Summary",
        ["daily-bill"] = "Daily IPD Bill",
        ["bill"] = "Final IPD Bill",
        ["investigations"] = "Investigation Summary",
        ["gynae"] = "Gynae / Obstetric Record",
        ["anc"] = "Antenatal (ANC) Card",
        ["labour"] = "Labour Record",
        ["partograph"] = "Partograph / Labour Observation Chart",
        ["delivery"] = "Delivery Record",
        ["newborn"] = "Newborn Record",
        ["birth"] = "Birth Record (for notification)",
        ["postnatal"] = "Postnatal Record",
        ["nicu-admission"] = "NICU Admission Form",
        ["nicu-chart"] = "NICU Observation Chart",
        ["feeding"] = "NICU Feeding & Intake / Output Chart",
        ["nicu-discharge"] = "NICU Discharge Summary",
    };

    [BindProperty(SupportsGet = true)] public string Form { get; set; } = "";
    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "date")] public DateOnly? Date { get; set; }
    /// <summary>Discharge slip: the doctor's printed name is optional (the stored record keeps it). ?name=0 hides it.</summary>
    [BindProperty(SupportsGet = true, Name = "name")] public string? NameMode { get; set; }
    public bool ShowDoctorName => NameMode != "0";

    public string Title => Forms.TryGetValue(Form, out var t) ? t : Form;
    public HospitalProfile Hospital { get; private set; } = null!;
    public IpdSettings Settings { get; private set; } = new();
    public bool Private { get; private set; }
    public IpdAdmission? A { get; private set; }
    public Patient P { get; private set; } = null!;
    public string AgeText { get; private set; } = "—";
    public Dictionary<RecordKind, IReadOnlyList<RecordView>> Records { get; } = new();
    public Dictionary<RecordKind, IReadOnlyList<ClinicalFormField>> Fields { get; } = new();
    public IpdTransfer? Transfer { get; private set; }
    public ShiftHandover? Handover { get; private set; }
    public IReadOnlyList<BillLine> Bill { get; private set; } = Array.Empty<BillLine>();
    /// <summary>Update 10: admission fee shown on the two-copy admission form.</summary>
    public Charge? AdmissionFeeCharge { get; private set; }
    public bool ShowFee { get; private set; }
    public string FeeStatus => AdmissionFeeCharge is not { } c ? "—" : c.Fee > 0 && c.NetAmount == 0 ? "FREE (waived)" : c.PaidAmount >= c.NetAmount ? "PAID" : c.PaidAmount > 0 ? "PARTIAL" : "UNPAID";
    public IReadOnlyList<MedicationOrder> Orders { get; private set; } = Array.Empty<MedicationOrder>();
    public IReadOnlyList<MedicationDose> Doses { get; private set; } = Array.Empty<MedicationDose>();
    public IReadOnlyList<InvestigationRow> Investigations { get; private set; } = Array.Empty<InvestigationRow>();
    public IReadOnlyList<IpdBedAllocation> Allocations { get; private set; } = Array.Empty<IpdBedAllocation>();
    public Pregnancy? Pregnancy { get; private set; }
    public LabourRecord? Labour { get; private set; }
    public DeliveryRecord? Delivery { get; private set; }
    public NewbornRecord? Newborn { get; private set; }
    public NicuDetails? Nicu { get; private set; }
    public IReadOnlyList<DeliveryRecord> Deliveries { get; private set; } = Array.Empty<DeliveryRecord>();
    public IReadOnlyList<IpdAdmission> Babies { get; private set; } = Array.Empty<IpdAdmission>();
    public IpdAdmission? Mother { get; private set; }
    public DateOnly Today { get; private set; }

    private static readonly Dictionary<string, RecordKind[]> FormKinds = new()
    {
        ["progress"] = new[] { RecordKind.ProgressNote },
        ["vitals"] = new[] { RecordKind.Vitals },
        ["nursing"] = new[] { RecordKind.NursingAssessment, RecordKind.NursingNote },
        ["discharge"] = Array.Empty<RecordKind>(),
        ["labour"] = new[] { RecordKind.LabourAssessment, RecordKind.LabourObservation },
        ["partograph"] = new[] { RecordKind.LabourObservation },
        ["postnatal"] = new[] { RecordKind.PostnatalAssessment },
        ["nicu-admission"] = new[] { RecordKind.NicuAssessment },
        ["nicu-chart"] = new[] { RecordKind.NicuObservation, RecordKind.NicuCare },
        ["feeding"] = new[] { RecordKind.NicuFeeding },
        ["nicu-discharge"] = new[] { RecordKind.NicuProgress },
        ["gynae"] = new[] { RecordKind.GynaeHistory, RecordKind.GynaeAssessment },
        ["anc"] = new[] { RecordKind.AncVisit },
    };

    private async Task<bool> AdmissionAsync(long id)
    {
        A = await Query.AdmissionAsync(id);
        if (A is null || !await CanAccessAsync(A)) return false;
        P = A.Patient!;
        return true;
    }

    private async Task LoadKindsAsync(long? admissionId, long? pregnancyId)
    {
        foreach (var k in FormKinds.GetValueOrDefault(Form, Array.Empty<RecordKind>()))
        {
            Records[k] = await Query.RecordsAsync(P.Id, admissionId, pregnancyId, new[] { k }, includeSuperseded: false);
            Fields[k] = await Setup.FieldsAsync(k, activeOnly: false);
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Forms.ContainsKey(Form)) return NotFound();
        Hospital = (await _settings.GetProfileAsync())!;
        Settings = await Setup.SettingsAsync();
        Private = await Setup.IsPrivateAsync();
        Today = await Clock.TodayAsync();
        _zone = await Clock.ZoneAsync();
        if (Form is "bill" or "daily-bill" && !await CanAsync(Perm.IpdViewFinancial)) return Forbid();
        if (Form is "gynae" or "anc" && !await CanAsync(Perm.IpdViewGynae)) return Forbid();
        if (Form == "partograph" && !await CanAsync(Perm.IpdPartograph) && !await CanAsync(Perm.IpdLabourObservations)) return Forbid();

        switch (Form)
        {
            case "transfer":
                Transfer = await _db.IpdTransfers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == Id);
                if (Transfer is null || !await AdmissionAsync(Transfer.AdmissionId)) return NotFound();
                break;
            case "handover":
                Handover = await _db.ShiftHandovers.AsNoTracking().FirstOrDefaultAsync(h => h.Id == Id);
                if (Handover is null || !await AdmissionAsync(Handover.AdmissionId)) return NotFound();
                break;
            case "gynae":
                var gp = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == Id);
                if (gp is null) return NotFound();
                P = gp;
                await LoadKindsAsync(null, null);
                Deliveries = await _db.DeliveryRecords.AsNoTracking().Where(d => d.MotherPatientId == Id).OrderBy(d => d.DeliveredUtc).ToListAsync();
                break;
            case "anc":
                Pregnancy = await _db.Pregnancies.AsNoTracking().Include(p => p.Patient).FirstOrDefaultAsync(p => p.Id == Id);
                if (Pregnancy is null) return NotFound();
                P = Pregnancy.Patient!;
                await LoadKindsAsync(null, Id);
                break;
            case "delivery":
            case "newborn":
            case "birth":
                if (Form == "delivery") Delivery = await _db.DeliveryRecords.AsNoTracking().Include(d => d.Mother).Include(d => d.Newborns).ThenInclude(n => n.NewbornPatient).FirstOrDefaultAsync(d => d.Id == Id);
                else
                {
                    Newborn = await _db.NewbornRecords.AsNoTracking().Include(n => n.NewbornPatient).FirstOrDefaultAsync(n => n.Id == Id);
                    if (Newborn is null) return NotFound();
                    Delivery = await _db.DeliveryRecords.AsNoTracking().Include(d => d.Mother).Include(d => d.Newborns).FirstOrDefaultAsync(d => d.Id == Newborn.DeliveryId);
                }
                if (Delivery is null || !await AdmissionAsync(Delivery.AdmissionId)) return NotFound();
                if (Newborn?.NewbornPatient is { } np) P = np;
                break;
            default:
                if (!await AdmissionAsync(Id)) return NotFound();
                await LoadKindsAsync(Id, null);
                Labour = await _db.LabourRecords.AsNoTracking().FirstOrDefaultAsync(l => l.AdmissionId == Id);
                Nicu = await _db.NicuDetails.AsNoTracking().FirstOrDefaultAsync(n => n.AdmissionId == Id);
                Deliveries = await _db.DeliveryRecords.AsNoTracking().Include(d => d.Newborns).ThenInclude(n => n.NewbornPatient).Where(d => d.AdmissionId == Id).ToListAsync();
                Babies = await _db.IpdAdmissions.AsNoTracking().Include(b => b.Patient).Where(b => b.MotherAdmissionId == Id).ToListAsync();
                if (A!.MotherAdmissionId is long m) Mother = await _db.IpdAdmissions.AsNoTracking().Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == m);
                Newborn = await _db.NewbornRecords.AsNoTracking().FirstOrDefaultAsync(n => n.NewbornPatientId == A.PatientId);
                Allocations = await Query.AllocationsAsync(Id);
                if (Form is "mar" or "discharge" or "nicu-discharge")
                {
                    Orders = await _db.MedicationOrders.AsNoTracking().Where(o => o.AdmissionId == Id).OrderBy(o => o.StartUtc).ToListAsync();
                    Doses = await _db.MedicationDoses.AsNoTracking().Where(d => d.AdmissionId == Id).OrderBy(d => d.ScheduledUtc).ToListAsync();
                }
                if (Form == "admission")
                {
                    // Update 10: admission fee and its payment status on both copies of the admission form.
                    var feeSvc = Settings.AdmissionServiceItemId;
                    AdmissionFeeCharge = A.AdmissionFeeChargeId is long fc ? await _db.Charges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == fc)
                        : feeSvc is null ? null : await _db.Charges.AsNoTracking().Where(c => c.IpdAdmissionId == Id && c.ServiceItemId == feeSvc && c.Status != ChargeStatus.Cancelled).OrderBy(c => c.Id).FirstOrDefaultAsync();
                    ShowFee = await CanAsync(Perm.IpdViewFinancial) || await CanAsync(Perm.IpdRecordAdmissionPayment);
                }
                if (Form is "bill" or "daily-bill")
                {
                    Bill = await Query.BillAsync(Id);
                    var day = Date ?? Today;
                    if (Form == "daily-bill") Bill = Bill.Where(b => b.Date == day).ToList();
                    Date = day;
                }
                if (Form is "investigations" or "discharge" or "nicu-discharge") Investigations = await Query.InvestigationsAsync(Id, A.PatientId);
                if ((A.PregnancyId ?? Labour?.PregnancyId) is long pg && await CanAsync(Perm.IpdViewGynae))
                    Pregnancy = await _db.Pregnancies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pg);
                break;
        }
        AgeText = AgeCalculator.Of(P, Today)?.ToString() ?? "—";
        await _audit.LogAsync(AuditActions.IpdFormPrinted, "IpdForm", $"{Form}/{Id}", $"{Title} viewed / printed for {P.PatientNumber}");
        return Page();
    }

    // ---------------------------------------------------------------------------------------------
    // Charts – drawn only from the recorded values. Missing values leave gaps (never treated as normal).
    // Dashed lines appear only for limits configured by the hospital (with their protocol source).
    // ---------------------------------------------------------------------------------------------

    public IHtmlContent Chart(IReadOnlyList<RecordView> records, ClinicalFormField field, DateTime? fromUtc = null, DateTime? toUtc = null) =>
        BurhanNexus.HMS.Web.Services.ClinicalChart.Render(records, field, _zone, fromUtc, toUtc);

    private TimeZoneInfo _zone = TimeZoneInfo.Utc;
    private DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}
