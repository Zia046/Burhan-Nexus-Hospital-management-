using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>IPD settings and the configurable clinical forms (fields, units, choices and protocol alert limits).</summary>
[HasPermission(Perm.IpdSettings)]
public class SettingsModel : IpdPageModel
{
    [BindProperty] public IpdSettingsInput Input { get; set; } = new();
    [BindProperty] public uint RowVersion { get; set; }
    [BindProperty] public FieldInput Field { get; set; } = new();
    [BindProperty(SupportsGet = true, Name = "field")] public int? EditField { get; set; }
    [BindProperty(SupportsGet = true, Name = "kind")] public RecordKind? Kind { get; set; }

    public IReadOnlyList<ClinicalFormField> Fields { get; private set; } = Array.Empty<ClinicalFormField>();
    public IReadOnlyList<ServiceRow> Services { get; private set; } = Array.Empty<ServiceRow>();
    public bool IsPrivate { get; private set; }

    private async Task LoadAsync()
    {
        Nav("ipd-settings", "settings");
        IsPrivate = await Setup.IsPrivateAsync();
        Fields = await Setup.FieldsAsync(Kind, activeOnly: false);
        Services = (await HttpContext.RequestServices.GetRequiredService<IFinanceSetupService>().ServicesAsync(activeOnly: true))
            .Where(s => s.Item.Kind is ServiceKind.Inpatient or ServiceKind.Other or ServiceKind.Procedure).ToList();
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        var s = await Setup.SettingsAsync();
        RowVersion = s.RowVersion;
        Input = new IpdSettingsInput
        {
            AdmissionServiceItemId = s.AdmissionServiceItemId, AllowDirectAdmission = s.AllowDirectAdmission, RequireBillClearanceForDischarge = s.RequireBillClearanceForDischarge,
            PrintHeader = s.PrintHeader, LetterheadTopMarginMm = s.LetterheadTopMarginMm, FormFooter = s.FormFooter, RequireMarConfirmations = s.RequireMarConfirmations,
        };
        if (Fields.FirstOrDefault(f => f.Id == EditField) is { } f)
            Field = new FieldInput { Kind = f.Kind, Key = f.Key, Label = f.Label, Section = f.Section, Type = f.Type, Unit = f.Unit, Choices = f.Choices, IsRequired = f.IsRequired,
                IsActive = f.IsActive, SortOrder = f.SortOrder, AlertLow = f.AlertLow, AlertHigh = f.AlertHigh, AlertSource = f.AlertSource };
        else Field.Kind = Kind ?? RecordKind.Vitals;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var r = await Setup.SaveSettingsAsync(Input, RowVersion);
        if (r.Succeeded) { FlashSuccess = "IPD settings saved."; return RedirectToPage(); }
        await LoadAsync();
        AddErrors(r);
        return Page();
    }

    public async Task<IActionResult> OnPostFieldAsync()
    {
        var r = await Setup.SaveFieldAsync(EditField, Field);
        if (r.Succeeded) { FlashSuccess = "Form field saved."; return Redirect($"/Ipd/Settings?kind={Field.Kind}"); }
        await LoadAsync();
        var s = await Setup.SettingsAsync();
        RowVersion = s.RowVersion;
        AddErrors(r);
        return Page();
    }
}
