using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>Operation theatres (configurable number, type, hours, cleaning interval, availability) and the OT equipment register.</summary>
[HasPermission(Perm.OtManageTheatres)]
public class TheatresModel : OtPageModel
{
    [BindProperty(SupportsGet = true, Name = "ot")] public int? EditTheatre { get; set; }
    [BindProperty(SupportsGet = true, Name = "eq")] public int? EditEquipment { get; set; }
    [BindProperty] public TheatreInput Theatre { get; set; } = new();
    [BindProperty] public EquipmentInput Equipment { get; set; } = new();

    public IReadOnlyList<OtTheatre> Theatres { get; private set; } = Array.Empty<OtTheatre>();
    public IReadOnlyList<OtEquipment> EquipmentList { get; private set; } = Array.Empty<OtEquipment>();
    public IReadOnlyList<OtEquipmentLog> Logs { get; private set; } = Array.Empty<OtEquipmentLog>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public IReadOnlyList<StockLocation> Locations { get; private set; } = Array.Empty<StockLocation>();
    public DateOnly Today { get; private set; }

    private async Task LoadAsync()
    {
        Nav("ot-theatres", "theatres");
        Today = await Clock.TodayAsync();
        Theatres = await Setup.TheatresAsync(activeOnly: false);
        EquipmentList = await Setup.EquipmentAsync(activeOnly: false);
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        Logs = await db.OtEquipmentLogs.AsNoTracking().OrderByDescending(l => l.Id).Take(30).ToListAsync();
        Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true))
            .Where(s => s.Item.Kind is ServiceKind.Procedure or ServiceKind.Inpatient or ServiceKind.Other).ToList();
        Locations = await db.StockLocations.AsNoTracking().Where(l => l.Kind == StockLocationKind.Department && l.IsActive).OrderBy(l => l.Name).ToListAsync();
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Theatres.FirstOrDefault(t => t.Id == EditTheatre) is { } t)
            Theatre = new TheatreInput { Code = t.Code, Name = t.Name, Type = t.Type, Location = t.Location, Description = t.Description, Equipment = t.Equipment, OpensAt = t.OpensAt, ClosesAt = t.ClosesAt,
                CleaningMinutes = t.CleaningMinutes, IsAvailable = t.IsAvailable, UnavailableReason = t.UnavailableReason, IsActive = t.IsActive, StockLocationId = t.StockLocationId,
                ChargeServiceItemId = t.ChargeServiceItemId, SortOrder = t.SortOrder };
        else Theatre.CleaningMinutes = (await Setup.SettingsAsync()).DefaultCleaningMinutes;
        if (EquipmentList.FirstOrDefault(e => e.Id == EditEquipment) is { } e)
            Equipment = new EquipmentInput { Code = e.Code, Name = e.Name, TheatreId = e.TheatreId, Status = e.Status, LastMaintenance = e.LastMaintenance, NextMaintenance = e.NextMaintenance,
                ServiceNotes = e.ServiceNotes, IsActive = e.IsActive };
    }

    private async Task<IActionResult> DoneAsync(Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostTheatreAsync() => await DoneAsync(await Setup.SaveTheatreAsync(EditTheatre, Theatre), "Operation theatre saved.");
    public async Task<IActionResult> OnPostEquipmentAsync() => await DoneAsync(await Setup.SaveEquipmentAsync(EditEquipment, Equipment), "Equipment saved.");
}
