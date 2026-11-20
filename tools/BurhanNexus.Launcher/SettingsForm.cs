namespace BurhanNexus.Launcher;

/// <summary>Asks for the central server address and tests it before saving.</summary>
internal sealed class SettingsForm : Form
{
    private readonly TextBox _address = new() { Width = 360 };
    private readonly Label _result = new() { AutoSize = true, MaximumSize = new Size(440, 0) };
    private readonly Button _test = new() { Text = "Test connection", AutoSize = true };
    private readonly Button _save = new() { Text = "Save", AutoSize = true, DialogResult = DialogResult.None };
    private readonly Button _cancel = new() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };

    public string? ServerUrl { get; private set; }

    public SettingsForm(string? current)
    {
        Text = "Burhan Nexus HMS – Server address";
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 10f);

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        layout.Controls.Add(new Label
        {
            AutoSize = true, MaximumSize = new Size(440, 0),
            Text = "Enter the address of the central HMS server computer, e.g. 192.168.1.10 or SERVER-PC (port 5000 is added automatically)."
        });
        layout.Controls.Add(new Label { AutoSize = true, Text = "سرور کمپیوٹر کا ایڈریس لکھیں، مثلاً 192.168.1.10", RightToLeft = RightToLeft.Yes });
        _address.Text = current ?? string.Empty;
        layout.Controls.Add(_address);
        layout.Controls.Add(_result);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(new Control[] { _test, _save, _cancel });
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = _save;
        CancelButton = _cancel;

        _test.Click += async (_, _) => await TestAsync();
        _save.Click += async (_, _) =>
        {
            if (await TestAsync() is not { } url)
            {
                if (MessageBox.Show(this, "The server did not answer. Save this address anyway?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                url = ClientConfig.Normalize(_address.Text);
                if (url is null) return;
            }
            ServerUrl = url;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private async Task<string?> TestAsync()
    {
        var url = ClientConfig.Normalize(_address.Text);
        if (url is null)
        {
            _result.ForeColor = Color.Firebrick;
            _result.Text = "This is not a valid address.";
            return null;
        }
        _result.ForeColor = SystemColors.GrayText;
        _result.Text = $"Testing {url} …";
        _test.Enabled = _save.Enabled = false;
        var (ok, message) = await ServerProbe.CheckAsync(url);
        _test.Enabled = _save.Enabled = true;
        _result.ForeColor = ok ? Color.SeaGreen : Color.Firebrick;
        _result.Text = ok ? $"✓ {url} – the HMS server answered." : $"✗ {url} – {message}";
        return ok ? url : null;
    }
}
