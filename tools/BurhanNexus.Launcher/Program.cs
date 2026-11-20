using Microsoft.Web.WebView2.Core;

namespace BurhanNexus.Launcher;

internal static class Program
{
    public static int ExitCode;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var opts = LaunchOptions.Parse(args);

        if (opts.SetServer is not null)
        {
            var url = ClientConfig.Normalize(opts.SetServer);
            if (url is null) return 3;
            try { ClientConfig.SaveServer(url, opts.MachineWide); return 0; }
            catch (Exception) { return 4; }
        }

        try { _ = CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch (WebView2RuntimeNotFoundException)
        {
            if (!opts.SelfTest)
                MessageBox.Show(
                    "Microsoft Edge WebView2 Runtime is not installed on this computer.\n\n" +
                    "Run the Burhan Nexus HMS Client Setup again (it installs WebView2), or install \"Microsoft Edge WebView2 Runtime\" from Microsoft.\n\n" +
                    "Until then the HMS can still be opened in Edge or Chrome at the server address.",
                    "Burhan Nexus HMS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 5;
        }

        var server = ClientConfig.Normalize(opts.Server) ?? ClientConfig.LoadServer();
        if (server is null)
        {
            if (opts.SelfTest) return 6;
            using var dlg = new SettingsForm(null);
            if (dlg.ShowDialog() != DialogResult.OK || dlg.ServerUrl is null) return 0;
            server = dlg.ServerUrl;
            TrySave(server);
        }

        Application.Run(new MainForm(server, opts));
        return ExitCode;
    }

    public static void TrySave(string url)
    {
        try { ClientConfig.SaveServer(url, machineWide: false); }
        catch (Exception ex) { MessageBox.Show("The address could not be saved: " + ex.Message, "Burhan Nexus HMS", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
