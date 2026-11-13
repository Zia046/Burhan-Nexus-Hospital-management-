using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Departments;

[HasPermission(Perm.DepartmentsManage)]
public class IndexModel : HmsPageModel
{
    private readonly IDepartmentService _departments;
    public IndexModel(IDepartmentService departments) => _departments = departments;

    public IReadOnlyList<DepartmentListItem> Items { get; private set; } = Array.Empty<DepartmentListItem>();
    [BindProperty(SupportsGet = true, Name = "edit")] public int? EditId { get; set; }

    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string? Code { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public uint RowVersion { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "departments";
        Items = await _departments.ListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var r = await _departments.CreateAsync(Name, Code, Description);
        if (r.Succeeded) FlashSuccess = $"Department '{Name.Trim()}' created.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        var r = await _departments.UpdateAsync(id, Name, Code, Description, IsActive, RowVersion);
        if (r.Succeeded) FlashSuccess = "Department saved.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
