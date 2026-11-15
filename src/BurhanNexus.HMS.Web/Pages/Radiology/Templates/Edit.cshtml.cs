using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology.Templates;

[HasPermission(Perm.RadManageTemplates)]
public class EditModel : RadiologyPageModel
{
    [BindProperty] public RadiologyTemplateInput Input { get; set; } = new();
    public int? Id { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Nav("rad-services", "services");
        Id = id;
        if (id is int tid)
        {
            var t = await Catalog.TemplateAsync(tid);
            if (t is null) return NotFound();
            Input = new RadiologyTemplateInput
            {
                Name = t.Name, Modality = t.Modality, Technique = t.Technique, Findings = t.Findings, Impression = t.Impression, Recommendations = t.Recommendations, IsActive = t.IsActive
            };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        var r = await Catalog.SaveTemplateAsync(id, Input);
        if (r.Succeeded) { FlashSuccess = "Report template saved. Existing reports are not changed."; return Redirect("/Radiology/Services"); }
        Nav("rad-services", "services");
        Id = id;
        AddErrors(r);
        return Page();
    }
}
