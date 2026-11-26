using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>Update 15: CBC configuration – activate / deactivate, rename and reorder the CBC parameters, or restore the default 14.</summary>
[HasPermission(Perm.LabManageParameters)]
public class CbcModel : HmsPageModel
{
    private readonly ICbcConfigService _cbc;
    public CbcModel(ICbcConfigService cbc) => _cbc = cbc;

    [BindProperty] public List<CbcRowInput> Rows { get; set; } = new();
    public LabTest? Test { get; private set; }

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "lab-catalogue";
        ViewData["LabTab"] = "cbc";
        Test = await _cbc.CbcTestAsync();
        if (Test is not null && Rows.Count == 0)
            Rows = Test.Parameters.OrderBy(p => p.IsActive ? 0 : 1).ThenBy(p => p.SortOrder).ThenBy(p => p.Id)
                .Select((p, i) => new CbcRowInput { Id = p.Id, Name = p.Name, IsActive = p.IsActive, Position = i + 1 }).ToList();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await _cbc.SaveAsync(Rows);
        if (r.Succeeded) { FlashSuccess = "CBC configuration saved. New CBC bookings use it; earlier reports stay as they were."; return RedirectToPage(); }
        AddErrors(r);
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreAsync()
    {
        var r = await _cbc.RestoreDefaultAsync();
        if (!r.Succeeded) { FlashError = r.ErrorText; return RedirectToPage(); }
        FlashSuccess = "Default CBC configuration restored (14 parameters)." + (r.Value!.Count > 0 ? " Not found in this CBC: " + string.Join(", ", r.Value) + "." : "");
        return RedirectToPage();
    }

    public LabTestParameter? Param(int id) => Test?.Parameters.FirstOrDefault(p => p.Id == id);
}
