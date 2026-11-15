using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>Surgery / procedure catalogue – starter records that the hospital edits, deactivates and extends.</summary>
[HasPermission(Perm.OtManageCatalogue)]
public class ProceduresModel : OtPageModel
{
    [BindProperty(SupportsGet = true, Name = "edit")] public int? EditId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty] public ProcedureInput Input { get; set; } = new();
    public IReadOnlyList<OtProcedure> Procedures { get; private set; } = Array.Empty<OtProcedure>();
    public IReadOnlyList<Department> Departments { get; private set; } = Array.Empty<Department>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();

    private async Task LoadAsync()
    {
        Nav("ot-procedures", "procedures");
        var all = await Setup.ProceduresAsync(activeOnly: false);
        var q = Q?.Trim();
        Procedures = string.IsNullOrEmpty(q) ? all : all.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (p.Specialty ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        Departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
        Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true))
            .Where(s => s.Item.Kind is ServiceKind.Procedure or ServiceKind.Inpatient or ServiceKind.Other).ToList();
        if (EditId is int id && all.FirstOrDefault(p => p.Id == id) is { } e && Request.Method == "GET")
            Input = new ProcedureInput { Code = e.Code, Name = e.Name, DepartmentId = e.DepartmentId, Specialty = e.Specialty, Category = e.Category, Description = e.Description,
                DurationMinutes = e.DurationMinutes, DefaultOtType = e.DefaultOtType, ServiceItemId = e.ServiceItemId, IsActive = e.IsActive, SortOrder = e.SortOrder,
                AnesthesiaTypes = Types(e).ToList() };
    }

    public static IEnumerable<AnesthesiaType> Types(OtProcedure p) =>
        (p.AnesthesiaTypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => Enum.TryParse<AnesthesiaType>(x, out var a) ? a : (AnesthesiaType?)null).OfType<AnesthesiaType>();

    public Task OnGetAsync() => LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await Setup.SaveProcedureAsync(EditId, Input);
        if (r.Succeeded) { FlashSuccess = "Procedure saved."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }
}
