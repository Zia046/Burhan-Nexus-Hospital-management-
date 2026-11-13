using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;

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

    public const string PatientNumberSequence = "patient_number_seq";
    public const string OpdNumberSequence = "opd_number_seq";
    public const string EmergencyNumberSequence = "emergency_number_seq";

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");
        // Database sequences guarantee unique numbers even when many computers register at the same moment.
        b.HasSequence<long>(PatientNumberSequence).StartsAt(1).IncrementsBy(1);
        b.HasSequence<long>(OpdNumberSequence).StartsAt(1).IncrementsBy(1);
        b.HasSequence<long>(EmergencyNumberSequence).StartsAt(1).IncrementsBy(1);

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
        });

        b.Entity<ServiceItem>(e =>
        {
            e.ToTable("ServiceItems");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DefaultFee).HasPrecision(12, 2);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
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

        b.Entity<BackupHistory>(e =>
        {
            e.ToTable("BackupHistory");
            e.HasIndex(x => x.FileName).IsUnique();
            e.HasIndex(x => x.StartedUtc);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        });
    }
}
