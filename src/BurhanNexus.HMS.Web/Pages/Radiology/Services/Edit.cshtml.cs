using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Radiology;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Radiology.Services;

/// <summary>Add / edit a radiology service. The fee is a Phase 3 service fee (history kept) and needs "Manage Radiology Fees".</summary>
[HasPermission(Perm.RadManageServices)]
public class EditModel : RadiologyPageModel
{
    [BindProperty] public RadiologyServiceInput Input { get; set; } = new();
    public int? Id { get; private set; }
    public bool CanManageFees { get; private set; }
    public decimal CurrentFee { get; private set; }
    public IReadOnlyList<RadiologyTemplate> Templates { get; private set; } = Array.Empty<RadiologyTemplate>();

    private async Task LoadAsync(int? id)
    {
        Nav("rad-services", "services");
        Id = id;
        CanManageFees = await CanAsync(Perm.RadManageFees);
        Templates = await Catalog.TemplatesAsync(activeOnly: true);
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await LoadAsync(id);
        if (id is int sid)
        {
            var row = await Catalog.ServiceAsync(sid);
            if (row is null) return NotFound();
            var s = row.Service;
            CurrentFee = row.Fee;
            Input = new RadiologyServiceInput
            {
                Code = s.Code, Name = s.Name, Modality = s.Modality, BodyPart = s.BodyPart, Description = s.Description, PreparationInstructions = s.PreparationInstructions,
                DurationMinutes = s.DurationMinutes, ReportTitle = s.ReportTitle, DefaultTemplateId = s.DefaultTemplateId, IsActive = s.IsActive, SortOrder = s.SortOrder,
                IsFree = row.Fee == 0, Fee = row.Fee, AllowPartialPayment = row.AllowPartial
            };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        var r = await Catalog.SaveServiceAsync(id, Input, await CanAsync(Perm.RadManageFees));
        if (r.Succeeded)
        {
            FlashSuccess = "Radiology service saved.";
            return Redirect("/Radiology/Services");
        }
        await LoadAsync(id);
        AddErrors(r);
        return Page();
    }
}
