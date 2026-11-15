using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Gynae;

/// <summary>A patient's Gynae record: history, assessments, all pregnancies (never overwritten) and admissions.</summary>
[HasPermission(Perm.IpdViewGynae)]
public class PatientModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    private readonly IPatientService _patients;
    public PatientModel(HmsDbContext db, IPatientService patients) { _db = db; _patients = patients; }

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "correct")] public long? CorrectId { get; set; }
    [BindProperty] public RecordInput Record { get; set; } = new();
    [BindProperty] public PregnancyInput Preg { get; set; } = new();

    public Patient P { get; private set; } = null!;
    public string AgeText { get; private set; } = "—";
    public IReadOnlyList<Pregnancy> Pregnancies { get; private set; } = Array.Empty<Pregnancy>();
    public IReadOnlyList<AdmissionRow> Admissions { get; private set; } = Array.Empty<AdmissionRow>();
    public Dictionary<RecordKind, IReadOnlyList<RecordView>> Records { get; } = new();
    public Dictionary<RecordKind, IReadOnlyList<ClinicalFormField>> Fields { get; } = new();
    public DateOnly Today { get; private set; }
    public static readonly RecordKind[] Kinds = { RecordKind.GynaeHistory, RecordKind.GynaeAssessment };

    private async Task<bool> LoadAsync()
    {
        Nav("ipd-gynae", "gynae");
        var p = await _patients.GetAsync(Id);
        if (p is null) return false;
        P = p;
        Today = await Clock.TodayAsync();
        AgeText = AgeCalculator.Of(P, Today)?.ToString() ?? "—";
        Pregnancies = await _db.Pregnancies.AsNoTracking().Where(x => x.PatientId == Id).OrderByDescending(x => x.Id).ToListAsync();
        Admissions = await Query.AdmissionsAsync("all", null, null, null, await WardsAsync(), patientId: Id);
        var recs = await Query.RecordsAsync(Id, null, null, Kinds);
        foreach (var k in Kinds)
        {
            Records[k] = recs.Where(r => r.Record.Kind == k).ToList();
            Fields[k] = await Setup.FieldsAsync(k, activeOnly: true);
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

    private async Task<IActionResult> AfterAsync(Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return Redirect($"/Ipd/Gynae/Patient/{Id}"); }
        if (!await LoadAsync()) return NotFound();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostRecordAsync()
    {
        if (!Kinds.Contains(Record.Kind)) return BadRequest();
        if (await DenyUnlessAsync(Perm.IpdEnterGynae) is { } deny) return deny;
        if (Record.CorrectsRecordId is not null && await DenyUnlessAsync(Perm.IpdCorrectRecords) is { } deny2) return deny2;
        Record.PatientId = Id;
        Record.AdmissionId = null;
        return await AfterAsync(await Ipd.AddRecordAsync(Record), "Saved.");
    }

    public async Task<IActionResult> OnPostPregnancyAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageAnc) is { } deny) return deny;
        var r = await Maternity.RegisterPregnancyAsync(Id, Preg);
        if (r.Succeeded) { FlashSuccess = "Pregnancy registered."; return Redirect($"/Ipd/Pregnancy/{r.Value}"); }
        return await AfterAsync(r, "");
    }
}
