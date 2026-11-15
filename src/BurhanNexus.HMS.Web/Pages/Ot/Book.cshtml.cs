using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>Surgery booking for an existing registered patient (elective, urgent or emergency).</summary>
[HasPermission(Perm.OtCreateBooking)]
public class BookModel : OtPageModel
{
    private readonly HmsDbContext _db;
    private readonly IPatientService _patients;
    public BookModel(HmsDbContext db, IPatientService patients) { _db = db; _patients = patients; }

    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public bool Emergency { get; set; }
    [BindProperty] public BookingInput Input { get; set; } = new();

    public Patient? Patient { get; private set; }
    public string AgeText { get; private set; } = "—";
    public IpdAdmission? Admission { get; private set; }
    public IReadOnlyList<PatientListItem> Matches { get; private set; } = Array.Empty<PatientListItem>();
    public IReadOnlyList<OtProcedure> Procedures { get; private set; } = Array.Empty<OtProcedure>();
    public IReadOnlyList<OtTheatre> Theatres { get; private set; } = Array.Empty<OtTheatre>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<string>? Check { get; private set; }

    private async Task LoadAsync()
    {
        Nav("ot-book", "book");
        if (PatientId is long pid)
        {
            Patient = await _patients.GetAsync(pid);
            if (Patient is null) return;
            AgeText = AgeCalculator.Of(Patient, await Clock.TodayAsync())?.ToString() ?? "—";
            Admission = await _db.IpdAdmissions.AsNoTracking().Include(a => a.CurrentWard).Include(a => a.CurrentBed)
                .FirstOrDefaultAsync(a => a.PatientId == pid && IpdService.ActiveStatuses.Contains(a.Status));
            Procedures = await Setup.ProceduresAsync(activeOnly: true);
            Theatres = (await Setup.TheatresAsync(activeOnly: true)).ToList();
            Doctors = await _db.Doctors.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
            Departments = await _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        }
        else if (!string.IsNullOrWhiteSpace(Q)) Matches = (await _patients.SearchAsync(new PatientSearchQuery { Q = Q, PageSize = 15 })).Items;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        Input.IdempotencyKey = Guid.NewGuid();
        Input.Priority = Emergency ? SurgeryPriority.Emergency : SurgeryPriority.Elective;
        Input.IpdAdmissionId = Admission?.Id;
        Input.StartLocal = Emergency ? null : (await Clock.LocalNowAsync()).Date.AddDays(1).AddHours(9);
    }

    public async Task<IActionResult> OnPostCheckAsync()
    {
        await LoadAsync();
        if (Input.StartLocal is DateTime s && Theatres.FirstOrDefault(t => t.Id == Input.TheatreId) is { } th)
        {
            var start = await Clock.LocalToUtcAsync(s);
            var dur = Input.DurationMinutes ?? Procedures.FirstOrDefault(p => p.Id == Input.ProcedureId)?.DurationMinutes ?? 60;
            var team = new[] { Input.SurgeonDoctorId, Input.AssistantDoctorId, Input.AnesthetistDoctorId }.Where(x => x is not null).Select(x => (x, (int?)null));
            Check = await Surgeries.ConflictsAsync(null, th.Id, start, start.AddMinutes(dur), team);
        }
        else ModelState.AddModelError("", "Select the theatre and the start time to check availability.");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.PatientId = PatientId ?? 0;
        var r = await Surgeries.BookAsync(Input);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = $"Surgery {r.Value!.Number} booked.";
        return Redirect($"/Ot/Surgery/{r.Value.Id}");
    }
}
