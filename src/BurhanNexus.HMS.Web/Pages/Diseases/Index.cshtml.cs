using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Diseases;

/// <summary>Update 18: Disease Management – the central Disease Master (add, edit, delete / deactivate, search).</summary>
[HasPermission(Perm.DiseasesView, Perm.DiseasesAdd, Perm.DiseasesEdit, Perm.DiseasesDelete)]
public class IndexModel : HmsPageModel
{
    private readonly IDiseaseService _diseases;
    public IndexModel(IDiseaseService diseases) => _diseases = diseases;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    /// <summary>active (default), inactive or all.</summary>
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "edit")] public int? EditId { get; set; }
    [BindProperty(SupportsGet = true, Name = "new")] public bool New { get; set; }
    [BindProperty] public DiseaseInput Form { get; set; } = new();
    [BindProperty] public uint? RowVersion { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public const int PageSize = 50;
    public IReadOnlyList<DiseaseRow> Rows { get; private set; } = Array.Empty<DiseaseRow>();
    public int Total { get; private set; }
    public Disease? Editing { get; private set; }
    public bool CanAdd { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanDelete { get; private set; }
    public bool ShowForm => (Editing is not null && CanEdit) || (New && CanAdd) || !ModelState.IsValid;

    private async Task LoadAsync()
    {
        ViewData["Nav"] = "diseases";
        CanAdd = await CanAsync(Perm.DiseasesAdd);
        CanEdit = await CanAsync(Perm.DiseasesEdit);
        CanDelete = await CanAsync(Perm.DiseasesDelete);
        bool? active = Status switch { "inactive" => false, "all" => null, _ => true };
        var all = await _diseases.ListAsync(Q, active);
        Total = all.Count;
        PageNo = Math.Clamp(PageNo, 1, Math.Max(1, (Total + PageSize - 1) / PageSize));
        Rows = all.Skip((PageNo - 1) * PageSize).Take(PageSize).ToList();
        Editing = EditId is int id ? await _diseases.GetAsync(id) : null;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Editing is { } d)
        {
            Form = new DiseaseInput { Name = d.Name, Code = d.Code, Description = d.Description, IsActive = d.IsActive };
            RowVersion = d.RowVersion;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await DenyUnlessAsync(EditId is null ? Perm.DiseasesAdd : Perm.DiseasesEdit) is { } deny) return deny;
        var r = await _diseases.SaveAsync(EditId, Form, RowVersion);
        if (!r.Succeeded)
        {
            await LoadAsync();
            AddErrors(r);
            return Page();
        }
        FlashSuccess = EditId is null ? $"Disease '{Form.Name?.Trim()}' added." : $"Disease '{Form.Name?.Trim()}' saved.";
        return Redirect("/Diseases");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (await DenyUnlessAsync(Perm.DiseasesDelete) is { } deny) return deny;
        var r = await _diseases.DeleteAsync(id);
        if (!r.Succeeded) FlashError = string.Join(" ", r.Errors);
        else FlashSuccess = r.Value ? "The disease is used in patient records – it was deactivated and hidden (the records are kept)." : "Disease deleted.";
        return Redirect("/Diseases");
    }
}
