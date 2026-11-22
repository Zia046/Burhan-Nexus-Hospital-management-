using System.Drawing.Printing;
using System.Text.Json;

namespace BurhanNexus.Launcher;

/// <summary>Printers chosen on this computer (per Windows user): receipt / thermal printer and A4 printer.</summary>
internal sealed class PrinterConfig
{
    public string? ReceiptPrinter { get; set; }
    public string? A4Printer { get; set; }
    /// <summary>Update 14: receipts go to the thermal printer as an ESC/POS image (exact length + cut). Off = through the Windows driver.</summary>
    public bool ReceiptAsImage { get; set; } = true;

    private static string File => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BurhanNexusHMS", "printers.json");

    public static PrinterConfig Load()
    {
        try { return System.IO.File.Exists(File) ? JsonSerializer.Deserialize<PrinterConfig>(System.IO.File.ReadAllText(File)) ?? new() : new(); }
        catch (Exception) { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static List<string> Installed()
    {
        try { return PrinterSettings.InstalledPrinters.Cast<string>().ToList(); }
        catch (Exception) { return new(); }
    }

    public static string? WindowsDefault()
    {
        try { var n = new PrinterSettings().PrinterName; return string.IsNullOrWhiteSpace(n) ? null : n; }
        catch (Exception) { return null; }
    }

    /// <summary>A printer that looks like a receipt printer (POS / 80 mm / thermal), else the Windows default.</summary>
    public static string? GuessReceipt(IEnumerable<string> printers)
    {
        var words = new[] { "pos", "80", "58", "thermal", "receipt", "xp-", "xprinter", "rongta", "tm-t", "hprt", "epson tm" };
        return printers.FirstOrDefault(p => words.Any(w => p.Contains(w, StringComparison.OrdinalIgnoreCase))) ?? WindowsDefault();
    }

    public static bool Exists(string? name) => name is not null && Installed().Contains(name, StringComparer.OrdinalIgnoreCase);
}
