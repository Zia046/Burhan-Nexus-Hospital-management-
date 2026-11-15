using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Radiology;

/// <summary>One radiology booking (order): its examinations, payment, referral details and added examinations.</summary>
[HasPermission(Perm.RadViewXRay, Perm.RadViewUltrasound, Perm.RadCreateBooking, Perm.RadEditBooking, Perm.RadViewHistory, Perm.RadEnterReport, Perm.RadVerifyReport)]
public class OrderModel : RadiologyPageModel
{
    public RadiologyOrder Order { get; private set; } = null!;
    public IReadOnlyDictionary<long, ExamPayment> Payments { get; private set; } = new Dictionary<long, ExamPayment>();
    public IReadOnlyList<RadiologyServiceRow> Services { get; private set; } = Array.Empty<RadiologyServiceRow>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public bool ShowFees { get; private set; }

    [BindProperty] public RadiologyOrderDetailsInput Details { get; set; } = new();
    [BindProperty] public List<int> AddServiceIds { get; set; } = new();

    private async Task<IActionResult?> LoadAsync(long id)
    {
        var o = await Rad.OrderAsync(id);
        if (o is null) return NotFound();
        var visible = new List<RadiologyExam>();
        foreach (var e in o.Exams) if (await CanSeeAsync(e.Modality)) visible.Add(e);
        if (visible.Count == 0 && o.Exams.Count > 0) return Forbid();
        Order = o;
        Nav("rad-bookings", "bookings");
        ShowFees = await CanAsync(Perm.RadViewFinancial);
        Payments = ShowFees ? await Rad.PaymentsAsync(id) : new Dictionary<long, ExamPayment>();
        if (await CanAsync(Perm.RadEditBooking))
        {
            var only = await OnlyModalityAsync();
            Services = (await Catalog.ServicesAsync(activeOnly: true))
                .Where(s => (only is null || s.Service.Modality == only) && o.Exams.All(x => x.ServiceId != s.Service.Id || x.Status == RadiologyExamStatus.Cancelled)).ToList();
            Doctors = await HttpContext.RequestServices.GetRequiredService<IRegistrationSetupService>().DoctorsAsync(activeOnly: true);
            Departments = await HttpContext.RequestServices.GetRequiredService<HmsDbContext>().Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        }
        Details = new RadiologyOrderDetailsInput
        {
            ReferralType = o.ReferralType, ReferringDoctorId = o.ReferringDoctorId, ExternalDoctorName = o.ExternalDoctorName,
            ReferringDepartmentId = o.ReferringDepartmentId, ClinicalIndication = o.ClinicalIndication, Priority = o.Priority
        };
        return null;
    }

    public async Task<IActionResult> OnGetAsync(long id) => await LoadAsync(id) ?? Page();

    private async Task<IActionResult> DoneAsync(long id, ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return RedirectToPage(new { id }); }
        var keep = Details;
        if (await LoadAsync(id) is { } fail) return fail;
        Details = keep;
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostDetailsAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.RadEditBooking) is { } deny) return deny;
        return await DoneAsync(id, await Rad.UpdateOrderAsync(id, Details), "Booking details saved.");
    }

    public async Task<IActionResult> OnPostAddAsync(long id)
    {
        if (await DenyUnlessAsync(Perm.RadEditBooking) is { } deny) return deny;
        if (await OnlyModalityAsync() is { } only && await HttpContext.RequestServices.GetRequiredService<HmsDbContext>().RadiologyServices
                .AnyAsync(s => AddServiceIds.Contains(s.Id) && s.Modality != only)) return Forbid();
        return await DoneAsync(id, await Rad.AddExamsAsync(id, AddServiceIds), "Examinations added. Their fees are payable at the cash counter.");
    }
}
