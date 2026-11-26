using System.Text.Json;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Infrastructure.Licensing;

namespace BurhanNexus.LicenseKeyGenerator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--selftest") return SelfTest(args[1]);
        ApplicationConfiguration.Initialize();
        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK || login.Vault is null || login.PrivateKey is null) return 0;
        Application.Run(new MainForm(login.Vault, login.PrivateKey));
        return 0;
    }

    /// <summary>
    /// CI: works in a temporary folder – creates a signing key, checks the password protection, issues Trial / 1-year / Lifetime keys,
    /// verifies them with the HMS verification code, checks that a changed key is rejected and that a signed revocation list verifies.
    /// </summary>
    private static int SelfTest(string dir)
    {
        var results = new List<string>();
        var ok = true;
        void Check(bool condition, string what) { results.Add((condition ? "PASS " : "FAIL ") + what); ok &= condition; }
        try
        {
            Directory.CreateDirectory(dir);
            Vault.Folder = Path.Combine(dir, "generator");
            var (vault, priv) = Vault.Create("ziaadmin", "Secret#2026x");
            vault.Save();
            var onDisk = File.ReadAllText(Vault.VaultPath);
            Check(!onDisk.Contains("PRIVATE KEY", StringComparison.Ordinal), "private key is not stored in clear text");
            var loaded = Vault.Load();
            Check(loaded.Unlock("ziaadmin", "wrong-password1") is null, "wrong password is refused");
            Check(loaded.Unlock("ziaadmin", "Secret#2026x") == priv, "right password unlocks the key");
            var verifier = new EcdsaLicenseKeyVerifier(loaded.PublicKeyPem);
            var inst = Guid.NewGuid();
            var now = DateTime.UtcNow;
            foreach (var (term, type) in new[] { (LicenseTerm.Trial, LicenseType.Trial), (LicenseTerm.Yearly, LicenseType.Annual), (LicenseTerm.Lifetime, LicenseType.Lifetime) })
            {
                var l = LicenseIssuer.Issue(priv, inst, "ABCD-1234-EF56-7890", "Test Hospital", "CUST-001", term, nowUtc: now);
                var good = verifier.TryVerify(l.Key, out var p, out var err);
                Check(good && p!.Type == type && p.CustomerId == "CUST-001" && p.MachineId == "ABCD-1234-EF56-7890", $"{term} key verifies ({err})");
                Check(term == LicenseTerm.Lifetime ? p?.ExpiresUtc is null : p?.ExpiresUtc > now, $"{term} expiry {(p?.ExpiresUtc?.ToString("yyyy-MM-dd") ?? "never")}");
                var tampered = l.Key[..^6] + (l.Key[^6] == 'A' ? 'B' : 'A') + l.Key[^5..];
                Check(!verifier.TryVerify(tampered, out _, out _), $"changed {term} key is rejected");
                Registry.Record(l);
            }
            Check(Registry.Load().Count == 3, "history has 3 licences");
            var first = Registry.Load()[0];
            Registry.SetStatus(first.LicenseId, "Revoked");
            var rev = EcdsaLicenseKeyVerifier.CreateRevocationList(new LicenseRevocationList { IssuedUtc = now, Serials = Registry.Revoked().ToList() }, priv);
            Check(verifier.TryVerifyRevocations(rev, out var list, out _) && list!.Serials.Single() == first.LicenseId, "signed revocation list verifies");
            var other = Vault.Create("someone", "Another#Pass1").PrivatePem;
            var forged = LicenseIssuer.Issue(other, inst, null, "Forged Hospital", null, LicenseTerm.Lifetime);
            Check(!verifier.TryVerify(forged.Key, out _, out _), "key signed by another private key is rejected");
            File.WriteAllText(Path.Combine(dir, "public-key.pem"), loaded.PublicKeyPem);
        }
        catch (Exception ex) { results.Add("FAIL exception: " + ex); ok = false; }
        File.WriteAllText(Path.Combine(dir, "selftest.json"), JsonSerializer.Serialize(new { ok, results }, new JsonSerializerOptions { WriteIndented = true }));
        return ok ? 0 : 1;
    }
}
