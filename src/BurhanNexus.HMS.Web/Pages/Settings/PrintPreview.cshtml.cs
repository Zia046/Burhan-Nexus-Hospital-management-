using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Pages.Settings;

/// <summary>Sample printable documents that demonstrate the saved branding. Contains no patient data.</summary>
[HasPermission(Perm.SettingsPrint)]
public class PrintPreviewModel : HmsPageModel
{
    private readonly IHospitalAdminService _hospital;
    public PrintPreviewModel(IHospitalAdminService hospital) => _hospital = hospital;

    public string Doc { get; private set; } = "report";
    public Core.Domain.HospitalConfiguration Config { get; private set; } = null!;

    public async Task OnGetAsync(string? doc)
    {
        Doc = doc == "receipt" ? "receipt" : "report";
        Config = await _hospital.GetConfigurationAsync();
    }
}
