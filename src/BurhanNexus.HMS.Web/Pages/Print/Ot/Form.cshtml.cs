using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Pages.Ot;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Print.Ot;

/// <summary>The 14 printable A4 OT documents. Printing only reads the stored records (the view is audited); header on plain paper or space for the letterhead.</summary>
[HasPermission(Perm.OtPrintDocuments)]
public class FormModel : OtPageModel
{
    private readonly HmsDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;
    public FormModel(HmsDbContext db, ISettingsService settings, IAuditService audit) { _db = db; _settings = settings; _audit = audit; }

    public static readonly IReadOnlyDictionary<string, string> Forms = new Dictionary<string, string>
    {
        ["booking"] = "OT Booking Slip",
        ["schedule"] = "OT Schedule",
        ["preop"] = "Pre-operative Assessment & Checklist",
        ["consent"] = "Consent Form",
        ["anesthesia"] = "Pre-anesthesia Assessment",
        ["anesthesia-record"] = "Anesthesia Record",
        ["checklist"] = "Surgical Safety Checklist",
        ["opnote"] = "Operation Notes",
        ["intraop"] = "Intra-operative Record",
        ["specimen"] = "Specimen Dispatch Form & Labels",
        ["pacu"] = "Recovery Room (PACU) Chart",
        ["postop"] = "Post-operative Instructions",
        ["bill"] = "OT Bill",
        ["handover"] = "Recovery Transfer / Handover",
    };

    private static readonly Dictionary<string, RecordKind[]> FormKinds = new()
    {
        ["preop"] = new[] { RecordKind.PreOpAssessment },
        ["anesthesia"] = new[] { RecordKind.AnesthesiaAssessment },
        ["anesthesia-record"] = new[] { RecordKind.IntraOpMonitoring },
        ["intraop"] = new[] { RecordKind.IntraOpMonitoring },
        ["pacu"] = new[] { RecordKind.PacuObservation },
        ["postop"] = new[] { RecordKind.PostOpRecord },
    };

