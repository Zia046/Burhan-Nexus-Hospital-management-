using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;

namespace BurhanNexus.HMS.Infrastructure.Licensing;

/// <summary>Licence term chosen by Burhan Nexus when a key is generated.</summary>
public enum LicenseTerm { Lifetime, Yearly, Custom, /* Update 15 */ Trial }

/// <summary>One issued licence (a row of the vendor's licence register).</summary>
public sealed record IssuedLicense(
    string LicenseId, DateTime GeneratedUtc, string HospitalName, string? Customer, LicenseTerm Term, DateTime? ExpiresUtc,
    Guid InstallationId, string? MachineId, string Status, string Key)
{
    public DateOnly? ExpiryDate => ExpiresUtc is { } e ? DateOnly.FromDateTime(e.AddDays(-1)) : null;
}

/// <summary>
/// Update 13: vendor-side licence issuing (used ONLY by the Burhan Nexus licence generator and by tests).
/// It needs the vendor PRIVATE key, which never leaves Burhan Nexus and is never shipped with the HMS.
/// </summary>
public static class LicenseIssuer
{
    public static readonly string[] RegisterColumns =
        { "LicenseId", "GeneratedUtc", "HospitalName", "Customer", "Type", "ExpiryDate", "InstallationId", "MachineId", "Status", "KeyFingerprint" };

    /// <summary>Splits an activation request code "installation-guid|MACHINE-ID" from the customer's licence screen.</summary>
    public static bool TryParseRequest(string? code, out Guid installationId, out string? machineId)
    {
        machineId = null;
        var parts = (code ?? string.Empty).Trim().Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2 || !Guid.TryParse(parts[0], out installationId)) { installationId = Guid.Empty; return false; }
        if (parts.Length == 2) machineId = parts[1].ToUpperInvariant();
        return true;
    }

    public static IssuedLicense Issue(string privateKeyPem, Guid installationId, string? machineId, string hospitalName, string? customer,
        LicenseTerm term, int years = 1, DateOnly? customExpiry = null, DateTime? nowUtc = null, DateOnly? startDate = null)
    {
        if (installationId == Guid.Empty) throw new ArgumentException("The Installation ID is required.");
        hospitalName = (hospitalName ?? string.Empty).Trim();
        if (hospitalName.Length is < 2 or > 200) throw new ArgumentException("Enter the hospital / customer name (2–200 characters).");
        var now = nowUtc ?? DateTime.UtcNow;
        // Update 15: the activation (start) date chosen in the generator; the 1-year expiry counts from it.
        var start = startDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) ?? now.Date;
        if (term == LicenseTerm.Trial && customExpiry is null) customExpiry = DateOnly.FromDateTime(start.AddDays(30));
        DateTime? expires = term switch
        {
            LicenseTerm.Lifetime => null,
            LicenseTerm.Yearly when years is >= 1 and <= 10 => start.AddYears(years).AddDays(1),
            LicenseTerm.Yearly => throw new ArgumentException("Years must be 1–10."),
            _ when customExpiry is DateOnly d && d.ToDateTime(TimeOnly.MinValue) > now.Date => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1),
            _ => throw new ArgumentException("A custom licence needs an expiry date after today.")
        };
        var payload = new LicenseKeyPayload
        {
            Type = term switch { LicenseTerm.Lifetime => LicenseType.Lifetime, LicenseTerm.Trial => LicenseType.Trial, _ => LicenseType.Annual },
            InstallationId = installationId,
            LicensedTo = hospitalName,
            IssuedUtc = now,
            ExpiresUtc = expires,
            Serial = $"BNL-{now:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}",
            Term = term.ToString(),
            MachineId = string.IsNullOrWhiteSpace(machineId) ? null : machineId.Trim().ToUpperInvariant(),
            CustomerId = string.IsNullOrWhiteSpace(customer) ? null : customer.Trim()
        };
        var key = EcdsaLicenseKeyVerifier.CreateKey(payload, privateKeyPem);
        return new IssuedLicense(payload.Serial, now, hospitalName, string.IsNullOrWhiteSpace(customer) ? null : customer.Trim(), term, expires,
            installationId, payload.MachineId, "Issued", key);
    }

    /// <summary>Appends the licence to the CSV register (created with a header when missing). The key itself is not stored – only its fingerprint.</summary>
    public static void AppendToRegister(string path, IssuedLicense l)
    {
        var exists = File.Exists(path);
        var sb = new StringBuilder();
        if (!exists) sb.AppendLine(string.Join(',', RegisterColumns));
        sb.AppendLine(string.Join(',', new[]
        {
            l.LicenseId, l.GeneratedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), l.HospitalName, l.Customer ?? "", l.Term.ToString(),
            l.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "Never", l.InstallationId.ToString(), l.MachineId ?? "", l.Status,
            EcdsaLicenseKeyVerifier.Fingerprint(l.Key)
        }.Select(Csv)));
        File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
    }

    public static List<string[]> ReadRegister(string path) =>
        File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8).Skip(1).Where(l => l.Length > 0).Select(ParseCsv).ToList() : new();

    /// <summary>Sets the status (Issued / Activated / Deactivated / Renewed / Cancelled) of a licence in the register.</summary>
    public static bool SetStatus(string path, string licenseId, string status)
    {
        if (!File.Exists(path)) return false;
        var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
        var found = false;
        for (var i = 1; i < lines.Count; i++)
        {
            var f = ParseCsv(lines[i]);
            if (f.Length < RegisterColumns.Length || !f[0].Equals(licenseId, StringComparison.OrdinalIgnoreCase)) continue;
            f[8] = status;
            lines[i] = string.Join(',', f.Select(Csv));
            found = true;
        }
        if (found) File.WriteAllLines(path, lines, Encoding.UTF8);
        return found;
    }

    private static string Csv(string v) => v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

    private static string[] ParseCsv(string line)
    {
        var res = new List<string>();
        var sb = new StringBuilder();
        var q = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (q)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') q = false;
                else sb.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == ',') { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        res.Add(sb.ToString());
        return res.ToArray();
    }
}
