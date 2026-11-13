namespace BurhanNexus.HMS.Core.Security;

public sealed record PermissionAction(string Key, string Name, string Description);

public sealed record ModuleDefinition(
    string Key,
    string Name,
    string Description,
    string Icon,
    bool IsAvailable,
    string? Url,
    IReadOnlyList<PermissionAction> Actions,
    bool IsFinancialModule = false)
{
    public string PermissionKey(string action) => $"{Key}.{action}";
}

public sealed record PermissionDefinition(
    string Key, string Module, string Action, string ModuleName, string ActionName, string Description, int SortOrder);

/// <summary>
/// The list of permissions the software understands. This defines what CAN be granted;
/// who receives which permission is decided entirely by the hospital's administrator at run time.
/// New modules in later phases add entries here; existing grants are never touched.
/// </summary>
public static class PermissionCatalog
{
    public static class Actions
    {
        public const string View = "View";
        public const string Add = "Add";
        public const string Edit = "Edit";
        public const string Delete = "Delete";
        public const string Print = "Print";
        public const string ExportPdf = "ExportPdf";
        public const string Verify = "Verify";
        public const string Approve = "Approve";
        public const string Cancel = "Cancel";
        public const string Refund = "Refund";
        public const string FinancialAccess = "FinancialAccess";
        public const string SettingsAccess = "SettingsAccess";
    }

    private static readonly PermissionAction[] StandardActions =
    {
        new(Actions.View, "View", "Open the module and view records"),
        new(Actions.Add, "Add", "Create new records"),
        new(Actions.Edit, "Edit", "Modify existing records"),
        new(Actions.Delete, "Delete", "Delete records"),
        new(Actions.Print, "Print", "Print documents"),
        new(Actions.ExportPdf, "Export PDF", "Export documents as PDF"),
        new(Actions.Verify, "Verify", "Verify results or entries"),
        new(Actions.Approve, "Approve", "Approve entries"),
        new(Actions.Cancel, "Cancel", "Cancel entries"),
        new(Actions.Refund, "Refund", "Issue refunds"),
        new(Actions.FinancialAccess, "Financial Access", "See amounts, collections and financial figures"),
        new(Actions.SettingsAccess, "Settings Access", "Change module settings"),
    };

    private static ModuleDefinition Future(string key, string name, string description, string icon, bool financial = false) =>
        new(key, name, description, icon, IsAvailable: false, Url: null, StandardActions, financial);

    public static readonly IReadOnlyList<ModuleDefinition> Modules = new List<ModuleDefinition>
    {
        new(Modules_.Dashboard, "Dashboard", "Home dashboard", "home", true, "/",
            new PermissionAction[]
            {
                new(Actions.View, "View", "Open the dashboard"),
                new(Actions.FinancialAccess, "Financial Figures", "See hospital-wide financial figures on the dashboard"),
            }),

        Future("Patients", "Patient Management", "Patient registration and records", "users-round"),
        Future("Opd", "OPD Management", "Out-patient department", "stethoscope"),
        Future("Emergency", "Emergency Management", "Emergency department", "siren"),
        Future("Laboratory", "Laboratory Management", "Laboratory tests and reports", "flask"),
        Future("XRay", "X-Ray Management", "X-Ray department", "scan"),
        Future("Ultrasound", "Ultrasound Management", "Ultrasound department", "wave"),
        Future("Pharmacy", "Pharmacy Management", "Medicine inventory and dispensing", "pill"),
        Future("MinorOt", "Minor OT", "Minor operation theatre settings", "scissors"),
        Future("Doctors", "Doctor Management", "Doctors, schedules and shares", "user-cross"),
        Future("Finance", "Financial Management", "Collections, expenses and accounts", "wallet", financial: true),
        Future("Reports", "Reports", "Operational and financial reports", "chart"),

        new(Modules_.UserManagement, "User Management", "Users, departments and responsibilities", "user-cog", true, "/Users",
            new PermissionAction[]
            {
                new(Actions.View, "View Users", "Open user management and view users"),
                new(Actions.Add, "Create Users", "Create new user accounts"),
                new(Actions.Edit, "Edit Users", "Edit user information"),
                new(UserActions.Activate, "Activate / Deactivate", "Activate or deactivate user accounts"),
                new(UserActions.ResetPassword, "Reset Passwords", "Reset other users' passwords"),
                new(UserActions.AssignDepartments, "Assign Departments", "Assign or remove departments for users"),
                new(UserActions.AssignPermissions, "Assign Permissions", "Assign responsibilities and individual permissions"),
                new(UserActions.ManageDepartments, "Manage Departments", "Create and edit departments"),
                new(UserActions.ManageRoles, "Manage Responsibilities", "Create and edit responsibility templates"),
                new(UserActions.ViewLoginHistory, "View Login History", "View login history of users"),
            }),

        new(Modules_.HospitalSettings, "Hospital Settings", "Hospital information, branding and preferences", "settings", true, "/Settings",
            new PermissionAction[]
            {
                new(Actions.View, "View", "View hospital settings"),
                new(Actions.Edit, "Edit", "Edit hospital information, branding, printing and preferences"),
                new(SettingsActions.ChangeHospitalType, "Change Hospital Type", "Change Government / Private type and operational rules"),
                new(Actions.Print, "Print Preview", "Open printable document previews"),
                new(Actions.SettingsAccess, "Security Settings", "Change session timeout and login protection"),
            }),

        new(Modules_.SystemBackup, "System Backup", "Database backup and restore", "database", true, "/Backup",
            new PermissionAction[]
            {
                new(Actions.View, "View", "View backup history"),
                new(BackupActions.Create, "Create Backup", "Create a manual backup"),
                new(BackupActions.Download, "Download Backup", "Download backup files"),
                new(BackupActions.Restore, "Restore Backup", "Restore the database from a backup"),
                new(Actions.Delete, "Delete Backup", "Delete backup files"),
            }),

        new(Modules_.LicenseManagement, "License Management", "Software licence", "key", true, "/Settings/License",
            new PermissionAction[]
            {
                new(Actions.View, "View", "View licence status"),
                new(LicenseActions.Activate, "Activate", "Enter activation keys"),
            }),

        new(Modules_.AuditLog, "Audit Log", "System activity log", "shield", true, "/Audit",
            new PermissionAction[]
            {
                new(Actions.View, "View", "View the audit log"),
                new(AuditActions.Export, "Export CSV", "Export the audit log as CSV"),
            }),
    };

