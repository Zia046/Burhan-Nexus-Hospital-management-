using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BurhanNexus.HMS.Infrastructure.Persistence;

public class HmsDbContext : DbContext
{
    public HmsDbContext(DbContextOptions<HmsDbContext> options) : base(options) { }

    public DbSet<HospitalSettings> HospitalSettings => Set<HospitalSettings>();
    public DbSet<HospitalConfiguration> HospitalConfigurations => Set<HospitalConfiguration>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<UserDepartment> UserDepartments => Set<UserDepartment>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<LicenseInformation> LicenseInformation => Set<LicenseInformation>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<BackupHistory> BackupHistory => Set<BackupHistory>();

    // Phase 2
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientIdentifier> PatientIdentifiers => Set<PatientIdentifier>();
    public DbSet<PatientEncounter> PatientEncounters => Set<PatientEncounter>();
    public DbSet<OpdVisit> OpdVisits => Set<OpdVisit>();
    public DbSet<EmergencyVisit> EmergencyVisits => Set<EmergencyVisit>();
    public DbSet<EmergencyStatusHistory> EmergencyStatusHistory => Set<EmergencyStatusHistory>();
    public DbSet<EmergencyCategory> EmergencyCategories => Set<EmergencyCategory>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<PrintedDocument> PrintedDocuments => Set<PrintedDocument>();
    public DbSet<RegistrationSettings> RegistrationSettings => Set<RegistrationSettings>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<PrinterProfile> PrinterProfiles => Set<PrinterProfile>();
    // Phase 3 – finance
    public DbSet<Counter> Counters => Set<Counter>();
    public DbSet<CounterAssignment> CounterAssignments => Set<CounterAssignment>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<ServiceFee> ServiceFees => Set<ServiceFee>();
    public DbSet<ServiceCounter> ServiceCounters => Set<ServiceCounter>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
    public DbSet<ReceiptPrint> ReceiptPrints => Set<ReceiptPrint>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<CashDeposit> CashDeposits => Set<CashDeposit>();
    public DbSet<CashierLedgerEntry> CashierLedger => Set<CashierLedgerEntry>();
    public DbSet<DoctorLedgerEntry> DoctorLedger => Set<DoctorLedgerEntry>();
    public DbSet<DoctorSettlement> DoctorSettlements => Set<DoctorSettlement>();
    public DbSet<FinancialAdjustment> FinancialAdjustments => Set<FinancialAdjustment>();
    public DbSet<DailyCashClosing> DailyCashClosings => Set<DailyCashClosing>();
    public DbSet<FinanceSettings> FinanceSettings => Set<FinanceSettings>();
    // Phase 4 – laboratory
    public DbSet<LabSampleType> LabSampleTypes => Set<LabSampleType>();
    public DbSet<LabCategory> LabCategories => Set<LabCategory>();
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<LabTestParameter> LabTestParameters => Set<LabTestParameter>();
    public DbSet<LabReferenceRange> LabReferenceRanges => Set<LabReferenceRange>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabOrderItem> LabOrderItems => Set<LabOrderItem>();
    public DbSet<LabResultValue> LabResultValues => Set<LabResultValue>();
    public DbSet<LabSample> LabSamples => Set<LabSample>();
    public DbSet<LabItemEvent> LabItemEvents => Set<LabItemEvent>();
    public DbSet<LabResultRevision> LabResultRevisions => Set<LabResultRevision>();
    public DbSet<LabReportPrint> LabReportPrints => Set<LabReportPrint>();
    public DbSet<LabSettings> LabSettings => Set<LabSettings>();
    public DbSet<LabInventoryItem> LabInventoryItems => Set<LabInventoryItem>();

    // Phase 5 – pharmacy
    public DbSet<PharmacyLookup> PharmacyLookups => Set<PharmacyLookup>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicinePriceChange> MedicinePriceChanges => Set<MedicinePriceChange>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<MedicineBatch> MedicineBatches => Set<MedicineBatch>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptItem> StockReceiptItems => Set<StockReceiptItem>();
    public DbSet<PatientMedicineIssue> PatientMedicineIssues => Set<PatientMedicineIssue>();
    public DbSet<PatientMedicineIssueItem> PatientMedicineIssueItems => Set<PatientMedicineIssueItem>();
    public DbSet<PharmacySale> PharmacySales => Set<PharmacySale>();
    public DbSet<PharmacySaleItem> PharmacySaleItems => Set<PharmacySaleItem>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<MedicineReturn> MedicineReturns => Set<MedicineReturn>();
    public DbSet<MedicineReturnItem> MedicineReturnItems => Set<MedicineReturnItem>();
    public DbSet<SupplierReturn> SupplierReturns => Set<SupplierReturn>();
    public DbSet<SupplierReturnItem> SupplierReturnItems => Set<SupplierReturnItem>();
    public DbSet<SupplierLedgerEntry> SupplierLedger => Set<SupplierLedgerEntry>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<PharmacySettings> PharmacySettings => Set<PharmacySettings>();

    // Phase 6 – radiology
    public DbSet<RadiologyService> RadiologyServices => Set<RadiologyService>();
    public DbSet<RadiologyTemplate> RadiologyTemplates => Set<RadiologyTemplate>();
    public DbSet<RadiologyOrder> RadiologyOrders => Set<RadiologyOrder>();
    public DbSet<RadiologyExam> RadiologyExams => Set<RadiologyExam>();
    public DbSet<RadiologyExamEvent> RadiologyExamEvents => Set<RadiologyExamEvent>();
    public DbSet<RadiologyReportRevision> RadiologyReportRevisions => Set<RadiologyReportRevision>();
    public DbSet<RadiologyReportPrint> RadiologyReportPrints => Set<RadiologyReportPrint>();
    public DbSet<RadiologyAttachment> RadiologyAttachments => Set<RadiologyAttachment>();
    public DbSet<RadiologySettings> RadiologySettings => Set<RadiologySettings>();

