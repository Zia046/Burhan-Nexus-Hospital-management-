using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

/// <summary>Service &amp; fee management. Fee changes are stored as history and only affect new transactions.</summary>
[HasPermission(Perm.FinanceManageServiceFees)]
public class ServicesModel : HmsPageModel
{
    private readonly IFinanceSetupService _setup;
    private readonly IDepartmentService _departments;
    private readonly IRegistrationSetupService _registration;

    public ServicesModel(IFinanceSetupService setup, IDepartmentService departments, IRegistrationSetupService registration)
    {
        _setup = setup;
        _departments = departments;
        _registration = registration;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public ServiceInput Form { get; set; } = new();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<CounterRow> Counters { get; private set; } = Array.Empty<CounterRow>();
    public IReadOnlyList<ServiceFee> FeeHistory { get; private set; } = Array.Empty<ServiceFee>();
    public ServiceRow? Editing { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-services";
        ViewData["FinanceTab"] = "services";
        Services = await _setup.ServicesAsync(activeOnly: false);
        Departments = await _departments.ListAsync(includeInactive: false);
        Doctors = await _registration.DoctorsAsync(activeOnly: false);
        Counters = await _setup.CountersAsync(activeOnly: true);
        Editing = Id is int id ? Services.FirstOrDefault(s => s.Item.Id == id) : null;
        if (Editing is not null) FeeHistory = await _setup.FeeHistoryAsync(Editing.Item.Id);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Id is not null && Editing is null) return NotFound();
        if (Editing is { } e)
            Form = new ServiceInput
            {
                Code = e.Item.Code, Name = e.Item.Name, Kind = e.Item.Kind, DepartmentId = e.Item.DepartmentId, DoctorId = e.Item.DoctorId,
                IsFree = e.CurrentFee == 0, Fee = e.CurrentFee, AllowPartialPayment = e.Item.AllowPartialPayment, IsActive = e.Item.IsActive,
                Description = e.Item.Description, CounterIds = e.Item.Counters.Select(c => c.CounterId).ToList()
            };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _setup.SaveServiceAsync(Id, Form);
        if (r.Succeeded) { FlashSuccess = "Service saved. Fee changes apply to new transactions only."; return RedirectToPage(new { id = r.Value }); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }
}
