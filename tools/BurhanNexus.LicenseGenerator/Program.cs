// Burhan Nexus licence key generator – FOR BURHAN NEXUS STAFF ONLY.
// Never ship this tool's private key to customers and never commit it to the repository.
//
//   bnhms-license                         interactive mode (asks every question)
//   bnhms-license keygen --out <folder>   create a NEW signing key pair (see warning)
//   bnhms-license issue --key <private.pem> --request "<installation-guid>|<MACHINE-ID>" --to "City Hospital"
//                 --type Lifetime|Yearly|Custom [--years 1] [--expires 2027-12-31] [--customer "Dr. X, 0300…"] [--register licenses.csv]
//   bnhms-license list --register licenses.csv
//   bnhms-license status --register licenses.csv --id BNL-20261118-1A2B3C4D --set Activated|Deactivated|Renewed|Cancelled
//   (old form, still supported) bnhms-license --key <pem> --installation <GUID> --type Annual|Lifetime --to "Hospital" [--years 1]
using System.Globalization;
using BurhanNexus.HMS.Infrastructure.Licensing;

var command = args.Length > 0 && !args[0].StartsWith('-') ? args[0].ToLowerInvariant() : args.Length == 0 ? "interactive" : "issue";
var rest = command == "issue" && args.Length > 0 && args[0].StartsWith('-') ? args : args.Skip(1).ToArray();
var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
for (var i = 0; i < rest.Length; i++)
    if (rest[i].StartsWith("--")) opts[rest[i][2..]] = i + 1 < rest.Length && !rest[i + 1].StartsWith("--") ? rest[++i] : "true";
string? Opt(string name) => opts.TryGetValue(name, out var v) && v.Length > 0 ? v : null;

try
{
    switch (command)
    {
        case "keygen": return KeyGen(Opt("out") ?? ".");
        case "list": return List(Opt("register") ?? "licenses.csv");
        case "status":
            if (Opt("id") is not { } id || Opt("set") is not { } st) { Console.Error.WriteLine("Usage: status --register <csv> --id <licence id> --set <status>"); return 1; }
            var ok = LicenseIssuer.SetStatus(Opt("register") ?? "licenses.csv", id, st);
            Console.WriteLine(ok ? $"{id}: status set to {st}." : $"{id} was not found in the register.");
            return ok ? 0 : 1;
        case "interactive": return Interactive();
        case "issue": return Issue();
        default: Console.Error.WriteLine("Unknown command. Run without arguments for the interactive mode."); return 1;
    }
}
catch (Exception ex) when (ex is ArgumentException or FormatException or IOException or System.Security.Cryptography.CryptographicException) { Console.Error.WriteLine("ERROR: " + ex.Message); return 1; }

int Issue()
{
    var keyPath = Opt("key");
    Guid inst; string? machine = Opt("machine");
    if (Opt("request") is { } req) { if (!LicenseIssuer.TryParseRequest(req, out inst, out var m)) { Console.Error.WriteLine("Invalid activation request code."); return 1; } machine ??= m; }
    else if (!Guid.TryParse(Opt("installation"), out inst)) { Console.Error.WriteLine("Give --request \"<installation>|<machine>\" or --installation <GUID>."); return 1; }
    if (keyPath is null || Opt("to") is not { } to || Opt("type") is not { } typeText)
    {
        Console.Error.WriteLine("Usage: issue --key <private.pem> --request \"<installation>|<machine>\" --to \"Hospital\" --type Lifetime|Yearly|Custom [--years N] [--expires yyyy-MM-dd]");
        return 1;
    }
    var term = typeText.ToLowerInvariant() switch { "lifetime" => LicenseTerm.Lifetime, "yearly" or "annual" => LicenseTerm.Yearly, "custom" => LicenseTerm.Custom, _ => throw new ArgumentException("Type must be Lifetime, Yearly or Custom.") };
    DateOnly? exp = Opt("expires") is { } e ? DateOnly.ParseExact(e, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
    if (exp is not null && term == LicenseTerm.Yearly) term = LicenseTerm.Custom;
    var years = int.TryParse(Opt("years"), out var y) ? y : 1;
    return Output(LicenseIssuer.Issue(File.ReadAllText(keyPath), inst, machine, to, Opt("customer"), term, years, exp), Opt("register") ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(keyPath))!, "licenses.csv"));
}

