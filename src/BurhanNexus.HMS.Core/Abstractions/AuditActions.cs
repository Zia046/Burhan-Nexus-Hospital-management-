namespace BurhanNexus.HMS.Core.Abstractions;

public static class AuditActions
{
    public const string SetupCompleted = "Setup.Completed";
    public const string SetupReset = "Setup.Reset";
    public const string LoginSucceeded = "Auth.Login";
    public const string LoginFailed = "Auth.LoginFailed";
    public const string AccountLocked = "Auth.AccountLocked";
    public const string Logout = "Auth.Logout";
    public const string SessionExpired = "Auth.SessionExpired";
    public const string PasswordChanged = "Auth.PasswordChanged";
    public const string UserCreated = "User.Created";
    public const string UserUpdated = "User.Updated";
    public const string UserActivated = "User.Activated";
    public const string UserDeactivated = "User.Deactivated";
    public const string UserPasswordReset = "User.PasswordReset";
    public const string UserUnlocked = "User.Unlocked";
    public const string UserDepartmentsChanged = "User.DepartmentsChanged";
    public const string UserRolesChanged = "User.ResponsibilitiesChanged";
    public const string UserPermissionsChanged = "User.PermissionsChanged";
    public const string PermissionDenied = "Security.AccessDenied";
    public const string DepartmentCreated = "Department.Created";
    public const string DepartmentUpdated = "Department.Updated";
    public const string RoleCreated = "Responsibility.Created";
    public const string RoleUpdated = "Responsibility.Updated";
    public const string RolePermissionsChanged = "Responsibility.PermissionsChanged";
    public const string HospitalSettingsChanged = "Settings.HospitalChanged";
    public const string LogoChanged = "Settings.LogoChanged";
    public const string PreferencesChanged = "Settings.PreferencesChanged";
    public const string PrintSettingsChanged = "Settings.PrintChanged";
    public const string HospitalTypeChanged = "Settings.HospitalTypeChanged";
    public const string OperationalRulesChanged = "Settings.OperationalRulesChanged";
    public const string SecuritySettingsChanged = "Settings.SecurityChanged";
    public const string LicenseTrialStarted = "License.TrialStarted";
    public const string LicenseActivated = "License.Activated";
    public const string LicenseActivationFailed = "License.ActivationFailed";
    public const string BackupCreated = "Backup.Created";
    public const string BackupFailed = "Backup.Failed";
    public const string BackupDownloaded = "Backup.Downloaded";
    public const string BackupDeleted = "Backup.Deleted";
    public const string BackupRestored = "Backup.Restored";
    public const string BackupRestoreFailed = "Backup.RestoreFailed";
    public const string AuditExported = "Audit.Exported";
}
