using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Infrastructure.Licensing;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 13 – licence generator (Lifetime / Yearly / Custom, licence ID, register), activation screen, expiry warning, clock tampering, deactivation.</summary>
public class Update13LicenseTests : HmsTestBase
{
    private DateTime Now => Factory.Clock.GetUtcNow().UtcDateTime;

    private async Task<Guid> InstallationAsync()
    {
        await using var db = Factory.NewDb();
        return await db.LicenseInformation.Select(l => l.InstallationId).SingleAsync();
    }

    private static async Task<string> ActivateAsync(Browser admin, string key)
    {
        await admin.PostFormAsync("/Settings/License", new[] { P("ActivationKey", key) });
        return await admin.GetHtmlAsync("/Settings/License");
    }

    [Fact]
    public void Update13_generator_issues_lifetime_yearly_and_custom_keys_with_licence_id_and_register()
    {
        var verifier = new EcdsaLicenseKeyVerifier(TestKeys.Instance.PublicPem);
        var inst = Guid.NewGuid();
        var now = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(LicenseIssuer.TryParseRequest($"{inst}|ab12-cd34-ef56-7890", out var parsed, out var machine));
        Assert.Equal(inst, parsed);
        Assert.Equal("AB12-CD34-EF56-7890", machine);

        var life = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, machine, "City Hospital", "Dr. Owner", LicenseTerm.Lifetime, nowUtc: now);
        var year = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "City Hospital", null, LicenseTerm.Yearly, 1, nowUtc: now);
        var custom = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "City Hospital", null, LicenseTerm.Custom, customExpiry: new DateOnly(2027, 3, 31), nowUtc: now);
        Assert.Null(life.ExpiresUtc);
        Assert.Equal(new DateOnly(2027, 11, 18), year.ExpiryDate);
        Assert.Equal(new DateOnly(2027, 3, 31), custom.ExpiryDate);
        Assert.Equal(3, new[] { life.LicenseId, year.LicenseId, custom.LicenseId }.Distinct().Count());
        Assert.All(new[] { life, year, custom }, l => Assert.StartsWith("BNL-20261118-", l.LicenseId));

        Assert.True(verifier.TryVerify(custom.Key, out var p, out _));
        Assert.Equal("Custom", p!.Term);
        Assert.Equal(LicenseType.Annual, p.Type);
        Assert.Equal(custom.LicenseId, p.Serial);
        Assert.True(verifier.TryVerify(life.Key, out var lp, out _));
        Assert.Equal("AB12-CD34-EF56-7890", lp!.MachineId);
        // Any change to a key – or a key signed with another private key – is refused.
        Assert.False(verifier.TryVerify(custom.Key.Replace(".", "x."), out _, out _));
        var (otherPrivate, _) = EcdsaLicenseKeyVerifier.CreateKeyPair();
        Assert.False(verifier.TryVerify(LicenseIssuer.Issue(otherPrivate, inst, null, "Fake Hospital", null, LicenseTerm.Lifetime).Key, out _, out var fakeErr));
        Assert.Contains("signature", fakeErr);
        Assert.Throws<ArgumentException>(() => LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "City Hospital", null, LicenseTerm.Custom, customExpiry: new DateOnly(2026, 11, 1), nowUtc: now));

        var register = Path.Combine(Path.GetTempPath(), $"bnhms-register-{Guid.NewGuid():N}.csv");
        try
        {
            foreach (var l in new[] { life, year, custom }) LicenseIssuer.AppendToRegister(register, l);
            var rows = LicenseIssuer.ReadRegister(register);
            Assert.Equal(3, rows.Count);
            Assert.Equal("Never", rows[0][5]);
            Assert.Equal("2027-03-31", rows[2][5]);
            Assert.All(rows, r => Assert.Equal("Issued", r[8]));
            Assert.DoesNotContain(custom.Key, File.ReadAllText(register)); // only a fingerprint is kept
            Assert.True(LicenseIssuer.SetStatus(register, custom.LicenseId, "Activated"));
            Assert.Equal("Activated", LicenseIssuer.ReadRegister(register)[2][8]);
        }
        finally { File.Delete(register); }
    }

    [Fact]
    public async Task Update13_custom_licence_activates_warns_before_expiry_and_expires_automatically()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var page = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("data-machine-id", page);
        Assert.Contains(MachineIdentity.Current, page);
        Assert.Contains($"{await InstallationAsync()}|{MachineIdentity.Current}", page);

        var expiry = DateOnly.FromDateTime(Now).AddDays(40);
        var lic = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, await InstallationAsync(), MachineIdentity.Current, "Burhan Test Hospital", null,
            LicenseTerm.Custom, customExpiry: expiry, nowUtc: Now);
        var html = await ActivateAsync(admin, lic.Key);
        Assert.Contains("Licence activated", html);
        Assert.Contains(lic.LicenseId, html);
        Assert.Contains("Licence (custom period)", html);
        Assert.DoesNotContain("data-license-warning", html);

        Factory.Clock.Advance(TimeSpan.FromDays(20));
        admin = await LoginAdminAsync();
        html = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("data-license-warning", html);
        Assert.Contains("data-license-expiring", await admin.GetHtmlAsync("/"));

        Factory.Clock.Advance(TimeSpan.FromDays(25));
        admin = await LoginAdminAsync();
        html = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("data-license-state=\"Expired\"", html);
        Assert.Contains("Read-only", html);
    }

    [Fact]
    public async Task Update13_key_for_another_computer_is_refused_and_setting_the_clock_back_makes_the_system_read_only()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var inst = await InstallationAsync();
        var other = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, "0000-0000-0000-0000", "Burhan Test Hospital", null, LicenseTerm.Lifetime, nowUtc: Now);
        Assert.Contains("different computer", await ActivateAsync(admin, other.Key));
        var yearly = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, MachineIdentity.Current, "Burhan Test Hospital", null, LicenseTerm.Yearly, nowUtc: Now);
        Assert.Contains("Yearly licence", await ActivateAsync(admin, yearly.Key));

        Factory.Clock.Advance(TimeSpan.FromDays(3));
        admin = await LoginAdminAsync();
        Assert.Contains("data-license-state=\"Active\"", await admin.GetHtmlAsync("/Settings/License"));
        // Someone sets the server date back by three days to stretch the licence.
        Factory.Clock.Advance(TimeSpan.FromDays(-3));
        var html = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("data-license-state=\"Invalid\"", html);
        Assert.Contains("clock appears to have been set back", html);
        var write = await admin.PostFormAsync("/Settings/Opd", new[] { P("OpdForm.TokenPrefix", "X") });
        Assert.Contains("readonly=1", write.Headers.Location?.OriginalString ?? "");
        // Correct date again → back to normal, no data lost.
        Factory.Clock.Advance(TimeSpan.FromDays(3).Add(TimeSpan.FromMinutes(2)));
        Assert.Contains("data-license-state=\"Active\"", await admin.GetHtmlAsync("/Settings/License"));
    }

    [Fact]
    public async Task Update13_administrator_deactivates_and_renews_the_licence()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var inst = await InstallationAsync();
        var first = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "Burhan Test Hospital", null, LicenseTerm.Yearly, nowUtc: Now);
        await ActivateAsync(admin, first.Key);

        // Without the confirmation nothing happens.
        await admin.PostFormAsync("/Settings/License", new[] { P("Reason", "Moving to new server") }, "/Settings/License?handler=Deactivate");
        Assert.Contains("data-license-state=\"Active\"", await admin.GetHtmlAsync("/Settings/License"));
        await admin.PostFormAsync("/Settings/License", new[] { P("Reason", "Moving to new server"), P("Confirm", "true") }, "/Settings/License?handler=Deactivate");
        var html = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("Licence deactivated", html);
        Assert.Contains("data-license-state=\"Invalid\"", html);
        Assert.Single(await AuditAsync(AuditActions.LicenseDeactivated));
        // The deactivated licence ID can never be activated again here; a new key renews the licence.
        Assert.Contains("cannot be used again", await ActivateAsync(admin, first.Key));
        var renewed = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "Burhan Test Hospital", null, LicenseTerm.Lifetime, nowUtc: Now);
        html = await ActivateAsync(admin, renewed.Key);
        Assert.Contains("Lifetime licence", html);
        Assert.Contains(renewed.LicenseId, html);

        // A user without the licence permission cannot deactivate.
        var staffId = await CreateUserAsync(admin, "lic.viewer", "Viewer#2026x");
        var staff = Factory.NewBrowser();
        await LoginAsync(staff, "lic.viewer", "Viewer#2026x");
        var denied = await staff.PostFormAsync("/Account/ChangePassword", new[] { P("Reason", "x"), P("Confirm", "true") }, "/Settings/License?handler=Deactivate");
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        Assert.Contains("Lifetime licence", await admin.GetHtmlAsync("/Settings/License"));
        _ = staffId;
    }
}