    public static class Modules_
    {
        public const string Dashboard = "Dashboard";
        public const string UserManagement = "UserManagement";
        public const string HospitalSettings = "HospitalSettings";
        public const string SystemBackup = "SystemBackup";
        public const string LicenseManagement = "LicenseManagement";
        public const string AuditLog = "AuditLog";
    }

    public static class UserActions
    {
        public const string Activate = "Activate";
        public const string ResetPassword = "ResetPassword";
        public const string AssignDepartments = "AssignDepartments";
        public const string AssignPermissions = "AssignPermissions";
        public const string ManageDepartments = "ManageDepartments";
        public const string ManageRoles = "ManageRoles";
        public const string ViewLoginHistory = "ViewLoginHistory";
    }

    public static class SettingsActions { public const string ChangeHospitalType = "ChangeHospitalType"; }
    public static class BackupActions { public const string Create = "Create"; public const string Download = "Download"; public const string Restore = "Restore"; }
    public static class LicenseActions { public const string Activate = "Activate"; }
    public static class AuditActions { public const string Export = "Export"; }

    public static IReadOnlyList<PermissionDefinition> All { get; } = BuildAll();

    public static IReadOnlySet<string> AllKeys { get; } = All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    public static ModuleDefinition? FindModule(string key) => Modules.FirstOrDefault(m => m.Key == key);

    private static List<PermissionDefinition> BuildAll()
    {
        var list = new List<PermissionDefinition>();
        var order = 0;
        foreach (var m in Modules)
        {
            foreach (var a in m.Actions)
            {
                list.Add(new PermissionDefinition(m.PermissionKey(a.Key), m.Key, a.Key, m.Name, a.Name, a.Description, order++));
            }
        }
        return list;
    }
}

/// <summary>Permission keys used directly by Phase 1 code.</summary>
public static class Perm
{
    public const string DashboardView = "Dashboard.View";
    public const string DashboardFinancial = "Dashboard.FinancialAccess";

    public const string UsersView = "UserManagement.View";
    public const string UsersAdd = "UserManagement.Add";
    public const string UsersEdit = "UserManagement.Edit";
    public const string UsersActivate = "UserManagement.Activate";
    public const string UsersResetPassword = "UserManagement.ResetPassword";
    public const string UsersAssignDepartments = "UserManagement.AssignDepartments";
    public const string UsersAssignPermissions = "UserManagement.AssignPermissions";
    public const string DepartmentsManage = "UserManagement.ManageDepartments";
    public const string RolesManage = "UserManagement.ManageRoles";
    public const string LoginHistoryView = "UserManagement.ViewLoginHistory";

    public const string SettingsView = "HospitalSettings.View";
    public const string SettingsEdit = "HospitalSettings.Edit";
    public const string SettingsChangeType = "HospitalSettings.ChangeHospitalType";
    public const string SettingsPrint = "HospitalSettings.Print";
    public const string SettingsSecurity = "HospitalSettings.SettingsAccess";

    public const string BackupView = "SystemBackup.View";
    public const string BackupCreate = "SystemBackup.Create";
    public const string BackupDownload = "SystemBackup.Download";
    public const string BackupRestore = "SystemBackup.Restore";
    public const string BackupDelete = "SystemBackup.Delete";

    public const string LicenseView = "LicenseManagement.View";
    public const string LicenseActivate = "LicenseManagement.Activate";

    public const string AuditView = "AuditLog.View";
    public const string AuditExport = "AuditLog.Export";
}
