using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>Pharmacy slips (80 / 58 mm): patient issue slip, retail sale receipt, transfer slip, receiving note and return slips.</summary>
public class PharmacyModel : Pages.Pharmacy.DocModel
{
    private readonly ISettingsService _settings;
    private readonly IAuditService _audit;

    public PharmacyModel(HmsDbContext db, ISettingsService settings, IAuditService audit) : base(db)
    {
        _settings = settings;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)] public int? Width { get; set; }
    public HospitalProfile Hospital { get; private set; } = null!;
    public string PharmacyName { get; private set; } = "";
    public string? Footer { get; private set; }

    public async Task<IActionResult> OnGetPrintAsync(string type, long id)
    {
        var r = await OnGetAsync(type, id);
        if (r is not PageResult) return r;
        Hospital = (await _settings.GetProfileAsync())!;
        var s = await HttpContext.RequestServices.GetRequiredService<IPharmacyCatalogService>().SettingsAsync();
        PharmacyName = s.PharmacyName ?? "Pharmacy";
        Footer = s.SlipFooter;
        Width = Width is 58 or 80 ? Width : 80;
        await _audit.LogAsync(AuditActions.PharmSlipPrinted, "Pharmacy", Number, $"{Number} slip opened for printing");
        return Page();
    }
}
