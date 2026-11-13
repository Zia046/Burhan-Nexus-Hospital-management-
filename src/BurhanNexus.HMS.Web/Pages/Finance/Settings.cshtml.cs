using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Finance;

[HasPermission(Perm.FinanceSettings)]
public class SettingsModel : HmsPageModel
{
    private readonly IFinanceSetupService _setup;
    public SettingsModel(IFinanceSetupService setup) => _setup = setup;

    [BindProperty] public FinanceSettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public PaymentMethodInput Method { get; set; } = new();
    [BindProperty(SupportsGet = true)] public int? MethodId { get; set; }
    public IReadOnlyList<PaymentMethod> Methods { get; private set; } = Array.Empty<PaymentMethod>();

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "finance-settings";
        ViewData["FinanceTab"] = "settings";
        Methods = await _setup.PaymentMethodsAsync(false);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        var s = await _setup.GetSettingsAsync();
        Input = new FinanceSettingsInput
        {
            ReceiptPrefix = s.ReceiptPrefix, IssueZeroReceiptForFreeServices = s.IssueZeroReceiptForFreeServices, ShowCashierOnReceipt = s.ShowCashierOnReceipt,
            ReceiptFooter = s.ReceiptFooter, DefaultReceiptWidthMm = s.DefaultReceiptWidthMm
        };
        RowVersion = s.RowVersion;
        if (MethodId is int id && Methods.FirstOrDefault(m => m.Id == id) is { } m)
            Method = new PaymentMethodInput { Code = m.Code, Name = m.Name, CountsAsCashInHand = m.CountsAsCashInHand, RequiresReference = m.RequiresReference, IsActive = m.IsActive, SortOrder = m.SortOrder };
        return Page();
    }

    public async Task<IActionResult> OnPostSettingsAsync()
    {
        var r = await _setup.UpdateSettingsAsync(Input, RowVersion);
        if (r.Succeeded) { FlashSuccess = "Financial settings saved."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostMethodAsync()
    {
        var r = await _setup.SavePaymentMethodAsync(MethodId, Method);
        if (r.Succeeded) { FlashSuccess = "Payment method saved."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }
}
