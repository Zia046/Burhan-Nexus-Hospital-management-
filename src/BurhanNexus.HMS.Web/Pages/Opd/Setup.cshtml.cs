using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Opd;

/// <summary>
/// OPD setup (Update 13): doctors with their consultation fee in one simple form. The OPD settings moved to Settings → OPD;
/// the service / fee reference table is no longer shown here (fees are managed in Fees &amp; Services).
/// </summary>
[HasPermission(Perm.OpdSettings)]
public class SetupModel : HmsPageModel
{
    private readonly IRegistrationSetupService _setup;
    private readonly IDepartmentService _departments;
    private readonly IFinanceSetupService _finance;

    public SetupModel(IRegistrationSetupService setup, IDepartmentService departments, IFinanceSetupService finance)
    {
        _setup = setup;
        _departments = departments;
        _finance = finance;
    }

    /// <summary>Consultation fee per doctor (null = not set) and whether it is marked FREE.</summary>
    public Dictionary<int, (decimal Fee, bool IsFree, int ServiceId)> Fees { get; private set; } = new();
    [BindProperty] public decimal? ConsultationFee { get; set; }
    [BindProperty] public bool ConsultationFree { get; set; }

    public RegistrationSettings Settings { get; private set; } = null!;
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<ServiceItem> Services { get; private set; } = Array.Empty<ServiceItem>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    [BindProperty(SupportsGet = true, Name = "doctor")] public int? EditDoctorId { get; set; }
    [BindProperty(SupportsGet = true, Name = "service")] public int? EditServiceId { get; set; }

    [BindProperty] public OpdSettingsInput OpdForm { get; set; } = new();
    [BindProperty] public DoctorInput DoctorForm { get; set; } = new();
    [BindProperty] public ServiceItemInput ServiceForm { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "opd";
        Settings = await _setup.GetSettingsAsync();
        Doctors = await _setup.DoctorsAsync(false);
        Services = await _setup.ServiceItemsAsync(false);
        Departments = await _departments.ListAsync(includeInactive: false);
        var today = await HttpContext.RequestServices.GetRequiredService<HospitalClock>().TodayAsync();
        foreach (var x in Services.Where(x => x.DoctorId is int && x.Kind == ServiceKind.Consultation && x.IsActive).OrderBy(x => x.Id))
            if (!Fees.ContainsKey(x.DoctorId!.Value)) Fees[x.DoctorId.Value] = (await _finance.FeeOnAsync(x.Id, today), x.IsFree, x.Id);
    }

    public async Task<IActionResult> OnPostSettingsAsync()
    {
        var r = await _setup.UpdateOpdAsync(OpdForm, RowVersion);
        if (r.Succeeded) FlashSuccess = "OPD settings saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDoctorAsync(int? id)
    {
        if (ConsultationFee is decimal f && (f < 0 || f > 10_000_000 || decimal.Round(f, 2) != f))
        { FlashError = "Enter a valid consultation fee (0 or more, 2 decimals)."; return Redirect(id is null ? "/Opd/Setup" : $"/Opd/Setup?doctor={id}"); }
        if (!ConsultationFree && ConsultationFee is 0)
        { FlashError = "A consultation fee of 0 is not allowed – enter the fee, or tick FREE."; return Redirect(id is null ? "/Opd/Setup" : $"/Opd/Setup?doctor={id}"); }
        var r = await _setup.SaveDoctorAsync(id, DoctorForm);
        if (!r.Succeeded) { FlashError = r.ErrorText; return RedirectToPage(); }
        var doctorId = r.Value;
        var msg = $"Doctor '{DoctorForm.Name.Trim()}' saved.";
        // Update 13: the consultation fee is entered with the doctor – one consultation service per doctor (code generated).
        if (ConsultationFee is not null || ConsultationFree)
        {
            var existingId = (await _setup.ServiceItemsAsync(false)).Where(x => x.DoctorId == doctorId && x.Kind == ServiceKind.Consultation).OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefault();
            var existing = existingId is int eid ? (await _finance.ServiceAsync(eid))?.Item : null;
            var svc = await _finance.SaveServiceAsync(existing?.Id, new ServiceInput
            {
                Code = existing?.Code ?? $"CON-{doctorId}", Name = $"Consultation – {DoctorForm.Name.Trim()}", Kind = ServiceKind.Consultation, DoctorId = doctorId,
                DepartmentId = DoctorForm.DepartmentId ?? existing?.DepartmentId, IsFree = ConsultationFree, Fee = ConsultationFree ? 0 : ConsultationFee ?? 0,
                FeeNote = "Consultation fee (OPD setup)", IsActive = true, AllowPartialPayment = existing?.AllowPartialPayment ?? false, Description = existing?.Description ?? "Doctor consultation",
                CounterIds = existing?.Counters.Select(c => c.CounterId).ToList() ?? new()
            });
            if (!svc.Succeeded) { FlashError = msg + " Fee not saved: " + string.Join(" ", svc.Errors); return Redirect($"/Opd/Setup?doctor={doctorId}"); }
            msg += ConsultationFree ? " Consultation: FREE." : $" Consultation fee {ConsultationFee:N2}.";
        }
        FlashSuccess = msg;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostServiceAsync(int? id)
    {
        var r = await _setup.SaveServiceItemAsync(id, ServiceForm);
        if (r.Succeeded) FlashSuccess = "Fee reference saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
