using System.Text.Json;

namespace BurhanNexus.Launcher;

/// <summary>
/// Where the central HMS server is. The installer writes the machine-wide file
/// (%ProgramData%\BurhanNexusHMS\client.json); a change made in the launcher is saved for the current
/// Windows user (%LOCALAPPDATA%\BurhanNexusHMS\client.json) and takes precedence.
/// </summary>
internal sealed class ClientConfig
{
    public string? ServerUrl { get; set; }

    public const int DefaultPort = 5000;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string MachineFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BurhanNexusHMS", "client.json");
    public static string UserFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BurhanNexusHMS", "client.json");

    public static string? LoadServer()
    {
        foreach (var file in new[] { UserFile, MachineFile })
        {
            try
            {
                if (!File.Exists(file)) continue;
                var c = JsonSerializer.Deserialize<ClientConfig>(File.ReadAllText(file));
                if (Normalize(c?.ServerUrl) is { } url) return url;
            }
            catch (Exception) { /* a damaged file is ignored; the user is asked again */ }
        }
        return null;
    }

    /// <summary>Saves the address. machineWide = installer (all users); otherwise the current user.</summary>
    public static void SaveServer(string url, bool machineWide)
    {
        var file = machineWide ? MachineFile : UserFile;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(new ClientConfig { ServerUrl = url }, Json));
    }

    /// <summary>"192.168.1.10" → "http://192.168.1.10:5000"; keeps an explicit scheme and port; drops any path.</summary>
    public static string? Normalize(string? input)
    {
        var s = (input ?? string.Empty).Trim().TrimEnd('/');
        if (s.Length == 0) return null;
        var hadScheme = s.Contains("://", StringComparison.Ordinal);
        if (!hadScheme) s = "http://" + s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) || u.Host.Length == 0)
            return null;
        var authority = s[(s.IndexOf("://", StringComparison.Ordinal) + 3)..].Split('/')[0];
        var explicitPort = authority.LastIndexOf(':') > authority.LastIndexOf(']');
        var port = explicitPort ? u.Port : DefaultPort;
        return new UriBuilder(u.Scheme, u.Host, port).Uri.GetLeftPart(UriPartial.Authority);
    }
}
