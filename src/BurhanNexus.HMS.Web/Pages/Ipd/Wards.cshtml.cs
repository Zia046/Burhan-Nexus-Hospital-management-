using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>Wards, rooms, beds / cots / incubators (configuration – no code changes needed).</summary>
[HasPermission(Perm.IpdManageWards, Perm.IpdManageRooms, Perm.IpdManageBeds)]
public class WardsModel : IpdPageModel
{
    [BindProperty(SupportsGet = true, Name = "ward")] public int? EditWard { get; set; }
    [BindProperty(SupportsGet = true, Name = "room")] public int? EditRoom { get; set; }
    [BindProperty(SupportsGet = true, Name = "bed")] public int? EditBed { get; set; }
    /// <summary>Update 14: after saving, the page stays on the same ward / room (the bed list shows that ward).</summary>
    [BindProperty(SupportsGet = true, Name = "inward")] public int? KeepWard { get; set; }
    [BindProperty(SupportsGet = true, Name = "inroom")] public int? KeepRoom { get; set; }
    [BindProperty] public WardInput Ward { get; set; } = new();
    [BindProperty] public RoomInput Room { get; set; } = new();
    [BindProperty] public BedInput Bed { get; set; } = new();

    public IReadOnlyList<IpdWard> Wards { get; private set; } = Array.Empty<IpdWard>();
    public IReadOnlyList<IpdRoom> Rooms { get; private set; } = Array.Empty<IpdRoom>();
    public IReadOnlyList<IpdBed> Beds { get; private set; } = Array.Empty<IpdBed>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public IReadOnlyList<StockLocation> Locations { get; private set; } = Array.Empty<StockLocation>();

    private async Task LoadAsync()
    {
        Nav("ipd-wards", "wards");
        Wards = await Setup.WardsAsync(activeOnly: false);
        Rooms = await Setup.RoomsAsync(null, activeOnly: false);
        Beds = await Setup.BedsAsync(null, activeOnly: false);
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        Departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true))
            .Where(s => s.Item.Kind is ServiceKind.Inpatient or ServiceKind.Other or ServiceKind.Procedure).ToList();
        Locations = await db.StockLocations.AsNoTracking().Where(l => l.Kind == StockLocationKind.Department && l.IsActive).OrderBy(l => l.Name).ToListAsync();
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Wards.FirstOrDefault(w => w.Id == EditWard) is { } w)
            Ward = new WardInput { Code = w.Code, Name = w.Name, Type = w.Type, DepartmentId = w.DepartmentId, Location = w.Location, Capacity = w.Capacity, ResponsibleStaff = w.ResponsibleStaff,
                BedChargeServiceItemId = w.BedChargeServiceItemId, StockLocationId = w.StockLocationId, IsActive = w.IsActive, SortOrder = w.SortOrder };
        if (Rooms.FirstOrDefault(r => r.Id == EditRoom) is { } r)
            Room = new RoomInput { WardId = r.WardId, Name = r.Name, Type = r.Type, BedCapacity = r.BedCapacity, IsActive = r.IsActive, Notes = r.Notes };
        if (Beds.FirstOrDefault(b => b.Id == EditBed) is { } b)
            Bed = new BedInput { RoomId = b.RoomId, Code = b.Code, Type = b.Type, EquipmentId = b.EquipmentId, IsActive = b.IsActive, SortOrder = b.SortOrder };
        KeepSelection();
    }

    private void KeepSelection()
    {
        if (Rooms.FirstOrDefault(r => r.Id == KeepRoom) is { } kr)
        {
            KeepWard ??= kr.WardId;
            if (EditBed is null) Bed.RoomId = kr.Id;
        }
        if (KeepWard is int kw && Wards.Any(w => w.Id == kw))
        {
            if (EditRoom is null) Room.WardId = kw;
            if (EditBed is null && Rooms.FirstOrDefault(r => r.Id == Bed.RoomId)?.WardId != kw && Rooms.FirstOrDefault(r => r.WardId == kw) is { } first) Bed.RoomId = first.Id;
        }
        else KeepWard = null;
    }

    /// <summary>Rooms shown in "Rooms & beds": the selected ward only (Update 14), else all.</summary>
    public IEnumerable<IpdRoom> ShownRooms => KeepWard is int w ? Rooms.Where(r => r.WardId == w) : Rooms;

    private async Task<IActionResult> DoneAsync(Core.Abstractions.ServiceResult r, string ok, object? keep = null)
    {
        if (r.Succeeded) { FlashSuccess = ok; return RedirectToPage(keep); }
        await LoadAsync();
        KeepSelection();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostWardAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageWards) is { } deny) return deny;
        return await DoneAsync(await Setup.SaveWardAsync(EditWard, Ward), "Ward saved.", EditWard is int w ? new { inward = w } : null);
    }

    public async Task<IActionResult> OnPostRoomAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageRooms) is { } deny) return deny;
        return await DoneAsync(await Setup.SaveRoomAsync(EditRoom, Room), "Room saved.", new { inward = Room.WardId });
    }

    public async Task<IActionResult> OnPostBedAsync()
    {
        if (await DenyUnlessAsync(Perm.IpdManageBeds) is { } deny) return deny;
        return await DoneAsync(await Setup.SaveBedAsync(EditBed, Bed), "Bed saved.", new { inroom = Bed.RoomId }); // Update 14: stay on this ward / room
    }
}
