using BurhanNexus.HMS.Infrastructure.Laboratory;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Lab;

/// <summary>Shared base for the patient-based laboratory work lists.</summary>
public abstract class LabListModel : HmsPageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    public IReadOnlyList<LabOrderSummary> Orders { get; protected set; } = Array.Empty<LabOrderSummary>();

    protected async Task LoadAsync(string scope, string nav, string tab)
    {
        ViewData["Nav"] = nav;
        ViewData["LabTab"] = tab;
        Orders = await HttpContext.RequestServices.GetRequiredService<ILabService>()
            .OrdersAsync(new LabOrderQuery { Scope = scope, Q = Q, Take = 300 });
    }
}
