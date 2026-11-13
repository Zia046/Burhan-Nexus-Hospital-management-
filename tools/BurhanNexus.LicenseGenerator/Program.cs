// Burhan Nexus licence key generator – FOR BURHAN NEXUS STAFF ONLY.
// Never ship this tool or the private key to customers.
//
// Usage:
//   bnhms-license --key <private-key.pem> --installation <GUID> --type Annual --to "City Hospital" [--years 1]
//   bnhms-license --key <private-key.pem> --installation <GUID> --type Lifetime --to "City Hospital"
using System.Security.Cryptography;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Infrastructure.Licensing;

var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
for (var i = 0; i < args.Length - 1; i += 2) opts[args[i].TrimStart('-')] = args[i + 1];

if (!opts.TryGetValue("key", out var keyPath) || !opts.TryGetValue("installation", out var inst) ||
    !opts.TryGetValue("type", out var typeText) || !opts.TryGetValue("to", out var licensedTo))
{
    Console.WriteLine("Usage: bnhms-license --key <private.pem> --installation <GUID> --type Annual|Lifetime --to \"Hospital name\" [--years 1]");
    return 1;
}
if (!Guid.TryParse(inst, out var installationId)) { Console.Error.WriteLine("Invalid installation ID."); return 1; }
if (!Enum.TryParse<LicenseType>(typeText, true, out var type) || type == LicenseType.Trial) { Console.Error.WriteLine("Type must be Annual or Lifetime."); return 1; }

var years = opts.TryGetValue("years", out var y) && int.TryParse(y, out var n) && n is > 0 and <= 10 ? n : 1;
var now = DateTime.UtcNow;
var payload = new LicenseKeyPayload
{
    Type = type,
    InstallationId = installationId,
    LicensedTo = licensedTo,
    IssuedUtc = now,
    ExpiresUtc = type == LicenseType.Annual ? now.Date.AddYears(years).AddDays(1) : null,
    Serial = "BN-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6))
};

var key = EcdsaLicenseKeyVerifier.CreateKey(payload, File.ReadAllText(keyPath));
if (!new EcdsaLicenseKeyVerifier().TryVerify(key, out _, out var err))
    Console.Error.WriteLine("WARNING: key does not verify against the public key embedded in the product: " + err);

Console.WriteLine($"Licensed to : {licensedTo}");
Console.WriteLine($"Type        : {type}{(payload.ExpiresUtc is { } e ? $" (expires {e:yyyy-MM-dd})" : "")}");
Console.WriteLine($"Serial      : {payload.Serial}");
Console.WriteLine();
Console.WriteLine(key);
return 0;
