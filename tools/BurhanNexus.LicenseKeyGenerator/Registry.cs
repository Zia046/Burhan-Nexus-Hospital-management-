using BurhanNexus.HMS.Infrastructure.Licensing;

namespace BurhanNexus.LicenseKeyGenerator;

/// <summary>One row of the licence history.</summary>
internal sealed record HistoryRow(string LicenseId, string Generated, string Hospital, string Customer, string Type, string Expiry, string Installation, string MachineId, string Status);

/// <summary>Licence history (CSV register next to the key) and a copy of every generated key (keys are not secret – they only work for their installation).</summary>
internal static class Registry
{
    public static void Record(IssuedLicense l)
    {
        Directory.CreateDirectory(Vault.IssuedFolder);
        LicenseIssuer.AppendToRegister(Vault.RegisterPath, l);
        File.WriteAllText(Path.Combine(Vault.IssuedFolder, l.LicenseId + ".txt"), l.Key);
    }

    public static IReadOnlyList<HistoryRow> Load() =>
        LicenseIssuer.ReadRegister(Vault.RegisterPath).Where(f => f.Length >= 9)
            .Select(f => new HistoryRow(f[0], f[1], f[2], f[3], f[4] == "Yearly" ? "1 Year" : f[4], f[5], f[6], f[7], f[8])).ToList();

    public static string? KeyOf(string licenseId)
    {
        var p = Path.Combine(Vault.IssuedFolder, licenseId + ".txt");
        return File.Exists(p) ? File.ReadAllText(p) : null;
    }

    public static bool SetStatus(string licenseId, string status) => LicenseIssuer.SetStatus(Vault.RegisterPath, licenseId, status);

    public static IEnumerable<string> Revoked() => Load().Where(r => r.Status == "Revoked").Select(r => r.LicenseId);
}
