using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>Update 10: NICU / IPD packages – price service, included services (not billed again) and the excluded items.</summary>
[HasPermission(Perm.IpdManagePackages)]
public class PackagesModel : IpdPageModel
{
    private readonly IIpdBillingService _billing;
    public PackagesModel(IIpdBillingService billing) => _billing = billing;

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public PackageInput Form { get; set; } = new();
    public IReadOnlyList<ServicePackage> Packages { get; private set; } = Array.Empty<ServicePackage>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();

    private async Task LoadAsync()
    {
        Nav("ipd-settings", "settings");
        Packages = await _billing.PackagesAsync(activeOnly: false);
        Services = await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Id is int id && Packages.FirstOrDefault(p => p.Id == id) is { } p)
            Form = new PackageInput { Code = p.Code, Name = p.Name, Description = p.Description, Excluded = p.Excluded, ServiceItemId = p.ServiceItemId, IsActive = p.IsActive, IncludedServiceIds = p.Items.Select(i => i.ServiceItemId).ToList() };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _billing.SavePackageAsync(Id, Form);
        if (!r.Succeeded) { await LoadAsync(); AddErrors(r); return Page(); }
        FlashSuccess = "Package saved.";
        return Redirect("/Ipd/Packages");
    }
}
