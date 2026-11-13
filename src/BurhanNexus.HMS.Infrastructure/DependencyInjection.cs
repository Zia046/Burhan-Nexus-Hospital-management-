using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Infrastructure.Backup;
using BurhanNexus.HMS.Infrastructure.Licensing;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BurhanNexus.HMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHmsInfrastructure(this IServiceCollection services, IConfiguration configuration,
        string? licensePublicKeyOverride = null)
    {
        services.Configure<HmsOptions>(configuration.GetSection(HmsOptions.SectionName));
        var options = configuration.GetSection(HmsOptions.SectionName).Get<HmsOptions>() ?? new HmsOptions();

        var connectionString = configuration.GetConnectionString("HmsDatabase")
            ?? throw new InvalidOperationException("Connection string 'HmsDatabase' is not configured in appsettings.json.");

        services.AddDbContext<HmsDbContext>(o => o.UseNpgsql(connectionString, npg =>
        {
            npg.MigrationsHistoryTable("__EFMigrationsHistory");
        }));

        var dataDir = options.ResolveDataDirectory();
        Directory.CreateDirectory(Path.Combine(dataDir, "keys"));
        services.AddDataProtection()
            .SetApplicationName("BurhanNexus.HMS")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));

        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SetupStateCache>();
        services.AddSingleton<BackupLock>();
        services.AddSingleton<ILicenseKeyVerifier>(new EcdsaLicenseKeyVerifier(licensePublicKeyOverride));

        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IHospitalAdminService, HospitalAdminService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<ISystemStatusService, SystemStatusService>();

        // Phase 2 – patients, OPD, emergency
        services.AddScoped<Patients.HospitalClock>();
        services.AddScoped<Patients.IRegistrationSetupService, Patients.RegistrationSetupService>();
        services.AddScoped<Patients.IPatientService, Patients.PatientService>();
        services.AddScoped<Patients.IEncounterService, Patients.EncounterService>();
        services.AddScoped<Patients.IAppointmentService, Patients.AppointmentService>();
        services.AddScoped<Printing.IPrinterProfileService, Printing.PrinterProfileService>();
        services.AddSingleton<Printing.INetworkPrinter, Printing.NetworkPrinter>();
        return services;
    }

    /// <summary>Applies migrations and synchronises reference data. Never deletes existing data.</summary>
    public static async Task InitializeHmsDatabaseAsync(this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HmsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("HmsDatabase");
        if (migrate)
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync(ct);
        }
        await DatabaseHardening.ApplyAsync(db, ct);
        await PermissionSeeder.SyncAsync(db, ct);
        await Patients.RegistrationSetupService.SeedAsync(db, DateTime.UtcNow, ct);
        if (await db.HospitalConfigurations.AnyAsync(c => c.SetupCompleted, ct))
            await scope.ServiceProvider.GetRequiredService<ILicenseService>().EnsureInitializedAsync(ct);
        logger.LogInformation("Database ready.");
    }
}
