using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Appointments;

[HasPermission(Perm.AppointmentsView)]
public class DetailsModel : HmsPageModel
{
    private readonly IAppointmentService _appointments;
    private readonly IRegistrationSetupService _setup;

    public DetailsModel(IAppointmentService appointments, IRegistrationSetupService setup)
    {
        _appointments = appointments;
        _setup = setup;
    }

    public Appointment Appointment { get; private set; } = null!;
    public RegistrationSettings Settings { get; private set; } = null!;
    [BindProperty] public EditAppointmentInput Edit { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public string? CancelReason { get; set; }
    [BindProperty] public WaiverInput Waiver { get; set; } = new();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();
    public Refund? WaiverRefund { get; private set; }

    private async Task<bool> LoadAsync(long id)
    {
        ViewData["Nav"] = "appointments";
        var a = await _appointments.GetAsync(id);
        if (a is null) return false;
        Appointment = a;
        Settings = await _setup.GetSettingsAsync();
        var db = HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Persistence.HmsDbContext>();
        Doctors = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.Doctors.Where(d => d.IsActive).OrderBy(d => d.Name));
        Methods = await HttpContext.RequestServices.GetRequiredService<BurhanNexus.HMS.Infrastructure.Finance.IFinanceSetupService>().PaymentMethodsAsync(activeOnly: true);
        if (a.WaiverRefundId is long rf) WaiverRefund = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Refunds.Where(x => x.Id == rf));
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        if (!await LoadAsync(id)) return NotFound();
        Edit = new EditAppointmentInput { AppointmentTime = Appointment.AppointmentTime, Complaint = Appointment.Complaint, Notes = Appointment.Notes };
        RowVersion = Appointment.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.AppointmentsEdit) is { } deny) return deny;
        var r = await _appointments.EditAsync(id, Edit, RowVersion);
        if (r.Succeeded) { FlashSuccess = "Appointment updated. The token number is unchanged."; return RedirectToPage(new { id }); }
        if (!await LoadAsync(id)) return NotFound();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.AppointmentsCancel) is { } deny) return deny;
        var r = await _appointments.CancelAsync(id, CancelReason);
        if (r.Succeeded) { FlashSuccess = "Appointment cancelled. Its token number will not be issued again."; return RedirectToPage(new { id }); }
        if (!await LoadAsync(id)) return NotFound();
        Edit = new EditAppointmentInput { AppointmentTime = Appointment.AppointmentTime, Complaint = Appointment.Complaint, Notes = Appointment.Notes };
        RowVersion = Appointment.RowVersion;
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostFreeAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.AppointmentsFreeByDoctor) is { } deny) return deny;
        var r = await _appointments.MarkFreeByDoctorAsync(id, Waiver);
        if (r.Succeeded) { FlashSuccess = "Recorded as Free by Doctor."; return RedirectToPage(new { id }); }
        if (!await LoadAsync(id)) return NotFound();
        Edit = new EditAppointmentInput { AppointmentTime = Appointment.AppointmentTime, Complaint = Appointment.Complaint, Notes = Appointment.Notes };
        RowVersion = Appointment.RowVersion;
        AddErrors(r);
        return Page();
    }
}
