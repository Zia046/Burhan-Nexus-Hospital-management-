using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>Laboratory order slip (thermal 80/58 mm or A5): visit number, tests, sample numbers, collection instructions.</summary>
[HasPermission(Perm.LabBook, Perm.LabCollectSamples)]
public class LabSlipModel : HmsPageModel
{
    private readonly ILabService _lab;
    private readonly ISettingsService _settings;
    public LabSlipModel(ILabService lab, ISettingsService settings) { _lab = lab; _settings = settings; }

    [BindProperty(SupportsGet = true)] public int? Width { get; set; }
    public LabOrder Order { get; private set; } = null!;
    public HospitalProfile Hospital { get; private set; } = null!;
    public LabPayment Payment { get; private set; } = null!;
    public bool ShowFees { get; private set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var o = await _lab.OrderAsync(id);
        if (o is null) return NotFound();
        Order = o;
        Hospital = (await _settings.GetProfileAsync())!;
        Payment = await _lab.OrderPaymentAsync(id);
        ShowFees = await CanAsync(Perm.LabViewFinancial);
        Width = Width is 58 or 80 ? Width : 80;
        return Page();
    }
}
