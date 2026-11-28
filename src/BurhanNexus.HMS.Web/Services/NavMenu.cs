using BurhanNexus.HMS.Core.Security;

namespace BurhanNexus.HMS.Web.Services;

/// <summary>One menu link. <see cref="Nav"/> is the page's ViewData["Nav"] key (used to highlight the active page).</summary>
public sealed record NavItem(string Nav, string Href, string Icon, string Label, bool EmitDataNav = true, bool PrivateOnly = false);

/// <summary>A menu category (the existing sidebar sections). <see cref="Visible"/> keeps the section's original visibility rule.</summary>
public sealed record NavSection(string Key, string Title, string Icon, bool Visible, IReadOnlyList<NavItem> Items);

/// <summary>
/// The application menu as data – the same sections, links, permissions and conditions as the original sidebar.
/// Ordinary users see it unchanged in the sidebar. For the system administrator the sidebar keeps only <see cref="AdminSidebar"/>
/// and every other link is grouped by its existing category in the top taskbar (no page appears twice).
/// Links are only shown when the permission allows; the pages still check permissions on the server.
/// </summary>
public static class NavMenu
{
    /// <summary>The 12 pages kept in the administrator's sidebar (by nav key). Update 18: "Finance" (dashboard) instead of
    /// "My Financial Account", and the Disease Master.</summary>
    public static readonly IReadOnlySet<string> AdminSidebar = new HashSet<string>
    {
        "appointment-book", "appointments", "opd", "emergency", "finance-account", "finance-services",
        "users", "departments", "audit", "settings", "backup", "diseases",
    };

    public static IReadOnlyList<NavSection> Build(IReadOnlySet<string> perms, bool isPrivate) => Build(perms, isPrivate, labSimple: false);

