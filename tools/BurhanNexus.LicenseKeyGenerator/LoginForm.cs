namespace BurhanNexus.LicenseKeyGenerator;

/// <summary>Administrator sign-in. On the very first start it creates the signing key (or restores a key backup).</summary>
internal sealed class LoginForm : Form
{
    private readonly TextBox _user = new() { Width = 320 };
    private readonly TextBox _pass = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly TextBox _pass2 = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly Label _info = new() { AutoSize = true, MaximumSize = new Size(420, 0) };
    private int _failures;

    public Vault? Vault { get; private set; }
    public string? PrivateKey { get; private set; }

    public LoginForm()
    {
        var first = !Vault.Exists;
        Text = "Burhan Nexus HMS License Key Generator – " + (first ? "first start" : "sign in");
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 10f);
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        layout.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Text = "Burhan Nexus HMS License Key Generator" });
        _info.Text = first
            ? "No signing key on this computer yet. Choose the administrator name and a strong password: a NEW private signing key is created on this computer and saved encrypted with this password. The password cannot be recovered – keep it safe, and make a key backup afterwards."
            : "Authorised Burhan Nexus administrators only.";
        layout.Controls.Add(_info);
        layout.Controls.Add(new Label { AutoSize = true, Text = "Administrator name" });
        layout.Controls.Add(_user);
        layout.Controls.Add(new Label { AutoSize = true, Text = "Password" + (first ? " (at least 10 characters, letters and digits)" : "") });
        layout.Controls.Add(_pass);
        if (first) { layout.Controls.Add(new Label { AutoSize = true, Text = "Repeat password" }); layout.Controls.Add(_pass2); }
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var ok = new Button { Text = first ? "Create signing key" : "Sign in", AutoSize = true };
        ok.Click += (_, _) => { if (first) CreateKey(); else SignIn(); };
        buttons.Controls.Add(ok);
        if (first)
        {
            var restore = new Button { Text = "Restore key backup…", AutoSize = true };
            restore.Click += (_, _) => Restore();
            buttons.Controls.Add(restore);
        }
        var cancel = new Button { Text = "Exit", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void CreateKey()
    {
        try
        {
            if (_pass.Text != _pass2.Text) throw new ArgumentException("The two passwords are not the same.");
            Cursor = Cursors.WaitCursor;
            var (v, priv) = Vault.Create(_user.Text, _pass.Text);
            v.Save();
            Vault = v; PrivateKey = priv;
            MessageBox.Show(this, $"The signing key was created (Key ID {v.KeyId}).\n\nNext steps:\n1. Key & backup → Backup private key (encrypted) to a USB drive.\n2. Key & backup → Save public key, and send ONLY the public key to the HMS developer so the HMS accepts the keys you generate.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { Cursor = Cursors.Default; }
    }

    private void SignIn()
    {
        if (_failures >= 5) { MessageBox.Show(this, "Too many wrong attempts. Start the program again.", Text); DialogResult = DialogResult.Cancel; return; }
        Cursor = Cursors.WaitCursor;
        try
        {
            var v = Vault.Load();
            var priv = v.Unlock(_user.Text, _pass.Text);
            if (priv is null) { _failures++; MessageBox.Show(this, "Wrong administrator name or password.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Vault = v; PrivateKey = priv;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or System.Text.Json.JsonException) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { Cursor = Cursors.Default; }
    }

    private void Restore()
    {
        using var dlg = new OpenFileDialog { Title = "Key backup", Filter = "Burhan Nexus key backup (*.vault)|*.vault|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            var v = Vault.RestoreFrom(dlg.FileName, _user.Text, _pass.Text);
            Vault = v; PrivateKey = v.Unlock(_user.Text, _pass.Text);
            MessageBox.Show(this, $"Key backup restored (Key ID {v.KeyId}).", Text);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or System.Text.Json.JsonException)
        { MessageBox.Show(this, "Enter the administrator name and password of the backup first.\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { Cursor = Cursors.Default; }
    }
}

internal static class AppIcon
{
    private static Icon? _icon;
    public static Icon? Get()
    {
        if (_icon is not null) return _icon;
        using var s = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico");
        return _icon = s is null ? null : new Icon(s);
    }
}
