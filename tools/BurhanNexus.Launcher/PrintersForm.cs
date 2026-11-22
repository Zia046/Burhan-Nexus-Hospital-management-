namespace BurhanNexus.Launcher;

/// <summary>Choose the receipt printer (direct printing) and the A4 printer of this computer.</summary>
internal sealed class PrintersForm : Form
{
    private readonly ComboBox _receipt = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly ComboBox _a4 = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly CheckBox _image = new() { AutoSize = true, Text = "Send receipts directly to the thermal printer (ESC/POS – exact length, auto cut). Recommended." };

    public PrintersForm()
    {
        Text = "Printers – Burhan Nexus HMS";
        Icon = AppIcon.Get();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14);
        Font = new Font("Segoe UI", 10f);
        var cfg = PrinterConfig.Load();
        var printers = PrinterConfig.Installed();
        const string ask = "(ask every time)";
        _receipt.Items.Add(ask); _a4.Items.Add(ask);
        foreach (var p in printers) { _receipt.Items.Add(p); _a4.Items.Add(p); }
        _receipt.SelectedItem = PrinterConfig.Exists(cfg.ReceiptPrinter) ? printers.First(p => p.Equals(cfg.ReceiptPrinter, StringComparison.OrdinalIgnoreCase)) : ask;
        _a4.SelectedItem = PrinterConfig.Exists(cfg.A4Printer) ? printers.First(p => p.Equals(cfg.A4Printer, StringComparison.OrdinalIgnoreCase)) : ask;

        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        layout.Controls.Add(new Label { AutoSize = true, Text = "Receipt / slip printer (58 or 80 mm) – receipts print directly, no window:" });
        layout.Controls.Add(_receipt);
        _image.Checked = cfg.ReceiptAsImage;
        layout.Controls.Add(_image);
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Untick only if your receipt printer is not an ESC/POS thermal printer (then the Windows driver's paper size is used)." });
        layout.Controls.Add(new Label { AutoSize = true, Text = "A4 printer (reports) – suggested in the print window:" });
        layout.Controls.Add(_a4);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            cfg.ReceiptPrinter = _receipt.SelectedItem as string == ask ? null : _receipt.SelectedItem as string;
            cfg.A4Printer = _a4.SelectedItem as string == ask ? null : _a4.SelectedItem as string;
            cfg.ReceiptAsImage = _image.Checked;
            try { cfg.Save(); } catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.AddRange(new Control[] { save, cancel });
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
