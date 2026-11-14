using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab.Catalogue;

/// <summary>
/// Test definition: details and price (price needs "Manage Laboratory Fees"), parameters ("Manage Test Parameters") and
/// reference ranges ("Manage Reference Ranges"). Every change raises the definition version; booked orders keep their snapshot.
/// </summary>
[HasPermission(Perm.LabManageCatalogue, Perm.LabManageParameters, Perm.LabManageRanges)]
public class TestModel : HmsPageModel
{
    private readonly ILabCatalogService _catalog;
    public TestModel(ILabCatalogService catalog) => _catalog = catalog;

    [BindProperty] public LabTestInput Input { get; set; } = new();
    [BindProperty] public LabParameterInput Param { get; set; } = new();
    [BindProperty] public LabRangeInput Range { get; set; } = new();

    public LabTest? Test { get; private set; }
    public decimal Price { get; private set; }
    public IReadOnlyList<LabCategory> Categories { get; private set; } = Array.Empty<LabCategory>();
    public IReadOnlyList<LabSampleType> SampleTypes { get; private set; } = Array.Empty<LabSampleType>();
    public bool CanFees { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanParams { get; private set; }
    public bool CanRanges { get; private set; }

    private async Task<bool> LoadAsync(int? id, bool fillInput)
    {
        ViewData["Nav"] = "lab-catalogue";
        ViewData["LabTab"] = "catalogue";
        Categories = await _catalog.CategoriesAsync(activeOnly: false);
        SampleTypes = await _catalog.SampleTypesAsync(activeOnly: false);
        CanFees = await CanAsync(Perm.LabManageFees);
        CanEdit = await CanAsync(Perm.LabManageCatalogue);
        CanParams = await CanAsync(Perm.LabManageParameters);
        CanRanges = await CanAsync(Perm.LabManageRanges);
        if (id is int tid)
        {
            var row = await _catalog.TestRowAsync(tid);
            if (row is null) return false;
            Test = row.Test;
            Price = row.Price;
            if (fillInput)
            {
                var t = row.Test;
                Input = new LabTestInput
                {
                    Code = t.Code, Name = t.Name, CategoryId = t.CategoryId, SampleTypeId = t.SampleTypeId, CollectionInstructions = t.CollectionInstructions,
                    Method = t.Method, Interpretation = t.Interpretation, TurnaroundHours = t.TurnaroundHours, IsActive = t.IsActive, SortOrder = t.SortOrder,
                    IsFree = row.Price == 0, Price = row.Price, AllowPartialPayment = row.AllowPartial
                };
            }
        }
        else if (fillInput) Input = new LabTestInput { IsFree = !CanFees };
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int? id) => await LoadAsync(id, true) ? Page() : NotFound();

    private async Task<IActionResult> FailAsync(int? id, Core.Abstractions.ServiceResult r)
    {
        if (!await LoadAsync(id, false)) return NotFound();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (await DenyUnlessAsync(Perm.LabManageCatalogue) is { } deny) return deny;
        var r = await _catalog.SaveTestAsync(id, Input, await CanAsync(Perm.LabManageFees));
        if (!r.Succeeded) return await FailAsync(id, r);
        FlashSuccess = "Test saved.";
        return RedirectToPage(new { id = r.Value });
    }

    public async Task<IActionResult> OnPostParameterAsync(int id, int? parameterId)
    {
        if (await DenyUnlessAsync(Perm.LabManageParameters) is { } deny) return deny;
        var r = await _catalog.SaveParameterAsync(id, parameterId, Param);
        if (!r.Succeeded) return await FailAsync(id, r);
        FlashSuccess = "Parameter saved.";
        return Redirect($"/Lab/Catalogue/Test/{id}#p{r.Value}");
    }

    public async Task<IActionResult> OnPostRangeAsync(int id, int parameterId)
    {
        if (await DenyUnlessAsync(Perm.LabManageRanges) is { } deny) return deny;
        var r = await _catalog.AddRangeAsync(parameterId, Range);
        if (!r.Succeeded) return await FailAsync(id, r);
        FlashSuccess = "Reference range added.";
        return Redirect($"/Lab/Catalogue/Test/{id}#p{parameterId}");
    }

    public async Task<IActionResult> OnPostDeleteRangeAsync(int id, int rangeId)
    {
        if (await DenyUnlessAsync(Perm.LabManageRanges) is { } deny) return deny;
        var r = await _catalog.DeleteRangeAsync(rangeId);
        if (r.Succeeded) FlashSuccess = "Reference range removed."; else FlashError = r.ErrorText;
        return RedirectToPage(new { id });
    }

    /// <summary>Shows a stored age in days the way it was entered (years, months or days).</summary>
    public static string Age(int? days)
    {
        if (days is not int d) return "";
        if (d == 0) return "0";
        var y = (int)Math.Round(d / 365.25);
        if (y > 0 && y * 365 + y / 4 == d) return $"{y} y";
        var m = (int)Math.Round(d / 30.44);
        if (m > 0 && (int)Math.Round(m * 30.44) == d) return $"{m} m";
        return $"{d} d";
    }
}
