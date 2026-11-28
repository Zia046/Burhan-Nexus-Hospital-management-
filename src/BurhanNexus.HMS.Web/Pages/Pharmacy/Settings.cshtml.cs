using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Pharmacy;

/// <summary>Pharmacy settings and the department stock locations (Minor OT, Emergency, Dressing Room, Ward ...).</summary>
[HasPermission(Perm.PharmSettings, Perm.PharmManageOtStock)]
public class SettingsModel : PharmacyPageModel
{
    private readonly HmsDbContext _db;
    public SettingsModel(HmsDbContext db) => _db = db;

    [BindProperty] public PharmacySettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    public PharmacySettings Settings { get; private set; } = null!;
    public IReadOnlyList<StockLocation> Locations { get; private set; } = Array.Empty<StockLocation>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public Dictionary<int, int> Units { get; private set; } = new();
    public bool CanSettings { get; private set; }
    public bool CanLocations { get; private set; }

    private async Task LoadAsync(bool fill)
    {
        await InitAsync("settings");
        CanSettings = await CanAsync(Perm.PharmSettings);
        CanLocations = await CanAsync(Perm.PharmManageOtStock);
        Settings = await Catalog.SettingsAsync();
        Locations = await Catalog.LocationsAsync(activeOnly: false);
        Departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
        Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true)).Where(s => s.Item.Kind != ServiceKind.Pharmacy).ToList();
        Units = await _db.StockBalances.GroupBy(b => b.LocationId).Select(g => new { g.Key, Sum = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        if (fill)
        {
            RowVersion = Settings.RowVersion;
            Input = new PharmacySettingsInput
            {
                PharmacyName = Settings.PharmacyName, NearExpiryDays = Settings.NearExpiryDays, EncounterValidDays = Settings.EncounterValidDays,
                GovernmentIssueServiceItemId = Settings.GovernmentIssueServiceItemId, SlipFooter = Settings.SlipFooter,
                FreeIssueEnabled = Settings.FreeIssueEnabled, PaidSaleEnabled = Settings.PaidSaleEnabled, SubsidyEnabled = Settings.SubsidyEnabled,
                DiscountEnabled = Settings.DiscountEnabled, FreeIssueApprovalRequired = Settings.FreeIssueApprovalRequired,
                DefaultIssueMode = Settings.EffectiveDefaultMode, AllowChangeIssueMode = Settings.AllowChangeIssueMode, StockClassificationEnabled = Settings.StockClassificationEnabled,
            };
        }
    }

    public async Task OnGetAsync() => await LoadAsync(true);

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.PharmSettings) is { } deny) return deny;
        Input.IssueConfigPosted = Request.Form.ContainsKey("Input.IssueConfigPosted"); // update 17
        var r = await Catalog.SaveSettingsAsync(Input, RowVersion);
        if (!r.Succeeded) { await LoadAsync(false); AddErrors(r); return Page(); }
        FlashSuccess = "Pharmacy settings saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLocationAsync(int? id, StockLocationInput location)
    {
        if (await DenyUnlessAsync(Perm.PharmManageOtStock) is { } deny) return deny;
        var r = await Catalog.SaveLocationAsync(id, location);
        if (r.Succeeded) FlashSuccess = "Stock location saved."; else FlashError = r.ErrorText;
        return Redirect("/Pharmacy/Settings#locations");
    }
}
