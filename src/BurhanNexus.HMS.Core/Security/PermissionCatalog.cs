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

        new(Modules_.Patients, "Patient Management", "Patient registration, search and records", "users-round", true, "/Patients",
            new PermissionAction[]
            {
                new(PatientActions.Search, "Search Patients", "Search patients and see the result list"),
                new(Actions.View, "View Patient Profile", "Open the patient profile"),
                new(Actions.Add, "Register Patients", "Register new patients"),
                new(Actions.Edit, "Edit Patient Information", "Correct patient details (Patient ID never changes)"),
                new(PatientActions.ViewVisits, "View Previous Visits", "See the visit history of a patient"),
                new(Actions.Print, "Print Patient Record", "Print the patient record summary"),
                new(PatientActions.Export, "Export Patient Records", "Export patient lists to CSV"),
                new(Actions.Delete, "Archive / Restore", "Archive or restore patient records (records are never deleted)"),
                new(PatientActions.MergeDuplicates, "Correct Duplicate Registrations", "Merge a duplicate registration into the correct patient (history kept)"),
                new(Actions.SettingsAccess, "Registration Settings", "Patient ID and visit number format, slip settings"),
                new(Actions.ExportPdf, "Export PDF", "Export documents as PDF"),
                new(Actions.Verify, "Verify", "Reserved for future use"),
                new(Actions.Approve, "Approve", "Reserved for future use"),
                new(Actions.Cancel, "Cancel", "Reserved for future use"),
                new(Actions.Refund, "Refund", "Reserved for future use"),
                new(Actions.FinancialAccess, "Financial Access", "Reserved for the billing phase"),
            }),
        new(Modules_.Opd, "OPD Management", "Out-patient visits and OPD slips", "stethoscope", true, "/Opd",
            new PermissionAction[]
            {
                new(Actions.View, "View OPD Visits", "Open OPD lists and visit details"),
                new(Actions.Add, "OPD Registration", "Register OPD visits"),
                new(Actions.Print, "OPD Slip Printing", "Print and reprint OPD slips / visit documents"),
                new(Actions.Edit, "Edit OPD Visit", "Correct department, doctor or notes of a visit"),
                new(Actions.SettingsAccess, "OPD Settings", "Doctors, tokens and OPD fee references"),
                new(Actions.Delete, "Delete", "Reserved for future use"),
                new(Actions.ExportPdf, "Export PDF", "Export documents as PDF"),
                new(Actions.Verify, "Verify", "Reserved for future use"),
                new(Actions.Approve, "Approve", "Reserved for future use"),
                new(Actions.Cancel, "Cancel", "Reserved for the billing phase"),
                new(Actions.Refund, "Refund", "Reserved for the billing phase"),
                new(Actions.FinancialAccess, "Financial Access", "Reserved for the billing phase"),
            }),
        new(Modules_.Emergency, "Emergency Management", "Emergency registration, slips and status", "siren", true, "/Emergency",
            new PermissionAction[]
            {
                new(Actions.View, "View Emergency Patients", "Open emergency lists and details"),
                new(Actions.Add, "Emergency Registration", "Register emergency patients"),
                new(Actions.Print, "Emergency Slip Printing", "Print and reprint emergency slips"),
                new(Actions.Edit, "Edit Emergency Details", "Update category, complaint, attendant and notes"),
                new(EmergencyActions.UpdateStatus, "Update Status", "Under treatment, referred, discharged"),
                new(EmergencyActions.CloseStatus, "Close / Admit / Re-open", "Restricted statuses: closed, admitted and re-opening"),
                new(Actions.SettingsAccess, "Emergency Settings", "Emergency categories"),
                new(Actions.Delete, "Delete", "Reserved for future use"),
                new(Actions.ExportPdf, "Export PDF", "Export documents as PDF"),
                new(Actions.Verify, "Verify", "Reserved for future use"),
                new(Actions.Approve, "Approve", "Reserved for future use"),
                new(Actions.Cancel, "Cancel", "Reserved for the billing phase"),
                new(Actions.Refund, "Refund", "Reserved for the billing phase"),
                new(Actions.FinancialAccess, "Financial Access", "Reserved for the billing phase"),
            }),
        Future("Laboratory", "Laboratory Management", "Laboratory tests and reports", "flask"),
        Future("XRay", "X-Ray Management", "X-Ray department", "scan"),
        Future("Ultrasound", "Ultrasound Management", "Ultrasound department", "wave"),
        Future("Pharmacy", "Pharmacy Management", "Medicine inventory and dispensing", "pill"),
        new(Modules_.Appointments, "Appointments", "Private hospital doctor appointments and thermal token slips", "calendar", true, "/Appointments",
            new PermissionAction[]
            {
                new(Actions.View, "View Appointments", "Doctor-wise appointment lists and details"),
                new(Actions.Add, "Create Appointment", "Book appointments (assigns the doctor token)"),
                new(Actions.Edit, "Edit Appointment", "Change time, complaint, notes; mark as seen"),
                new(Actions.Cancel, "Cancel Appointment", "Cancel appointments (token is never reused)"),
                new(Actions.Print, "Print Appointment Slip", "Print the first copy of the thermal slip"),
                new(AppointmentActions.Reprint, "Reprint Appointment Slip", "Print further copies (same token)"),
                new(AppointmentActions.Reports, "Doctor-Wise Appointment Reports", "Daily, monthly and yearly statistics per doctor"),
                new(AppointmentActions.PrinterSettings, "Manage Printer Settings", "Thermal printer settings for workstations"),
                new(Actions.FinancialAccess, "See Consultation Fees", "See fee amounts in appointment screens"),
                new(Actions.SettingsAccess, "Appointment Settings", "Slip text, appointment time and booking rules"),
                new(Actions.Delete, "Delete", "Reserved – appointments are never deleted"),
                new(Actions.ExportPdf, "Export PDF", "Export documents as PDF"),
                new(Actions.Verify, "Verify", "Reserved for future use"),
                new(Actions.Approve, "Approve", "Reserved for future use"),
                new(Actions.Refund, "Refund", "Reserved for the billing phase"),
            }),
        Future("MinorOt", "Minor OT", "Minor operation theatre settings", "scissors"),
        Future("Doctors", "Doctor Management", "Doctors, schedules and shares", "user-cross"),
        new(Modules_.Finance, "Financial Management", "Cashier counters, receipts, deposits, refunds, doctor accounts and financial reports", "wallet", true, "/Finance",
            new PermissionAction[]
            {
                new(FinanceActions.ViewOwnAccount, "View Own Financial Account", "Own collections, deposits and outstanding balance"),
                new(FinanceActions.ViewOwnReceipts, "View Own Receipt History", "Receipts issued by this user"),
                new(FinanceActions.Collect, "Collect Patient Payments", "Take payments and create receipts at an assigned counter"),
                new(FinanceActions.IssueReceipts, "Issue (Print) Receipts", "Print the first copy of a receipt"),
                new(FinanceActions.Reprint, "Reprint Receipts", "Print further copies (same receipt number)"),
                new(FinanceActions.Discount, "Give Discounts", "Enter a discount on a service fee"),
                new(FinanceActions.ViewCancelled, "View Cancelled Receipts", "See cancelled receipts in own history"),
                new(FinanceActions.SubmitDeposit, "Submit Cash Deposit", "Hand over collected cash to Admin"),
                new(FinanceActions.DailyClosing, "Perform Daily Cash Closing", "Create the daily closing summary of own collections"),
                new(FinanceActions.CounterReports, "View Counter Reports", "Counter-wise collections, balances and receipts"),
                new(FinanceActions.ManageCounters, "Manage Counters", "Create counters and assign / transfer employees"),
                new(FinanceActions.ManageServiceFees, "Manage Service Fees", "Add services, change fees and payment rules"),
                new(FinanceActions.ViewAllAccounts, "View All Financial Accounts", "Every cashier's account, all receipts and hospital-wide closing"),
                new(FinanceActions.Dashboard, "View Hospital Financial Dashboard", "Hospital-wide totals, charts and reports"),
                new(FinanceActions.ConfirmDeposits, "Confirm Cash Deposits", "Confirm cash received from cashiers"),
                new(FinanceActions.RejectDeposits, "Reject Cash Deposits", "Reject a deposit with a reason"),
                new(FinanceActions.CancelReceipts, "Cancel Receipts", "Cancel an issued receipt with a reason (never deleted)"),
                new(FinanceActions.ProcessRefunds, "Process Refunds", "Enter refund requests for patients"),
                new(FinanceActions.ApproveRefunds, "Approve Refunds", "Approve or reject refunds (money returned)"),
                new(FinanceActions.ManageDoctorAccounts, "Manage Doctor Accounts", "Doctor consultation accounts and fee-sharing arrangements"),
                new(FinanceActions.RecordDoctorPayments, "Record Doctor Payments", "Pay doctors (full or partial settlement)"),
                new(FinanceActions.Adjustments, "Record Financial Adjustments", "Correct cashier / doctor balances with a reason"),
                new(FinanceActions.Export, "Export Financial Reports", "Excel / CSV export of financial reports"),
                new(FinanceActions.Settings, "Manage Financial Settings", "Payment methods and receipt settings"),
            }, IsFinancialModule: true),
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
        public const string Patients = "Patients";
        public const string Opd = "Opd";
        public const string Emergency = "Emergency";
        public const string Appointments = "Appointments";
        public const string Finance = "Finance";
    }

    public static class FinanceActions
    {
        public const string ViewOwnAccount = "ViewOwnAccount";
        public const string ViewOwnReceipts = "ViewOwnReceipts";
        public const string Collect = "Collect";
        public const string IssueReceipts = "IssueReceipts";
        public const string Reprint = "Reprint";
        public const string Discount = "Discount";
        public const string ViewCancelled = "ViewCancelled";
        public const string SubmitDeposit = "SubmitDeposit";
        public const string DailyClosing = "DailyClosing";
        public const string CounterReports = "CounterReports";
        public const string ManageCounters = "ManageCounters";
        public const string ManageServiceFees = "ManageServiceFees";
        public const string ViewAllAccounts = "ViewAllAccounts";
        public const string Dashboard = "Dashboard";
        public const string ConfirmDeposits = "ConfirmDeposits";
        public const string RejectDeposits = "RejectDeposits";
        public const string CancelReceipts = "CancelReceipts";
        public const string ProcessRefunds = "ProcessRefunds";
        public const string ApproveRefunds = "ApproveRefunds";
        public const string ManageDoctorAccounts = "ManageDoctorAccounts";
        public const string RecordDoctorPayments = "RecordDoctorPayments";
        public const string Adjustments = "Adjustments";
        public const string Export = "Export";
        public const string Settings = "Settings";
    }

    public static class AppointmentActions
    {
        public const string Reprint = "Reprint";
        public const string Reports = "Reports";
        public const string PrinterSettings = "PrinterSettings";
    }

    public static class PatientActions
    {
        public const string Search = "Search";
        public const string ViewVisits = "ViewVisits";
        public const string Export = "Export";
        public const string MergeDuplicates = "MergeDuplicates";
    }

    public static class EmergencyActions
    {
        public const string UpdateStatus = "UpdateStatus";
        public const string CloseStatus = "CloseStatus";
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

    public const string PatientsSearch = "Patients.Search";
    public const string PatientsView = "Patients.View";
    public const string PatientsAdd = "Patients.Add";
    public const string PatientsEdit = "Patients.Edit";
    public const string PatientsVisits = "Patients.ViewVisits";
    public const string PatientsPrint = "Patients.Print";
    public const string PatientsExport = "Patients.Export";
    public const string PatientsArchive = "Patients.Delete";
    public const string PatientsMerge = "Patients.MergeDuplicates";
    public const string PatientsSettings = "Patients.SettingsAccess";

    public const string OpdView = "Opd.View";
    public const string OpdAdd = "Opd.Add";
    public const string OpdPrint = "Opd.Print";
    public const string OpdEdit = "Opd.Edit";
    public const string OpdSettings = "Opd.SettingsAccess";

    public const string EmergencyView = "Emergency.View";
    public const string EmergencyAdd = "Emergency.Add";
    public const string EmergencyPrint = "Emergency.Print";
    public const string EmergencyEdit = "Emergency.Edit";
    public const string EmergencyStatus = "Emergency.UpdateStatus";
    public const string EmergencyClose = "Emergency.CloseStatus";
    public const string EmergencySettings = "Emergency.SettingsAccess";

    public const string AppointmentsView = "Appointments.View";
    public const string AppointmentsAdd = "Appointments.Add";
    public const string AppointmentsEdit = "Appointments.Edit";
    public const string AppointmentsCancel = "Appointments.Cancel";
    public const string AppointmentsPrint = "Appointments.Print";
    public const string AppointmentsReprint = "Appointments.Reprint";
    public const string AppointmentsReports = "Appointments.Reports";
    public const string PrinterSettings = "Appointments.PrinterSettings";
    public const string AppointmentsFees = "Appointments.FinancialAccess";
    public const string AppointmentsSettings = "Appointments.SettingsAccess";
    public const string FinanceViewOwnAccount = "Finance.ViewOwnAccount";
    public const string FinanceViewOwnReceipts = "Finance.ViewOwnReceipts";
    public const string FinanceCollect = "Finance.Collect";
    public const string FinanceIssueReceipts = "Finance.IssueReceipts";
    public const string FinanceReprint = "Finance.Reprint";
    public const string FinanceDiscount = "Finance.Discount";
    public const string FinanceViewCancelled = "Finance.ViewCancelled";
    public const string FinanceSubmitDeposit = "Finance.SubmitDeposit";
    public const string FinanceDailyClosing = "Finance.DailyClosing";
    public const string FinanceCounterReports = "Finance.CounterReports";
    public const string FinanceManageCounters = "Finance.ManageCounters";
    public const string FinanceManageServiceFees = "Finance.ManageServiceFees";
    public const string FinanceViewAllAccounts = "Finance.ViewAllAccounts";
    public const string FinanceDashboard = "Finance.Dashboard";
    public const string FinanceConfirmDeposits = "Finance.ConfirmDeposits";
    public const string FinanceRejectDeposits = "Finance.RejectDeposits";
    public const string FinanceCancelReceipts = "Finance.CancelReceipts";
    public const string FinanceProcessRefunds = "Finance.ProcessRefunds";
    public const string FinanceApproveRefunds = "Finance.ApproveRefunds";
    public const string FinanceManageDoctorAccounts = "Finance.ManageDoctorAccounts";
    public const string FinanceRecordDoctorPayments = "Finance.RecordDoctorPayments";
    public const string FinanceAdjustments = "Finance.Adjustments";
    public const string FinanceExport = "Finance.Export";
    public const string FinanceSettings = "Finance.Settings";

    public const string AuditView = "AuditLog.View";
    public const string AuditExport = "AuditLog.Export";
}
