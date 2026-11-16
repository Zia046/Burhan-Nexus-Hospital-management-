using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Fees;

/// <summary>Update 10: service categories of the central catalogue (added and deactivated, never deleted).</summary>
[HasPermission(Perm.SvcManageCategories)]
public class CategoriesModel : HmsPageModel
{
    private readonly IFinanceSetupService _setup;
    public CategoriesModel(IFinanceSetupService setup) => _setup = setup;

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public ServiceCategoryInput Form { get; set; } = new();
    public IReadOnlyList<ServiceCategoryRow> Rows { get; private set; } = Array.Empty<ServiceCategoryRow>();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "fees-categories";
        ViewData["FeesTab"] = "categories";
        Rows = await _setup.CategoriesAsync(activeOnly: false);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Id is int id)
        {
            var c = Rows.FirstOrDefault(r => r.Category.Id == id)?.Category;
            if (c is null) return NotFound();
            Form = new ServiceCategoryInput { Code = c.Code, Name = c.Name, Description = c.Description, IsActive = c.IsActive, SortOrder = c.SortOrder };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _setup.SaveCategoryAsync(Id, Form);
        if (!r.Succeeded) { await LoadAsync(); AddErrors(r); return Page(); }
        FlashSuccess = "Category saved.";
        return Redirect("/Fees/Categories");
    }
}
