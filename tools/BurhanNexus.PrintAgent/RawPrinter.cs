using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BurhanNexus.PrintAgent;

/// <summary>Sends bytes to a Windows printer queue with the RAW datatype (ESC/POS goes to the printer unchanged).</summary>
public static class RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DOC_INFO_1
    {
        public string? pDocName;
        public string? pOutputFile;
        public string? pDataType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_4
    {
        public IntPtr pPrinterName;
        public IntPtr pServerName;
        public uint Attributes;
    }

    private const uint PRINTER_ENUM_LOCAL = 0x2;
    private const uint PRINTER_ENUM_CONNECTIONS = 0x4;

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "OpenPrinterW")]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "StartDocPrinterW")]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] DOC_INFO_1 di);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);
    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "EnumPrintersW")]
    private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr pPrinterEnum, uint cbBuf, out uint pcbNeeded, out uint pcReturned);

    public static bool Supported => OperatingSystem.IsWindows();

    public static IReadOnlyList<string> List()
    {
        if (!Supported) return Array.Empty<string>();
        const uint flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
        EnumPrinters(flags, null, 4, IntPtr.Zero, 0, out var needed, out _);
        if (needed == 0) return Array.Empty<string>();
        var buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            if (!EnumPrinters(flags, null, 4, buffer, needed, out _, out var count)) return Array.Empty<string>();
            var size = Marshal.SizeOf<PRINTER_INFO_4>();
            var list = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PRINTER_INFO_4>(buffer + i * size);
                var name = Marshal.PtrToStringUni(info.pPrinterName);
                if (!string.IsNullOrEmpty(name)) list.Add(name);
            }
            return list.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static void Send(string printerName, string jobName, byte[] data)
    {
        if (!Supported) throw new PlatformNotSupportedException("Raw printing is only available on Windows.");
        if (!OpenPrinter(printerName, out var h, IntPtr.Zero))
            throw new InvalidOperationException($"Printer '{printerName}' was not found on this computer ({new Win32Exception(Marshal.GetLastWin32Error()).Message}).");
        try
        {
            var doc = new DOC_INFO_1 { pDocName = jobName, pDataType = "RAW" };
            if (StartDocPrinter(h, 1, doc) == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "StartDocPrinter failed");
            try
            {
                if (!StartPagePrinter(h)) throw new Win32Exception(Marshal.GetLastWin32Error(), "StartPagePrinter failed");
                if (!WritePrinter(h, data, data.Length, out var written) || written != data.Length)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WritePrinter failed");
                EndPagePrinter(h);
            }
            finally
            {
                EndDocPrinter(h);
            }
        }
        finally
        {
            ClosePrinter(h);
        }
    }
}
