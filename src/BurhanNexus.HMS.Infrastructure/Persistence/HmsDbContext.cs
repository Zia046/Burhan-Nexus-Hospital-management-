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

    protected override void OnModelCreating(ModelBuilder b)
    {
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
