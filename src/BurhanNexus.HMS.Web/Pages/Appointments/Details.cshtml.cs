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

    private async Task<bool> LoadAsync(long id)
    {
        ViewData["Nav"] = "appointments";
        var a = await _appointments.GetAsync(id);
        if (a is null) return false;
        Appointment = a;
        Settings = await _setup.GetSettingsAsync();
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

    public async Task<IActionResult> OnPostSeenAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.AppointmentsEdit) is { } deny) return deny;
        var r = await _appointments.MarkSeenAsync(id);
        if (r.Succeeded) FlashSuccess = "Marked as seen."; else FlashError = r.ErrorText;
        return RedirectToPage(new { id });
    }
}
