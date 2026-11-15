using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>One pregnancy: details (audited changes), high-risk flag, ANC visits and the full pregnancy timeline.</summary>
[HasPermission(Perm.IpdViewGynae)]
public class PregnancyModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    public PregnancyModel(HmsDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "correct")] public long? CorrectId { get; set; }
    [BindProperty] public PregnancyInput Preg { get; set; } = new();
    [BindProperty] public HighRiskInput Risk { get; set; } = new();
    [BindProperty] public RecordInput Record { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public PregnancyStatus CloseAs { get; set; } = PregnancyStatus.Ended;
    [BindProperty] public string? Note { get; set; }

    public Pregnancy G { get; private set; } = null!;
    public DateOnly Today { get; private set; }
    public IReadOnlyList<RecordView> Visits { get; private set; } = Array.Empty<RecordView>();
    public IReadOnlyList<ClinicalFormField> Fields { get; private set; } = Array.Empty<ClinicalFormField>();
    public IReadOnlyList<TimelineItem> Timeline { get; private set; } = Array.Empty<TimelineItem>();

    private async Task<bool> LoadAsync()
    {
        Nav("ipd-gynae", "gynae");
        var g = await _db.Pregnancies.AsNoTracking().Include(p => p.Patient).FirstOrDefaultAsync(p => p.Id == Id);
        if (g is null) return false;
        G = g;
        Today = await Clock.TodayAsync();
        Visits = await Query.RecordsAsync(g.PatientId, null, Id, new[] { RecordKind.AncVisit });
        Fields = await Setup.FieldsAsync(RecordKind.AncVisit, activeOnly: true);
        Timeline = (await Query.PregnancyTimelineAsync(Id)).OrderByDescending(t => t.Utc).ToList();
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        RowVersion = G.RowVersion;
        Preg = new PregnancyInput { Lmp = G.Lmp, Edd = G.Edd, Gravida = G.Gravida, Para = G.Para, Abortions = G.Abortions, LivingChildren = G.LivingChildren,
            PreviousComplications = G.PreviousComplications, MedicalHistory = G.MedicalHistory, RiskFactors = G.RiskFactors };
        Risk = new HighRiskInput { IsHighRisk = G.IsHighRisk, Reason = G.HighRiskReason, Assessment = G.HighRiskAssessment, Referral = G.HighRiskReferral, FollowUpPlan = G.FollowUpPlan };
        return Page();
    }

    private async Task<IActionResult> AfterAsync(Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return Redirect($"/Ipd/Pregnancy/{Id}"); }
        if (!await LoadAsync()) return NotFound();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageAnc) is { } deny) return deny;
        return await AfterAsync(await Maternity.UpdatePregnancyAsync(Id, Preg, RowVersion), "Pregnancy details saved (changes are audited).");
    }

    public async Task<IActionResult> OnPostRiskAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageAnc) is { } deny) return deny;
        return await AfterAsync(await Maternity.SetHighRiskAsync(Id, Risk), Risk.IsHighRisk ? "Marked as high-risk pregnancy." : "High-risk flag removed.");
    }

    public async Task<IActionResult> OnPostCloseAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageAnc) is { } deny) return deny;
        return await AfterAsync(await Maternity.ClosePregnancyAsync(Id, CloseAs, Note), "Pregnancy closed.");
    }

    public async Task<IActionResult> OnPostRecordAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageAnc) is { } deny) return deny;
        if (Record.CorrectsRecordId is not null && await DenyUnlessAsync(Perm.IpdCorrectRecords) is { } deny2) return deny2;
        var g = await _db.Pregnancies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == Id);
        if (g is null) return NotFound();
        Record.Kind = RecordKind.AncVisit;
        Record.PatientId = g.PatientId;
        Record.PregnancyId = Id;
        Record.AdmissionId = null;
        return await AfterAsync(await Ipd.AddRecordAsync(Record), "ANC visit saved.");
    }
}
