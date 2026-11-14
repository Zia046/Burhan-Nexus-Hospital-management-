using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab.Catalogue;

/// <summary>Laboratory test catalogue with categories and sample types.</summary>
[HasPermission(Perm.LabManageCatalogue)]
public class IndexModel : HmsPageModel
{
    private readonly ILabCatalogService _catalog;
    public IndexModel(ILabCatalogService catalog) => _catalog = catalog;

    public IReadOnlyList<LabTestRow> Tests { get; private set; } = Array.Empty<LabTestRow>();
    public IReadOnlyList<LabCategory> Categories { get; private set; } = Array.Empty<LabCategory>();
    public IReadOnlyList<LabSampleType> SampleTypes { get; private set; } = Array.Empty<LabSampleType>();
    public bool ShowFees { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "lab-catalogue";
        ViewData["LabTab"] = "catalogue";
        Tests = await _catalog.TestsAsync(activeOnly: false);
        Categories = await _catalog.CategoriesAsync(activeOnly: false);
        SampleTypes = await _catalog.SampleTypesAsync(activeOnly: false);
        ShowFees = await CanAsync(Perm.LabViewFinancial) || await CanAsync(Perm.LabManageFees);
    }

    public async Task<IActionResult> OnPostCategoryAsync(int? id, string? name, int sortOrder, bool isActive)
    {
        var r = await _catalog.SaveCategoryAsync(id, name, sortOrder, isActive);
        if (r.Succeeded) FlashSuccess = "Category saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSampleTypeAsync(int? id, string? code, string? name, string? container, bool isActive)
    {
        var r = await _catalog.SaveSampleTypeAsync(id, code, name, container, isActive);
        if (r.Succeeded) FlashSuccess = "Sample type saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
