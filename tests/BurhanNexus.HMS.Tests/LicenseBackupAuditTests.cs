using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Licensing;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public class LicenseTests : HmsTestBase
{
    private string Key(Guid installation, LicenseType type, DateTime? expires) =>
        EcdsaLicenseKeyVerifier.CreateKey(new LicenseKeyPayload
        {
            Type = type, InstallationId = installation, LicensedTo = "Burhan Test Hospital",
            IssuedUtc = DateTime.UtcNow, ExpiresUtc = expires, Serial = "TEST-" + Guid.NewGuid().ToString("N")[..6]
        }, TestKeys.Instance.PrivatePem);

    private async Task<Guid> InstallationAsync()
    {
        await using var db = Factory.NewDb();
        return await db.LicenseInformation.Select(l => l.InstallationId).SingleAsync();
    }

    [Fact]
    public async Task Trial_starts_for_30_days_and_expiry_makes_system_read_only_without_losing_data()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var page = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("Trial (active)", page);
        Assert.Contains("30 day(s) remaining", page);
        await CreateUserAsync(admin, "before.expiry", "Before1234");

        Factory.Clock.Advance(TimeSpan.FromDays(31));
        admin = await LoginAdminAsync();
        var expired = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("Trial expired", expired);
        Assert.Contains("Read-only", expired);

        // Reading still works; changes are blocked; nothing was deleted.
        Assert.Contains("before.expiry", await admin.GetHtmlAsync("/Users"));
        var token = await admin.TokenAsync("/Users/Create");
        var blocked = await admin.Http.PostAsync("/Users/Create", new FormUrlEncodedContent(new[]
        {
            P("FullName", "After"), P("Username", "after.expiry"), P("Password", "After12345"), P("ConfirmPassword", "After12345"),
            P("__RequestVerificationToken", token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Equal("/Settings/License?readonly=1", blocked.Headers.Location?.OriginalString);
        await using (var db = Factory.NewDb())
        {
            Assert.False(await db.Users.AnyAsync(u => u.Username == "after.expiry"));
            Assert.True(await db.Users.AnyAsync(u => u.Username == "before.expiry"));
        }

        // Backups are still allowed while read-only.
        var backup = await admin.PostFormAsync("/Backup", new[] { P("Notes", "while expired") }, "/Backup?handler=Create");
        Assert.Equal(HttpStatusCode.Redirect, backup.StatusCode);

        // Activating a lifetime licence restores full access.
        var activate = await admin.PostFormAsync("/Settings/License", new[] { P("ActivationKey", Key(await InstallationAsync(), LicenseType.Lifetime, null)) });
        Assert.Equal(HttpStatusCode.Redirect, activate.StatusCode);
        var active = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("Lifetime licence", active);
        Assert.Contains("Never (lifetime)", active);
        await CreateUserAsync(admin, "after.activation", "After12345");
    }

    [Fact]
    public async Task Invalid_foreign_and_tampered_keys_are_rejected_and_annual_key_activates()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var installation = await InstallationAsync();

        async Task<string> Try(string key)
        {
            await admin.PostFormAsync("/Settings/License", new[] { P("ActivationKey", key) });
            return await admin.GetHtmlAsync("/Settings/License");
        }

        Assert.Contains("not valid", await Try("BNHMS1-garbage.garbage"));
        Assert.Contains("different installation", await Try(Key(Guid.NewGuid(), LicenseType.Lifetime, null)));
        var good = Key(installation, LicenseType.Annual, DateTime.UtcNow.AddYears(1));
        var tampered = good[..^4] + (good[^4] == 'A' ? "BBBB" : "AAAA");
        Assert.Contains("signature is not valid", await Try(tampered));
        Assert.Contains("already expired", await Try(Key(installation, LicenseType.Annual, DateTime.UtcNow.AddDays(-1))));

        var ok = await Try(good);
        Assert.Contains("Annual licence", ok);
        Assert.Contains("Licence activated", ok);
        Assert.Single(await AuditAsync(AuditActions.LicenseActivated));
        Assert.Equal(4, (await AuditAsync(AuditActions.LicenseActivationFailed)).Count);

        // Tampering with the licence row in the database is detected, data is untouched.
        await using (var db = Factory.NewDb())
            await db.Database.ExecuteSqlRawAsync("UPDATE \"LicenseInformation\" SET \"SealedState\" = 'tampered'");
        Factory.Clock.Advance(TimeSpan.FromMinutes(2)); // let the 1-minute status cache expire
        admin = await LoginAdminAsync();
        Assert.Contains("verification required", (await admin.GetHtmlAsync("/Settings/License")).ToLowerInvariant());
    }
}

public class BackupAndAuditTests : HmsTestBase
{
    [Fact]
    public async Task Manual_backup_download_and_protected_restore()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var create = await admin.PostFormAsync("/Backup", new[] { P("Notes", "first") }, "/Backup?handler=Create");
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var list = await admin.GetHtmlAsync("/Backup");
        Assert.Contains("Backup created", list);

        int backupId;
        string fileName;
        await using (var db = Factory.NewDb())
        {
            var b = await db.BackupHistory.SingleAsync();
            Assert.Equal(BackupStatus.Completed, b.Status);
            Assert.True(b.SizeBytes > 0);
            Assert.NotNull(b.Sha256);
            backupId = b.Id;
            fileName = b.FileName;
        }
        Assert.True(File.Exists(Path.Combine(Factory.BackupDir, fileName)));

        var download = await admin.GetAsync($"/Backup?handler=Download&id={backupId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.True((await download.Content.ReadAsByteArrayAsync()).Length > 0);

        // A user created AFTER the backup will disappear when the backup is restored.
        await CreateUserAsync(admin, "created.after.backup", "After12345");

        // Ordinary user cannot see, download or restore backups.
        var staffId = await CreateUserAsync(admin, "ordinary", "Ord#Pass2026");
        await SetPermissionsAsync(admin, staffId, (Perm.DashboardView, "grant"));
        var staff = Factory.NewBrowser();
        await LoginAsync(staff, "ordinary", "Ord#Pass2026");
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync($"/Backup?handler=Download&id={backupId}")).Headers.Location!.ToString());
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync($"/Backup/Restore/{backupId}")).Headers.Location!.ToString());

        // Restore requires confirmation text and the correct password.
        var noConfirm = await admin.PostFormAsync($"/Backup/Restore/{backupId}", new[] { P("Password", AdminPassword), P("Understood", "true"), P("Confirmation", "yes") });
        Assert.Contains("Type RESTORE", await noConfirm.Content.ReadAsStringAsync());
        var wrongPassword = await admin.PostFormAsync($"/Backup/Restore/{backupId}", new[] { P("Password", "wrong-pass1"), P("Understood", "true"), P("Confirmation", "RESTORE") });
        Assert.Contains("password is incorrect", await wrongPassword.Content.ReadAsStringAsync());

        var restore = await admin.PostFormAsync($"/Backup/Restore/{backupId}", new[] { P("Password", AdminPassword), P("Understood", "true"), P("Confirmation", "RESTORE") });
        Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
        Assert.Equal("/Account/Login", restore.Headers.Location?.OriginalString);

        await using (var db = Factory.NewDb())
        {
            Assert.False(await db.Users.AnyAsync(u => u.Username == "created.after.backup"));
            Assert.Contains(await db.BackupHistory.ToListAsync(), b => b.Kind == BackupKind.PreRestoreSafety && b.Status == BackupStatus.Completed);
            Assert.Single(await db.AuditLogs.Where(a => a.Action == AuditActions.BackupRestored).ToListAsync());
        }

        // The system works normally after the restore.
        var again = await LoginAdminAsync();
        Assert.Contains("Burhan Test Hospital", await again.GetHtmlAsync("/"));
    }

    [Fact]
    public async Task Audit_log_is_read_only_and_exportable()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var settings = await admin.GetHtmlAsync("/Settings");
        Assert.Contains("Burhan Test Hospital", settings);

        uint version;
        await using (var db = Factory.NewDb()) version = (await db.HospitalSettings.SingleAsync()).RowVersion;
        var update = await admin.PostFormAsync("/Settings", new[]
        {
            P("Input.HospitalName", "Burhan Renamed Hospital"), P("Input.Address", "Main Road"), P("Input.City", "Mardan"),
            P("Input.Province", "Khyber Pakhtunkhwa"), P("Input.ContactNumber", "0937-123456"), P("Input.Email", "info@renamed.pk"),
            P("RowVersion", version.ToString())
        }, "/Settings?handler=Info");
        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);
        Assert.Contains("Burhan Renamed Hospital", await admin.GetHtmlAsync("/"));
        var change = Assert.Single(await AuditAsync(AuditActions.HospitalSettingsChanged));
        Assert.Contains("Burhan Test Hospital", change.OldValues);
        Assert.Contains("Burhan Renamed Hospital", change.NewValues);

        var audit = await admin.GetHtmlAsync("/Audit");
        Assert.Contains(AuditActions.HospitalSettingsChanged, audit);
        var csv = await admin.GetAsync("/Audit?handler=Export");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Settings.HospitalChanged", await csv.Content.ReadAsStringAsync());

        // The database itself refuses to modify or delete audit records.
        await using var db2 = Factory.NewDb();
        await Assert.ThrowsAnyAsync<Exception>(() => db2.Database.ExecuteSqlRawAsync("DELETE FROM \"AuditLogs\""));
        await Assert.ThrowsAnyAsync<Exception>(() => db2.Database.ExecuteSqlRawAsync("UPDATE \"AuditLogs\" SET \"Action\" = 'x'"));

        var preview = await admin.GetHtmlAsync("/Settings/PrintPreview?doc=receipt");
        Assert.Contains("Burhan Renamed Hospital", preview);
        Assert.Contains("SAMPLE RECEIPT", preview);
    }

    [Fact]
    public async Task Hospital_type_change_requires_password_and_confirmation()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        await admin.PostFormAsync("/Settings/HospitalType", new[] { P("NewType", "Government"), P("ApplyDefaults", "true"), P("Password", "wrong-pass1"), P("Confirmation", "CHANGE") }, "/Settings/HospitalType?handler=ChangeType");
        await using (var db = Factory.NewDb()) Assert.Equal(HospitalType.Private, (await db.HospitalConfigurations.SingleAsync()).HospitalType);

        await admin.PostFormAsync("/Settings/HospitalType", new[] { P("NewType", "Government"), P("ApplyDefaults", "true"), P("Password", AdminPassword), P("Confirmation", "CHANGE") }, "/Settings/HospitalType?handler=ChangeType");
        await using (var db = Factory.NewDb())
        {
            var c = await db.HospitalConfigurations.SingleAsync();
            Assert.Equal(HospitalType.Government, c.HospitalType);
            Assert.False(c.PharmacyPricingEnabled);
            Assert.True(c.GovernmentStockIssueEnabled);
        }
        Assert.Single(await AuditAsync(AuditActions.HospitalTypeChanged));
    }
}
