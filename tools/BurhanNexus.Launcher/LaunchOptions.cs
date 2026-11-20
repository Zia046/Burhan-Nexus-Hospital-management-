namespace BurhanNexus.Launcher;

/// <summary>
/// Command line:
///   (none)                              open the HMS window
///   --server URL                        use this server for this run (not saved)
///   --set-server URL [--machine]        save the address (installer uses --machine) and exit
///   --profile NAME                      separate browser profile (e.g. two windows logged in as different users)
///   --selftest [--expect TEXT] [--expect-offline] [--reconnect] [--out file.png] [--result file.json] [--timeout 90]
///                                       automated check used by CI; exits 0 when passed
/// </summary>
internal sealed class LaunchOptions
{
    public string? Server { get; private set; }
    public string? SetServer { get; private set; }
    public bool MachineWide { get; private set; }
    public string Profile { get; private set; } = "default";
    public bool SelfTest { get; private set; }
    public string? Expect { get; private set; }
    public bool ExpectOffline { get; private set; }
    public bool Reconnect { get; private set; }
    public string? Out { get; private set; }
    public string? Result { get; private set; }
    public int TimeoutSeconds { get; private set; } = 90;

    public static LaunchOptions Parse(string[] args)
    {
        var o = new LaunchOptions();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : string.Empty;
            switch (args[i].ToLowerInvariant())
            {
                case "--server": o.Server = Next(); break;
                case "--set-server": o.SetServer = Next(); break;
                case "--machine": o.MachineWide = true; break;
                case "--profile": o.Profile = SafeName(Next()); break;
                case "--selftest": o.SelfTest = true; break;
                case "--expect": o.Expect = Next(); break;
                case "--expect-offline": o.ExpectOffline = true; break;
                case "--reconnect": o.Reconnect = true; break;
                case "--out": o.Out = Next(); break;
                case "--result": o.Result = Next(); break;
                case "--timeout": o.TimeoutSeconds = int.TryParse(Next(), out var t) && t > 0 ? t : 90; break;
            }
        }
        return o;
    }

    private static string SafeName(string s)
    {
        var clean = new string(s.Where(char.IsLetterOrDigit).ToArray());
        return clean.Length == 0 ? "default" : clean;
    }
}
