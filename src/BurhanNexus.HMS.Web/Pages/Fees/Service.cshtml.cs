using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Fees;

/// <summary>
/// Update 10: add / edit / deactivate a service and change its fee. Changing details needs "Add / Edit / Deactivate Services";
/// changing a fee needs "Manage Service Fees" – both are checked here on the server, not only by hiding fields.
/// </summary>
[HasPermission(Perm.SvcManageServices, Perm.SvcManageFees)]
public class ServiceModel : HmsPageModel
{
    private readonly IFinanceSetupService _setup;
    private readonly IDepartmentService _departments;
    private readonly IRegistrationSetupService _registration;

    public ServiceModel(IFinanceSetupService setup, IDepartmentService departments, IRegistrationSetupService registration)
    {
        _setup = setup; _departments = departments; _registration = registration;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public ServiceInput Form { get; set; } = new();
    public ServiceRow? Editing { get; private set; }
    public IReadOnlyList<ServiceCategoryRow> Categories { get; private set; } = Array.Empty<ServiceCategoryRow>();
    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = Array.Empty<DepartmentListItem>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<ServiceFee> FeeHistory { get; private set; } = Array.Empty<ServiceFee>();
    public bool CanServices { get; private set; }
    public bool CanFees { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "fees-catalogue";
        ViewData["FeesTab"] = Id is null ? "new" : "catalogue";
        CanServices = await CanAsync(Perm.SvcManageServices);
        CanFees = await CanAsync(Perm.SvcManageFees);
        Categories = await _setup.CategoriesAsync(activeOnly: false);
        Departments = await _departments.ListAsync(includeInactive: false);
        Doctors = await _registration.DoctorsAsync(activeOnly: false);
        if (Id is int id)
        {
            Editing = await _setup.ServiceAsync(id);
            if (Editing is not null) FeeHistory = await _setup.FeeHistoryAsync(id);
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Id is not null && Editing is null) return NotFound();
        if (Id is null && !CanServices) return Forbid();
        if (Editing is { } e)
            Form = new ServiceInput
            {
                Code = e.Item.Code, Name = e.Item.Name, Kind = e.Item.Kind, DepartmentId = e.Item.DepartmentId, DoctorId = e.Item.DoctorId,
                IsFree = e.CurrentFee == 0 && e.CurrentGovernmentFee is null or 0, Fee = e.CurrentFee, GovernmentFee = e.CurrentGovernmentFee,
                AllowPartialPayment = e.Item.AllowPartialPayment, IsActive = e.Item.IsActive, Description = e.Item.Description,
                CounterIds = e.Item.Counters.Select(c => c.CounterId).ToList(), ServiceCategoryId = e.Item.ServiceCategoryId,
                ChargeUnit = e.Item.ChargeUnit, Applicability = e.Item.Applicability
            };
        else if (Request.Query.TryGetValue("category", out var c) && int.TryParse(c, out var cid)) Form.ServiceCategoryId = cid;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (Id is not null && Editing is null) return NotFound();
        Form.SetGovernmentFee = true;
        if (!CanFees)
        {
            // Detail-only users: the fee stays as it is (the fee fields are not posted).
            if (Editing is { } cur) { Form.Fee = cur.CurrentFee; Form.GovernmentFee = cur.CurrentGovernmentFee; Form.IsFree = cur.CurrentFee == 0 && cur.CurrentGovernmentFee is null or 0; }
            else { Form.Fee = 0; Form.GovernmentFee = null; Form.IsFree = true; }
            Form.FeeEffectiveFrom = null;
        }
        if (Form.IsFree) { Form.Fee = 0; Form.GovernmentFee = null; }
        var feeChanged = Editing is null ? Form.Fee != 0 || Form.GovernmentFee is not null
            : Form.Fee != Editing.CurrentFee || Form.GovernmentFee != Editing.CurrentGovernmentFee || Form.FeeEffectiveFrom is not null;
        if (Editing is { } e && !CanServices)
        {
            // Fee-only users: every detail stays as it is.
            Form.Code = e.Item.Code; Form.Name = e.Item.Name; Form.Kind = e.Item.Kind; Form.DepartmentId = e.Item.DepartmentId; Form.DoctorId = e.Item.DoctorId;
            Form.AllowPartialPayment = e.Item.AllowPartialPayment; Form.IsActive = e.Item.IsActive; Form.Description = e.Item.Description;
            Form.CounterIds = e.Item.Counters.Select(c => c.CounterId).ToList(); Form.ServiceCategoryId = e.Item.ServiceCategoryId;
            Form.ChargeUnit = e.Item.ChargeUnit; Form.Applicability = e.Item.Applicability;
        }
        if (Editing is null && !CanServices) return Forbid();
        if (feeChanged && !CanFees)
        {
            ModelState.AddModelError(string.Empty, "You may change the service details but not its fee (permission: Manage Service Fees).");
            return Page();
        }
        if (Editing is not null && !feeChanged) Form.FeeEffectiveFrom = null;
        if (Editing is { } ed) Form.CounterIds = ed.Item.Counters.Select(c => c.CounterId).ToList(); // counters are managed in Finance → Services
        var r = await _setup.SaveServiceAsync(Id, Form);
        if (!r.Succeeded) { AddErrors(r); return Page(); }
        FlashSuccess = "Service saved. A fee change applies to new transactions only – old bills keep their original fee.";
        return Redirect($"/Fees/Service?id={r.Value}");
    }
}
