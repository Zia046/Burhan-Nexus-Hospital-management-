using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace BurhanNexus.Launcher;

/// <summary>
/// Direct thermal printing (Update 14): the slip, exactly as shown on screen, is sent to the receipt printer as an ESC/POS
/// raster image through the Windows spooler (RAW). The printer prints exactly the slip's length and cuts – no paper size of the
/// Windows driver is involved, so drivers that print a blank page (or a long A4-length page) for custom paper sizes still work.
/// </summary>
internal static class EscPos
{
    /// <summary>Printable dots of the roll at 203 dpi: 80 mm roll → 72 mm = 576 dots, 58 mm roll → 48 mm = 384 dots.</summary>
    public static int DotsFor(int rollMm) => rollMm == 58 ? 384 : 576;

    /// <summary>ESC/POS job for one or more slip images: each slip is printed, fed a little and cut.</summary>
    public static byte[] Build(IEnumerable<Bitmap> slips, int dots)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x1B, 0x40 }); // ESC @  initialise
        foreach (var bmp in slips)
        {
            var (bytesPerRow, rows, data) = Raster(bmp, dots);
            // GS v 0 in bands of at most 256 rows (small printer buffers).
            for (var y = 0; y < rows; y += 256)
            {
                var h = Math.Min(256, rows - y);
                ms.Write(new byte[] { 0x1D, 0x76, 0x30, 0x00, (byte)(bytesPerRow & 0xFF), (byte)(bytesPerRow >> 8), (byte)(h & 0xFF), (byte)(h >> 8) });
                ms.Write(data, y * bytesPerRow, h * bytesPerRow);
            }
            ms.Write(new byte[] { 0x1B, 0x64, 0x04 });       // ESC d 4   feed past the cutter
            ms.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // GS V 66 0 (partial) cut – ignored by printers without a cutter
        }
        return ms.ToArray();
    }

    /// <summary>1-bit raster (1 = black) of the image scaled to the roll width; white rows at the bottom are dropped.</summary>
    public static (int BytesPerRow, int Rows, byte[] Data) Raster(Bitmap src, int dots)
    {
        var width = Math.Min(dots, src.Width) / 8 * 8;
        var height = (int)Math.Round(src.Height * (double)width / src.Width);
        using var bmp = new Bitmap(width, Math.Max(1, height), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, width, bmp.Height);
        }
        var bytesPerRow = width / 8;
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new byte[bd.Stride * bmp.Height];
        Marshal.Copy(bd.Scan0, px, 0, px.Length);
        bmp.UnlockBits(bd);
        var data = new byte[bytesPerRow * bmp.Height];
        var lastInk = -1;
        for (var y = 0; y < bmp.Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * bd.Stride + x * 4;
                var a = px[i + 3] / 255.0;
                var lum = (0.114 * px[i] + 0.587 * px[i + 1] + 0.299 * px[i + 2]) * a + 255 * (1 - a);
                if (lum < 160) { data[y * bytesPerRow + x / 8] |= (byte)(0x80 >> (x % 8)); lastInk = y; }
            }
        }
        var rows = Math.Max(1, Math.Min(bmp.Height, lastInk + 8));
        return (bytesPerRow, rows, data[..(rows * bytesPerRow)]);
    }

    // ---- Windows spooler, RAW data ----
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo1 { public string? pDocName; public string? pOutputFile; public string? pDataType; }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool ClosePrinter(IntPtr h);
    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr h, int level, [In] DocInfo1 di);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool StartPagePrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(IntPtr h);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(IntPtr h, byte[] data, int count, out int written);

    /// <summary>Sends the bytes unchanged to the printer (like the Windows test page's "RAW" data format).</summary>
    public static void SendRaw(string printer, byte[] data, string jobName)
    {
        if (!OpenPrinter(printer, out var h, IntPtr.Zero)) throw new InvalidOperationException($"The printer \"{printer}\" could not be opened (error {Marshal.GetLastWin32Error()}).");
        try
        {
            if (StartDocPrinter(h, 1, new DocInfo1 { pDocName = jobName, pDataType = "RAW" }) == 0)
                throw new InvalidOperationException($"The print job could not be started (error {Marshal.GetLastWin32Error()}).");
            try
            {
                StartPagePrinter(h);
                if (!WritePrinter(h, data, data.Length, out var written) || written != data.Length)
                    throw new InvalidOperationException($"Sending to the printer failed (error {Marshal.GetLastWin32Error()}).");
                EndPagePrinter(h);
            }
            finally { EndDocPrinter(h); }
        }
        finally { ClosePrinter(h); }
    }
}
