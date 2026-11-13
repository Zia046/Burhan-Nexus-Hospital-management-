using System.Data;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Licensing;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BurhanNexus.HMS.Infrastructure.Services;

public sealed class SetupInput
{
    // Hospital
    public string HospitalName { get; set; } = string.Empty;
    public HospitalType HospitalType { get; set; }
    public string? RegistrationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public byte[]? LogoContent { get; set; }
    public string? LogoContentType { get; set; }

    // Administrator (first installation only)
    public string AdminFullName { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string? AdminContactNumber { get; set; }

    // Preferences
    public string CurrencyCode { get; set; } = "PKR";
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string TimeFormat { get; set; } = "hh:mm tt";
    public string DefaultLanguage { get; set; } = "en";
    public string? ReceiptPrinterName { get; set; }
    public string ReceiptPaperSize { get; set; } = "80mm";
    public string? ReportPrinterName { get; set; }
    public string ReportPaperSize { get; set; } = "A4";
}

public enum SetupState
{
    /// <summary>Fresh installation: wizard creates hospital and first administrator.</summary>
    NotConfigured,
    /// <summary>Administrator reset the setup: wizard re-collects hospital details, existing users are kept.</summary>
    ResetRequested,
    Completed
}

public interface ISetupService
{
    Task<SetupState> GetStateAsync(CancellationToken ct = default);
    Task<ServiceResult> CompleteAsync(SetupInput input, CancellationToken ct = default);
    Task<ServiceResult> RequestResetAsync(CancellationToken ct = default);
}

public sealed class SetupService : ISetupService
{
    public static readonly string[] DefaultDepartments =
    {
        "Administration", "Reception", "OPD", "Emergency", "Laboratory", "Pharmacy", "X-Ray", "Ultrasound", "Minor OT", "Accounts"
    };

    private readonly HmsDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ILicenseService _license;
    private readonly IAuditService _audit;
    private readonly ISettingsService _settings;
    private readonly IRequestContext _ctx;
    private readonly TimeProvider _time;
    private readonly SetupStateCache _cache;

    public SetupService(HmsDbContext db, IPasswordService passwords, ILicenseService license, IAuditService audit,
        ISettingsService settings, IRequestContext ctx, TimeProvider time, SetupStateCache cache)
    {
        _cache = cache;
        _db = db;
        _passwords = passwords;
        _license = license;
        _audit = audit;
        _settings = settings;
        _ctx = ctx;
        _time = time;
    }

    public async Task<SetupState> GetStateAsync(CancellationToken ct = default)
    {
        if (_cache.Completed) return SetupState.Completed;
        var config = await _db.HospitalConfigurations.AsNoTracking().Select(c => new { c.SetupCompleted }).FirstOrDefaultAsync(ct);
        if (config is null) return SetupState.NotConfigured;
        if (config.SetupCompleted)
        {
            _cache.Completed = true;
            return SetupState.Completed;
        }
        return await _db.Users.AnyAsync(ct) ? SetupState.ResetRequested : SetupState.NotConfigured;
    }

