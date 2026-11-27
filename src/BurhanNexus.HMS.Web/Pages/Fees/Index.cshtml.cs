using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Fees;

/// <summary>
/// Update 16: Fees &amp; Services is only for OPD, Emergency and Consultant (per-doctor) fees. Laboratory prices are set in
/// Laboratory → Test Catalogue, radiology fees in Radiology → Services and IPD / ward / NICU / maternity / OT fees in IPD → Fees.
/// Only the administrator may change a fee (checked on the server in <see cref="FinanceSetupService"/>).
/// </summary>
[HasPermission(Perm.SvcViewFees, Perm.SvcManageCatalogue, Perm.SvcManageServices, Perm.SvcManageFees)]
public class IndexModel : HmsPageModel
{
    private readonly IFinanceSetupService _setup;
    private readonly IRegistrationSetupService _registration;
    public IndexModel(IFinanceSetupService setup, IRegistrationSetupService registration) { _setup = setup; _registration = registration; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    /// <summary>opd (default), emergency or consultant.</summary>
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty] public SimpleServiceInput NewService { get; set; } = new();

    public IReadOnlyList<ServiceRow> Rows { get; private set; } = Array.Empty<ServiceRow>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    /// <summary>Consultant tab: active doctors who have no consultation fee yet.</summary>
    public IReadOnlyList<Doctor> DoctorsWithoutFee { get; private set; } = Array.Empty<Doctor>();
    public bool CanEdit { get; private set; }
    public bool IsAdmin { get; private set; }
    public string CurrentTab => Tab is "emergency" or "consultant" ? Tab : "opd";

    public static string? Section(ServiceItem s)
    {
        if (ServiceModel.ManagedElsewhere(s) is not null) return null;
        if (s.Kind == ServiceKind.Consultation || s.DoctorId is not null) return "consultant";
        if (s.Kind == ServiceKind.EmergencyRegistration || s.ServiceCategory?.Code == ServiceCatalogue.Er) return "emergency";
        return "opd";
    }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "fees-catalogue";
        ViewData["FeesTab"] = "catalogue";
        IsAdmin = User.IsSystemAdministrator();
        CanEdit = await CanAsync(Perm.SvcManageServices) || IsAdmin;
        Doctors = (await _registration.DoctorsAsync(activeOnly: true)).ToList();
        var all = await _setup.ServicesAsync(activeOnly: false);
        var withFee = all.Where(r => r.Item.IsActive && r.Item.DoctorId is int && Section(r.Item) == "consultant").Select(r => r.Item.DoctorId!.Value).ToHashSet();
        DoctorsWithoutFee = Doctors.Where(d => !withFee.Contains(d.Id)).ToList();
        IEnumerable<ServiceRow> rows = all.Where(r => Section(r.Item) == CurrentTab);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            rows = rows.Where(r => r.Item.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Item.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                                   || (r.Item.Doctor?.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        rows = Status switch { "inactive" => rows.Where(r => !r.Item.IsActive), "all" => rows, _ => rows.Where(r => r.Item.IsActive) };
        Rows = rows.OrderBy(r => r.Item.Kind).ThenBy(r => r.Item.Doctor?.Name).ThenBy(r => r.Item.Name).ToList();
        NewService.Category ??= CurrentTab switch { "emergency" => SimpleServiceCategory.Emergency, "consultant" => SimpleServiceCategory.Consultant, _ => SimpleServiceCategory.Opd };
    }

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>Change one fee from the list (administrator only).</summary>
    public async Task<IActionResult> OnPostFeeAsync(int id, decimal fee, bool isFree)
    {
        if (!User.IsSystemAdministrator()) return Forbid();
        var row = await _setup.ServiceAsync(id);
        if (row is null) return NotFound();
        if (ServiceModel.ManagedElsewhere(row.Item) is { } m) { FlashError = $"The fee of {row.Item.Name} is set in {m.Where}."; return Redirect($"/Fees?tab={CurrentTab}"); }
        var r = await _setup.SetFeeAsync(id, fee, isFree, null);
        if (r.Succeeded) FlashSuccess = $"Fee of {row.Item.Name} saved. It applies to new transactions only – old bills keep their fee.";
        else FlashError = string.Join(" ", r.Errors);
        return Redirect($"/Fees?tab={CurrentTab}");
    }

    /// <summary>Consultant fee of a doctor (administrator only).</summary>
    public async Task<IActionResult> OnPostDoctorFeeAsync(int doctorId, decimal fee, bool isFree)
    {
        if (!User.IsSystemAdministrator()) return Forbid();
        var r = await _setup.SetDoctorFeeAsync(doctorId, fee, isFree);
        if (r.Succeeded) FlashSuccess = "Consultant fee saved.";
        else FlashError = string.Join(" ", r.Errors);
        return Redirect("/Fees?tab=consultant");
    }

    /// <summary>"Add Service" – Name, Category (OPD / Emergency / Consultant), Fee, Active (administrator only).</summary>
    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!User.IsSystemAdministrator()) return Forbid();
        var r = await _setup.AddSimpleServiceAsync(NewService);
        if (!r.Succeeded) { AddErrors(r); Tab = NewService.Category switch { SimpleServiceCategory.Emergency => "emergency", SimpleServiceCategory.Consultant => "consultant", _ => "opd" }; await LoadAsync(); return Page(); }
        FlashSuccess = $"Service {NewService.Name} added.";
        return Redirect($"/Fees?tab={NewService.Category switch { SimpleServiceCategory.Emergency => "emergency", SimpleServiceCategory.Consultant => "consultant", _ => "opd" }}");
    }

    public static string Unit(ChargeUnit u) => u switch
    {
        ChargeUnit.PerTest => "Per test", ChargeUnit.PerVisit => "Per visit", ChargeUnit.PerAdmission => "Per admission", ChargeUnit.PerDay => "Per day",
        ChargeUnit.PerHour => "Per hour", ChargeUnit.PerSession => "Per session", ChargeUnit.PerProcedure => "Per procedure", ChargeUnit.PerItem => "Per item", _ => "Fixed charge",
    };
}
