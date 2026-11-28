using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

[HasPermission(Perm.PatientsView)]
public class ProfileModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IEncounterService _encounters;
    private readonly HospitalClock _clock;

    public ProfileModel(IPatientService patients, IEncounterService encounters, HospitalClock clock)
    {
        _patients = patients;
        _encounters = encounters;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "type")] public string? TypeFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "from")] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;
    [BindProperty] public string? Reason { get; set; }

    public Patient Patient { get; private set; } = null!;
    public string AgeText { get; private set; } = "—";
    public bool CanSeeVisits { get; private set; }
    public PagedResult<EncounterListItem> Visits { get; private set; } = new(Array.Empty<EncounterListItem>(), 0, 1, 25);
    public int OpdCount { get; private set; }
    public int EmergencyCount { get; private set; }
    /// <summary>Phase 5: recent medicines issued to the patient (only with "View Patient Medicine History").</summary>
    public IReadOnlyList<BurhanNexus.HMS.Infrastructure.Pharmacy.PatientMedicineRow>? Medicines { get; private set; }
    /// <summary>Phase 6: radiology history section (only with "View Patient Radiology History").</summary>
    public IReadOnlyList<BurhanNexus.HMS.Infrastructure.Radiology.RadiologyExamRow>? Radiology { get; private set; }
    /// <summary>Phase 7: IPD admissions (only with "View Inpatient Records"; limited to the user's wards).</summary>
    public IReadOnlyList<BurhanNexus.HMS.Infrastructure.Ipd.AdmissionRow>? Admissions { get; private set; }
    public IReadOnlyList<Surgery>? Surgeries { get; private set; }
    /// <summary>Update 18: visit-wise disease history (never overwritten).</summary>
    public IReadOnlyList<PatientDisease>? Diseases { get; private set; }
    /// <summary>Update 18: consultations with Return Fee, Free by Doctor and Discount.</summary>
    public IReadOnlyList<BurhanNexus.HMS.Infrastructure.Patients.ConsultationRow>? Consultations { get; private set; }
    public bool CanReturnFee { get; private set; }
    public bool CanFreeByDoctor { get; private set; }
    public bool CanDiscount { get; private set; }
    public bool CanAddDisease { get; private set; }
    private IDiseaseService DiseaseSvc => HttpContext.RequestServices.GetRequiredService<IDiseaseService>();
    private IAppointmentService Appointments => HttpContext.RequestServices.GetRequiredService<IAppointmentService>();

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "patients";
        var p = await _patients.GetAsync(Id);
        if (p is null) return NotFound();
        Patient = p;
        AgeText = AgeCalculator.Of(p, await _clock.TodayAsync())?.ToString() ?? "—";
        CanSeeVisits = await CanAsync(Perm.PatientsVisits);
        if (await CanAsync(Perm.PharmViewPatientHistory))
            Medicines = (await HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Pharmacy.IPharmacyQueryService>().PatientHistoryAsync(Id, false)).Take(10).ToList();
        if (await CanAsync(Perm.RadViewHistory))
            Radiology = await HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Radiology.IRadiologyService>()
                .ExamsAsync(new BurhanNexus.HMS.Infrastructure.Radiology.RadiologyWorklistQuery { PatientId = Id, Take = 10 });
        if (await CanAsync(Perm.IpdViewPatients))
        {
            var ipdQuery = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Ipd.IIpdQueryService>();
            var wards = await ipdQuery.VisibleWardIdsAsync(CurrentUserId, await CanAsync(Perm.IpdViewAllWards));
            Admissions = await ipdQuery.AdmissionsAsync("all", null, null, null, wards, patientId: Id, take: 10);
        }
        if (await CanAsync(Perm.OtDashboard))
            Surgeries = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Persistence.HmsDbContext>().Surgeries
                    .Where(s => s.PatientId == Id).OrderByDescending(s => s.PlannedStartUtc).Take(10).Select(s => new Surgery
                    { Id = s.Id, Number = s.Number, ProcedureName = s.ProcedureName, PlannedStartUtc = s.PlannedStartUtc, Status = s.Status, Priority = s.Priority }));
        if (await CanAsync(Perm.DiseasesView))
        {
            Diseases = await DiseaseSvc.HistoryAsync(Id);
            CanAddDisease = await CanAsync(Perm.PatientsEdit) || await CanAsync(Perm.OpdAdd);
        }
        if (await CanAsync(Perm.AppointmentsView))
        {
            Consultations = await Appointments.PatientConsultationsAsync(Id);
            CanReturnFee = await CanAsync(Perm.FinanceProcessRefunds);
            CanFreeByDoctor = await CanAsync(Perm.AppointmentsFreeByDoctor);
            CanDiscount = await CanAsync(Perm.FinanceDiscount);
        }
        if (CanSeeVisits)
        {
            EncounterType? type = TypeFilter switch { "opd" => EncounterType.Opd, "emergency" => EncounterType.Emergency, _ => null };
            Visits = await _encounters.ListAsync(new EncounterQuery { PatientId = Id, Type = type, From = From, To = To, Page = PageNumber, PageSize = 25 });
            var all = await _encounters.ListAsync(new EncounterQuery { PatientId = Id, PageSize = 200 });
            OpdCount = all.Items.Count(v => v.Type == EncounterType.Opd);
            EmergencyCount = all.Items.Count(v => v.Type == EncounterType.Emergency);
        }
        return Page();
    }

    /// <summary>Update 18: add a disease to the patient's history (a later visit / date – earlier records stay).</summary>
    public async Task<IActionResult> OnPostDiseaseAsync(string? diseaseName, long? encounterId, DateOnly? visitDate, string? notes)
    {
        if (!(await CanAsync(Perm.PatientsEdit) || await CanAsync(Perm.OpdAdd)) || !await CanAsync(Perm.DiseasesView)) return Forbid();
        if (string.IsNullOrWhiteSpace(diseaseName)) { FlashError = "Enter the disease."; return Redirect($"/Patients/Profile/{Id}#diseases"); }
        var r = await DiseaseSvc.RecordAsync(Id, diseaseName, encounterId, null, await CanAsync(Perm.DiseasesAdd), visitDate, notes);
        if (r.Succeeded) FlashSuccess = "Disease added to the patient's history."; else FlashError = string.Join(" ", r.Errors);
        return Redirect($"/Patients/Profile/{Id}#diseases");
    }

    public async Task<IActionResult> OnPostReturnFeeAsync(long appointmentId, decimal amount, string? reason)
    {
        if (await DenyUnlessAsync(Perm.FinanceProcessRefunds) is { } deny) return deny;
        var r = await Appointments.ReturnFeeAsync(appointmentId, amount, reason, 0);
        if (r.Succeeded) FlashSuccess = $"Fee return {r.Value!.RefundNumber} of {amount:N2} recorded – it is paid out once the refund is approved.";
        else FlashError = string.Join(" ", r.Errors);
        return Redirect($"/Patients/Profile/{Id}#consultations");
    }

    public async Task<IActionResult> OnPostFreeByDoctorAsync(long appointmentId, string? reason)
    {
        if (await DenyUnlessAsync(Perm.AppointmentsFreeByDoctor) is { } deny) return deny;
        // Fee already collected → the money is returned through a refund (never by deleting the payment).
        var row = (await Appointments.PatientConsultationsAsync(Id)).FirstOrDefault(c => c.AppointmentId == appointmentId);
        if (row is null) return NotFound();
        var r = await Appointments.MarkFreeByDoctorAsync(appointmentId, new WaiverInput { Reason = reason, CashReturned = row.Returnable > 0 });
        if (r.Succeeded) FlashSuccess = row.Returnable > 0 ? "Free by Doctor recorded – the collected fee is refunded (approval in Finance → Refunds)." : "Free by Doctor recorded – no fee is charged.";
        else FlashError = string.Join(" ", r.Errors);
        return Redirect($"/Patients/Profile/{Id}#consultations");
    }

    public async Task<IActionResult> OnPostDiscountAsync(long appointmentId, string? discountType, decimal value, string? reason)
    {
        if (await DenyUnlessAsync(Perm.FinanceDiscount) is { } deny) return deny;
        var r = await Appointments.DiscountAsync(appointmentId, discountType, value, reason, 0);
        if (r.Succeeded) FlashSuccess = "Consultation discount recorded."; else FlashError = string.Join(" ", r.Errors);
        return Redirect($"/Patients/Profile/{Id}#consultations");
    }

    public async Task<IActionResult> OnPostArchiveAsync(bool archive)
    {
        if (await DenyUnlessAsync(Perm.PatientsArchive) is { } deny) return deny;
        var r = await _patients.SetArchivedAsync(Id, archive, Reason);
        if (r.Succeeded) FlashSuccess = archive ? "Patient record archived. It is kept and can be restored at any time." : "Patient record restored.";
        else FlashError = r.ErrorText;
        return Redirect($"/Patients/Profile/{Id}");
    }
}
