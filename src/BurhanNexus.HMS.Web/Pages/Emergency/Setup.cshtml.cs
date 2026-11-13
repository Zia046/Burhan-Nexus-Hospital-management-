using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Emergency;

[HasPermission(Perm.EmergencySettings)]
public class SetupModel : HmsPageModel
{
    private readonly IRegistrationSetupService _setup;
    public SetupModel(IRegistrationSetupService setup) => _setup = setup;

    public IReadOnlyList<EmergencyCategory> Categories { get; private set; } = Array.Empty<EmergencyCategory>();
    [BindProperty(SupportsGet = true, Name = "edit")] public int? EditId { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "emergency";
        Categories = await _setup.CategoriesAsync(false);
    }

    public async Task<IActionResult> OnPostAsync(int? id, string name, int sortOrder, bool isActive)
    {
        var r = await _setup.SaveCategoryAsync(id, name, sortOrder, isActive);
        if (r.Succeeded) FlashSuccess = "Category saved."; else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
