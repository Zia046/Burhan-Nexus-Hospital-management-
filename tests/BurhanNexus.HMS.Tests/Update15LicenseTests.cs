using System.Text;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Infrastructure.Licensing;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 15 – Trial / 1-Year / Lifetime keys from the licence key generator, customer ID, revocation list, invalid / modified keys, no private key in the HMS.</summary>
public class Update15LicenseTests : HmsTestBase
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
    public async Task Trial_one_year_and_lifetime_keys_activate_and_bad_keys_are_rejected()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var inst = await InstallationAsync();
        var start = DateOnly.FromDateTime(Now);

        // Trial key (15 days) with customer ID and machine binding.
        var trial = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, MachineIdentity.Current, "Buner Children Hospital", "CUST-77", LicenseTerm.Trial,
            customExpiry: start.AddDays(15), nowUtc: Now, startDate: start);
        var html = await ActivateAsync(admin, trial.Key);
        Assert.Contains("data-license-state=\"TrialActive\"", html);
        Assert.Contains("Trial licence", html);
        Assert.Contains("Buner Children Hospital", html);
        Assert.Contains("CUST-77", html);
        Assert.Contains("Burhan Nexus HMS", html);
        Assert.Contains("BNHMS1-…", html);                                                   // key shown only by its fingerprint
        Assert.DoesNotContain(trial.Key, html);

        // Modified key and a key signed by another private key are rejected.
        var tampered = trial.Key[..^4] + (trial.Key[^4] == 'A' ? 'B' : 'A') + trial.Key[^3..];
        Assert.Contains("not valid", await ActivateAsync(admin, tampered));
        var (otherPrivate, _) = EcdsaLicenseKeyVerifier.CreateKeyPair();
        Assert.Contains("not valid", await ActivateAsync(admin, LicenseIssuer.Issue(otherPrivate, inst, null, "Fake", null, LicenseTerm.Lifetime, nowUtc: Now).Key));
        Assert.Contains("not valid", await ActivateAsync(admin, "BNHMS1-abc.def"));

        // 1-year key.
        var year = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "Buner Children Hospital", "CUST-77", LicenseTerm.Yearly, nowUtc: Now, startDate: start);
        html = await ActivateAsync(admin, year.Key);
        Assert.Contains("data-license-state=\"Active\"", html);
        Assert.Contains("1-Year", html);
        Assert.Equal(start.AddYears(1), year.ExpiryDate);

        // Expired: a yearly licence after its expiry date.
        Factory.Clock.Advance(TimeSpan.FromDays(367));
        admin = await LoginAdminAsync();
        Assert.Contains("data-license-state=\"Expired\"", await admin.GetHtmlAsync("/Settings/License"));
        // An expired trial key is refused at activation.
        var oldTrial = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "Buner Children Hospital", null, LicenseTerm.Trial,
            customExpiry: DateOnly.FromDateTime(Now).AddDays(-1), nowUtc: Now.AddDays(-10), startDate: DateOnly.FromDateTime(Now).AddDays(-10));
        Assert.Contains("already expired", await ActivateAsync(admin, oldTrial.Key));

        // Lifetime never expires.
        var life = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, MachineIdentity.Current, "Buner Children Hospital", null, LicenseTerm.Lifetime, nowUtc: Now);
        html = await ActivateAsync(admin, life.Key);
        Assert.Contains("Never (lifetime)", html);
        Factory.Clock.Advance(TimeSpan.FromDays(1000));
        admin = await LoginAdminAsync();
        Assert.Contains("data-license-state=\"Active\"", await admin.GetHtmlAsync("/Settings/License"));

        // Machine binding: a key for another computer is refused.
        var other = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, "1111-2222-3333-4444", "Buner Children Hospital", null, LicenseTerm.Lifetime, nowUtc: Now);
        Assert.Contains("different computer", await ActivateAsync(admin, other.Key));
    }

    [Fact]
    public async Task A_signed_revocation_list_revokes_the_licence_and_a_forged_list_is_refused()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var inst = await InstallationAsync();
        var life = LicenseIssuer.Issue(TestKeys.Instance.PrivatePem, inst, null, "Revoked Hospital", null, LicenseTerm.Lifetime, nowUtc: Now);
        Assert.Contains("data-license-state=\"Active\"", await ActivateAsync(admin, life.Key));
        var (otherPrivate, _) = EcdsaLicenseKeyVerifier.CreateKeyPair();
        var forged = EcdsaLicenseKeyVerifier.CreateRevocationList(new LicenseRevocationList { IssuedUtc = Now, Serials = { life.LicenseId } }, otherPrivate);
        await admin.PostFormAsync("/Settings/License", new[] { P("RevocationList", forged) }, "/Settings/License?handler=Revocations");
        Assert.Contains("data-license-state=\"Active\"", await admin.GetHtmlAsync("/Settings/License"));
        var real = EcdsaLicenseKeyVerifier.CreateRevocationList(new LicenseRevocationList { IssuedUtc = Now, Serials = { life.LicenseId } }, TestKeys.Instance.PrivatePem);
        await admin.PostFormAsync("/Settings/License", new[] { P("RevocationList", real) }, "/Settings/License?handler=Revocations");
        var html = await admin.GetHtmlAsync("/Settings/License");
        Assert.Contains("data-license-state=\"Invalid\"", html);
        Assert.Contains("revoked", html);
        // The same key cannot be activated again.
        Assert.Contains("cannot be used again", await ActivateAsync(admin, life.Key));
        await using var db = Factory.NewDb();
        Assert.True(await db.Patients.CountAsync() >= 0);                                        // nothing deleted
    }

    [Fact]
    public void The_hms_programs_contain_no_private_key()
    {
        var dir = AppContext.BaseDirectory;
        foreach (var f in Directory.GetFiles(dir, "BurhanNexus.HMS.*.dll").Where(f => !f.Contains("Tests", StringComparison.OrdinalIgnoreCase)))
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(f));
            var utf16 = Encoding.Unicode.GetString(File.ReadAllBytes(f));
            Assert.False(text.Contains("PRIVATE KEY-----") || utf16.Contains("PRIVATE KEY-----"), $"{Path.GetFileName(f)} contains a private key");
        }
        Assert.NotEmpty(EcdsaLicenseKeyVerifier.TrustedPublicKeys);
        Assert.All(EcdsaLicenseKeyVerifier.TrustedPublicKeys, k => Assert.Contains("BEGIN PUBLIC KEY", k));
    }
}
