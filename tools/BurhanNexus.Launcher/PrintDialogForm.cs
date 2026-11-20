namespace BurhanNexus.Launcher;

/// <summary>
/// Small print window of the HMS app: printer + copies. Prints without the browser's date / web address header and footer.
/// Receipt mode: "always print receipts directly to this printer" (no window next time).
/// </summary>
internal sealed class PrintDialogForm : Form
{
    private readonly ComboBox _printer = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly NumericUpDown _copies = new() { Minimum = 1, Maximum = 10, Value = 1, Width = 60 };
    private readonly CheckBox _remember = new() { AutoSize = true, Checked = true };

    public string? Printer => _printer.SelectedItem as string;
    public int Copies => (int)_copies.Value;
    public bool Remember => _remember.Checked;
    public bool WantsBrowserDialog { get; private set; }

    public PrintDialogForm(bool receipt, string? current, int widthMm = 80)
    {
        Text = receipt ? "Print receipt" : "Print";
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14);
        Font = new Font("Segoe UI", 10f);

        var printers = PrinterConfig.Installed();
        _printer.Items.AddRange(printers.Cast<object>().ToArray());
        var pick = PrinterConfig.Exists(current) ? current : receipt ? PrinterConfig.GuessReceipt(printers) : PrinterConfig.WindowsDefault();
        if (pick is not null) _printer.SelectedItem = printers.FirstOrDefault(p => p.Equals(pick, StringComparison.OrdinalIgnoreCase));
        if (_printer.SelectedIndex < 0 && _printer.Items.Count > 0) _printer.SelectedIndex = 0;

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        layout.Controls.Add(new Label { AutoSize = true, Text = receipt ? $"Receipt printer ({widthMm} mm roll):" : "Printer:" });
        layout.Controls.Add(_printer);
        var copies = new FlowLayoutPanel { AutoSize = true };
        copies.Controls.Add(new Label { AutoSize = true, Text = "Copies:", Padding = new Padding(0, 6, 0, 0) });
        copies.Controls.Add(_copies);
        layout.Controls.Add(copies);
        _remember.Text = receipt ? "Always print receipts directly to this printer (no window next time)" : "Remember this printer";
        layout.Controls.Add(_remember);
        if (printers.Count == 0) layout.Controls.Add(new Label { AutoSize = true, ForeColor = Color.Firebrick, Text = "No printer is installed in Windows." });

        var buttons = new FlowLayoutPanel { AutoSize = true };
        var print = new Button { Text = "Print", AutoSize = true, Enabled = printers.Count > 0 };
        var more = new Button { Text = "Preview / more options…", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        print.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        more.Click += (_, _) => { WantsBrowserDialog = true; DialogResult = DialogResult.OK; Close(); };
        buttons.Controls.AddRange(new Control[] { print, more, cancel });
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = print;
        CancelButton = cancel;
    }
}
