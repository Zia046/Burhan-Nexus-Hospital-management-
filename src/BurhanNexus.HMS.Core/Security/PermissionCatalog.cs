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
        new(Modules_.Laboratory, "Laboratory Management", "Test booking, samples, result entry, verification and laboratory reports", "flask", true, "/Lab",
            new PermissionAction[]
            {
                new(LabActions.Dashboard, "View Laboratory Dashboard", "Laboratory work-list and counts"),
                new(LabActions.RegisterPatient, "Register Laboratory Patient", "Register new patients from the laboratory"),
                new(LabActions.Book, "Book Tests", "Create laboratory orders"),
                new(LabActions.EditOrders, "Edit Test Orders", "Add tests, change referral and notes"),
                new(LabActions.CancelOrders, "Cancel Test Orders", "Cancel booked tests with a reason"),
                new(LabActions.ViewResults, "View Patient Results", "Patient-based results page and reports on screen"),
                new(LabActions.ManageCatalogue, "Manage Test Catalogue", "Add / edit / deactivate tests, categories and sample types"),
                new(LabActions.ManageParameters, "Manage Test Parameters", "Parameters, units and result types"),
                new(LabActions.ManageRanges, "Manage Reference Ranges", "Normal / reference ranges (gender and age specific)"),
                new(LabActions.CollectSamples, "Collect Samples", "Mark samples collected / received"),
                new(LabActions.RejectSamples, "Reject Samples", "Reject a sample with a reason"),
                new(LabActions.EnterResults, "Enter Test Results", "Enter results of pending tests"),
                new(LabActions.EditResults, "Edit Unverified Results", "Change results that are waiting for verification or were sent back"),
                new(LabActions.VerifyResults, "Verify Results", "Verify or reject single test results"),
                new(LabActions.VerifyOwnResults, "Verify Own Results", "Verify results one entered oneself (only if the laboratory settings allow it)"),
                new(LabActions.ApproveReports, "Approve Reports", "Verify all completed tests of a visit together"),
                new(LabActions.PrintReports, "Print Reports", "Print / release verified reports"),
                new(LabActions.ReprintReports, "Reprint Reports", "Print further copies and previous reports"),
                new(LabActions.ExportPdf, "Export PDF", "Save reports as PDF"),
                new(LabActions.ViewPrevious, "View Previous Results", "Patient's earlier laboratory reports"),
                new(LabActions.DeleteResults, "Delete / Withdraw Results", "Withdraw a completed or verified result with a reason (revision kept)"),
                new(LabActions.Reports, "Laboratory Reports", "Laboratory statistics and exports"),
                new(LabActions.Settings, "Manage Laboratory Settings", "Laboratory branding, signatory and report settings"),
                new(LabActions.ViewFinancial, "View Laboratory Financial Information", "Laboratory fees, payment status and collection"),
                new(LabActions.ManageFees, "Manage Laboratory Fees", "Set test prices (fee history kept)"),
            }),
        new(Modules_.Radiology, "Radiology Management (X-Ray & Ultrasound)", "Radiology bookings, worklists, examinations, reporting, verification and A4 reports", "scan", true, "/Radiology",
            new PermissionAction[]
            {
                new(RadiologyActions.Dashboard, "View Radiology Dashboard", "Radiology counts and work-lists"),
                new(RadiologyActions.ViewXRay, "View X-Ray Worklist", "X-Ray queue and examinations"),
                new(RadiologyActions.ViewUltrasound, "View Ultrasound Worklist", "Ultrasound queue and examinations"),
                new(RadiologyActions.CreateBooking, "Create Booking", "Book X-Ray / Ultrasound examinations (from the doctor's handwritten advice)"),
                new(RadiologyActions.EditBooking, "Edit Booking", "Change date / time, referral, indication and priority; add examinations"),
                new(RadiologyActions.CancelBooking, "Cancel Booking", "Cancel examinations with a reason (kept in history)"),
                new(RadiologyActions.StartExam, "Start Examination", "Mark patient arrived and start the examination"),
                new(RadiologyActions.CompleteExam, "Complete Examination", "Record completion, technician and technical notes"),
                new(RadiologyActions.EnterReport, "Enter Report", "Write a new report (findings, impression …)"),
                new(RadiologyActions.EditDraft, "Edit Draft Report", "Change a saved draft or a report sent back for correction"),
                new(RadiologyActions.SubmitReport, "Submit Report", "Submit a report for verification"),
                new(RadiologyActions.VerifyReport, "Verify Report", "Approve or reject reports awaiting verification"),
                new(RadiologyActions.VerifyOwnReport, "Verify Own Report", "Verify a report one entered oneself (only if the radiology settings allow it)"),
                new(RadiologyActions.ReleaseReport, "Release Report", "Release verified reports as final"),
                new(RadiologyActions.AmendReport, "Amend Verified Report", "Re-open a verified report for correction with a reason (revision kept)"),
                new(RadiologyActions.PrintReport, "Print Report", "Print released reports"),
                new(RadiologyActions.ReprintReport, "Reprint Previous Report", "Print further copies and historical reports"),
                new(RadiologyActions.ExportPdf, "Export PDF", "Save reports and statistics as PDF"),
                new(RadiologyActions.ViewHistory, "View Patient Radiology History", "Patient's earlier examinations and reports"),
                new(RadiologyActions.ViewAttachments, "View Attachments", "Open / download examination images and documents"),
                new(RadiologyActions.ManageAttachments, "Manage Attachments", "Upload images / documents and remove them with a reason"),
                new(RadiologyActions.ManageServices, "Manage Radiology Services", "Add / edit / deactivate X-Ray and Ultrasound services"),
                new(RadiologyActions.ManageTemplates, "Manage Report Templates", "Create and edit report templates"),
                new(RadiologyActions.ViewStatistics, "View Statistics", "Radiology reports and statistics"),
                new(RadiologyActions.ViewFinancial, "View Financial Information", "Radiology fees, payment status and revenue"),
                new(RadiologyActions.ManageFees, "Manage Radiology Fees", "Set examination fees (fee history kept)"),
                new(RadiologyActions.Settings, "Manage Settings", "Report header / footer, letterhead, verification and booking rules"),
            }),
        new(Modules_.Pharmacy, "Pharmacy Management", "Medicine catalogue, stock, batches & expiry, patient issue, retail sales, department transfers and reports", "pill", true, "/Pharmacy",
            new PermissionAction[]
            {
                new(PharmacyActions.Dashboard, "View Pharmacy Dashboard", "Pharmacy dashboard, alerts and stock overview"),
                new(PharmacyActions.ViewCatalogue, "View Medicine Catalogue", "Search medicines and see their stock"),
                new(PharmacyActions.AddMedicine, "Add Medicine", "Add new medicines to the catalogue"),
                new(PharmacyActions.EditMedicine, "Edit Medicine", "Change medicine details, activate / deactivate"),
                new(PharmacyActions.ManageCategories, "Manage Medicine Categories", "Categories, manufacturers, dosage forms, strengths, pack sizes and units"),
                new(PharmacyActions.ManageSuppliers, "Manage Suppliers", "Add / edit / deactivate suppliers and stock sources"),
                new(PharmacyActions.ReceiveStock, "Receive Stock", "Record stock receiving (batch, expiry, quantity)"),
                new(PharmacyActions.ManagePurchases, "Manage Purchases", "Private: purchase prices, invoices, supplier balances and payments"),
                new(PharmacyActions.IssueToPatients, "Issue Medicine to Patients", "Issue medicines to registered OPD / Emergency patients"),
                new(PharmacyActions.ProcessWardRequests, "Process Ward Medicine Requests", "View nurses' medicine demands, approve, issue (fully / partly), reject or mark unavailable"),
                new(PharmacyActions.RetailSales, "Manage Retail Sales", "Private: walk-in medicine sales with receipt"),
                new(PharmacyActions.ApplyDiscount, "Apply Discount", "Private: discounts on sales / issues within the medicine's limit"),
                new(PharmacyActions.SelectBatch, "Select Another Batch (FEFO override)", "Issue / sell / transfer a batch other than the earliest-expiring one"),
                new(PharmacyActions.TransferStock, "Transfer Stock", "Transfer stock between the pharmacy and departments"),
                new(PharmacyActions.ViewDepartmentStock, "View Department Stock", "See Minor OT and other department stock balances"),
                new(PharmacyActions.ManageOtStock, "Manage Department Stock Locations", "Configure Minor OT and other departments that keep stock"),
                new(PharmacyActions.AdjustStock, "Adjust Stock", "Request stock corrections with a reason"),
                new(PharmacyActions.ApproveAdjustments, "Approve Stock Adjustments", "Approve or reject stock corrections and write-offs"),
                new(PharmacyActions.ProcessReturns, "Process Returns", "Customer / patient returns and supplier returns"),
                new(PharmacyActions.ViewStockReports, "View Stock Reports", "Current stock, batch-wise stock and movement history"),
                new(PharmacyActions.ViewExpiryReports, "View Expiry Reports", "Near-expiry and expired medicines"),
                new(PharmacyActions.ViewLowStockReports, "View Low-Stock Reports", "Medicines below their minimum level"),
                new(PharmacyActions.ViewPatientHistory, "View Patient Medicine History", "Medicines issued to a patient"),
                new(PharmacyActions.ViewFinancialReports, "View Pharmacy Financial Reports", "Private: prices, sales, purchases, profit and stock value"),
                new(PharmacyActions.ManagePrices, "Manage Medicine Prices", "Private: change purchase / sale prices (history kept)"),
                new(PharmacyActions.Settings, "Manage Pharmacy Settings", "Expiry warning period, slip text and pharmacy charges"),
            }),
        new(Modules_.Appointments, "Appointments", "Private hospital doctor appointments and thermal token slips", "calendar", true, "/Appointments",
            new PermissionAction[]
            {
                new(Actions.View, "View Appointments", "Doctor-wise appointment lists and details"),
                new(Actions.Add, "Create Appointment", "Book appointments (assigns the doctor token)"),
                new(Actions.Edit, "Edit Appointment", "Change time, complaint and notes"),
                new(Actions.Cancel, "Cancel Appointment", "Cancel appointments (token is never reused)"),
                new(Actions.Print, "Print Appointment Slip", "Print the first copy of the thermal slip"),
                new(AppointmentActions.CollectFee, "Collect Consultation Fee", "Record the consultation fee as paid while booking (the token slip is the payment proof)"),
                new(AppointmentActions.FreeByDoctor, "Mark as Free by Doctor", "Record a doctor's consultation-fee waiver (reason, authorising doctor)"),
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
        new(Modules_.Ipd, "IPD, Maternity & NICU", "Admissions, wards & beds, Gynae, ANC, Labour Room, delivery, newborns, NICU, nursing and medication administration", "hospital", true, "/Ipd",
            new PermissionAction[]
            {
                new(IpdActions.Dashboard, "View IPD Dashboard", "Admissions, bed occupancy and ward-wise counts"),
                new(IpdActions.ViewPatients, "View Inpatient Records", "Open admissions and their clinical record"),
                new(IpdActions.ViewAllWards, "View All Wards", "See patients of every ward (otherwise only wards of the user's departments)"),
                new(IpdActions.RegisterAdmission, "Register Admission", "Admit patients (from OPD, Emergency, Gynae, Maternity or direct)"),
                new(IpdActions.EditAdmission, "Edit Admission", "Change admission details, attendant, diagnosis; cancel a wrong admission"),
                new(IpdActions.AllocateBed, "Allocate Bed", "Allocate / change beds and set bed status"),
                new(IpdActions.TransferPatient, "Transfer Patient", "Transfer between beds, rooms, wards, departments and facilities"),
                new(IpdActions.RequestDischarge, "Prepare Discharge", "Write the discharge summary and request discharge"),
                new(IpdActions.DischargePatient, "Discharge Patient", "Authorise and complete discharge (releases the bed)"),
                new(IpdActions.ManageWards, "Manage Wards", "Create / edit / deactivate wards"),
                new(IpdActions.ManageRooms, "Manage Rooms", "Create / edit rooms"),
                new(IpdActions.ManageBeds, "Manage Beds", "Create beds, cots and incubators; bed status"),
                new(IpdActions.ProgressNotes, "Enter Progress Notes", "Daily progress notes and clinical observations"),
                new(IpdActions.ViewGynae, "View Gynae Records", "Gynae / obstetric history, assessments and pregnancy records (sensitive)"),
                new(IpdActions.EnterGynae, "Enter Gynae Assessment", "Gynae history and assessments"),
                new(IpdActions.ManageAnc, "Manage ANC Records", "Pregnancy registration, ANC visits and high-risk documentation"),
                new(IpdActions.ManageLabour, "Manage Labour Room", "Labour admission and labour status"),
                new(IpdActions.LabourObservations, "Enter Labour Observations", "Maternal / fetal observations and labour progress"),
                new(IpdActions.Partograph, "Enter Partograph", "Partograph entries"),
                new(IpdActions.EnterDelivery, "Enter Delivery Record", "Delivery records (NVD, assisted, C-section)"),
                new(IpdActions.RegisterNewborn, "Register Newborn", "Birth records and newborn Patient IDs"),
                new(IpdActions.LinkMotherBaby, "Link Mother and Baby", "Mother–baby relationship, rooming-in"),
                new(IpdActions.Postnatal, "Postnatal Care", "Postnatal assessments of the mother"),
                new(IpdActions.ManageNicu, "Manage NICU", "NICU admission, cot / incubator allocation, transfer and discharge"),
                new(IpdActions.NicuObservations, "Enter NICU Observations", "NICU vitals, feeding, intake / output, care and progress"),
                new(IpdActions.ManageNursing, "Manage Nursing Records", "Nursing assessments, vitals and nursing tasks"),
                new(IpdActions.NursingNotes, "Enter Nursing Notes", "Chronological nursing notes"),
                new(IpdActions.Handover, "Manage Shift Handover", "Write and acknowledge shift handovers"),
                new(IpdActions.MedicationOrders, "Enter Medication Orders", "Record medication orders (also from handwritten prescriptions)"),
                new(IpdActions.RecordMedication, "Record Medication Administration", "Record administered / missed / withheld doses"),
                new(IpdActions.RequestMedicines, "Request Medicines from Pharmacy", "Nursing medicine demand for admitted patients of the ward"),
                new(IpdActions.ViewInvestigations, "View Investigation Results", "Laboratory and radiology orders / results of the admission"),
                new(IpdActions.CorrectRecords, "Correct Clinical Records", "Enter corrections of clinical records (the original is kept)"),
                new(IpdActions.ViewFinancial, "View Patient Financial Details", "Charges, payments and balance of an admission"),
                new(IpdActions.ManageBilling, "Manage IPD Billing", "Post bed and service charges, final bill"),
                new(IpdActions.PrintForms, "Print Forms", "Print clinical forms and summaries"),
                new(IpdActions.ExportPdf, "Export PDF", "Save forms as PDF"),
                new(IpdActions.Settings, "Manage IPD Settings", "Clinical form fields, alert limits, charges and printing"),
            }),
        new(Modules_.Ot, "Operation Theatre", "Theatres, surgery booking, pre-op, consent, anesthesia, safety checklist, operation notes, recovery, CSSD and OT billing", "scissors", true, "/Ot",
            new PermissionAction[]
            {
                new(OtActions.Dashboard, "View OT Dashboard", "Daily surgeries, theatres, recovery and schedule"),
                new(OtActions.ManageTheatres, "Manage OT Rooms", "Theatres, operating hours, availability and OT equipment"),
                new(OtActions.ManageCatalogue, "Manage Surgery Catalogue", "Procedures, consent types and checklist items"),
                new(OtActions.CreateBooking, "Create Surgery Booking", "Book surgeries (elective, urgent, emergency) for registered patients"),
                new(OtActions.EditBooking, "Edit Surgery Booking", "Reschedule, change status and booking details"),
                new(OtActions.CancelSurgery, "Cancel Surgery", "Cancel or postpone a surgery with a reason"),
                new(OtActions.ViewPreOp, "View Pre-Operative Assessment", "Open the pre-operative assessment, investigations and checklist"),
                new(OtActions.EnterPreOp, "Enter Pre-Operative Assessment", "Record pre-operative assessment and checklist"),
                new(OtActions.ManageConsent, "Manage Surgical Consent", "Document consent and upload signed / scanned forms"),
                new(OtActions.ManageTeam, "Manage Surgical Team", "Assign surgeons, anesthetists, nurses and technicians"),
                new(OtActions.EnterAnesthesia, "Enter Anesthesia Records", "Pre-anesthesia assessment, anesthesia record, drugs and monitoring"),
                new(OtActions.EnterIntraOp, "Enter Intra-Operative Records", "Start / end surgery, intra-operative record, operation notes and specimens"),
                new(OtActions.FinalizeNotes, "Finalize & Amend Operation Notes", "Finalize operation notes and record amendments"),
                new(OtActions.CompleteChecklist, "Complete Surgical Safety Checklist", "Sign In, Time Out and Sign Out"),
                new(OtActions.AuthorizeException, "Authorize Safety Exceptions", "Complete a checklist / start an emergency surgery with mandatory items pending (reason required)"),
                new(OtActions.ManageStock, "Manage OT Stock", "View OT stock, receive transfers and returns"),
                new(OtActions.RecordConsumption, "Record Major OT Consumption", "Patient-linked medicine / consumable use from OT stock"),
                new(OtActions.ManageRecovery, "Manage Recovery Room", "PACU observations, status, transfer and handover"),
                new(OtActions.EnterPostOp, "Enter Post-Operative Records", "Post-operative records and instructions"),
                new(OtActions.ManageCssd, "Manage CSSD", "Instruments, sets, sterilization batches, release, issue and return"),
                new(OtActions.ViewBilling, "View OT Billing", "Charges, payments and balance of a surgery"),
                new(OtActions.ManageCharges, "Manage OT Charges", "Post OT charges and set the OT fee services"),
                new(OtActions.PrintDocuments, "Print Documents", "Print OT documents and forms"),
                new(OtActions.ExportReports, "Export Reports", "Export OT reports"),
                new(OtActions.ViewStatistics, "View OT Statistics", "OT reports and statistics"),
                new(OtActions.Settings, "Manage OT Settings", "Charges, printing and clinical form fields of the OT"),
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
        public const string Laboratory = "Laboratory";
        public const string Pharmacy = "Pharmacy";
        public const string Radiology = "Radiology";
        public const string Ipd = "Ipd";
        public const string Ot = "Ot";
    }

    public static class OtActions
    {
        public const string Dashboard = "Dashboard";
        public const string ManageTheatres = "ManageTheatres";
        public const string ManageCatalogue = "ManageCatalogue";
        public const string CreateBooking = "CreateBooking";
        public const string EditBooking = "EditBooking";
        public const string CancelSurgery = "CancelSurgery";
        public const string ViewPreOp = "ViewPreOp";
        public const string EnterPreOp = "EnterPreOp";
        public const string ManageConsent = "ManageConsent";
        public const string ManageTeam = "ManageTeam";
        public const string EnterAnesthesia = "EnterAnesthesia";
        public const string EnterIntraOp = "EnterIntraOp";
        public const string FinalizeNotes = "FinalizeNotes";
        public const string CompleteChecklist = "CompleteChecklist";
        public const string AuthorizeException = "AuthorizeException";
        public const string ManageStock = "ManageStock";
        public const string RecordConsumption = "RecordConsumption";
        public const string ManageRecovery = "ManageRecovery";
        public const string EnterPostOp = "EnterPostOp";
        public const string ManageCssd = "ManageCssd";
        public const string ViewBilling = "ViewBilling";
        public const string ManageCharges = "ManageCharges";
        public const string PrintDocuments = "PrintDocuments";
        public const string ExportReports = "ExportReports";
        public const string ViewStatistics = "ViewStatistics";
        public const string Settings = "Settings";
    }

    public static class IpdActions
    {
        public const string Dashboard = "Dashboard";
        public const string ViewPatients = "ViewPatients";
        public const string ViewAllWards = "ViewAllWards";
        public const string RegisterAdmission = "RegisterAdmission";
        public const string EditAdmission = "EditAdmission";
        public const string AllocateBed = "AllocateBed";
        public const string TransferPatient = "TransferPatient";
        public const string RequestDischarge = "RequestDischarge";
        public const string DischargePatient = "DischargePatient";
        public const string ManageWards = "ManageWards";
        public const string ManageRooms = "ManageRooms";
        public const string ManageBeds = "ManageBeds";
        public const string ProgressNotes = "ProgressNotes";
        public const string ViewGynae = "ViewGynae";
        public const string EnterGynae = "EnterGynae";
        public const string ManageAnc = "ManageAnc";
        public const string ManageLabour = "ManageLabour";
        public const string LabourObservations = "LabourObservations";
        public const string Partograph = "Partograph";
        public const string EnterDelivery = "EnterDelivery";
        public const string RegisterNewborn = "RegisterNewborn";
        public const string LinkMotherBaby = "LinkMotherBaby";
        public const string Postnatal = "Postnatal";
        public const string ManageNicu = "ManageNicu";
        public const string NicuObservations = "NicuObservations";
        public const string ManageNursing = "ManageNursing";
        public const string NursingNotes = "NursingNotes";
        public const string Handover = "Handover";
        public const string MedicationOrders = "MedicationOrders";
        public const string RecordMedication = "RecordMedication";
        public const string RequestMedicines = "RequestMedicines";
        public const string ViewInvestigations = "ViewInvestigations";
        public const string CorrectRecords = "CorrectRecords";
        public const string ViewFinancial = "ViewFinancial";
        public const string ManageBilling = "ManageBilling";
        public const string PrintForms = "PrintForms";
        public const string ExportPdf = "ExportPdf";
        public const string Settings = "Settings";
    }

    public static class RadiologyActions
    {
        public const string Dashboard = "Dashboard";
        public const string ViewXRay = "ViewXRayWorklist";
        public const string ViewUltrasound = "ViewUltrasoundWorklist";
        public const string CreateBooking = "CreateBooking";
        public const string EditBooking = "EditBooking";
        public const string CancelBooking = "CancelBooking";
        public const string StartExam = "StartExamination";
        public const string CompleteExam = "CompleteExamination";
        public const string EnterReport = "EnterReport";
        public const string EditDraft = "EditDraftReport";
        public const string SubmitReport = "SubmitReport";
        public const string VerifyReport = "VerifyReport";
        public const string VerifyOwnReport = "VerifyOwnReport";
        public const string ReleaseReport = "ReleaseReport";
        public const string AmendReport = "AmendReport";
        public const string PrintReport = "PrintReport";
        public const string ReprintReport = "ReprintReport";
        public const string ExportPdf = "ExportPdf";
        public const string ViewHistory = "ViewHistory";
        public const string ViewAttachments = "ViewAttachments";
        public const string ManageAttachments = "ManageAttachments";
        public const string ManageServices = "ManageServices";
        public const string ManageTemplates = "ManageTemplates";
        public const string ViewStatistics = "ViewStatistics";
        public const string ViewFinancial = "ViewFinancial";
        public const string ManageFees = "ManageFees";
        public const string Settings = "Settings";
    }

    public static class PharmacyActions
    {
        public const string Dashboard = "Dashboard";
        public const string ViewCatalogue = "ViewCatalogue";
        public const string AddMedicine = "AddMedicine";
        public const string EditMedicine = "EditMedicine";
        public const string ManageCategories = "ManageCategories";
        public const string ManageSuppliers = "ManageSuppliers";
        public const string ReceiveStock = "ReceiveStock";
        public const string ManagePurchases = "ManagePurchases";
        public const string IssueToPatients = "IssueToPatients";
        public const string ProcessWardRequests = "ProcessWardRequests";
        public const string RetailSales = "RetailSales";
        public const string ApplyDiscount = "ApplyDiscount";
        public const string SelectBatch = "SelectBatch";
        public const string TransferStock = "TransferStock";
        public const string ViewDepartmentStock = "ViewDepartmentStock";
        public const string ManageOtStock = "ManageOtStock";
        public const string AdjustStock = "AdjustStock";
        public const string ApproveAdjustments = "ApproveAdjustments";
        public const string ProcessReturns = "ProcessReturns";
        public const string ViewStockReports = "ViewStockReports";
        public const string ViewExpiryReports = "ViewExpiryReports";
        public const string ViewLowStockReports = "ViewLowStockReports";
        public const string ViewPatientHistory = "ViewPatientHistory";
        public const string ViewFinancialReports = "ViewFinancialReports";
        public const string ManagePrices = "ManagePrices";
        public const string Settings = "Settings";
    }

    public static class LabActions
    {
        public const string Dashboard = "Dashboard";
        public const string RegisterPatient = "RegisterPatient";
        public const string Book = "Book";
        public const string EditOrders = "EditOrders";
        public const string CancelOrders = "CancelOrders";
        public const string ViewResults = "ViewResults";
        public const string ManageCatalogue = "ManageCatalogue";
        public const string ManageParameters = "ManageParameters";
        public const string ManageRanges = "ManageRanges";
        public const string CollectSamples = "CollectSamples";
        public const string RejectSamples = "RejectSamples";
        public const string EnterResults = "EnterResults";
        public const string EditResults = "EditResults";
        public const string VerifyResults = "VerifyResults";
        public const string VerifyOwnResults = "VerifyOwnResults";
        public const string ApproveReports = "ApproveReports";
        public const string PrintReports = "PrintReports";
        public const string ReprintReports = "ReprintReports";
        public const string ExportPdf = "ExportPdf";
        public const string ViewPrevious = "ViewPrevious";
        public const string DeleteResults = "DeleteResults";
        public const string Reports = "Reports";
        public const string Settings = "Settings";
        public const string ViewFinancial = "ViewFinancial";
        public const string ManageFees = "ManageFees";
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
        public const string CollectFee = "CollectFee";
        public const string FreeByDoctor = "FreeByDoctor";
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
    public const string AppointmentsCollectFee = "Appointments.CollectFee";
    public const string AppointmentsFreeByDoctor = "Appointments.FreeByDoctor";
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
    public const string IpdDashboard = "Ipd.Dashboard";
    public const string IpdViewPatients = "Ipd.ViewPatients";
    public const string IpdViewAllWards = "Ipd.ViewAllWards";
    public const string IpdRegisterAdmission = "Ipd.RegisterAdmission";
    public const string IpdEditAdmission = "Ipd.EditAdmission";
    public const string IpdAllocateBed = "Ipd.AllocateBed";
    public const string IpdTransferPatient = "Ipd.TransferPatient";
    public const string IpdRequestDischarge = "Ipd.RequestDischarge";
    public const string IpdDischargePatient = "Ipd.DischargePatient";
    public const string IpdManageWards = "Ipd.ManageWards";
    public const string IpdManageRooms = "Ipd.ManageRooms";
    public const string IpdManageBeds = "Ipd.ManageBeds";
    public const string IpdProgressNotes = "Ipd.ProgressNotes";
    public const string IpdViewGynae = "Ipd.ViewGynae";
    public const string IpdEnterGynae = "Ipd.EnterGynae";
    public const string IpdManageAnc = "Ipd.ManageAnc";
    public const string IpdManageLabour = "Ipd.ManageLabour";
    public const string IpdLabourObservations = "Ipd.LabourObservations";
    public const string IpdPartograph = "Ipd.Partograph";
    public const string IpdEnterDelivery = "Ipd.EnterDelivery";
    public const string IpdRegisterNewborn = "Ipd.RegisterNewborn";
    public const string IpdLinkMotherBaby = "Ipd.LinkMotherBaby";
    public const string IpdPostnatal = "Ipd.Postnatal";
    public const string IpdManageNicu = "Ipd.ManageNicu";
    public const string IpdNicuObservations = "Ipd.NicuObservations";
    public const string IpdManageNursing = "Ipd.ManageNursing";
    public const string IpdNursingNotes = "Ipd.NursingNotes";
    public const string IpdHandover = "Ipd.Handover";
    public const string IpdMedicationOrders = "Ipd.MedicationOrders";
    public const string IpdRecordMedication = "Ipd.RecordMedication";
    public const string IpdRequestMedicines = "Ipd.RequestMedicines";
    public const string IpdViewInvestigations = "Ipd.ViewInvestigations";
    public const string IpdCorrectRecords = "Ipd.CorrectRecords";
    public const string IpdViewFinancial = "Ipd.ViewFinancial";
    public const string IpdManageBilling = "Ipd.ManageBilling";
    public const string IpdPrintForms = "Ipd.PrintForms";
    public const string IpdExportPdf = "Ipd.ExportPdf";
    public const string IpdSettings = "Ipd.Settings";
    public const string OtDashboard = "Ot.Dashboard";
    public const string OtManageTheatres = "Ot.ManageTheatres";
    public const string OtManageCatalogue = "Ot.ManageCatalogue";
    public const string OtCreateBooking = "Ot.CreateBooking";
    public const string OtEditBooking = "Ot.EditBooking";
    public const string OtCancelSurgery = "Ot.CancelSurgery";
    public const string OtViewPreOp = "Ot.ViewPreOp";
    public const string OtEnterPreOp = "Ot.EnterPreOp";
    public const string OtManageConsent = "Ot.ManageConsent";
    public const string OtManageTeam = "Ot.ManageTeam";
    public const string OtEnterAnesthesia = "Ot.EnterAnesthesia";
    public const string OtEnterIntraOp = "Ot.EnterIntraOp";
    public const string OtFinalizeNotes = "Ot.FinalizeNotes";
    public const string OtCompleteChecklist = "Ot.CompleteChecklist";
    public const string OtAuthorizeException = "Ot.AuthorizeException";
    public const string OtManageStock = "Ot.ManageStock";
    public const string OtRecordConsumption = "Ot.RecordConsumption";
    public const string OtManageRecovery = "Ot.ManageRecovery";
    public const string OtEnterPostOp = "Ot.EnterPostOp";
    public const string OtManageCssd = "Ot.ManageCssd";
    public const string OtViewBilling = "Ot.ViewBilling";
    public const string OtManageCharges = "Ot.ManageCharges";
    public const string OtPrintDocuments = "Ot.PrintDocuments";
    public const string OtExportReports = "Ot.ExportReports";
    public const string OtViewStatistics = "Ot.ViewStatistics";
    public const string OtSettings = "Ot.Settings";
    public const string RadDashboard = "Radiology.Dashboard";
    public const string RadViewXRay = "Radiology.ViewXRayWorklist";
    public const string RadViewUltrasound = "Radiology.ViewUltrasoundWorklist";
    public const string RadCreateBooking = "Radiology.CreateBooking";
    public const string RadEditBooking = "Radiology.EditBooking";
    public const string RadCancelBooking = "Radiology.CancelBooking";
    public const string RadStartExam = "Radiology.StartExamination";
    public const string RadCompleteExam = "Radiology.CompleteExamination";
    public const string RadEnterReport = "Radiology.EnterReport";
    public const string RadEditDraft = "Radiology.EditDraftReport";
    public const string RadSubmitReport = "Radiology.SubmitReport";
    public const string RadVerifyReport = "Radiology.VerifyReport";
    public const string RadVerifyOwnReport = "Radiology.VerifyOwnReport";
    public const string RadReleaseReport = "Radiology.ReleaseReport";
    public const string RadAmendReport = "Radiology.AmendReport";
    public const string RadPrintReport = "Radiology.PrintReport";
    public const string RadReprintReport = "Radiology.ReprintReport";
    public const string RadExportPdf = "Radiology.ExportPdf";
    public const string RadViewHistory = "Radiology.ViewHistory";
    public const string RadViewAttachments = "Radiology.ViewAttachments";
    public const string RadManageAttachments = "Radiology.ManageAttachments";
    public const string RadManageServices = "Radiology.ManageServices";
    public const string RadManageTemplates = "Radiology.ManageTemplates";
    public const string RadViewStatistics = "Radiology.ViewStatistics";
    public const string RadViewFinancial = "Radiology.ViewFinancial";
    public const string RadManageFees = "Radiology.ManageFees";
    public const string RadSettings = "Radiology.Settings";

    public const string LabDashboard = "Laboratory.Dashboard";
    public const string LabRegisterPatient = "Laboratory.RegisterPatient";
    public const string LabBook = "Laboratory.Book";
    public const string LabEditOrders = "Laboratory.EditOrders";
    public const string LabCancelOrders = "Laboratory.CancelOrders";
    public const string LabViewResults = "Laboratory.ViewResults";
    public const string LabManageCatalogue = "Laboratory.ManageCatalogue";
    public const string LabManageParameters = "Laboratory.ManageParameters";
    public const string LabManageRanges = "Laboratory.ManageRanges";
    public const string LabCollectSamples = "Laboratory.CollectSamples";
    public const string LabRejectSamples = "Laboratory.RejectSamples";
    public const string LabEnterResults = "Laboratory.EnterResults";
    public const string LabEditResults = "Laboratory.EditResults";
    public const string LabVerifyResults = "Laboratory.VerifyResults";
    public const string LabVerifyOwnResults = "Laboratory.VerifyOwnResults";
    public const string LabApproveReports = "Laboratory.ApproveReports";
    public const string LabPrintReports = "Laboratory.PrintReports";
    public const string LabReprintReports = "Laboratory.ReprintReports";
    public const string LabExportPdf = "Laboratory.ExportPdf";
    public const string LabViewPrevious = "Laboratory.ViewPrevious";
    public const string LabDeleteResults = "Laboratory.DeleteResults";
    public const string LabReports = "Laboratory.Reports";
    public const string LabSettings = "Laboratory.Settings";
    public const string LabViewFinancial = "Laboratory.ViewFinancial";
    public const string LabManageFees = "Laboratory.ManageFees";
    public const string PharmDashboard = "Pharmacy.Dashboard";
    public const string PharmViewCatalogue = "Pharmacy.ViewCatalogue";
    public const string PharmAddMedicine = "Pharmacy.AddMedicine";
    public const string PharmEditMedicine = "Pharmacy.EditMedicine";
    public const string PharmManageCategories = "Pharmacy.ManageCategories";
    public const string PharmManageSuppliers = "Pharmacy.ManageSuppliers";
    public const string PharmReceiveStock = "Pharmacy.ReceiveStock";
    public const string PharmManagePurchases = "Pharmacy.ManagePurchases";
    public const string PharmIssueToPatients = "Pharmacy.IssueToPatients";
    public const string PharmProcessWardRequests = "Pharmacy.ProcessWardRequests";
    public const string PharmRetailSales = "Pharmacy.RetailSales";
    public const string PharmApplyDiscount = "Pharmacy.ApplyDiscount";
    public const string PharmSelectBatch = "Pharmacy.SelectBatch";
    public const string PharmTransferStock = "Pharmacy.TransferStock";
    public const string PharmViewDepartmentStock = "Pharmacy.ViewDepartmentStock";
    public const string PharmManageOtStock = "Pharmacy.ManageOtStock";
    public const string PharmAdjustStock = "Pharmacy.AdjustStock";
    public const string PharmApproveAdjustments = "Pharmacy.ApproveAdjustments";
    public const string PharmProcessReturns = "Pharmacy.ProcessReturns";
    public const string PharmViewStockReports = "Pharmacy.ViewStockReports";
    public const string PharmViewExpiryReports = "Pharmacy.ViewExpiryReports";
    public const string PharmViewLowStockReports = "Pharmacy.ViewLowStockReports";
    public const string PharmViewPatientHistory = "Pharmacy.ViewPatientHistory";
    public const string PharmViewFinancialReports = "Pharmacy.ViewFinancialReports";
    public const string PharmManagePrices = "Pharmacy.ManagePrices";
    public const string PharmSettings = "Pharmacy.Settings";

    public const string AuditView = "AuditLog.View";
    public const string AuditExport = "AuditLog.Export";
}
