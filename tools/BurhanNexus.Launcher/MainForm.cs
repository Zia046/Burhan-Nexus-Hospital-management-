using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BurhanNexus.Launcher;

/// <summary>
/// The HMS window: the existing web interface of the central server inside WebView2 (the Edge engine).
/// No data is stored here except the browser profile (login cookie, zoom) in %LOCALAPPDATA%.
/// When the server cannot be reached a reconnect screen is shown and the connection is retried every 5 seconds.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly Panel _offline = new() { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };
    private readonly Label _offlineDetail = new() { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(620, 0) };
    private readonly System.Windows.Forms.Timer _retry = new() { Interval = 5000 };
    private readonly System.Windows.Forms.Timer _watch = new() { Interval = 15000 };
    private readonly LaunchOptions _opts;
    private CoreWebView2Environment? _env;
    private string _server;
    private string? _lastGoodUrl;
    private bool _reloadWhenBack;
    private bool _checking;
    private int _watchFailures;
    private bool _sawOffline;
    private bool _testDone;

    private const int WmSysCommand = 0x112, MenuSettings = 0x100, MenuReload = 0x110, MenuFullScreen = 0x120, MenuAbout = 0x130;
    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool revert);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    public MainForm(string server, LaunchOptions opts)
    {
        _server = server;
        _opts = opts;
        Text = "Burhan Nexus HMS";
        Icon = AppIcon.Get();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1360, 860);
        MinimumSize = new Size(900, 600);
        if (!opts.SelfTest) WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 10f);
        BuildOfflinePanel();
        Controls.Add(_web);
        Controls.Add(_offline);
        _retry.Tick += async (_, _) => await CheckServerAsync();
        _watch.Tick += async (_, _) => await WatchAsync();
    }

    private void BuildOfflinePanel()
    {
        var box = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None };
        var icon = AppIcon.Get();
        if (icon is not null) box.Controls.Add(new PictureBox { Image = new Icon(icon, 64, 64).ToBitmap(), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom });
        box.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 16f), Text = "Cannot reach the HMS server", Tag = "offline-title" });
        box.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI", 13f), Text = "سرور سے رابطہ نہیں ہو رہا – دوبارہ کوشش جاری ہے", RightToLeft = RightToLeft.Yes });
        box.Controls.Add(_offlineDetail);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var retry = new Button { Text = "Retry now", AutoSize = true };
        var settings = new Button { Text = "Server settings…", AutoSize = true };
        retry.Click += async (_, _) => await CheckServerAsync();
        settings.Click += (_, _) => OpenSettings();
        buttons.Controls.AddRange(new Control[] { retry, settings });
        box.Controls.Add(buttons);
        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        center.Controls.Add(box, 0, 0);
        _offline.Controls.Add(center);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var menu = GetSystemMenu(Handle, false);
        AppendMenu(menu, 0x800, UIntPtr.Zero, null);
        AppendMenu(menu, 0, (UIntPtr)MenuSettings, "Server settings…");
        AppendMenu(menu, 0, (UIntPtr)MenuReload, "Reload page\tF5");
        AppendMenu(menu, 0, (UIntPtr)MenuFullScreen, "Full screen on/off\tF11");
        AppendMenu(menu, 0, (UIntPtr)MenuAbout, "About Burhan Nexus HMS Client");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmSysCommand)
        {
            switch ((int)(m.WParam.ToInt64() & 0xFFF0))
            {
                case MenuSettings: OpenSettings(); return;
                case MenuReload: _web.CoreWebView2?.Reload(); return;
                case MenuFullScreen: ToggleFullScreen(); return;
                case MenuAbout:
                    MessageBox.Show(this, $"Burhan Nexus HMS Client {Application.ProductVersion}\nServer: {_server}\nWebView2: {SafeVersion()}\n\nThis program keeps no hospital data; everything is on the central server.",
                        "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
            }
        }
        base.WndProc(ref m);
    }

    private static string SafeVersion()
    {
        try { return CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch (Exception) { return "?"; }
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_opts.SelfTest) StartSelfTestTimer();
        try
        {
            var udf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BurhanNexusHMS", "WebView2", _opts.Profile);
            Directory.CreateDirectory(udf);
            _env = await CoreWebView2Environment.CreateAsync(null, udf);
            await _web.EnsureCoreWebView2Async(_env);
        }
        catch (Exception ex)
        {
            Finish(false, "WebView2 could not start: " + ex.Message);
            if (!_opts.SelfTest) MessageBox.Show(this, "The HMS window could not start: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        var core = _web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.DocumentTitleChanged += (_, _) => Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Burhan Nexus HMS" : $"{core.DocumentTitle} – Burhan Nexus HMS";
        core.NavigationStarting += (_, ev) =>
        {
            if (Navigation.IsInternal(ev.Uri, _server)) return;
            ev.Cancel = true;
            Navigation.OpenExternal(ev.Uri);
        };
        core.NewWindowRequested += OnNewWindow;
        core.NavigationCompleted += async (_, ev) => await OnNavigationCompleted(ev);
        _web.KeyDown += (_, ev) =>
        {
            if (ev.KeyCode == Keys.F11) ToggleFullScreen();
            else if (ev.Control && ev.Alt && ev.KeyCode == Keys.S) OpenSettings();
        };

        await CheckServerAsync(initial: true);
    }

    private async void OnNewWindow(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!Navigation.IsInternal(e.Uri, _server))
        {
            e.Handled = true;
            Navigation.OpenExternal(e.Uri);
            return;
        }
        if (_env is null) return;
        var deferral = e.GetDeferral();
        try
        {
            var popup = new PopupForm(_server);
            popup.Show(this);
            e.NewWindow = await popup.InitAsync(_env);
            e.Handled = true;
        }
        finally { deferral.Complete(); }
    }

    private static bool IsNetworkError(CoreWebView2WebErrorStatus s) => s is
        CoreWebView2WebErrorStatus.CannotConnect or CoreWebView2WebErrorStatus.ConnectionAborted or CoreWebView2WebErrorStatus.ConnectionReset or
        CoreWebView2WebErrorStatus.Disconnected or CoreWebView2WebErrorStatus.HostNameNotResolved or CoreWebView2WebErrorStatus.ServerUnreachable or
        CoreWebView2WebErrorStatus.Timeout or CoreWebView2WebErrorStatus.ErrorHttpInvalidServerResponse;

    private async Task OnNavigationCompleted(CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess && IsNetworkError(e.WebErrorStatus))
        {
            _reloadWhenBack = true;
            ShowOffline($"Last error: {e.WebErrorStatus}");
            return;
        }
        // Any page the server delivered (also its own error pages) counts as connected.
        var src = _web.Source?.ToString();
        if (src is not null && Navigation.IsInternal(src, _server) && src.StartsWith("http", StringComparison.OrdinalIgnoreCase)) _lastGoodUrl = src;
        HideOffline();
        if (_opts.SelfTest) await SelfTestOnPageAsync();
    }

    private async Task CheckServerAsync(bool initial = false)
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var (ok, message) = await ServerProbe.CheckAsync(_server);
            if (!ok)
            {
                if (initial) _reloadWhenBack = true;
                ShowOffline(message);
                return;
            }
            if (initial || _reloadWhenBack || _web.Source is null || _web.Source.ToString() == "about:blank")
            {
                _reloadWhenBack = false;
                _web.CoreWebView2?.Navigate(_lastGoodUrl ?? _server + "/");
            }
            else HideOffline();
        }
        finally { _checking = false; }
    }

    /// <summary>While the page is open, notice a lost server (e.g. network cable, server restart) without reloading the page.</summary>
    private async Task WatchAsync()
    {
        if (_offline.Visible || _checking) return;
        var (ok, message) = await ServerProbe.CheckAsync(_server);
        _watchFailures = ok ? 0 : _watchFailures + 1;
        if (_watchFailures >= 2) ShowOffline(message + " (the open page is kept – unsaved entries are not lost)");
    }

    private void ShowOffline(string detail)
    {
        _sawOffline = true;
        _offlineDetail.Text = $"Server: {_server}\n{detail}\nRetrying every 5 seconds…";
        if (!_offline.Visible)
        {
            _offline.Visible = true;
            _offline.BringToFront();
        }
        _watch.Stop();
        _retry.Start();
        if (_opts.SelfTest && _opts.ExpectOffline) _ = SelfTestOfflineAsync();
    }

    private void HideOffline()
    {
        _retry.Stop();
        _watchFailures = 0;
        if (_offline.Visible) _offline.Visible = false;
        _watch.Start();
    }

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_server);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.ServerUrl is null) return;
        _server = dlg.ServerUrl;
        Program.TrySave(_server);
        _lastGoodUrl = null;
        _reloadWhenBack = true;
        _ = CheckServerAsync(initial: true);
    }

    private FormWindowState _before;
    private void ToggleFullScreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = _before;
        }
        else
        {
            _before = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            WindowState = FormWindowState.Maximized;
        }
    }

    // ---------------- automated self-test (CI) ----------------

    private void StartSelfTestTimer()
    {
        var t = new System.Windows.Forms.Timer { Interval = _opts.TimeoutSeconds * 1000 };
        t.Tick += (_, _) => { t.Stop(); Finish(false, $"Timed out after {_opts.TimeoutSeconds} s (offline seen: {_sawOffline}, page: {_web.Source})"); };
        t.Start();
    }

    private async Task SelfTestOnPageAsync()
    {
        if (_testDone || _opts.ExpectOffline) return;
        if (_opts.Reconnect && !_sawOffline) return;
        string text;
        try { text = JsonSerializer.Deserialize<string>(await _web.CoreWebView2.ExecuteScriptAsync("document.body ? document.body.innerText : ''")) ?? string.Empty; }
        catch (Exception ex) { text = "script failed: " + ex.Message; }
        if (_opts.Expect is { Length: > 0 } expect && !text.Contains(expect, StringComparison.OrdinalIgnoreCase)) return; // wait for the expected page (redirects)
        if (_opts.Out is { } png)
        {
            try
            {
                await using var fs = File.Create(png);
                await _web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
            }
            catch (Exception) { /* the screenshot is evidence only */ }
        }
        Finish(true, $"Page loaded: {_web.Source} – {_web.CoreWebView2.DocumentTitle}" + (_opts.Reconnect ? " (after the server came back)" : ""));
    }

    private async Task SelfTestOfflineAsync()
    {
        if (_testDone) return;
        await Task.Delay(1500);
        if (_testDone) return;
        if (_opts.Out is { } png)
        {
            try
            {
                using var bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
                DrawToBitmap(bmp, new Rectangle(Point.Empty, ClientSize));
                bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception) { }
        }
        Finish(_offline.Visible, _offline.Visible ? "Reconnect screen shown for an unreachable server" : "Reconnect screen not visible");
    }

    private void Finish(bool ok, string message)
    {
        if (!_opts.SelfTest || _testDone) return;
        _testDone = true;
        Program.ExitCode = ok ? 0 : 1;
        if (_opts.Result is { } file)
        {
            try
            {
                File.WriteAllText(file, JsonSerializer.Serialize(new { ok, message, server = _server, profile = _opts.Profile, webView2 = SafeVersion(), utc = DateTime.UtcNow }));
            }
            catch (Exception) { }
        }
        BeginInvoke(new Action(Close));
    }
}
