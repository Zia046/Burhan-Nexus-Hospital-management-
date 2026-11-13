using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public class SetupAndLoginTests : HmsTestBase
{
    [Fact]
    public async Task Fresh_install_redirects_everything_to_setup_wizard()
    {
        var b = Factory.NewBrowser();
        foreach (var url in new[] { "/", "/Account/Login", "/Users", "/Settings" })
        {
            var r = await b.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.Equal("/Setup", r.Headers.Location?.OriginalString);
        }
        var html = await b.GetHtmlAsync("/Setup");
        Assert.Contains("Initial Setup Wizard", html);
        var api = await b.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, api.StatusCode);
    }

    [Fact]
    public async Task Setup_validates_input_and_saves_everything_centrally()
    {
        var b = Factory.NewBrowser();
        // Invalid: mismatched passwords, bad email, missing type.
        var bad = SetupFields().Where(f => f.Key is not ("HospitalType" or "AdminConfirmPassword" or "Email"))
            .Append(P("AdminConfirmPassword", "different1")).Append(P("Email", "not-an-email"));
        var r1 = await b.PostMultipartAsync("/Setup", bad);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var body = await r1.Content.ReadAsStringAsync();
        Assert.Contains("Please select the hospital type", body);
        Assert.Contains("The passwords do not match", body);

        await SetupAsync("Government");

        await using var db = Factory.NewDb();
        var settings = await db.HospitalSettings.SingleAsync();
        var config = await db.HospitalConfigurations.SingleAsync();
        Assert.Equal("Burhan Test Hospital", settings.HospitalName);
        Assert.Equal("Peshawar", settings.City);
        Assert.Equal(TinyPng, settings.LogoContent);
        Assert.Equal("image/png", settings.LogoContentType);
        Assert.Equal(HospitalType.Government, config.HospitalType);
        Assert.True(config.SetupCompleted);
        Assert.False(config.PharmacyPricingEnabled);
        Assert.True(config.GovernmentStockIssueEnabled);
        Assert.Equal("PKR", config.CurrencyCode);

        var admin = await db.Users.Include(u => u.Credential).SingleAsync();
        Assert.True(admin.IsSystemAdministrator);
        Assert.Equal("admin", admin.Username);
        Assert.DoesNotContain(AdminPassword, admin.Credential!.PasswordHash);
        Assert.StartsWith("AQAAAA", admin.Credential.PasswordHash); // ASP.NET Identity v3 PBKDF2 format

        Assert.True(await db.Departments.CountAsync() >= 10);
        Assert.Single(await AuditAsync(AuditActions.SetupCompleted));

        var license = await db.LicenseInformation.SingleAsync();
        Assert.Equal(LicenseType.Trial, license.LicenseType);
        Assert.InRange((license.ExpiresUtc!.Value - license.TrialStartedUtc).TotalDays, 29.9, 30.1);
    }

    [Fact]
    public async Task Setup_wizard_cannot_run_again_after_completion()
    {
        await SetupAsync();
        var b = Factory.NewBrowser();
        var get = await b.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.Equal("/Account/Login", get.Headers.Location?.OriginalString);

        // Direct POST with a token from the login page must not create a second admin.
        var token = await b.TokenAsync("/Account/Login");
        var content = new FormUrlEncodedContent(SetupFields(admin: "intruder").Append(P("__RequestVerificationToken", token)));
        var post = await b.Http.PostAsync("/Setup", content);
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        await using var db = Factory.NewDb();
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Login_logout_history_and_audit()
    {
        await SetupAsync();
        var b = Factory.NewBrowser();
        var login = await b.GetHtmlAsync("/Account/Login");
        Assert.Contains("Burhan Test Hospital", login);
        Assert.Contains("data-toggle-password", login);
        Assert.Contains("Remember username", login);

        var bad = await LoginAsync(b, AdminUser, "wrong-password1");
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Invalid username or password", await bad.Content.ReadAsStringAsync());

        var ok = await b.PostFormAsync("/Account/Login", new[] { P("Username", "ADMIN"), P("Password", AdminPassword), P("RememberUsername", "true"), P("Workstation", "Reception-1") });
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Contains(ok.Headers.GetValues("Set-Cookie"), c => c.StartsWith("BNHMS.RememberUser=admin"));

        var dash = await b.GetHtmlAsync("/");
        Assert.Contains("Burhan Test Hospital", dash);
        Assert.Contains("Registered users", dash);
        Assert.Contains("Connected", dash);

        var logout = await b.PostFormAsync("/", Array.Empty<KeyValuePair<string, string>>(), "/Account/Logout");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var after = await b.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", after.Headers.Location!.ToString());

        await using var db = Factory.NewDb();
        var history = await db.LoginHistory.OrderBy(l => l.Id).ToListAsync();
        Assert.Contains(history, h => !h.Success && h.FailureReason == "Wrong password");
        var success = Assert.Single(history, h => h.Success);
        Assert.Equal("Reception-1", success.Workstation);
        Assert.NotNull(success.LogoutUtc);
        Assert.NotEmpty(await AuditAsync(AuditActions.LoginFailed));
        Assert.Equal("Reception-1", Assert.Single(await AuditAsync(AuditActions.LoginSucceeded)).Workstation);
        Assert.Single(await AuditAsync(AuditActions.Logout));
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        await SetupAsync();
        var b = Factory.NewBrowser();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++) last = await LoginAsync(b, AdminUser, "wrong-password1");
        Assert.Contains("locked", await last!.Content.ReadAsStringAsync());

        var correct = await LoginAsync(b, AdminUser, AdminPassword);
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
        Assert.Contains("locked", await correct.Content.ReadAsStringAsync());
        Assert.Single(await AuditAsync(AuditActions.AccountLocked));

        Factory.Clock.Advance(TimeSpan.FromMinutes(16));
        var unlocked = await LoginAsync(b, AdminUser, AdminPassword);
        Assert.Equal(HttpStatusCode.Redirect, unlocked.StatusCode);
    }

    [Fact]
    public async Task Idle_session_times_out()
    {
        await SetupAsync();
        var b = await LoginAdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/")).StatusCode);
        Factory.Clock.Advance(TimeSpan.FromMinutes(31));
        var r = await b.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Contains("reason=timeout", r.Headers.Location!.ToString());
        Assert.Single(await AuditAsync(AuditActions.SessionExpired));
    }

    [Fact]
    public async Task Logo_is_served_from_database_and_security_headers_are_set()
    {
        await SetupAsync();
        var b = Factory.NewBrowser();
        var logo = await b.GetAsync("/branding/logo");
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TinyPng, await logo.Content.ReadAsByteArrayAsync());

        var login = await b.GetAsync("/Account/Login");
        Assert.Equal("DENY", login.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("script-src 'self'", login.Headers.GetValues("Content-Security-Policy").Single());

        var health = await b.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task Data_survives_application_restart()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        await CreateUserAsync(admin, "persist.user", "Persist123");
        var name = Factory.DatabaseName;
        var dataDir = Factory.DataDir;
        var backupDir = Factory.BackupDir;
        await Factory.DisposeAsync();

        // "Restart the server": a brand-new application instance on the same database and data folder.
        Factory = new HmsFactory(name, dataDir, backupDir);
        var b = Factory.NewBrowser();
        var r = await LoginAsync(b, "persist.user", "Persist123");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var status = await (await LoginAdminAsync()).GetHtmlAsync("/Settings/License");
        Assert.Contains("Trial (active)", status);
    }
}