    // Phase 7 – IPD, maternity, NICU, nursing
    public DbSet<IpdWard> IpdWards => Set<IpdWard>();
    public DbSet<IpdRoom> IpdRooms => Set<IpdRoom>();
    public DbSet<IpdBed> IpdBeds => Set<IpdBed>();
    public DbSet<IpdBedStatusChange> IpdBedStatusChanges => Set<IpdBedStatusChange>();
    public DbSet<IpdAdmission> IpdAdmissions => Set<IpdAdmission>();
    public DbSet<IpdBedAllocation> IpdBedAllocations => Set<IpdBedAllocation>();
    public DbSet<IpdTransfer> IpdTransfers => Set<IpdTransfer>();
    public DbSet<IpdEvent> IpdEvents => Set<IpdEvent>();
    public DbSet<ClinicalFormField> ClinicalFormFields => Set<ClinicalFormField>();
    public DbSet<ClinicalRecord> ClinicalRecords => Set<ClinicalRecord>();
    public DbSet<Pregnancy> Pregnancies => Set<Pregnancy>();
    public DbSet<LabourRecord> LabourRecords => Set<LabourRecord>();
    public DbSet<DeliveryRecord> DeliveryRecords => Set<DeliveryRecord>();
    public DbSet<NewbornRecord> NewbornRecords => Set<NewbornRecord>();
    public DbSet<NicuDetails> NicuDetails => Set<NicuDetails>();
    public DbSet<NursingTask> NursingTasks => Set<NursingTask>();
    public DbSet<ShiftHandover> ShiftHandovers => Set<ShiftHandover>();
    public DbSet<MedicationOrder> MedicationOrders => Set<MedicationOrder>();
    public DbSet<MedicationDose> MedicationDoses => Set<MedicationDose>();
    public DbSet<IpdBedCharge> IpdBedCharges => Set<IpdBedCharge>();
    public DbSet<IpdSettings> IpdSettings => Set<IpdSettings>();
    // Phase 8 – operation theatre
    public DbSet<OtTheatre> OtTheatres => Set<OtTheatre>();
    public DbSet<OtProcedure> OtProcedures => Set<OtProcedure>();
    public DbSet<Surgery> Surgeries => Set<Surgery>();
    public DbSet<SurgeryTeamMember> SurgeryTeamMembers => Set<SurgeryTeamMember>();
    public DbSet<SurgeryEvent> SurgeryEvents => Set<SurgeryEvent>();
    public DbSet<OtConsentType> OtConsentTypes => Set<OtConsentType>();
    public DbSet<SurgeryConsent> SurgeryConsents => Set<SurgeryConsent>();
    public DbSet<OtDocument> OtDocuments => Set<OtDocument>();
    public DbSet<OtChecklistItem> OtChecklistItems => Set<OtChecklistItem>();
    public DbSet<SurgeryChecklistEntry> SurgeryChecklistEntries => Set<SurgeryChecklistEntry>();
    public DbSet<SurgeryChecklistStage> SurgeryChecklistStages => Set<SurgeryChecklistStage>();
    public DbSet<OtDrugEntry> OtDrugEntries => Set<OtDrugEntry>();
    public DbSet<OperativeNote> OperativeNotes => Set<OperativeNote>();
    public DbSet<SurgerySpecimen> SurgerySpecimens => Set<SurgerySpecimen>();
    public DbSet<OtConsumption> OtConsumptions => Set<OtConsumption>();
    public DbSet<OtEquipment> OtEquipment => Set<OtEquipment>();
    public DbSet<OtEquipmentLog> OtEquipmentLogs => Set<OtEquipmentLog>();
    public DbSet<RecoveryEpisode> RecoveryEpisodes => Set<RecoveryEpisode>();
    public DbSet<CssdInstrument> CssdInstruments => Set<CssdInstrument>();
    public DbSet<CssdSet> CssdSets => Set<CssdSet>();
    public DbSet<CssdSetItem> CssdSetItems => Set<CssdSetItem>();
    public DbSet<CssdBatch> CssdBatches => Set<CssdBatch>();
    public DbSet<CssdBatchSet> CssdBatchSets => Set<CssdBatchSet>();
    public DbSet<CssdIssue> CssdIssues => Set<CssdIssue>();
    public DbSet<SurgeryCharge> SurgeryCharges => Set<SurgeryCharge>();
    public DbSet<OtSettings> OtSettings => Set<OtSettings>();

    public const string PatientNumberSequence = "patient_number_seq";
    public const string OpdNumberSequence = "opd_number_seq";
    public const string EmergencyNumberSequence = "emergency_number_seq";
    public const string ReceiptNumberSequence = "receipt_number_seq";
    public const string ChargeNumberSequence = "charge_number_seq";
    public const string RefundNumberSequence = "refund_number_seq";
    public const string DepositNumberSequence = "deposit_number_seq";
    public const string SettlementNumberSequence = "settlement_number_seq";
    public const string AdjustmentNumberSequence = "adjustment_number_seq";
    public const string LabOrderNumberSequence = "lab_order_number_seq";
    public const string LabSampleNumberSequence = "lab_sample_number_seq";
    public const string MedicineCodeSequence = "medicine_code_seq";
    public const string SupplierCodeSequence = "supplier_code_seq";
    public const string StockReceiptSequence = "pharm_receipt_seq";
    public const string MedicineIssueSequence = "pharm_issue_seq";
    public const string PharmacySaleSequence = "pharm_sale_seq";
    public const string StockTransferSequence = "pharm_transfer_seq";
    public const string StockAdjustmentSequence = "pharm_adjustment_seq";
    public const string MedicineReturnSequence = "pharm_return_seq";
    public const string SupplierReturnSequence = "pharm_supplier_return_seq";
    public const string SupplierPaymentSequence = "pharm_supplier_payment_seq";
    public const string RadiologyOrderSequence = "radiology_order_seq";
    public const string RadiologyServiceCodeSequence = "radiology_service_code_seq";
    public const string IpdAdmissionSequence = "ipd_admission_seq";
    public const string PregnancySequence = "pregnancy_seq";
    public const string DeliverySequence = "delivery_seq";
    public const string SurgerySequence = "surgery_seq";
    public const string CssdBatchSequence = "cssd_batch_seq";

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");
        // Database sequences guarantee unique numbers even when many computers register at the same moment.
        b.HasSequence<long>(PatientNumberSequence).StartsAt(1).IncrementsBy(1);
        b.HasSequence<long>(OpdNumberSequence).StartsAt(1).IncrementsBy(1);
        b.HasSequence<long>(EmergencyNumberSequence).StartsAt(1).IncrementsBy(1);
        foreach (var seq in new[] { MedicineCodeSequence, SupplierCodeSequence, StockReceiptSequence, MedicineIssueSequence, PharmacySaleSequence, StockTransferSequence,
                                     StockAdjustmentSequence, MedicineReturnSequence, SupplierReturnSequence, SupplierPaymentSequence, RadiologyOrderSequence, RadiologyServiceCodeSequence, IpdAdmissionSequence, PregnancySequence, DeliverySequence, SurgerySequence, CssdBatchSequence, LabOrderNumberSequence, LabSampleNumberSequence, ReceiptNumberSequence, ChargeNumberSequence, RefundNumberSequence, DepositNumberSequence, SettlementNumberSequence, AdjustmentNumberSequence })
            b.HasSequence<long>(seq).StartsAt(1).IncrementsBy(1);