    public async Task<ServiceResult> CompleteAsync(SetupInput input, CancellationToken ct = default)
    {
        var state = await GetStateAsync(ct);
        if (state == SetupState.Completed) return ServiceResult.Fail("Setup has already been completed.");

        var errors = Validate(input, state == SetupState.NotConfigured);
        if (errors.Count > 0) return ServiceResult.Fail(errors.ToArray());

        var now = _time.GetUtcNow().UtcDateTime;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var settings = await _db.HospitalSettings.FirstOrDefaultAsync(ct);
            if (settings is null)
            {
                settings = new HospitalSettings { Id = HospitalSettings.SingletonId, CreatedUtc = now };
                _db.HospitalSettings.Add(settings);
            }
            settings.HospitalName = input.HospitalName.Trim();
            settings.RegistrationNumber = Clean(input.RegistrationNumber);
            settings.Address = input.Address.Trim();
            settings.City = input.City.Trim();
            settings.Province = input.Province.Trim();
            settings.ContactNumber = input.ContactNumber.Trim();
            settings.Email = input.Email.Trim();
            settings.Website = Clean(input.Website);
            if (input.LogoContent is { Length: > 0 })
            {
                settings.LogoContent = input.LogoContent;
                settings.LogoContentType = input.LogoContentType;
                settings.LogoUpdatedUtc = now;
            }
            settings.ReceiptHeader ??= "Thank you for choosing " + settings.HospitalName;
            settings.ReceiptFooter ??= "Please keep this receipt for your record.";
            settings.ReportFooter ??= "This is a computer generated document.";
            settings.UpdatedUtc = now;

            var config = await _db.HospitalConfigurations.FirstOrDefaultAsync(ct);
            if (config is null)
            {
                config = new HospitalConfiguration { Id = HospitalConfiguration.SingletonId, CreatedUtc = now };
                _db.HospitalConfigurations.Add(config);
            }
            config.HospitalType = input.HospitalType;
            config.ApplyHospitalTypeDefaults();
            config.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
            config.CurrencySymbol = config.CurrencyCode == "PKR" ? "Rs." : config.CurrencyCode;
            config.TimeZoneId = input.TimeZoneId;
            config.DateFormat = input.DateFormat;
            config.TimeFormat = input.TimeFormat;
            config.DefaultLanguage = input.DefaultLanguage;
            config.ReceiptPrinterName = Clean(input.ReceiptPrinterName);
            config.ReceiptPaperSize = input.ReceiptPaperSize;
            config.ReportPrinterName = Clean(input.ReportPrinterName);
            config.ReportPaperSize = input.ReportPaperSize;
            config.UpdatedUtc = now;

            User? admin = null;
            if (state == SetupState.NotConfigured)
            {
                admin = new User
                {
                    FullName = input.AdminFullName.Trim(),
                    Username = input.AdminUsername.Trim(),
                    NormalizedUsername = User.Normalize(input.AdminUsername),
                    ContactNumber = Clean(input.AdminContactNumber),
                    Designation = "System Administrator",
                    IsActive = true,
                    IsSystemAdministrator = true,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                    Credential = new UserCredential
                    {
                        PasswordHash = _passwords.Hash(input.AdminPassword),
                        PasswordChangedUtc = now
                    }
                };
                _db.Users.Add(admin);

                var existingDepartments = await _db.Departments.Select(d => d.Name).ToListAsync(ct);
                foreach (var name in DefaultDepartments.Except(existingDepartments, StringComparer.OrdinalIgnoreCase))
                    _db.Departments.Add(new Department { Name = name, IsActive = true, CreatedUtc = now, UpdatedUtc = now });
            }

            await _db.SaveChangesAsync(ct);

            if (admin is not null)
            {
                var adminDept = await _db.Departments.FirstOrDefaultAsync(d => d.Name == "Administration", ct);
                if (adminDept is not null)
                    _db.UserDepartments.Add(new UserDepartment { UserId = admin.Id, DepartmentId = adminDept.Id, AssignedUtc = now });
            }

            config.SetupCompleted = true;
            config.SetupCompletedUtc = now;
            config.SetupCompletedByUserId = admin?.Id ?? _ctx.UserId;

            _db.AuditLogs.Add(new AuditLog
            {
                TimestampUtc = now,
                UserId = admin?.Id ?? _ctx.UserId,
                Username = admin?.Username ?? _ctx.Username,
                Action = AuditActions.SetupCompleted,
                EntityType = "HospitalConfiguration",
                EntityId = "1",
                Description = state == SetupState.NotConfigured
                    ? "Initial setup completed and first administrator created"
                    : "Setup wizard completed again after reset",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    settings.HospitalName, HospitalType = config.HospitalType.ToString(), settings.City, settings.Province,
                    config.CurrencyCode, config.TimeZoneId, Administrator = admin?.Username
                }),
                IpAddress = _ctx.IpAddress,
                Workstation = _ctx.Workstation,
                UserAgent = AuditService.Truncate(_ctx.UserAgent, 500)
            });
            await _db.SaveChangesAsync(ct);
            await _license.EnsureInitializedAsync(ct);
            await tx.CommitAsync(ct);
            await Patients.RegistrationSetupService.SeedAsync(_db, now, ct);
        }
        catch (Exception ex) when (ex is DbUpdateException { InnerException: PostgresException pg } && pg.SqlState is "23505" or "40001"
                                   || ex is PostgresException { SqlState: "40001" })
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            return ServiceResult.Fail("Setup was completed from another computer at the same time. Please refresh the page.");
        }

        _settings.Invalidate();
        _cache.Completed = true;
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RequestResetAsync(CancellationToken ct = default)
    {
        var config = await _db.HospitalConfigurations.FirstOrDefaultAsync(ct);
        if (config is null || !config.SetupCompleted) return ServiceResult.Fail("Setup is not completed.");
        config.SetupCompleted = false;
        config.UpdatedUtc = _time.GetUtcNow().UtcDateTime;
        _audit.Add(AuditActions.SetupReset, "HospitalConfiguration", "1",
            "Administrator reset the setup wizard. Existing users and data are kept.");
        await _db.SaveChangesAsync(ct);
        _cache.Completed = false;
        _settings.Invalidate();
        return ServiceResult.Ok();
    }

    private List<string> Validate(SetupInput i, bool requireAdmin)
    {
        var e = new List<string>();
        void Required(string? v, string name, int max)
        {
            if (string.IsNullOrWhiteSpace(v)) e.Add($"{name} is required.");
            else if (v.Trim().Length > max) e.Add($"{name} must not exceed {max} characters.");
        }

        Required(i.HospitalName, "Hospital name", 200);
        if (!Enum.IsDefined(i.HospitalType)) e.Add("Please select the hospital type.");
        Required(i.Address, "Hospital address", 500);
        Required(i.City, "City", 100);
        Required(i.Province, "Province", 100);
        Required(i.ContactNumber, "Contact number", 50);
        Required(i.Email, "Email address", 200);
        if (!string.IsNullOrWhiteSpace(i.Email) && !Validation.IsValidEmail(i.Email)) e.Add("Email address is not valid.");
        if (!string.IsNullOrWhiteSpace(i.ContactNumber) && !Validation.IsValidPhone(i.ContactNumber)) e.Add("Contact number is not valid.");
        if (!string.IsNullOrWhiteSpace(i.Website) && !Validation.IsValidUrl(i.Website)) e.Add("Website address is not valid.");
        if ((i.RegistrationNumber?.Length ?? 0) > 100) e.Add("Registration number must not exceed 100 characters.");

        if (requireAdmin)
        {
            Required(i.AdminFullName, "Administrator full name", 150);
            Required(i.AdminUsername, "Username", 64);
            if (!string.IsNullOrWhiteSpace(i.AdminUsername) && !Validation.IsValidUsername(i.AdminUsername))
                e.Add("Username may contain only letters, numbers, dot, underscore and hyphen (3–64 characters).");
            e.AddRange(_passwords.Validate(i.AdminPassword, i.AdminUsername));
            if (!string.IsNullOrWhiteSpace(i.AdminContactNumber) && !Validation.IsValidPhone(i.AdminContactNumber))
                e.Add("Administrator contact number is not valid.");
        }

        if (!Options.Currencies.Contains(i.CurrencyCode)) e.Add("Please select a supported currency.");
        if (!Options.TimeZones.Contains(i.TimeZoneId)) e.Add("Please select a supported time zone.");
        if (!Options.DateFormats.Contains(i.DateFormat)) e.Add("Please select a supported date format.");
        if (!Options.TimeFormats.Contains(i.TimeFormat)) e.Add("Please select a supported time format.");
        if (!Options.Languages.ContainsKey(i.DefaultLanguage)) e.Add("Please select a supported language.");
        if (!Options.ReceiptPaperSizes.Contains(i.ReceiptPaperSize)) e.Add("Please select a receipt paper size.");
        if (!Options.ReportPaperSizes.Contains(i.ReportPaperSize)) e.Add("Please select a report paper size.");
        if ((i.ReceiptPrinterName?.Length ?? 0) > 200 || (i.ReportPrinterName?.Length ?? 0) > 200) e.Add("Printer name is too long.");
        return e;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Process-wide cache so completed setups are not re-checked on every request.</summary>
public sealed class SetupStateCache
{
    private volatile bool _completed;
    public bool Completed { get => _completed; set => _completed = value; }
}
