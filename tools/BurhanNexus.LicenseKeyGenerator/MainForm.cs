using BurhanNexus.HMS.Core.Licensing;
using BurhanNexus.HMS.Infrastructure.Licensing;

namespace BurhanNexus.LicenseKeyGenerator;

/// <summary>Generate licence keys, licence history (copy again / revoke / revocation list) and the signing key (public key, backup, password).</summary>
internal sealed class MainForm : Form
{
    private readonly Vault _vault;
    private string _private;

    // Generate
    private readonly TextBox _hospital = new() { Width = 420 };
    private readonly TextBox _customer = new() { Width = 420 };
    private readonly ComboBox _product = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    private readonly TextBox _request = new() { Width = 420 };
    private readonly CheckBox _bindMachine = new() { AutoSize = true, Checked = true, Text = "Bind the licence to this Machine ID (the key works only on that computer)" };
    private readonly RadioButton _trial = new() { AutoSize = true, Text = "Trial" };
    private readonly RadioButton _year = new() { AutoSize = true, Text = "1 Year", Checked = true };
    private readonly RadioButton _life = new() { AutoSize = true, Text = "Lifetime" };
    private readonly DateTimePicker _start = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", Width = 160 };
    private readonly DateTimePicker _expiry = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMM yyyy", Width = 160 };
    private readonly Label _expiryNote = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _output = new() { Width = 600, Height = 90, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f) };
    private IssuedLicense? _last;

    // History
    private readonly ListView _history = new() { View = View.Details, FullRowSelect = true, Width = 900, Height = 380, HideSelection = false };

    public MainForm(Vault vault, string privateKey)
    {
        _vault = vault;
        _private = privateKey;
        Text = $"Burhan Nexus HMS License Key Generator – {vault.Username} (key {vault.KeyId})";
        Icon = AppIcon.Get();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(980, 680);
        Font = new Font("Segoe UI", 10f);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(GenerateTab());
        tabs.TabPages.Add(HistoryTab());
        tabs.TabPages.Add(KeyTab());
        tabs.SelectedIndexChanged += (_, _) => { if (tabs.SelectedIndex == 1) LoadHistory(); };
        Controls.Add(tabs);
        FormClosed += (_, _) => _private = string.Empty;
    }

    private static FlowLayoutPanel Column() => new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(12), Dock = DockStyle.Fill, AutoScroll = true };
    private static Label L(string t) => new() { AutoSize = true, Text = t, Margin = new Padding(3, 8, 3, 0) };

    private TabPage GenerateTab()
    {
        var page = new TabPage("Generate licence");
        var c = Column();
        _product.Items.Add("Burhan Nexus HMS (BNHMS)");
        _product.SelectedIndex = 0;
        c.Controls.Add(L("Hospital name")); c.Controls.Add(_hospital);
        c.Controls.Add(L("Hospital registration / customer ID")); c.Controls.Add(_customer);
        c.Controls.Add(L("Software product")); c.Controls.Add(_product);
        c.Controls.Add(L("Activation request code from the hospital's Settings → License & Activation page (Installation ID | Machine ID)")); c.Controls.Add(_request);
        c.Controls.Add(_bindMachine);
        var types = new FlowLayoutPanel { AutoSize = true };
        types.Controls.AddRange(new Control[] { L("Licence type:"), _trial, _year, _life });
        c.Controls.Add(types);
        var dates = new FlowLayoutPanel { AutoSize = true };
        dates.Controls.AddRange(new Control[] { L("Activation date"), _start, L("Expiry date"), _expiry, _expiryNote });
        c.Controls.Add(dates);
        foreach (var r in new[] { _trial, _year, _life }) r.CheckedChanged += (_, _) => UpdateExpiry();
        _start.ValueChanged += (_, _) => UpdateExpiry();
        UpdateExpiry();
        var gen = new Button { Text = "Generate License Key", AutoSize = true };
        gen.Click += (_, _) => Generate();
        c.Controls.Add(gen);
        c.Controls.Add(L("Activation key"));
        c.Controls.Add(_output);
        var actions = new FlowLayoutPanel { AutoSize = true };
        var copy = new Button { Text = "Copy License Key", AutoSize = true };
        copy.Click += (_, _) => { if (_last is not null) { Clipboard.SetText(_last.Key); MessageBox.Show(this, "The key was copied.", Text); } };
        var save = new Button { Text = "Save License…", AutoSize = true };
        save.Click += (_, _) => SaveLicense(_last);
        actions.Controls.AddRange(new Control[] { copy, save });
        c.Controls.Add(actions);
        page.Controls.Add(c);
        return page;
    }

    private void UpdateExpiry()
    {
        _expiry.Enabled = !_life.Checked;
        if (_trial.Checked) { _expiry.Value = _start.Value.Date.AddDays(30); _expiryNote.Text = "trial – change the end date if needed"; }
        else if (_year.Checked) { _expiry.Value = _start.Value.Date.AddYears(1); _expiryNote.Text = "valid until the end of this day"; }
        else _expiryNote.Text = "Lifetime – never expires";
    }

    private void Generate()
    {
        try
        {
            if (!LicenseIssuer.TryParseRequest(_request.Text, out var inst, out var machine))
                throw new ArgumentException("Paste the activation request code shown on the hospital's licence page (Installation ID | Machine ID).");
            if (_bindMachine.Checked && machine is null) throw new ArgumentException("The request code has no Machine ID – untick “Bind the licence to this Machine ID” or ask for the full code.");
            var start = DateOnly.FromDateTime(_start.Value.Date);
            var expiry = DateOnly.FromDateTime(_expiry.Value.Date);
            var term = _life.Checked ? LicenseTerm.Lifetime : _trial.Checked ? LicenseTerm.Trial
                : expiry == start.AddYears(1) ? LicenseTerm.Yearly : LicenseTerm.Custom;
            if (!_life.Checked && expiry <= start) throw new ArgumentException("The expiry date must be after the activation date.");
            var l = LicenseIssuer.Issue(_private, inst, _bindMachine.Checked ? machine : null, _hospital.Text, _customer.Text, term,
                customExpiry: term is LicenseTerm.Lifetime or LicenseTerm.Yearly ? null : expiry, startDate: start);
            if (!new EcdsaLicenseKeyVerifier(_vault.PublicKeyPem).TryVerify(l.Key, out _, out var err)) throw new InvalidOperationException("The new key does not verify: " + err);
            Registry.Record(l);
            _last = l;
            _output.Text = l.Key;
            MessageBox.Show(this, $"Licence {l.LicenseId} generated for {l.HospitalName}.\nType: {(_year.Checked ? "1 Year" : term.ToString())}   Expiry: {(l.ExpiryDate?.ToString("dd MMM yyyy") ?? "never")}", Text);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void SaveLicense(IssuedLicense? l)
    {
        if (l is null) return;
        using var dlg = new SaveFileDialog { FileName = $"{l.LicenseId}.txt", Filter = "Text file (*.txt)|*.txt" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dlg.FileName,
            $"Burhan Nexus HMS – activation key\r\nLicence ID: {l.LicenseId}\r\nHospital: {l.HospitalName}\r\nCustomer ID: {l.Customer}\r\n" +
            $"Expiry: {(l.ExpiryDate?.ToString("dd MMM yyyy") ?? "never (lifetime)")}\r\n\r\nEnter this key in Settings → License & Activation:\r\n{l.Key}\r\n");
    }

    private TabPage HistoryTab()
    {
        var page = new TabPage("Licence history");
        var c = Column();
        foreach (var (h, w) in new[] { ("Licence ID", 170), ("Generated (UTC)", 140), ("Hospital", 170), ("Customer ID", 100), ("Type", 70), ("Expiry", 90), ("Machine ID", 150), ("Status", 80) })
            _history.Columns.Add(h, w);
        c.Controls.Add(_history);
        var actions = new FlowLayoutPanel { AutoSize = true };
        var copy = new Button { Text = "Copy key again", AutoSize = true };
        copy.Click += (_, _) => { if (Selected() is { } id && Registry.KeyOf(id) is { } k) { Clipboard.SetText(k); MessageBox.Show(this, "The key was copied.", Text); } };
        var revoke = new Button { Text = "Revoke licence…", AutoSize = true };
        revoke.Click += (_, _) =>
        {
            if (Selected() is not { } id) return;
            if (MessageBox.Show(this, $"Revoke licence {id}? Then export the revocation list and import it on that hospital's licence page.", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            Registry.SetStatus(id, "Revoked");
            LoadHistory();
        };
        var export = new Button { Text = "Export revocation list…", AutoSize = true };
        export.Click += (_, _) => ExportRevocations();
        actions.Controls.AddRange(new Control[] { copy, revoke, export });
        c.Controls.Add(actions);
        page.Controls.Add(c);
        return page;
    }

    private string? Selected() => _history.SelectedItems.Count == 1 ? _history.SelectedItems[0].Text : null;

    private void LoadHistory()
    {
        _history.Items.Clear();
        foreach (var r in Registry.Load().Reverse())
            _history.Items.Add(new ListViewItem(new[] { r.LicenseId, r.Generated, r.Hospital, r.Customer, r.Type, r.Expiry, r.MachineId, r.Status }));
    }

    private void ExportRevocations()
    {
        var ids = Registry.Revoked().ToList();
        if (ids.Count == 0) { MessageBox.Show(this, "No licence is revoked.", Text); return; }
        var text = EcdsaLicenseKeyVerifier.CreateRevocationList(new LicenseRevocationList { IssuedUtc = DateTime.UtcNow, Serials = ids }, _private);
        using var dlg = new SaveFileDialog { FileName = $"BNHMS-revocations-{DateTime.Now:yyyyMMdd}.txt", Filter = "Text file (*.txt)|*.txt" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dlg.FileName, text);
        MessageBox.Show(this, $"{ids.Count} revoked licence(s) saved. Import this file on the hospital's licence page.", Text);
    }

    private TabPage KeyTab()
    {
        var page = new TabPage("Key & backup");
        var c = Column();
        c.Controls.Add(L($"Signing key ID: {_vault.KeyId}   (created {_vault.CreatedUtc:dd MMM yyyy})"));
        c.Controls.Add(L("Public key – NOT secret. Send it to the HMS developer once, so the HMS accepts the keys from this generator:"));
        c.Controls.Add(new TextBox { Width = 600, Height = 90, Multiline = true, ReadOnly = true, Text = _vault.PublicKeyPem.Replace("\n", "\r\n"), Font = new Font("Consolas", 9f) });
        var savePub = new Button { Text = "Save public key…", AutoSize = true };
        savePub.Click += (_, _) =>
        {
            using var dlg = new SaveFileDialog { FileName = $"BurhanNexus-HMS-public-key-{_vault.KeyId}.pem", Filter = "PEM (*.pem)|*.pem" };
            if (dlg.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dlg.FileName, _vault.PublicKeyPem);
        };
        c.Controls.Add(savePub);
        c.Controls.Add(L("The PRIVATE key stays on this computer, encrypted with your password. Never send it to anyone (not to the developer, not by WhatsApp or email)."));
        var backup = new Button { Text = "Backup private key (encrypted)…", AutoSize = true };
        backup.Click += (_, _) =>
        {
            using var dlg = new SaveFileDialog { FileName = $"BurhanNexus-signing-key-{_vault.KeyId}.vault", Filter = "Key backup (*.vault)|*.vault" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _vault.Save(dlg.FileName);
            MessageBox.Show(this, "Backup saved. It is encrypted with your current password – keep the USB drive and the password in two different safe places.", Text);
        };
        c.Controls.Add(backup);
        var pwd = new Button { Text = "Change password…", AutoSize = true };
        pwd.Click += (_, _) => ChangePassword();
        c.Controls.Add(pwd);
        page.Controls.Add(c);
        return page;
    }

    private void ChangePassword()
    {
        using var f = new Form { Text = "Change password", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12), Font = Font, MaximizeBox = false, MinimizeBox = false };
        var cur = new TextBox { Width = 300, UseSystemPasswordChar = true };
        var p1 = new TextBox { Width = 300, UseSystemPasswordChar = true };
        var p2 = new TextBox { Width = 300, UseSystemPasswordChar = true };
        var ok = new Button { Text = "Change", AutoSize = true };
        var col = Column();
        col.Controls.AddRange(new Control[] { L("Current password"), cur, L("New password"), p1, L("Repeat new password"), p2, ok });
        f.Controls.Add(col);
        ok.Click += (_, _) =>
        {
            try
            {
                if (_vault.Unlock(_vault.Username, cur.Text) is null) throw new ArgumentException("The current password is wrong.");
                if (p1.Text != p2.Text) throw new ArgumentException("The new passwords are not the same.");
                _vault.ChangePassword(_private, p1.Text);
                _vault.Save();
                MessageBox.Show(f, "Password changed. Make a new key backup – older backups still open with the old password.", Text);
                f.Close();
            }
            catch (ArgumentException ex) { MessageBox.Show(f, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        f.ShowDialog(this);
    }
}
