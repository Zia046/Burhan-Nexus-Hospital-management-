using System.Security.Cryptography;
using System.Text;

namespace BurhanNexus.HMS.Infrastructure.Licensing;

/// <summary>
/// Update 13: a stable, anonymous Machine ID for licence binding (works offline).
/// Windows: the OS MachineGuid; Linux: /etc/machine-id; otherwise the computer name. Only a hash is shown – never the raw value.
/// </summary>
public static class MachineIdentity
{
    private static readonly Lazy<string> _current = new(Compute);

    /// <summary>Machine ID of this computer, e.g. "7F3A-91C2-0B5D-E4A8".</summary>
    public static string Current => _current.Value;

    public static string Normalize(string? id) =>
        new string((id ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string Compute()
    {
        string raw = string.Empty;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                raw = key?.GetValue("MachineGuid") as string ?? string.Empty;
            }
            else if (File.Exists("/etc/machine-id")) raw = File.ReadAllText("/etc/machine-id").Trim();
        }
        catch (Exception) { raw = string.Empty; }
        if (raw.Length == 0) raw = Environment.MachineName;
        var hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("BNHMS-MACHINE|" + raw.ToUpperInvariant())))[..16];
        return string.Join('-', Enumerable.Range(0, 4).Select(i => hex.Substring(i * 4, 4)));
    }
}