int Output(IssuedLicense l, string register)
{
    if (!new EcdsaLicenseKeyVerifier().TryVerify(l.Key, out _, out var err))
        Console.Error.WriteLine("WARNING: this key does NOT verify against the public key built into the HMS: " + err);
    LicenseIssuer.AppendToRegister(register, l);
    Console.WriteLine();
    Console.WriteLine($"Licence ID    : {l.LicenseId}");
    Console.WriteLine($"Hospital      : {l.HospitalName}{(l.Customer is null ? "" : $"  ({l.Customer})")}");
    Console.WriteLine($"Type          : {l.Term}");
    Console.WriteLine($"Generated     : {l.GeneratedUtc:yyyy-MM-dd HH:mm} UTC");
    Console.WriteLine($"Expiry        : {(l.ExpiryDate is { } d ? d.ToString("yyyy-MM-dd") : "Never (lifetime)")}");
    Console.WriteLine($"Installation  : {l.InstallationId}");
    Console.WriteLine($"Machine ID    : {l.MachineId ?? "(not bound)"}");
    Console.WriteLine($"Status        : {l.Status}   (register: {register})");
    Console.WriteLine();
    Console.WriteLine("Activation key (send this to the customer):");
    Console.WriteLine(l.Key);
    return 0;
}

int Interactive()
{
    Console.WriteLine("Burhan Nexus HMS – Licence Key Generator");
    Console.WriteLine("=========================================");
    string Ask(string q, string? def = null) { Console.Write(def is null ? $"{q}: " : $"{q} [{def}]: "); var a = Console.ReadLine()?.Trim(); return string.IsNullOrEmpty(a) ? def ?? "" : a; }
    var keyPath = Ask("Private key file", "burhan-nexus-hms-license.private.pem");
    if (!File.Exists(keyPath)) { Console.Error.WriteLine("Private key file not found. Create one with: bnhms-license keygen --out <folder>"); return 1; }
    if (!LicenseIssuer.TryParseRequest(Ask("Activation request code (from the customer's Licence screen)"), out var inst, out var machine)) { Console.Error.WriteLine("Invalid request code."); return 1; }
    var hospital = Ask("Hospital / customer name");
    var customer = Ask("Contact person / phone (optional)", "");
    var t = Ask("Licence type: 1 = Lifetime, 2 = Yearly, 3 = Custom expiry", "2");
    var term = t switch { "1" => LicenseTerm.Lifetime, "3" => LicenseTerm.Custom, _ => LicenseTerm.Yearly };
    var years = term == LicenseTerm.Yearly && int.TryParse(Ask("Years", "1"), out var y) ? y : 1;
    DateOnly? exp = term == LicenseTerm.Custom ? DateOnly.ParseExact(Ask("Expiry date (yyyy-MM-dd)"), "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
    return Output(LicenseIssuer.Issue(File.ReadAllText(keyPath), inst, machine, hospital, customer, term, years, exp),
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(keyPath))!, "licenses.csv"));
}

int KeyGen(string folder)
{
    Directory.CreateDirectory(folder);
    var priv = Path.Combine(folder, "burhan-nexus-hms-license.private.pem");
    var pub = Path.Combine(folder, "burhan-nexus-hms-license.public.pem");
    if (File.Exists(priv)) { Console.Error.WriteLine($"{priv} already exists – it is never overwritten."); return 1; }
    var (p, q) = EcdsaLicenseKeyVerifier.CreateKeyPair();
    File.WriteAllText(priv, p);
    File.WriteAllText(pub, q);
    Console.WriteLine($"Private key : {priv}   <- KEEP SECRET. Back it up offline. Never e-mail, ship or commit it.");
    Console.WriteLine($"Public key  : {pub}");
    Console.WriteLine();
    Console.WriteLine("WARNING: the HMS accepts only keys signed by the private key that matches the public key built into it");
    Console.WriteLine("(EcdsaLicenseKeyVerifier.VendorPublicKeyPem). A NEW key pair means a new HMS build, and keys from the old pair stop working there.");
    Console.WriteLine();
    Console.WriteLine(q);
    return 0;
}

int List(string register)
{
    var rows = LicenseIssuer.ReadRegister(register);
    if (rows.Count == 0) { Console.WriteLine("No licences in the register."); return 0; }
    Console.WriteLine($"{"Licence ID",-24} {"Generated",-19} {"Type",-9} {"Expiry",-10} {"Status",-11} Hospital");
    foreach (var r in rows) Console.WriteLine($"{r[0],-24} {r[1],-19} {r[4],-9} {r[5],-10} {r[8],-11} {r[2]}");
    return 0;
}