    /// <param name="labSimple">Update 10: laboratory simple workflow – no sample collection or verification pages in the menu.</param>
    public static IReadOnlyList<NavSection> Build(IReadOnlySet<string> perms, bool isPrivate, bool labSimple)
    {
        bool Has(string p) => perms.Contains(p);
        bool Any(params string[] keys) => keys.Any(perms.Contains);
        var sections = new List<NavSection>();
        void Add(string key, string title, string icon, bool visible, params (bool Show, NavItem Item)[] items) =>
            sections.Add(new NavSection(key, title, icon, visible, items.Where(i => i.Show).Select(i => i.Item).ToList()));

        Add("appointments", "Appointments", "calendar", Any(Perm.AppointmentsView, Perm.AppointmentsAdd, Perm.AppointmentsReports),
            (Has(Perm.AppointmentsAdd), new("appointment-book", Has(Perm.PatientsAdd) ? "/Patients/Register?next=appointment" : "/Appointments/Book", "plus", "New Appointment")), // update 17: opens registration directly
            (Has(Perm.AppointmentsView), new("appointments", "/Appointments", "calendar", "Doctor-wise Lists")),
            (Has(Perm.AppointmentsReports), new("appointment-reports", "/Appointments/Reports", "chart", "Appointment Reports", EmitDataNav: false)));

        Add("patients", "Patients & Visits", "users-round", Any(Perm.PatientsAdd, Perm.PatientsSearch, Perm.OpdView, Perm.OpdAdd, Perm.EmergencyView, Perm.EmergencyAdd),
            (Has(Perm.PatientsAdd), new("patient-register", "/Patients/Register", "plus", "Register Patient", EmitDataNav: false)),
            (Has(Perm.PatientsSearch), new("patients", "/Patients", "search", "Patient Search", EmitDataNav: false)),
            (Has(Perm.OpdView), new("opd", "/Opd", "stethoscope", "OPD", EmitDataNav: false)),
            (!Has(Perm.OpdView) && Has(Perm.OpdAdd), new("opd", "/Opd/Register", "stethoscope", "OPD Registration", EmitDataNav: false)),
            (Has(Perm.EmergencyView), new("emergency", "/Emergency", "siren", "Emergency", EmitDataNav: false)),
            (!Has(Perm.EmergencyView) && Has(Perm.EmergencyAdd), new("emergency", "/Emergency/Register", "siren", "Emergency Registration", EmitDataNav: false)));

        Add("lab", "Laboratory", "flask", Any(Perm.LabDashboard, Perm.LabBook, Perm.LabCollectSamples, Perm.LabEnterResults, Perm.LabVerifyResults, Perm.LabApproveReports, Perm.LabViewResults, Perm.LabReports, Perm.LabManageCatalogue, Perm.LabSettings),
            (Has(Perm.LabDashboard), new("lab-dashboard", "/Lab", "flask", "Lab Dashboard")),
            (Has(Perm.LabBook), new("lab-book", "/Lab/Book", "plus", "New Lab Visit")),
            (Has(Perm.LabCollectSamples) && !labSimple, new("lab-samples", "/Lab/Samples", "scan", "Sample Collection")),
            (Has(Perm.LabEnterResults), new("lab-pending", "/Lab/Pending", "edit", labSimple ? "Pending Lab Results" : "Pending Tests")),
            (Any(Perm.LabVerifyResults, Perm.LabApproveReports) && !labSimple, new("lab-verify", "/Lab/Verify", "check-circle", "Verification")),
            (Has(Perm.LabViewResults), new("lab-results", "/Lab/Results", "file", "Lab Results")),
            (Has(Perm.LabReports), new("lab-reports", "/Lab/Reports", "chart", "Lab Reports")),
            (Has(Perm.LabManageCatalogue), new("lab-catalogue", "/Lab/Catalogue", "sliders", "Test Catalogue")),
            (Has(Perm.LabSettings), new("lab-settings", "/Lab/Settings", "settings", "Lab Settings")));

        Add("ipd", "IPD & Maternity", "hospital", Any(Perm.IpdDashboard, Perm.IpdViewPatients, Perm.IpdRegisterAdmission, Perm.IpdAllocateBed, Perm.IpdViewGynae, Perm.IpdManageLabour, Perm.IpdLabourObservations,
                Perm.IpdManageNicu, Perm.IpdNicuObservations, Perm.IpdManageNursing, Perm.IpdNursingNotes, Perm.IpdRecordMedication, Perm.IpdHandover, Perm.IpdManageWards, Perm.IpdManageRooms, Perm.IpdManageBeds, Perm.IpdSettings),
            (Has(Perm.IpdDashboard), new("ipd-dashboard", "/Ipd", "hospital", "IPD Dashboard")),
            (Has(Perm.IpdRegisterAdmission), new("ipd-admit", "/Ipd/Admit", "plus", "Admit Patient")),
            (Has(Perm.IpdViewPatients), new("ipd-patients", "/Ipd/Admissions", "users", "Inpatients")),
            (Any(Perm.IpdViewPatients, Perm.IpdAllocateBed, Perm.IpdManageBeds), new("ipd-beds", "/Ipd/Beds", "hospital", "Bed Map")),
            (Has(Perm.IpdViewGynae), new("ipd-gynae", "/Ipd/Gynae", "stethoscope", "Gynae & ANC")),
            (Any(Perm.IpdManageLabour, Perm.IpdLabourObservations), new("ipd-labour", "/Ipd/Labour", "activity", "Labour Room")),
            (Any(Perm.IpdManageNicu, Perm.IpdNicuObservations), new("ipd-nicu", "/Ipd/Nicu", "users-round", "NICU")),
            (Any(Perm.IpdManageNursing, Perm.IpdNursingNotes, Perm.IpdRecordMedication, Perm.IpdHandover), new("ipd-nursing", "/Ipd/Nursing", "check-circle", "Nursing")),
            (Any(Perm.IpdManageWards, Perm.IpdManageRooms, Perm.IpdManageBeds), new("ipd-wards", "/Ipd/Wards", "sliders", "Wards & Beds")),
            (Has(Perm.IpdManagePackages), new("ipd-packages", "/Ipd/Packages", "database", "Packages", EmitDataNav: false)),
            (Has(Perm.IpdSettings), new("ipd-settings", "/Ipd/Settings", "settings", "IPD Settings")));

        Add("ot", "Operation Theatre", "scissors", Any(Perm.OtDashboard, Perm.OtCreateBooking, Perm.OtManageRecovery, Perm.OtManageCssd, Perm.OtViewStatistics,
                Perm.OtManageTheatres, Perm.OtManageCatalogue, Perm.OtSettings),
            (Has(Perm.OtDashboard), new("ot-dashboard", "/Ot", "scissors", "OT Dashboard")),
            (Has(Perm.OtCreateBooking), new("ot-book", "/Ot/Book", "plus", "Book Surgery")),
            (Has(Perm.OtDashboard), new("ot-schedule", "/Ot/Schedule", "calendar", "OT Schedule")),
            (Has(Perm.OtManageRecovery), new("ot-recovery", "/Ot/Recovery", "activity", "Recovery / PACU")),
            (Has(Perm.OtManageCssd), new("ot-cssd", "/Ot/Cssd", "refresh", "CSSD")),
            (Has(Perm.OtViewStatistics), new("ot-reports", "/Ot/Reports", "chart", "OT Reports")),
            (Has(Perm.OtManageTheatres), new("ot-theatres", "/Ot/Theatres", "hospital", "OT Rooms & Equipment")),
            (Has(Perm.OtManageCatalogue), new("ot-procedures", "/Ot/Procedures", "sliders", "Surgery Catalogue")),
            (Any(Perm.OtSettings, Perm.OtManageCatalogue), new("ot-setup", "/Ot/Setup", "settings", "OT Settings")));

        Add("radiology", "Radiology", "scan", Any(Perm.RadDashboard, Perm.RadCreateBooking, Perm.RadViewXRay, Perm.RadViewUltrasound, Perm.RadViewEcg, Perm.RadEnterReport, Perm.RadEditDraft, Perm.RadVerifyReport,
                Perm.RadViewHistory, Perm.RadViewStatistics, Perm.RadManageServices, Perm.RadManageTemplates, Perm.RadSettings),
            (Has(Perm.RadDashboard), new("rad-dashboard", "/Radiology", "scan", "Radiology Dashboard")),
            (Has(Perm.RadCreateBooking), new("rad-book", "/Radiology/Book", "plus", "New Booking")),
            (Has(Perm.RadViewXRay), new("rad-xray", "/Radiology/Worklist/XRay", "scan", "X-Ray Worklist")),
            (Has(Perm.RadViewUltrasound), new("rad-ultrasound", "/Radiology/Worklist/Ultrasound", "wave", "Ultrasound Worklist")),
            (Has(Perm.RadViewEcg), new("rad-ecg", "/Radiology/Worklist/Ecg", "activity", "ECG Worklist")),
            (Any(Perm.RadCreateBooking, Perm.RadEditBooking, Perm.RadViewXRay, Perm.RadViewUltrasound, Perm.RadViewEcg), new("rad-bookings", "/Radiology/Bookings", "calendar", "Bookings")),
            (Any(Perm.RadEnterReport, Perm.RadEditDraft), new("rad-reporting", "/Radiology/Reporting", "edit", "Reporting")),
            (Has(Perm.RadVerifyReport), new("rad-verify", "/Radiology/Verify", "check-circle", "Report Verification")),
            (Has(Perm.RadViewHistory), new("rad-history", "/Radiology/History", "history", "Patient History")),
            (Has(Perm.RadViewStatistics), new("rad-reports", "/Radiology/Reports", "chart", "Radiology Reports")),
            (Any(Perm.RadManageServices, Perm.RadManageTemplates), new("rad-services", "/Radiology/Services", "sliders", "Services & Templates")),
            (Has(Perm.RadSettings), new("rad-settings", "/Radiology/Settings", "settings", "Radiology Settings")));

        Add("pharmacy", "Pharmacy", "pill", Any(Perm.PharmDashboard, Perm.PharmViewCatalogue, Perm.PharmIssueToPatients, Perm.PharmRetailSales, Perm.PharmReceiveStock, Perm.PharmTransferStock,
                Perm.PharmViewStockReports, Perm.PharmViewDepartmentStock, Perm.PharmAdjustStock, Perm.PharmApproveAdjustments, Perm.PharmManageSuppliers, Perm.PharmSettings, Perm.PharmApproveFreeIssue),
            (Has(Perm.PharmDashboard), new("pharm-dashboard", "/Pharmacy", "pill", "Pharmacy Dashboard")),
            (Has(Perm.PharmIssueToPatients), new("pharm-issue", "/Pharmacy/Issue", "users-round", "Patient Issue")),
            (Has(Perm.PharmProcessWardRequests), new("pharm-ward", "/Pharmacy/WardRequests", "file", "Ward Requests")),
            (Has(Perm.PharmApproveFreeIssue), new("pharm-approvals", "/Pharmacy/Approvals", "check", "Free Issue Approvals", EmitDataNav: false)),
            (Has(Perm.PharmRetailSales) && isPrivate, new("pharm-sale", "/Pharmacy/Sale", "wallet", "Retail Sale", PrivateOnly: true)),
            (Has(Perm.PharmReceiveStock), new("pharm-receive", "/Pharmacy/Receive", "download", "Supplier Invoice")),
            (Has(Perm.PharmTransferStock), new("pharm-transfer", "/Pharmacy/Transfer", "refresh", "Stock Transfer")),
            (Any(Perm.PharmViewStockReports, Perm.PharmViewDepartmentStock), new("pharm-stock", "/Pharmacy/Stock", "database", "Stock & Batches")),
            (Has(Perm.PharmViewCatalogue), new("pharm-medicines", "/Pharmacy/Medicines", "sliders", "Medicines")));

        Add("finance", "Finance", "wallet", Any(Perm.FinanceViewOwnAccount, Perm.FinanceCollect, Perm.FinanceViewOwnReceipts, Perm.FinanceDashboard, Perm.FinanceConfirmDeposits,
                Perm.FinanceManageDoctorAccounts, Perm.FinanceViewOwnDoctorAccount, Perm.FinanceCounterReports, Perm.FinanceManageServiceFees),
            (Has(Perm.FinanceCollect), new("finance-collect", "/Finance/Collect", "plus", "Collect Payment")),
            // Update 18: with the hospital dashboard the item is "Finance" (the dashboard with every finance option); own account stays reachable.
            (Has(Perm.FinanceDashboard), new("finance-account", "/Finance/Dashboard", "wallet", "Finance")),
            (Has(Perm.FinanceViewOwnAccount) && Has(Perm.FinanceDashboard), new("finance-own", "/Finance", "wallet", "My Financial Account", EmitDataNav: false)),
            (Has(Perm.FinanceViewOwnAccount) && !Has(Perm.FinanceDashboard), new("finance-account", "/Finance", "wallet", "My Financial Account")),
            (Has(Perm.FinanceViewOwnReceipts), new("finance-receipts", "/Finance/Receipts", "file", "Receipts")),
            (Has(Perm.FinanceDashboard), new("finance-revenue", "/Finance/Revenue", "chart", "Revenue & Outstanding", EmitDataNav: false)),
            (Has(Perm.FinanceConfirmDeposits), new("finance-deposits", "/Finance/Deposits", "download", "Cash Deposits")),
            (Has(Perm.FinanceConfirmDeposits) && Has(Perm.FinanceViewAllAccounts), new("finance-pay-hospital", "/Finance/PayToHospital", "check-circle", "Pay to Hospital", EmitDataNav: false)),
            (Has(Perm.FinanceManageDoctorAccounts) || Has(Perm.FinanceViewOwnDoctorAccount), new("finance-doctors", "/Finance/Doctors", "stethoscope", Has(Perm.FinanceManageDoctorAccounts) ? "Doctor Accounts" : "My Doctor Account")),
            (Has(Perm.FinanceManageServiceFees), new("finance-services", "/Finance/Services", "sliders", "Counters per Service")));

        Add("fees", "Fees & Services", "sliders", Any(Perm.SvcViewFees, Perm.SvcManageCatalogue, Perm.SvcManageServices, Perm.SvcManageFees, Perm.SvcManageCategories, Perm.SvcReports),
            (Any(Perm.SvcViewFees, Perm.SvcManageCatalogue, Perm.SvcManageServices, Perm.SvcManageFees), new("fees-catalogue", "/Fees", "sliders", "Service Catalogue")),
            (Has(Perm.SvcManageCategories), new("fees-categories", "/Fees/Categories", "database", "Categories")),
            (Has(Perm.SvcReports), new("fees-reports", "/Fees/Reports", "chart", "Service-wise Report")));

        // Update 18: the Disease Master and disease analytics.
        Add("diseases", "Diseases", "activity", Any(Perm.DiseasesView, Perm.DiseasesAdd, Perm.DiseasesEdit, Perm.DiseasesDelete, Perm.DiseasesAnalytics),
            (Any(Perm.DiseasesView, Perm.DiseasesAdd, Perm.DiseasesEdit, Perm.DiseasesDelete), new("diseases", "/Diseases", "activity", "Diseases", EmitDataNav: false)),
            (Has(Perm.DiseasesAnalytics), new("disease-analytics", "/Diseases/Analytics", "chart", "Disease Analytics", EmitDataNav: false)));

        Add("admin", "Administration", "building", Any(Perm.UsersView, Perm.DepartmentsManage, Perm.RolesManage, Perm.LoginHistoryView, Perm.AuditView),
            (Has(Perm.UsersView), new("users", "/Users", "users", "Users", EmitDataNav: false)),
            (Has(Perm.DepartmentsManage), new("departments", "/Departments", "building", "Departments", EmitDataNav: false)),
            (Has(Perm.RolesManage), new("roles", "/Roles", "badge", "Responsibilities", EmitDataNav: false)),
            (Has(Perm.LoginHistoryView), new("logins", "/Users/LoginHistory", "history", "Login History", EmitDataNav: false)),
            (Has(Perm.AuditView), new("audit", "/Audit", "shield", "Audit Log", EmitDataNav: false)));

        Add("system", "System", "settings", Any(Perm.SettingsView, Perm.LicenseView, Perm.BackupView, Perm.PatientsSettings),
            (Has(Perm.PatientsSettings) && !Has(Perm.SettingsView), new("registration-settings", "/Settings/Registration", "badge", "Patient ID & Slips", EmitDataNav: false)),
            (Has(Perm.SettingsView), new("settings", "/Settings", "settings", "Hospital Settings", EmitDataNav: false)),
            (Has(Perm.LicenseView), new("license", "/Settings/License", "key", "License", EmitDataNav: false)),
            (Has(Perm.BackupView), new("backup", "/Backup", "database", "Backup & Restore", EmitDataNav: false)));

        return sections;
    }
}
