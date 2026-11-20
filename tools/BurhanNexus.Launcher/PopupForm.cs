using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BurhanNexus.Launcher;

/// <summary>A second window for HMS pages that open in a new tab/window (print previews, slips, reports).</summary>
internal sealed class PopupForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly string _server;

    public PopupForm(string server)
    {
        _server = server;
        Text = "Burhan Nexus HMS";
        Icon = AppIcon.Get();
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1000, 760);
        Controls.Add(_web);
    }

    public async Task<CoreWebView2> InitAsync(CoreWebView2Environment env)
    {
        await _web.EnsureCoreWebView2Async(env);
        var core = _web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.DocumentTitleChanged += (_, _) => Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Burhan Nexus HMS" : core.DocumentTitle;
        core.WindowCloseRequested += (_, _) => Close();
        core.NavigationStarting += (_, e) =>
        {
            if (Navigation.IsInternal(e.Uri, _server)) return;
            e.Cancel = true;
            Navigation.OpenExternal(e.Uri);
        };
        core.NewWindowRequested += (_, e) =>
        {
            // Keep it simple: a further window from a popup opens in this same window.
            e.Handled = true;
            if (Navigation.IsInternal(e.Uri, _server)) core.Navigate(e.Uri); else Navigation.OpenExternal(e.Uri);
        };
        return core;
    }
}
