using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Backup;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Backup;

[HasPermission(Perm.BackupView)]
public class IndexModel : HmsPageModel
{
    private readonly IBackupService _backups;
    public IndexModel(IBackupService backups) => _backups = backups;

    public IReadOnlyList<BackupHistory> Items { get; private set; } = Array.Empty<BackupHistory>();
    public string? ToolsWarning { get; private set; }
    public string Folder => _backups.BackupDirectory;
    [BindProperty] public string? Notes { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "backup";
        Items = await _backups.ListAsync();
        ToolsWarning = _backups.ToolsStatus();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (await DenyUnlessAsync(Perm.BackupCreate) is { } deny) return deny;
        var r = await _backups.CreateAsync(BackupKind.Manual, Notes, HttpContext.RequestAborted);
        if (r.Succeeded) FlashSuccess = $"Backup created: {r.Value!.FileName}";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int id)
    {
        if (await DenyUnlessAsync(Perm.BackupDownload) is { } deny) return deny;
        var r = await _backups.GetFileForDownloadAsync(id);
        if (!r.Succeeded)
        {
            FlashError = r.ErrorText;
            return RedirectToPage();
        }
        return PhysicalFile(r.Value!, "application/octet-stream", Path.GetFileName(r.Value!));
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (await DenyUnlessAsync(Perm.BackupDelete) is { } deny) return deny;
        var r = await _backups.DeleteAsync(id);
        if (r.Succeeded) FlashSuccess = "Backup deleted.";
        else FlashError = r.ErrorText;
        return RedirectToPage();
    }
}
