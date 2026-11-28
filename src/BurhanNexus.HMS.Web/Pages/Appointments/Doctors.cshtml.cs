using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Appointments;

/// <summary>
/// Update 17: the doctor list of the Doctor-wise Lists with "+ Add Doctor" – name, consultation fee, specialty / department, contact, status.
/// The consultation fee is the doctor's own consultation service (used automatically by the token booking; old appointments keep their fee).
/// Fees are set by the administrator (Update 16 rule, checked on the server).
/// </summary>
[HasPermission(Perm.AppointmentsView, Perm.AppointmentsManageDoctors)]
public class DoctorsModel : HmsPageModel
{
    private readonly IRegistrationSetupService _setup;
    private readonly IDepartmentService _departments;
    private readonly IFinanceSetupService _finance;

    public DoctorsModel(IRegistrationSetupService setup, IDepartmentService departments, IFinanceSetupService finance)
    {
        _setup = setup;
        _departments = departments;
        _finance = finance;
    }

    [BindProperty(SupportsGet = true, Name = "edit")] public int? EditId { get; set; }
    [BindProperty(SupportsGet = true, Name = "new")] public bool New { get; set; }
    [BindProperty] public DoctorInput Form { get; set; } = new();
    [BindProperty] public decimal? Fee { get; set; }
    [BindProperty] public bool FeeIsFree { get; set; }

    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    /// <summary>Doctor → (fee, free).</summary>
    public Dictionary<int, (decimal Fee, bool Free)> Fees { get; private set; } = new();
    public bool CanManage { get; private set; }
    public bool CanFees { get; private set; }
    public Doctor? Editing { get; private set; }
    public bool ShowForm => CanManage && (New || Editing is not null || !ModelState.IsValid);

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "appointments";
        CanManage = await CanAsync(Perm.AppointmentsManageDoctors);
        CanFees = User.IsSystemAdministrator();
        Doctors = await _setup.DoctorsAsync(activeOnly: false);
        Departments = await _departments.ListAsync(includeInactive: false);
        foreach (var r in (await _finance.ServicesAsync(activeOnly: true)).Where(r => r.Item.DoctorId is int && r.Item.Kind == ServiceKind.Consultation).OrderBy(r => r.Item.Id))
            Fees.TryAdd(r.Item.DoctorId!.Value, (r.CurrentFee, r.Item.IsFree));
        Editing = EditId is int id ? Doctors.FirstOrDefault(d => d.Id == id) : null;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Editing is { } d)
        {
            Form = new DoctorInput { Name = d.Name, Qualification = d.Qualification, Specialty = d.Specialty, RegistrationNumber = d.RegistrationNumber,
                ContactNumber = d.ContactNumber, DepartmentId = d.DepartmentId, IsActive = d.IsActive };
            if (Fees.TryGetValue(d.Id, out var f)) { Fee = f.Fee; FeeIsFree = f.Free; }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.AppointmentsManageDoctors) is { } deny) return deny;
        await LoadAsync();
        var isNew = EditId is null;
        var admin = User.IsSystemAdministrator();
        // The consultation fee is required for a new doctor (the administrator sets fees; others may add the doctor with "fee not set").
        if (admin && isNew && !FeeIsFree && Fee is not > 0) ModelState.AddModelError(nameof(Fee), "Enter the consultation fee (or tick FREE).");
        if (Fee is decimal f && (f < 0 || f > 10_000_000 || decimal.Round(f, 2) != f)) ModelState.AddModelError(nameof(Fee), "Enter a valid consultation fee.");
        Form.ContactNumber ??= ""; // empty = clear the contact number
        if (!ModelState.IsValid) return Page();
        var r = await _setup.SaveDoctorAsync(EditId, Form);
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        var msg = $"Doctor '{Form.Name.Trim()}' {(isNew ? "added" : "saved")}.";
        if (admin && (Fee is not null || FeeIsFree))
        {
            var current = Fees.TryGetValue(r.Value, out var cf) ? cf : ((decimal, bool)?)null;
            if (current is null || current.Value.Item1 != (Fee ?? 0) || current.Value.Item2 != FeeIsFree)
            {
                var fee = await _finance.SetDoctorFeeAsync(r.Value, FeeIsFree ? 0 : Fee ?? 0, FeeIsFree);
                if (!fee.Succeeded) { FlashError = msg + " Fee not saved: " + string.Join(" ", fee.Errors); return Redirect($"/Appointments/Doctors?edit={r.Value}"); }
                msg += FeeIsFree ? " Consultation: FREE." : $" Consultation fee {Fee:N2} (new appointments only).";
            }
        }
        else if (isNew && !admin) msg += " The administrator sets the consultation fee.";
        FlashSuccess = msg;
        return Redirect("/Appointments/Doctors");
    }
}
