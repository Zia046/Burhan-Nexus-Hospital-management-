using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Licensing;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;

namespace BurhanNexus.HMS.Web.Pages;

public class IndexModel : HmsPageModel
{
    private readonly IUserService _users;
    private readonly ILicenseService _license;
    private readonly ISystemStatusService _status;
    private readonly IAuditQueryService _audit;
    private readonly IHospitalAdminService _hospital;

    public IndexModel(IUserService users, ILicenseService license, ISystemStatusService status, IAuditQueryService audit, IHospitalAdminService hospital)
    {
        _users = users;
        _license = license;
        _status = status;
        _audit = audit;
        _hospital = hospital;
    }

    public IReadOnlySet<string> Perms { get; private set; } = new HashSet<string>();
    public (int Total, int Active, int Inactive)? UserCounts { get; private set; }
    public LicenseStatus? License { get; private set; }
    public SystemHealth? Health { get; private set; }
    public IReadOnlyList<AuditLog> Recent { get; private set; } = Array.Empty<AuditLog>();
    public HospitalConfiguration? Configuration { get; private set; }
    public IReadOnlyList<ModuleDefinition> UpcomingModules { get; private set; } = Array.Empty<ModuleDefinition>();
    public string ServerAddresses { get; private set; } = string.Empty;
    public bool IsAdminView { get; private set; }
    public bool ShowDashboard { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Nav"] = "dashboard";
        Perms = await Permissions.GetAsync();
        ShowDashboard = Perms.Contains(Perm.DashboardView);
        IsAdminView = Perms.Contains(Perm.UsersView) || Perms.Contains(Perm.SettingsView) || Perms.Contains(Perm.BackupView);

        if (Perms.Contains(Perm.UsersView)) UserCounts = await _users.CountsAsync();
        if (Perms.Contains(Perm.LicenseView)) License = await _license.GetStatusAsync();
        if (Perms.Contains(Perm.SettingsView) || Perms.Contains(Perm.BackupView))
        {
            Health = await _status.CheckAsync();
            ServerAddresses = NetworkInfo.LanAddressesText(Request);
        }
        if (Perms.Contains(Perm.SettingsView)) Configuration = await _hospital.GetConfigurationAsync();
        if (Perms.Contains(Perm.AuditView)) Recent = await _audit.RecentAsync(8);

        // Future modules: shown as "coming soon" only if the user has been given access to them.
        UpcomingModules = PermissionCatalog.Modules
            .Where(m => !m.IsAvailable && (User.IsSystemAdministrator() || Perms.Contains(m.PermissionKey(PermissionCatalog.Actions.View))))
            .Where(m => !m.IsFinancialModule || Perms.Contains(m.PermissionKey(PermissionCatalog.Actions.View)))
            .ToList();
    }
}
