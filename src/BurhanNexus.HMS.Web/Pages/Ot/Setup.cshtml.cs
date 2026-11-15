using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>OT settings: consent types and printable templates, safety / pre-op checklist items, OT fee services and print options.</summary>
[HasPermission(Perm.OtSettings, Perm.OtManageCatalogue)]
public class SetupModel : OtPageModel
{
    [BindProperty(SupportsGet = true, Name = "consent")] public int? EditConsent { get; set; }
    [BindProperty(SupportsGet = true, Name = "item")] public int? EditItem { get; set; }
    [BindProperty] public OtSettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public string? ConsentName { get; set; }
    [BindProperty] public string? ConsentTemplate { get; set; }
    [BindProperty] public bool ConsentActive { get; set; } = true;
    [BindProperty] public int ConsentSort { get; set; }
    [BindProperty] public ChecklistStage ItemStage { get; set; } = ChecklistStage.SignIn;
    [BindProperty] public string? ItemLabel { get; set; }
    [BindProperty] public bool ItemMandatory { get; set; } = true;
    [BindProperty] public bool ItemActive { get; set; } = true;
    [BindProperty] public int ItemSort { get; set; }

    public IReadOnlyList<OtConsentType> ConsentTypes { get; private set; } = Array.Empty<OtConsentType>();
    public IReadOnlyList<OtChecklistItem> Items { get; private set; } = Array.Empty<OtChecklistItem>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public bool IsPrivate { get; private set; }
    public static readonly RecordKind[] OtForms = { RecordKind.PreOpAssessment, RecordKind.AnesthesiaAssessment, RecordKind.IntraOpMonitoring, RecordKind.PacuObservation, RecordKind.PostOpRecord };

    private async Task LoadAsync()
    {
        Nav("ot-setup", "setup");
        IsPrivate = await Setup.IsPrivateAsync();
        ConsentTypes = await Setup.ConsentTypesAsync(activeOnly: false);
        Items = await Setup.ChecklistAsync(null, activeOnly: false);
        Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true))
            .Where(s => s.Item.Kind is not (ServiceKind.Pharmacy or ServiceKind.Laboratory or ServiceKind.XRay or ServiceKind.Ultrasound or ServiceKind.OpdRegistration or ServiceKind.EmergencyRegistration)).ToList();
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        var s = await Setup.SettingsAsync();
        RowVersion = s.RowVersion;
        Input = new OtSettingsInput
        {
            SurgeonFeeItemId = s.SurgeonFeeItemId, AssistantFeeItemId = s.AssistantFeeItemId, AnesthesiaFeeItemId = s.AnesthesiaFeeItemId, AnesthesiaSuppliesItemId = s.AnesthesiaSuppliesItemId,
            RecoveryItemId = s.RecoveryItemId, EquipmentItemId = s.EquipmentItemId, ConsumablesItemId = s.ConsumablesItemId, DefaultCleaningMinutes = s.DefaultCleaningMinutes,
            PrintHeader = s.PrintHeader, LetterheadTopMarginMm = s.LetterheadTopMarginMm, FormFooter = s.FormFooter, MaxUploadMb = s.MaxUploadMb,
        };
        if (ConsentTypes.FirstOrDefault(c => c.Id == EditConsent) is { } c) { ConsentName = c.Name; ConsentTemplate = c.TemplateText; ConsentActive = c.IsActive; ConsentSort = c.SortOrder; }
        if (Items.FirstOrDefault(i => i.Id == EditItem) is { } it) { ItemStage = it.Stage; ItemLabel = it.Label; ItemMandatory = it.IsMandatory; ItemActive = it.IsActive; ItemSort = it.SortOrder; }
    }

    private async Task<IActionResult> DoneAsync(Core.Abstractions.ServiceResult r, string ok)
    {
        if (r.Succeeded) { FlashSuccess = ok; return RedirectToPage(); }
        await LoadAsync();
        RowVersion = (await Setup.SettingsAsync()).RowVersion;
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(Perm.OtSettings) is { } deny) return deny;
        return await DoneAsync(await Setup.SaveSettingsAsync(Input, RowVersion), "OT settings saved.");
    }

    public async Task<IActionResult> OnPostConsentAsync()
    {
        if (await DenyUnlessAsync(Perm.OtSettings) is { } deny) return deny;
        return await DoneAsync(await Setup.SaveConsentTypeAsync(EditConsent, ConsentName, ConsentTemplate, ConsentActive, ConsentSort), "Consent type saved.");
    }

    public async Task<IActionResult> OnPostItemAsync()
    {
        if (await DenyUnlessAsync(Perm.OtSettings) is { } deny) return deny;
        return await DoneAsync(await Setup.SaveChecklistItemAsync(EditItem, ItemStage, ItemLabel, ItemMandatory, ItemActive, ItemSort), "Checklist item saved.");
    }
}