    [BindProperty(SupportsGet = true)] public string Form { get; set; } = "";
    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "date")] public DateOnly? Date { get; set; }
    [BindProperty(SupportsGet = true, Name = "view")] public string? View { get; set; }

    public string Title => Form == "consent" && Consent is not null ? $"{Consent.ConsentTypeName} – Consent Form" : Forms.TryGetValue(Form, out var t) ? t : Form;
    public HospitalProfile Hospital { get; private set; } = null!;
    public OtSettings Settings { get; private set; } = new();
    public Surgery? S { get; private set; }
    public Patient? P => S?.Patient;
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<SurgeryTeamMember> Team { get; private set; } = Array.Empty<SurgeryTeamMember>();
    public Dictionary<RecordKind, IReadOnlyList<RecordView>> Records { get; } = new();
    public Dictionary<RecordKind, IReadOnlyList<ClinicalFormField>> Fields { get; } = new();
    public Dictionary<ChecklistStage, IReadOnlyList<OtChecklistItem>> Items { get; } = new();
    public Dictionary<int, SurgeryChecklistEntry> Latest { get; private set; } = new();
    public IReadOnlyList<SurgeryChecklistStage> Stages { get; private set; } = Array.Empty<SurgeryChecklistStage>();
    public SurgeryConsent? Consent { get; private set; }
    public string? ConsentText { get; private set; }
    public IReadOnlyList<SurgeryConsent> Consents { get; private set; } = Array.Empty<SurgeryConsent>();
    public OperativeNote? Note { get; private set; }
    public IReadOnlyList<OtDrugEntry> Drugs { get; private set; } = Array.Empty<OtDrugEntry>();
    public IReadOnlyList<SurgerySpecimen> Specimens { get; private set; } = Array.Empty<SurgerySpecimen>();
    public RecoveryEpisode? Recovery { get; private set; }
    public IReadOnlyList<OtBillLine> Bill { get; private set; } = Array.Empty<OtBillLine>();
    public IReadOnlyList<SurgeryRow> Schedule { get; private set; } = Array.Empty<SurgeryRow>();
    public IReadOnlyList<OtConsumption> Consumption { get; private set; } = Array.Empty<OtConsumption>();
    public DateOnly Today { get; private set; }
    public DateOnly ScheduleFrom { get; private set; }
    public DateOnly ScheduleTo { get; private set; }
    public bool IsPrivate { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Forms.ContainsKey(Form)) return NotFound();
        if (Form == "bill" && !await CanAsync(Perm.OtViewBilling)) return Forbid();
        Hospital = (await _settings.GetProfileAsync())!;
        Settings = await Setup.SettingsAsync();
        IsPrivate = await Setup.IsPrivateAsync();
        Today = await Clock.TodayAsync();
        _zone = await Clock.ZoneAsync();
        if (Form == "schedule")
        {
            ScheduleFrom = Date ?? Today;
            ScheduleTo = View == "week" ? ScheduleFrom.AddDays(6) : ScheduleFrom;
            Schedule = (await Query.SurgeriesAsync(new OtFilter { From = ScheduleFrom, To = ScheduleTo }))
                .Where(r => r.Status != SurgeryStatus.Cancelled).OrderBy(r => r.Theatre).ThenBy(r => r.PlannedStartUtc).ToList();
            await _audit.LogAsync(AuditActions.OtFormPrinted, "OtForm", $"schedule/{ScheduleFrom:yyyy-MM-dd}", $"OT schedule {ScheduleFrom:yyyy-MM-dd}..{ScheduleTo:yyyy-MM-dd} viewed / printed");
            return Page();
        }
        long surgeryId = Id;
        if (Form == "consent")
        {
            Consent = await _db.SurgeryConsents.AsNoTracking().Include(c => c.ConsentType).FirstOrDefaultAsync(c => c.Id == Id);
            if (Consent is null) return NotFound();
            surgeryId = Consent.SurgeryId;
        }
        S = await Query.SurgeryAsync(surgeryId);
        if (S is null) return NotFound();
        AgeText = AgeCalculator.Of(S.Patient!, Today)?.ToString() ?? "—";
        Team = await Query.TeamAsync(surgeryId, includeRemoved: false);
        foreach (var k in FormKinds.GetValueOrDefault(Form, Array.Empty<RecordKind>()))
        {
            Records[k] = await Query.RecordsAsync(surgeryId, new[] { k }, includeSuperseded: false);
            Fields[k] = await HttpContext.RequestServices.GetRequiredService<IIpdSetupService>().FieldsAsync(k, activeOnly: false);
        }
        if (Form is "preop" or "checklist")
        {
            foreach (var st in Form == "preop" ? new[] { ChecklistStage.PreOp } : new[] { ChecklistStage.SignIn, ChecklistStage.TimeOut, ChecklistStage.SignOut })
                Items[st] = await Setup.ChecklistAsync(st, activeOnly: false);
            Latest = (await _db.SurgeryChecklistEntries.AsNoTracking().Where(e => e.SurgeryId == surgeryId).OrderBy(e => e.Id).ToListAsync())
                .GroupBy(e => e.ItemId).ToDictionary(g => g.Key, g => g.Last());
            Stages = await _db.SurgeryChecklistStages.AsNoTracking().Where(x => x.SurgeryId == surgeryId).ToListAsync();
            // Items no longer active but answered for this surgery stay on its printout.
            foreach (var st in Items.Keys.ToList())
                Items[st] = Items[st].Where(i => i.IsActive || Latest.ContainsKey(i.Id)).ToList();
        }
        if (Form == "preop") Consents = await _db.SurgeryConsents.AsNoTracking().Where(c => c.SurgeryId == surgeryId).ToListAsync();
        if (Form is "opnote" or "intraop" or "postop" or "specimen")
        {
            var notes = await _db.OperativeNotes.AsNoTracking().Where(n => n.SurgeryId == surgeryId).OrderBy(n => n.Version).ToListAsync();
            Note = notes.LastOrDefault(n => n.Status == NoteStatus.Final) ?? notes.LastOrDefault();
        }
        var phase = Form switch { "anesthesia-record" => "Anesthesia", "intraop" => "IntraOp", "pacu" => "Recovery", _ => null };
        if (phase is not null) Drugs = await _db.OtDrugEntries.AsNoTracking().Where(d => d.SurgeryId == surgeryId && d.Phase == phase).OrderBy(d => d.GivenUtc).ToListAsync();
        if (Form is "specimen" or "intraop") Specimens = await _db.SurgerySpecimens.AsNoTracking().Include(x => x.LabOrder).Where(x => x.SurgeryId == surgeryId).OrderBy(x => x.Id).ToListAsync();
        if (Form == "intraop") Consumption = await _db.OtConsumptions.AsNoTracking().Where(c => c.SurgeryId == surgeryId).OrderBy(c => c.Id).ToListAsync();
        if (Form is "pacu" or "handover" or "postop") Recovery = await _db.RecoveryEpisodes.AsNoTracking().FirstOrDefaultAsync(r => r.SurgeryId == surgeryId);
        if (Form == "bill") Bill = await Billing.BillAsync(surgeryId);
        if (Form == "consent")
        {
            var who = Team.FirstOrDefault(t => t.Role == OtRole.PrimarySurgeon)?.Name;
            ConsentText = (Consent!.ConsentType?.TemplateText ?? "")
                .Replace("{patient}", S.Patient!.FullName).Replace("{procedure}", S.ProcedureName).Replace("{hospital}", Hospital.HospitalName).Replace("{surgeon}", who ?? "the surgeon");
        }
        await _audit.LogAsync(AuditActions.OtFormPrinted, "OtForm", $"{Form}/{Id}", $"{Title} viewed / printed for {S.Patient!.PatientNumber} ({S.Number})");
        return Page();
    }

    public string Member(OtRole role) => string.Join(", ", Team.Where(t => t.Role == role).Select(t => t.Name)) is { Length: > 0 } n ? n : "—";

    /// <summary>Reuses the IPD chart drawing: values only as recorded, gaps = not recorded, limits only if the hospital configured them.</summary>
    public IHtmlContent Chart(IReadOnlyList<RecordView> records, ClinicalFormField field) => ClinicalChart.Render(records, field, _zone);
    private TimeZoneInfo _zone = TimeZoneInfo.Utc;
}
