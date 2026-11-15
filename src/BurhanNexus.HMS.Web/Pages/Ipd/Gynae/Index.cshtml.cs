using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd.Gynae;

/// <summary>Gynae &amp; ANC unit: find a patient, active pregnancies (high-risk first) and Gynae inpatients.</summary>
[HasPermission(Perm.IpdViewGynae)]
public class IndexModel : IpdPageModel
{
    private readonly HmsDbContext _db;
    private readonly IPatientService _patients;
    public IndexModel(HmsDbContext db, IPatientService patients) { _db = db; _patients = patients; }

    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "risk")] public bool HighRiskOnly { get; set; }
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<Pregnancy> Pregnancies { get; private set; } = Array.Empty<Pregnancy>();
    public IReadOnlyList<AdmissionRow> Inpatients { get; private set; } = Array.Empty<AdmissionRow>();
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync()
    {
        Nav("ipd-gynae", "gynae");
        Today = await Clock.TodayAsync();
        if (!string.IsNullOrWhiteSpace(Q)) Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 20 })).Items;
        var x = _db.Pregnancies.AsNoTracking().Include(p => p.Patient).Where(p => p.Status == PregnancyStatus.Active);
        if (HighRiskOnly) x = x.Where(p => p.IsHighRisk);
        Pregnancies = await x.OrderByDescending(p => p.IsHighRisk).ThenBy(p => p.Edd).Take(300).ToListAsync();
        Inpatients = await Query.AdmissionsAsync("active", null, null, AdmissionKind.Gynae, await WardsAsync());
    }
}