        b.Entity<HospitalSettings>(e =>
        {
            e.ToTable("HospitalSettings", t => t.HasCheckConstraint("CK_HospitalSettings_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<HospitalConfiguration>(e =>
        {
            e.ToTable("HospitalConfiguration", t => t.HasCheckConstraint("CK_HospitalConfiguration_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.HospitalType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasIndex(x => x.NormalizedUsername).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Credential).WithOne(c => c.User!).HasForeignKey<UserCredential>(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserCredential>(e =>
        {
            e.ToTable("UserCredentials");
            e.HasKey(x => x.UserId);
        });

        b.Entity<Department>(e =>
        {
            e.ToTable("Departments");
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Code).IsUnique().HasFilter("\"Code\" IS NOT NULL");
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<UserDepartment>(e =>
        {
            e.ToTable("UserDepartments");
            e.HasKey(x => new { x.UserId, x.DepartmentId });
            e.HasOne(x => x.User).WithMany(u => u.Departments).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Department).WithMany(d => d.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => new { x.Module, x.Action }).IsUnique();
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<UserPermission>(e =>
        {
            e.ToTable("UserPermissions");
            e.HasKey(x => new { x.UserId, x.PermissionId });
            e.HasOne(x => x.User).WithMany(u => u.Permissions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LicenseInformation>(e =>
        {
            e.ToTable("LicenseInformation", t => t.HasCheckConstraint("CK_LicenseInformation_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.LicenseType).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<LoginHistory>(e =>
        {
            e.ToTable("LoginHistory");
            e.HasIndex(x => x.AttemptedUtc);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.SessionId).IsUnique().HasFilter("\"SessionId\" IS NOT NULL");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasIndex(x => x.TimestampUtc);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Action);
            e.Property(x => x.OldValues).HasColumnType("jsonb");
            e.Property(x => x.NewValues).HasColumnType("jsonb");
        });

        b.Entity<SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.HasKey(x => x.Key);
        });

        b.Entity<Patient>(e =>
        {
            e.ToTable("Patients");
            e.HasIndex(x => x.PatientNumber).IsUnique();
            e.HasIndex(x => x.SequenceNumber).IsUnique();
            e.HasIndex(x => x.CnicDigits);
            e.HasIndex(x => x.MobileDigits);
            e.HasIndex(x => x.RegisteredUtc);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.FullName).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Patients_FullName_trgm");
            e.HasIndex(x => x.GuardianName).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Patients_GuardianName_trgm");
            e.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.GuardianRelation).HasConversion<string>().HasMaxLength(12);
            e.Property(x => x.AgeUnitAtRegistration).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.MergedIntoPatient).WithMany().HasForeignKey(x => x.MergedIntoPatientId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PatientIdentifier>(e =>
        {
            e.ToTable("PatientIdentifiers");
            e.HasIndex(x => new { x.Type, x.Value }).IsUnique();
            e.HasIndex(x => x.Value);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Patient).WithMany(p => p.Identifiers).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PatientEncounter>(e =>
        {
            e.ToTable("PatientEncounters");
            e.HasIndex(x => x.EncounterNumber).IsUnique();
            e.HasIndex(x => new { x.Type, x.SequenceNumber }).IsUnique();
            e.HasIndex(x => new { x.PatientId, x.EncounterUtc });
            e.HasIndex(x => new { x.Type, x.EncounterDate });
            e.HasIndex(x => new { x.TokenScope, x.EncounterDate, x.TokenNumber }).IsUnique().HasFilter("\"TokenNumber\" IS NOT NULL");
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.HospitalTypeAtVisit).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Patient).WithMany(p => p.Encounters).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OpdVisit>(e =>
        {
            e.ToTable("OpdVisits");
            e.HasKey(x => x.EncounterId);
            e.HasOne(x => x.Encounter).WithOne(x => x.Opd!).HasForeignKey<OpdVisit>(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EmergencyVisit>(e =>
        {
            e.ToTable("EmergencyVisits");
            e.HasKey(x => x.EncounterId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Encounter).WithOne(x => x.Emergency!).HasForeignKey<EmergencyVisit>(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EmergencyStatusHistory>(e =>
        {
            e.ToTable("EmergencyStatusHistory");
            e.HasIndex(x => new { x.EncounterId, x.ChangedUtc });
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Visit).WithMany(v => v.StatusHistory).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EmergencyCategory>(e =>
        {
            e.ToTable("EmergencyCategories");
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Doctor>(e =>
        {
            e.ToTable("Doctors");
            e.HasIndex(x => x.Name);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ShareType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.ShareValue).HasPrecision(14, 2);
        });

        b.Entity<ServiceItem>(e =>
        {
            e.ToTable("ServiceItems");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DefaultFee).HasPrecision(12, 2);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PrintedDocument>(e =>
        {
            e.ToTable("PrintedDocuments");
            e.HasIndex(x => new { x.EncounterId, x.PrintedUtc });
            e.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Encounter).WithMany(x => x.PrintedDocuments).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RegistrationSettings>(e =>
        {
            e.ToTable("RegistrationSettings", t => t.HasCheckConstraint("CK_RegistrationSettings_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.OpdTokenScope).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<Appointment>(e =>
        {
            e.ToTable("Appointments");
            // Doctor-wise independent daily token: the database itself rejects a duplicate token for the same doctor and date.
            e.HasIndex(x => new { x.DoctorId, x.AppointmentDate, x.TokenNumber }).IsUnique().HasDatabaseName("UX_Appointments_Doctor_Date_Token");
            e.HasIndex(x => new { x.AppointmentDate, x.DoctorId, x.Status });
            e.HasIndex(x => x.PatientId);
            e.HasIndex(x => x.EncounterId).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ConsultationFee).HasPrecision(12, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t => t.HasCheckConstraint("CK_Appointments_Token_Positive", "\"TokenNumber\" > 0"));
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Encounter).WithOne().HasForeignKey<Appointment>(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
            // Update 9: consultation fee at booking + "Free by Doctor".
            e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.AmountPaid).HasPrecision(12, 2);
            e.Property(x => x.WaivedAmount).HasPrecision(12, 2);
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL").HasDatabaseName("UX_Appointments_Idempotency");
            e.HasIndex(x => new { x.PaymentStatus, x.AppointmentDate });
            e.HasIndex(x => x.ReceiptId);
            e.HasOne(x => x.Receipt).WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PrinterProfile>(e =>
        {
            e.ToTable("PrinterProfiles");
            e.HasIndex(x => x.WorkstationName).IsUnique();
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Alignment).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.FontSize).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        ConfigureFinance(b);
        ConfigureLaboratory(b);
        ConfigurePharmacy(b);
        ConfigureRadiology(b);
        ConfigureIpd(b);
        ConfigureOt(b);
        b.Entity<LabOrder>().HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.IpdAdmissionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RadiologyOrder>().HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.IpdAdmissionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PatientMedicineIssue>().HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.IpdAdmissionId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<BackupHistory>(e =>
        {
            e.ToTable("BackupHistory");
            e.HasIndex(x => x.FileName).IsUnique();
            e.HasIndex(x => x.StartedUtc);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        });
    }

    private static void ConfigureFinance(ModelBuilder b)
    {
        static void Money<T>(EntityTypeBuilder<T> e) where T : class
        {
            foreach (var p in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)))
                e.Property(p.Name).HasPrecision(14, 2);
        }

        b.Entity<Counter>(e =>
        {
            e.ToTable("Counters");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CounterAssignment>(e =>
        {
            e.ToTable("CounterAssignments");
            e.Ignore(x => x.IsCurrent);
            // One current assignment per user and counter.
            e.HasIndex(x => new { x.CounterId, x.UserId }).IsUnique().HasFilter("\"EndedUtc\" IS NULL").HasDatabaseName("UX_CounterAssignments_Current");
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Counter).WithMany(x => x.Assignments).HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PaymentMethod>(e =>
        {
            e.ToTable("PaymentMethods");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
        });
        b.Entity<ServiceFee>(e =>
        {
            e.ToTable("ServiceFees", t => t.HasCheckConstraint("CK_ServiceFees_NonNegative", "\"Fee\" >= 0"));
            Money(e);
            e.HasIndex(x => new { x.ServiceItemId, x.EffectiveFrom });
            e.HasOne(x => x.ServiceItem).WithMany(x => x.Fees).HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ServiceCounter>(e =>
        {
            e.ToTable("ServiceCounters");
            e.HasKey(x => new { x.ServiceItemId, x.CounterId });
            e.HasOne(x => x.ServiceItem).WithMany(x => x.Counters).HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Counter).WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Charge>(e =>
        {
            e.ToTable("Charges", t =>
            {
                t.HasCheckConstraint("CK_Charges_Amounts", "\"Fee\" >= 0 AND \"Discount\" >= 0 AND \"Discount\" <= \"Fee\" AND \"NetAmount\" = \"Fee\" - \"Discount\"");
                t.HasCheckConstraint("CK_Charges_Paid", "\"PaidAmount\" >= 0 AND \"PaidAmount\" <= \"NetAmount\" AND \"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"PaidAmount\"");
            });
            Money(e);
            e.HasIndex(x => x.ChargeNumber).IsUnique();
            e.HasIndex(x => new { x.PatientId, x.Status });
            e.HasIndex(x => x.EncounterId);
            e.HasIndex(x => new { x.DoctorId, x.BusinessDate });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ServiceKind).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.DoctorShareType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.Balance);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Receipt>().WithMany().HasForeignKey(x => x.OriginReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LabOrder>().WithMany().HasForeignKey(x => x.LabOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.LabOrderId);
            e.HasOne<PharmacySale>().WithMany().HasForeignKey(x => x.PharmacySaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PharmacySaleId);
            e.HasOne<PatientMedicineIssue>().WithMany().HasForeignKey(x => x.PharmacyIssueId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PharmacyIssueId);
            e.HasOne<RadiologyOrder>().WithMany().HasForeignKey(x => x.RadiologyOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.RadiologyOrderId);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.IpdAdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.IpdAdmissionId);
        });
        b.Entity<Receipt>(e =>
        {
            e.ToTable("Receipts", t => t.HasCheckConstraint("CK_Receipts_Amounts", "\"AmountPaid\" >= 0 AND \"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"AmountPaid\""));
            Money(e);
            e.HasIndex(x => x.ReceiptNumber).IsUnique().HasDatabaseName("UX_Receipts_Number");
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_Receipts_Idempotency");
            e.HasIndex(x => new { x.BusinessDate, x.CounterId });
            e.HasIndex(x => new { x.CashierUserId, x.BusinessDate });
            e.HasIndex(x => x.PatientId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Counter).WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ReceiptLine>(e =>
        {
            e.ToTable("ReceiptLines", t => t.HasCheckConstraint("CK_ReceiptLines_Amounts", "\"Amount\" >= 0 AND \"DoctorShare\" >= 0 AND \"DoctorShare\" <= \"Amount\" AND \"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"Amount\""));
            Money(e);
            e.HasIndex(x => x.ChargeId);
            e.HasOne(x => x.Receipt).WithMany(x => x.Lines).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Charge).WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ReceiptPrint>(e =>
        {
            e.ToTable("ReceiptPrints");
            e.HasIndex(x => new { x.ReceiptId, x.CopyNumber }).IsUnique();
            e.HasOne(x => x.Receipt).WithMany(x => x.Prints).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Refund>(e =>
        {
            e.HasIndex(x => x.WaiverAppointmentId).IsUnique().HasFilter("\"WaiverAppointmentId\" IS NOT NULL AND \"Status\" <> 'Rejected'").HasDatabaseName("UX_Refunds_WaiverAppointment");
            e.ToTable("Refunds", t => t.HasCheckConstraint("CK_Refunds_Positive", "\"Amount\" > 0"));
            Money(e);
            e.HasIndex(x => x.RefundNumber).IsUnique();
            e.HasIndex(x => new { x.ReceiptId, x.Status });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Receipt).WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CashDeposit>(e =>
        {
            e.ToTable("CashDeposits", t => t.HasCheckConstraint("CK_CashDeposits_Positive", "\"Amount\" > 0 AND (\"ReceivedAmount\" IS NULL OR (\"ReceivedAmount\" > 0 AND \"ReceivedAmount\" <= \"Amount\"))"));
            Money(e);
            e.HasIndex(x => x.DepositNumber).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Status });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Counter).WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CashierLedgerEntry>(e =>
        {
            e.ToTable("CashierLedger");
            Money(e);
            e.HasIndex(x => new { x.UserId, x.BusinessDate });
            e.HasIndex(x => new { x.CounterId, x.BusinessDate });
            e.HasIndex(x => x.ReceiptId);
            e.Property(x => x.EntryType).HasConversion<string>().HasMaxLength(30);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Counter>().WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Refund>().WithMany().HasForeignKey(x => x.RefundId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CashDeposit>().WithMany().HasForeignKey(x => x.DepositId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinancialAdjustment>().WithMany().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PaymentMethod>().WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<DoctorLedgerEntry>(e =>
        {
            e.ToTable("DoctorLedger");
            Money(e);
            e.HasIndex(x => new { x.DoctorId, x.BusinessDate });
            e.Property(x => x.EntryType).HasConversion<string>().HasMaxLength(30);
            e.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Receipt>().WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ReceiptLine>().WithMany().HasForeignKey(x => x.ReceiptLineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Refund>().WithMany().HasForeignKey(x => x.RefundId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DoctorSettlement>().WithMany().HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FinancialAdjustment>().WithMany().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
            // A doctor's fee waiver is recorded at most once per appointment (no duplicate adjustments).
            e.HasIndex(x => x.AppointmentId).IsUnique().HasFilter("\"AppointmentId\" IS NOT NULL AND \"EntryType\" = 'FeeWaiver'").HasDatabaseName("UX_DoctorLedger_FeeWaiver");
        });
        b.Entity<DoctorSettlement>(e =>
        {
            e.ToTable("DoctorSettlements", t => t.HasCheckConstraint("CK_DoctorSettlements_Positive", "\"Amount\" > 0"));
            Money(e);
            e.HasIndex(x => x.SettlementNumber).IsUnique();
            e.HasIndex(x => new { x.DoctorId, x.BusinessDate });
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<FinancialAdjustment>(e =>
        {
            e.ToTable("FinancialAdjustments", t => t.HasCheckConstraint("CK_FinancialAdjustments_NonZero", "\"Amount\" <> 0"));
            Money(e);
            e.HasIndex(x => x.AdjustmentNumber).IsUnique();
            e.Property(x => x.Target).HasConversion<string>().HasMaxLength(20);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<DailyCashClosing>(e =>
        {
            e.ToTable("DailyCashClosings");
            Money(e);
            e.HasIndex(x => new { x.BusinessDate, x.Scope, x.UserId, x.CounterId });
            e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<FinanceSettings>(e =>
        {
            e.ToTable("FinanceSettings", t => t.HasCheckConstraint("CK_FinanceSettings_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
        });
    }

    private static void ConfigureLaboratory(ModelBuilder b)
    {
        b.Entity<LabSampleType>(e => { e.ToTable("LabSampleTypes"); e.HasIndex(x => x.Code).IsUnique(); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<LabCategory>(e => { e.ToTable("LabCategories"); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<LabTest>(e =>
        {
            e.ToTable("LabTests");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.ServiceItemId).IsUnique();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SampleType).WithMany().HasForeignKey(x => x.SampleTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabTestParameter>(e =>
        {
            e.ToTable("LabTestParameters");
            e.Property(x => x.ResultType).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.LabTestId, x.SortOrder });
            e.HasOne(x => x.LabTest).WithMany(x => x.Parameters).HasForeignKey(x => x.LabTestId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabReferenceRange>(e =>
        {
            e.ToTable("LabReferenceRanges");
            e.Property(x => x.Gender).HasConversion<string>().HasMaxLength(10);
            foreach (var p in new[] { nameof(LabReferenceRange.Low), nameof(LabReferenceRange.High), nameof(LabReferenceRange.CriticalLow), nameof(LabReferenceRange.CriticalHigh) })
                e.Property(p).HasPrecision(14, 4);
            e.HasOne(x => x.Parameter).WithMany(x => x.Ranges).HasForeignKey(x => x.ParameterId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LabOrder>(e =>
        {
            e.ToTable("LabOrders");
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_LabOrders_Idempotency");
            e.HasIndex(x => new { x.PatientId, x.BusinessDate });
            e.HasIndex(x => x.BusinessDate);
            e.Property(x => x.ReferralType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.ReferredBy);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReferringDoctor).WithMany().HasForeignKey(x => x.ReferringDoctorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabOrderItem>(e =>
        {
            e.ToTable("LabOrderItems");
            e.HasIndex(x => new { x.Status, x.LabOrderId });
            e.HasIndex(x => x.ChargeId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.LabOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.LabTest).WithMany().HasForeignKey(x => x.LabTestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sample).WithMany().HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Charge).WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabResultValue>(e =>
        {
            e.ToTable("LabResultValues");
            e.Property(x => x.ResultType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Flag).HasConversion<string>().HasMaxLength(20);
            foreach (var p in new[] { nameof(LabResultValue.RefLow), nameof(LabResultValue.RefHigh), nameof(LabResultValue.CriticalLow), nameof(LabResultValue.CriticalHigh), nameof(LabResultValue.NumericValue) })
                e.Property(p).HasPrecision(14, 4);
            e.Ignore(x => x.ReferenceDisplay);
            e.HasIndex(x => x.OrderItemId);
            e.HasOne(x => x.OrderItem).WithMany(x => x.Values).HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabSample>(e =>
        {
            e.ToTable("LabSamples");
            e.HasIndex(x => x.SampleNumber).IsUnique();
            e.HasIndex(x => new { x.LabOrderId, x.Status });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Order).WithMany(x => x.Samples).HasForeignKey(x => x.LabOrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabItemEvent>(e =>
        {
            e.ToTable("LabItemEvents");
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.OrderItemId);
            e.HasIndex(x => new { x.EventType, x.BusinessDate });
            e.HasOne<LabOrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabResultRevision>(e =>
        {
            e.ToTable("LabResultRevisions");
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
            e.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.OrderItemId, x.RevisionNo }).IsUnique();
            e.HasOne<LabOrderItem>().WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabReportPrint>(e =>
        {
            e.ToTable("LabReportPrints");
            e.HasIndex(x => new { x.LabOrderId, x.CopyNumber }).IsUnique();
            e.HasOne<LabOrder>().WithMany().HasForeignKey(x => x.LabOrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabSettings>(e =>
        {
            e.ToTable("LabSettings", t => t.HasCheckConstraint("CK_LabSettings_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Layout).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        b.Entity<LabInventoryItem>(e =>
        {
            e.ToTable("LabInventoryItems");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ReorderLevel).HasPrecision(14, 2);
        });
    }

    private static void ConfigureIpd(ModelBuilder b)
    {
        b.Entity<IpdWard>(e =>
        {
            e.ToTable("IpdWards", t => t.HasCheckConstraint("CK_IpdWards_Capacity", "\"Capacity\" >= 0"));
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BedChargeServiceItem).WithMany().HasForeignKey(x => x.BedChargeServiceItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StockLocation>().WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdRoom>(e =>
        {
            e.ToTable("IpdRooms");
            e.HasIndex(x => new { x.WardId, x.Name }).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Ward).WithMany().HasForeignKey(x => x.WardId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdBed>(e =>
        {
            e.ToTable("IpdBeds");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.CurrentAdmissionId).IsUnique().HasFilter("\"CurrentAdmissionId\" IS NOT NULL");
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Ward).WithMany().HasForeignKey(x => x.WardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.CurrentAdmissionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdBedStatusChange>(e =>
        {
            e.ToTable("IpdBedStatusChanges");
            e.Property(x => x.From).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.To).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.BedId);
            e.HasOne<IpdBed>().WithMany().HasForeignKey(x => x.BedId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdAdmission>(e =>
        {
            e.ToTable("IpdAdmissions", t => t.HasCheckConstraint("CK_IpdAdmissions_Mother", "\"MotherAdmissionId\" IS NULL OR \"MotherAdmissionId\" <> \"Id\""));
            e.HasIndex(x => x.AdmissionNumber).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_IpdAdmissions_Idempotency");
            e.HasIndex(x => new { x.PatientId, x.Status });
            // A patient has at most one open admission (database-level guard).
            e.HasIndex(x => x.PatientId).IsUnique().HasDatabaseName("UX_IpdAdmissions_OneActive")
                .HasFilter("\"Status\" IN ('Registered','Admitted','UnderTreatment','Transferred','DischargeRequested')");
            e.HasIndex(x => new { x.Status, x.Kind });
            e.HasIndex(x => x.AdmissionDate);
            foreach (var p in new[] { nameof(IpdAdmission.Kind), nameof(IpdAdmission.Source), nameof(IpdAdmission.Status), nameof(IpdAdmission.DischargeType) })
                e.Property(p).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.IsActive);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SourceEncounter).WithMany().HasForeignKey(x => x.SourceEncounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AdmittingDoctor).WithMany().HasForeignKey(x => x.AdmittingDoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CurrentWard).WithMany().HasForeignKey(x => x.CurrentWardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CurrentBed).WithMany().HasForeignKey(x => x.CurrentBedId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.MotherAdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pregnancy>().WithMany().HasForeignKey(x => x.PregnancyId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdBedAllocation>(e =>
        {
            e.ToTable("IpdBedAllocations", t => t.HasCheckConstraint("CK_IpdBedAllocations_Period", "\"ToUtc\" IS NULL OR \"ToUtc\" >= \"FromUtc\""));
            // Database-level protection: one occupant per bed, one open bed per admission (a rooming-in baby shares the mother's bed).
            e.HasIndex(x => x.BedId).IsUnique().HasFilter("\"ToUtc\" IS NULL AND NOT \"IsShared\"").HasDatabaseName("UX_IpdBedAllocations_OpenBed");
            e.HasIndex(x => x.AdmissionId).IsUnique().HasFilter("\"ToUtc\" IS NULL").HasDatabaseName("UX_IpdBedAllocations_OpenAdmission");
            e.HasIndex(x => new { x.BedId, x.FromUtc });
            e.HasOne(x => x.Bed).WithMany().HasForeignKey(x => x.BedId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdWard>().WithMany().HasForeignKey(x => x.WardId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.SharedWithAdmissionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdTransfer>(e =>
        {
            e.ToTable("IpdTransfers");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.AdmissionId);
            e.HasIndex(x => x.TransferUtc);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdEvent>(e =>
        {
            e.ToTable("IpdEvents");
            e.HasIndex(x => x.AdmissionId);
            e.HasIndex(x => x.PregnancyId);
            e.HasIndex(x => new { x.Category, x.CreatedUtc });
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pregnancy>().WithMany().HasForeignKey(x => x.PregnancyId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ClinicalFormField>(e =>
        {
            e.ToTable("ClinicalFormFields");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.AlertLow).HasPrecision(12, 3);
            e.Property(x => x.AlertHigh).HasPrecision(12, 3);
            e.HasIndex(x => new { x.Kind, x.Key }).IsUnique();
        });
        b.Entity<ClinicalRecord>(e =>
        {
            e.ToTable("ClinicalRecords");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Values).HasColumnType("jsonb");
            e.HasIndex(x => new { x.AdmissionId, x.Kind, x.RecordedUtc });
            e.HasIndex(x => new { x.PatientId, x.Kind });
            e.HasIndex(x => x.PregnancyId);
            e.HasIndex(x => x.CorrectsRecordId).IsUnique().HasFilter("\"CorrectsRecordId\" IS NOT NULL");
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pregnancy>().WithMany().HasForeignKey(x => x.PregnancyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ClinicalRecord>().WithMany().HasForeignKey(x => x.CorrectsRecordId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Pregnancy>(e =>
        {
            e.ToTable("Pregnancies");
            e.HasIndex(x => x.Number).IsUnique();
            // A woman has at most one ongoing pregnancy record; earlier ones stay untouched.
            e.HasIndex(x => x.PatientId).IsUnique().HasFilter("\"Status\" = 'Active'").HasDatabaseName("UX_Pregnancies_OneActive");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LabourRecord>(e =>
        {
            e.ToTable("LabourRecords");
            e.HasIndex(x => x.AdmissionId).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Admission).WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Pregnancy).WithMany().HasForeignKey(x => x.PregnancyId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<DeliveryRecord>(e =>
        {
            e.ToTable("DeliveryRecords", t => t.HasCheckConstraint("CK_DeliveryRecords_Babies", "\"BabyCount\" >= 1"));
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_DeliveryRecords_Idempotency");
            e.HasIndex(x => x.DeliveryDate);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Admission).WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Mother).WithMany().HasForeignKey(x => x.MotherPatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pregnancy>().WithMany().HasForeignKey(x => x.PregnancyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LabourRecord>().WithMany().HasForeignKey(x => x.LabourRecordId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<NewbornRecord>(e =>
        {
            e.ToTable("NewbornRecords", t => t.HasCheckConstraint("CK_NewbornRecords_NotMother", "\"NewbornPatientId\" IS NULL OR \"NewbornPatientId\" <> \"MotherPatientId\""));
            e.HasIndex(x => new { x.DeliveryId, x.BirthOrder }).IsUnique();
            e.HasIndex(x => x.NewbornPatientId).IsUnique().HasFilter("\"NewbornPatientId\" IS NOT NULL");
            e.HasIndex(x => x.MotherPatientId);
            e.Property(x => x.Sex).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Condition).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Placement).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Delivery).WithMany(x => x.Newborns).HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.NewbornPatient).WithMany().HasForeignKey(x => x.NewbornPatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.MotherPatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.NewbornAdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pregnancy>().WithMany().HasForeignKey(x => x.PregnancyId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<NicuDetails>(e =>
        {
            e.ToTable("NicuDetails");
            e.HasIndex(x => x.AdmissionId).IsUnique();
            e.HasOne(x => x.Admission).WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.NewbornRecord).WithMany().HasForeignKey(x => x.NewbornRecordId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<NursingTask>(e =>
        {
            e.ToTable("NursingTasks");
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.AdmissionId, x.Status });
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ShiftHandover>(e =>
        {
            e.ToTable("ShiftHandovers");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.AdmissionId);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MedicationOrder>(e =>
        {
            e.ToTable("MedicationOrders", t => t.HasCheckConstraint("CK_MedicationOrders_Units", "\"StockUnitsPerDose\" >= 0"));
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.AdmissionId);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MedicationDose>(e =>
        {
            e.ToTable("MedicationDoses");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.OrderId, x.ScheduledUtc }).IsUnique();
            e.HasIndex(x => new { x.AdmissionId, x.ScheduledUtc });
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<InventoryTransaction>().WithMany().HasForeignKey(x => x.StockTransactionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdBedCharge>(e =>
        {
            e.ToTable("IpdBedCharges");
            e.HasIndex(x => new { x.AdmissionId, x.Night }).IsUnique();
            e.HasOne<IpdAdmission>().WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IpdSettings>(e =>
        {
            e.ToTable("IpdSettings", t => t.HasCheckConstraint("CK_IpdSettings_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<ServiceItem>().WithMany().HasForeignKey(x => x.AdmissionServiceItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOt(ModelBuilder b)
    {
        b.Entity<OtTheatre>(e =>
        {
            e.ToTable("OtTheatres", t => t.HasCheckConstraint("CK_OtTheatres_Cleaning", "\"CleaningMinutes\" BETWEEN 0 AND 480"));
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.StockLocation).WithMany().HasForeignKey(x => x.StockLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChargeServiceItem).WithMany().HasForeignKey(x => x.ChargeServiceItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtProcedure>(e =>
        {
            e.ToTable("OtProcedures", t => t.HasCheckConstraint("CK_OtProcedures_Duration", "\"DurationMinutes\" BETWEEN 5 AND 1440"));
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.DefaultOtType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Surgery>(e =>
        {
            e.ToTable("Surgeries", t => t.HasCheckConstraint("CK_Surgeries_Period", "\"PlannedEndUtc\" > \"PlannedStartUtc\" AND \"DurationMinutes\" > 0 AND \"CleaningMinutes\" >= 0"));
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_Surgeries_Idempotency");
            e.HasIndex(x => new { x.TheatreId, x.PlannedStartUtc });
            e.HasIndex(x => new { x.PlannedDate, x.Status });
            e.HasIndex(x => x.PatientId);
            e.HasIndex(x => x.IpdAdmissionId);
            foreach (var p in new[] { nameof(Surgery.Status), nameof(Surgery.Priority), nameof(Surgery.AnesthesiaType) })
                e.Property(p).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.HoldsSchedule);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.IpdAdmission).WithMany().HasForeignKey(x => x.IpdAdmissionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Procedure).WithMany().HasForeignKey(x => x.ProcedureId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Theatre).WithMany().HasForeignKey(x => x.TheatreId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SurgeryTeamMember>(e =>
        {
            e.ToTable("SurgeryTeamMembers", t => t.HasCheckConstraint("CK_SurgeryTeamMembers_Person", "\"DoctorId\" IS NOT NULL OR \"UserId\" IS NOT NULL"));
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.SurgeryId);
            e.HasIndex(x => x.DoctorId);
            e.HasIndex(x => x.UserId);
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SurgeryEvent>(e =>
        {
            e.ToTable("SurgeryEvents");
            e.HasIndex(x => new { x.SurgeryId, x.CreatedUtc });
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtConsentType>(e => { e.ToTable("OtConsentTypes"); e.HasIndex(x => x.Name).IsUnique(); });
        b.Entity<SurgeryConsent>(e =>
        {
            e.ToTable("SurgeryConsents");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.SurgeryId);
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ConsentType).WithMany().HasForeignKey(x => x.ConsentTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtDocument>(e =>
        {
            e.ToTable("OtDocuments");
            e.HasIndex(x => x.SurgeryId);
            e.HasIndex(x => x.StoredPath).IsUnique();
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SurgeryConsent>().WithMany().HasForeignKey(x => x.ConsentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtChecklistItem>(e =>
        {
            e.ToTable("OtChecklistItems");
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Stage, x.Label }).IsUnique();
        });
        b.Entity<SurgeryChecklistEntry>(e =>
        {
            e.ToTable("SurgeryChecklistEntries");
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Answer).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.SurgeryId, x.ItemId, x.Id });
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OtChecklistItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SurgeryChecklistStage>(e =>
        {
            e.ToTable("SurgeryChecklistStages");
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.SurgeryId, x.Stage }).IsUnique();
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtDrugEntry>(e =>
        {
            e.ToTable("OtDrugEntries");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => new { x.SurgeryId, x.GivenUtc });
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OperativeNote>(e =>
        {
            e.ToTable("OperativeNotes");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.SurgeryId, x.Version }).IsUnique();
            e.HasIndex(x => x.SurgeryId).IsUnique().HasFilter("\"Status\" = 'Draft'").HasDatabaseName("UX_OperativeNotes_OneDraft");
            e.HasIndex(x => x.AmendsNoteId).IsUnique().HasFilter("\"AmendsNoteId\" IS NOT NULL");
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OperativeNote>().WithMany().HasForeignKey(x => x.AmendsNoteId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SurgerySpecimen>(e =>
        {
            e.ToTable("SurgerySpecimens");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.SurgeryId);
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.LabOrder).WithMany().HasForeignKey(x => x.LabOrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtConsumption>(e =>
        {
            e.ToTable("OtConsumptions", t => t.HasCheckConstraint("CK_OtConsumptions_Quantity", "\"Quantity\" > 0"));
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => x.SurgeryId);
            e.HasIndex(x => x.ReturnOfId);
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MedicineBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StockLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OtConsumption>().WithMany().HasForeignKey(x => x.ReturnOfId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtEquipment>(e =>
        {
            e.ToTable("OtEquipment");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Theatre).WithMany().HasForeignKey(x => x.TheatreId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtEquipmentLog>(e =>
        {
            e.ToTable("OtEquipmentLogs");
            e.HasIndex(x => x.EquipmentId);
            e.HasOne<OtEquipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RecoveryEpisode>(e =>
        {
            e.ToTable("RecoveryEpisodes");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.IsOpen);
            e.HasIndex(x => x.SurgeryId).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Surgery).WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CssdInstrument>(e => { e.ToTable("CssdInstruments"); e.HasIndex(x => x.Code).IsUnique(); });
        b.Entity<CssdSet>(e =>
        {
            e.ToTable("CssdSets");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OtTheatre>().WithMany().HasForeignKey(x => x.TheatreId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CssdSetItem>(e =>
        {
            e.ToTable("CssdSetItems", t => t.HasCheckConstraint("CK_CssdSetItems_Quantity", "\"Quantity\" > 0"));
            e.HasIndex(x => new { x.SetId, x.InstrumentId }).IsUnique();
            e.HasOne(x => x.Instrument).WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CssdBatch>(e =>
        {
            e.ToTable("CssdBatches");
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasMany(x => x.Sets).WithOne().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CssdBatchSet>(e =>
        {
            e.ToTable("CssdBatchSets");
            e.HasIndex(x => new { x.BatchId, x.SetId }).IsUnique();
            e.HasOne(x => x.Set).WithMany().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CssdIssue>(e =>
        {
            e.ToTable("CssdIssues");
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.SetId).IsUnique().HasFilter("\"ReturnedUtc\" IS NULL").HasDatabaseName("UX_CssdIssues_OpenSet");
            e.HasIndex(x => x.SurgeryId);
            e.HasOne(x => x.Set).WithMany().HasForeignKey(x => x.SetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CssdBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SurgeryCharge>(e =>
        {
            e.ToTable("SurgeryCharges");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.SurgeryId, x.Kind }).IsUnique().HasFilter("\"Kind\" <> 'Other'").HasDatabaseName("UX_SurgeryCharges_Standard");
            e.HasIndex(x => x.ChargeId).IsUnique();
            e.HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Charge).WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OtSettings>(e =>
        {
            e.ToTable("OtSettings", t => t.HasCheckConstraint("CK_OtSettings_Single", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        b.Entity<ClinicalRecord>().HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ClinicalRecord>().HasIndex(x => new { x.SurgeryId, x.Kind });
        b.Entity<Charge>().HasOne<Surgery>().WithMany().HasForeignKey(x => x.SurgeryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Charge>().HasIndex(x => x.SurgeryId);
    }

    private static void ConfigureRadiology(ModelBuilder b)
    {
        b.Entity<RadiologyTemplate>(e =>
        {
            e.ToTable("RadiologyTemplates");
            e.Property(x => x.Modality).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Modality, x.Name }).IsUnique();
        });
        b.Entity<RadiologyService>(e =>
        {
            e.ToTable("RadiologyServices");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.ServiceItemId).IsUnique();
            e.Property(x => x.Modality).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DefaultTemplate).WithMany().HasForeignKey(x => x.DefaultTemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RadiologyOrder>(e =>
        {
            e.ToTable("RadiologyOrders");
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_RadiologyOrders_Idempotency");
            e.HasIndex(x => new { x.PatientId, x.BusinessDate });
            e.HasIndex(x => x.BusinessDate);
            e.Property(x => x.ReferralType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.ReferredBy);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReferringDepartment).WithMany().HasForeignKey(x => x.ReferringDepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReferringDoctor).WithMany().HasForeignKey(x => x.ReferringDoctorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RadiologyExam>(e =>
        {
            e.ToTable("RadiologyExams", t => t.HasCheckConstraint("CK_RadiologyExams_Token", "\"TokenNumber\" > 0"));
            e.HasIndex(x => new { x.Status, x.Modality, x.ScheduledDate });
            e.HasIndex(x => new { x.Modality, x.ScheduledDate, x.TokenNumber });
            e.HasIndex(x => x.ChargeId).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Modality).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Order).WithMany(x => x.Exams).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Charge).WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RadiologyExamEvent>(e =>
        {
            e.ToTable("RadiologyExamEvents");
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.ExamId);
            e.HasIndex(x => new { x.EventType, x.BusinessDate });
            e.HasOne<RadiologyExam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RadiologyReportRevision>(e =>
        {
            e.ToTable("RadiologyReportRevisions");
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
            e.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.ExamId, x.RevisionNo }).IsUnique();
            e.HasOne<RadiologyExam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RadiologyReportPrint>(e =>
        {
            e.ToTable("RadiologyReportPrints");
            e.HasIndex(x => new { x.ExamId, x.CopyNumber }).IsUnique();
            e.HasOne<RadiologyExam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RadiologyAttachment>(e =>
        {
            e.ToTable("RadiologyAttachments", t => t.HasCheckConstraint("CK_RadiologyAttachments_Size", "\"SizeBytes\" > 0"));
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(x => x.ExamId);
            e.HasIndex(x => x.PatientId);
            e.HasIndex(x => x.StoredPath).IsUnique();
            e.HasOne<RadiologyExam>().WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<RadiologySettings>(e =>
        {
            e.ToTable("RadiologySettings", t => t.HasCheckConstraint("CK_RadiologySettings_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
        });
    }

    private static void ConfigurePharmacy(ModelBuilder b)
    {
        static void Money<T>(EntityTypeBuilder<T> e) where T : class
        {
            foreach (var p in typeof(T).GetProperties().Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)))
                e.Property(p.Name).HasPrecision(14, p.Name.Contains("Cost") || p.Name == "PurchasePrice" ? 4 : 2);
        }

        b.Entity<PharmacyLookup>(e =>
        {
            e.ToTable("PharmacyLookups");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Kind, x.Name }).IsUnique();
        });
        b.Entity<Medicine>(e =>
        {
            e.ToTable("Medicines", t => t.HasCheckConstraint("CK_Medicines_Values", "\"MinimumStock\" >= 0 AND \"UnitsPerPack\" >= 1 AND \"MaxDiscountPercent\" BETWEEN 0 AND 100"));
            Money(e);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.GenericName);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.DisplayName);
            e.HasOne(x => x.Manufacturer).WithMany().HasForeignKey(x => x.ManufacturerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DosageForm).WithMany().HasForeignKey(x => x.DosageFormId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MedicinePriceChange>(e =>
        {
            e.ToTable("MedicinePriceChanges");
            Money(e);
            e.HasIndex(x => x.MedicineId);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Supplier>(e =>
        {
            e.ToTable("Suppliers");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        b.Entity<StockLocation>(e =>
        {
            e.ToTable("StockLocations");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MedicineBatch>(e =>
        {
            e.ToTable("MedicineBatches");
            Money(e);
            e.HasIndex(x => new { x.MedicineId, x.BatchNumber }).IsUnique().HasDatabaseName("UX_MedicineBatches_Batch");
            e.HasIndex(x => x.ExpiryDate);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockBalance>(e =>
        {
            e.ToTable("StockBalances", t => t.HasCheckConstraint("CK_StockBalances_NotNegative", "\"Quantity\" >= 0"));
            e.HasKey(x => new { x.LocationId, x.BatchId });
            e.HasIndex(x => new { x.MedicineId, x.LocationId });
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<InventoryTransaction>(e =>
        {
            e.ToTable("InventoryTransactions", t => t.HasCheckConstraint("CK_InventoryTransactions_Values", "\"Quantity\" <> 0 AND \"BalanceAfter\" >= 0"));
            Money(e);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.MedicineId, x.LocationId, x.Id });
            e.HasIndex(x => new { x.BatchId, x.LocationId });
            e.HasIndex(x => new { x.Type, x.BusinessDate });
            e.HasIndex(x => new { x.DocumentType, x.DocumentId });
            e.HasIndex(x => x.PatientId);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StockLocation>().WithMany().HasForeignKey(x => x.CounterpartLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockReceipt>(e =>
        {
            e.ToTable("StockReceipts", t => t.HasCheckConstraint("CK_StockReceipts_GovernmentNoPrices",
                "\"Mode\" = 'Private' OR (\"GrossAmount\" IS NULL AND \"NetAmount\" IS NULL AND \"DiscountAmount\" IS NULL AND \"AdditionalCharges\" IS NULL)"));
            Money(e);
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_StockReceipts_Idempotency");
            e.HasIndex(x => x.BusinessDate);
            e.HasIndex(x => x.SupplierId);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockReceiptItem>(e =>
        {
            e.ToTable("StockReceiptItems", t => t.HasCheckConstraint("CK_StockReceiptItems_Quantity", "\"Quantity\" > 0"));
            Money(e);
            e.HasOne(x => x.StockReceipt).WithMany(x => x.Items).HasForeignKey(x => x.StockReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PatientMedicineIssue>(e =>
        {
            e.ToTable("PatientMedicineIssues", t => t.HasCheckConstraint("CK_PatientMedicineIssues_Government",
                "\"Mode\" = 'Private' OR (\"EncounterId\" IS NOT NULL AND \"GrossAmount\" IS NULL AND \"NetAmount\" IS NULL)"));
            Money(e);
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_PatientMedicineIssues_Idempotency");
            e.HasIndex(x => new { x.PatientId, x.BusinessDate });
            e.HasIndex(x => x.EncounterId);
            e.HasIndex(x => x.BusinessDate);
            e.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PatientMedicineIssueItem>(e =>
        {
            e.ToTable("PatientMedicineIssueItems", t => t.HasCheckConstraint("CK_PatientMedicineIssueItems_Quantity", "\"Quantity\" > 0 AND \"ReturnedQuantity\" BETWEEN 0 AND \"Quantity\""));
            Money(e);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.MedicineId);
            e.HasOne(x => x.Issue).WithMany(x => x.Items).HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Charge).WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PharmacySale>(e =>
        {
            e.ToTable("PharmacySales");
            Money(e);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_PharmacySales_Idempotency");
            e.HasIndex(x => x.BusinessDate);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Receipt).WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Counter).WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PharmacySaleItem>(e =>
        {
            e.ToTable("PharmacySaleItems", t => t.HasCheckConstraint("CK_PharmacySaleItems_Quantity", "\"Quantity\" > 0 AND \"ReturnedQuantity\" BETWEEN 0 AND \"Quantity\""));
            Money(e);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.MedicineId);
            e.HasOne(x => x.Sale).WithMany(x => x.Items).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Charge).WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockTransfer>(e =>
        {
            e.ToTable("StockTransfers", t => t.HasCheckConstraint("CK_StockTransfers_Locations", "\"FromLocationId\" <> \"ToLocationId\""));
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_StockTransfers_Idempotency");
            e.HasIndex(x => x.BusinessDate);
            e.HasOne(x => x.FromLocation).WithMany().HasForeignKey(x => x.FromLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ToLocation).WithMany().HasForeignKey(x => x.ToLocationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockTransferItem>(e =>
        {
            e.ToTable("StockTransferItems", t => t.HasCheckConstraint("CK_StockTransferItems_Quantity", "\"Quantity\" > 0"));
            e.HasOne(x => x.Transfer).WithMany(x => x.Items).HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StockAdjustment>(e =>
        {
            e.ToTable("StockAdjustments", t => t.HasCheckConstraint("CK_StockAdjustments_Quantity", "\"Quantity\" > 0"));
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_StockAdjustments_Idempotency");
            e.HasIndex(x => new { x.Status, x.BusinessDate });
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MedicineReturn>(e =>
        {
            e.ToTable("MedicineReturns");
            Money(e);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_MedicineReturns_Idempotency");
            e.HasIndex(x => x.BusinessDate);
            e.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Issue).WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Refund).WithMany().HasForeignKey(x => x.RefundId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StockLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<MedicineReturnItem>(e =>
        {
            e.ToTable("MedicineReturnItems", t => t.HasCheckConstraint("CK_MedicineReturnItems_Quantity", "\"Quantity\" > 0"));
            Money(e);
            e.HasOne(x => x.Return).WithMany(x => x.Items).HasForeignKey(x => x.ReturnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PharmacySaleItem>().WithMany().HasForeignKey(x => x.SaleItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PatientMedicineIssueItem>().WithMany().HasForeignKey(x => x.IssueItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MedicineBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SupplierReturn>(e =>
        {
            e.ToTable("SupplierReturns");
            Money(e);
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_SupplierReturns_Idempotency");
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SupplierReturnItem>(e =>
        {
            e.ToTable("SupplierReturnItems", t => t.HasCheckConstraint("CK_SupplierReturnItems_Quantity", "\"Quantity\" > 0"));
            Money(e);
            e.HasOne(x => x.SupplierReturn).WithMany(x => x.Items).HasForeignKey(x => x.SupplierReturnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MedicineBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SupplierLedgerEntry>(e =>
        {
            e.ToTable("SupplierLedger");
            Money(e);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.SupplierId, x.BusinessDate });
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<StockReceipt>().WithMany().HasForeignKey(x => x.StockReceiptId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SupplierPayment>().WithMany().HasForeignKey(x => x.SupplierPaymentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SupplierReturn>().WithMany().HasForeignKey(x => x.SupplierReturnId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SupplierPayment>(e =>
        {
            e.ToTable("SupplierPayments", t => t.HasCheckConstraint("CK_SupplierPayments_Amount", "\"Amount\" > 0"));
            Money(e);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_SupplierPayments_Idempotency");
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PharmacySettings>(e =>
        {
            e.ToTable("PharmacySettings", t => t.HasCheckConstraint("CK_PharmacySettings_Singleton", "\"Id\" = 1"));
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.MedicineServiceItem).WithMany().HasForeignKey(x => x.MedicineServiceItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GovernmentIssueServiceItem).WithMany().HasForeignKey(x => x.GovernmentIssueServiceItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
