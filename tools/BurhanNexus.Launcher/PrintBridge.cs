using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace BurhanNexus.Launcher;

/// <summary>
/// Printing inside the HMS app. The page's own "Print" (window.print) is handled here instead of the browser:
///  * thermal receipts / slips (58 / 80 mm): page = roll width x slip height, no margins → no blank paper is fed;
///    printed directly to the chosen receipt printer (asked once);
///  * A4 / A5 documents: small window (printer, copies) → printed WITHOUT the browser's date / title / web address
///    header and footer; "Preview / more options…" still opens the normal browser print preview.
/// The HMS pages themselves are unchanged; other browsers keep using their own print dialog.
/// </summary>
internal static class PrintBridge
{
    private const string Script = """
        (function () {
          if (window.__bnPrint) return; window.__bnPrint = true;
          var nativePrint = window.print.bind(window);
          window.print = function () {
            try {
              var b = document.body, thermal = !!b && b.classList.contains('thermal-body') && !b.classList.contains('a4');
              var width = 0, height = 0;
              if (thermal) {
                width = b.classList.contains('w58') ? 58 : 80;
                var dw = parseInt(b.getAttribute('data-width'), 10); if (dw === 58 || dw === 80) width = dw;
                var slips = document.querySelectorAll('.t-slip'), max = 0, rects = [];
                for (var i = 0; i < slips.length; i++) {
                  var rc = slips[i].getBoundingClientRect(); if (rc.height > max) max = rc.height;
                  if (rc.width > 0 && rc.height > 0) rects.push({ x: rc.left + window.scrollX, y: rc.top + window.scrollY, w: rc.width, h: rc.height });
                }
                height = max > 0 ? Math.ceil(max * 25.4 / 96) + 4 : 0;
                if (!height) thermal = false;
              }
              var pageSize = '';
              try {
                for (var si = 0; si < document.styleSheets.length; si++) {
                  var rules = document.styleSheets[si].cssRules || [];
                  for (var ri = 0; ri < rules.length; ri++) if (rules[ri].type === 6 && rules[ri].style && rules[ri].style.getPropertyValue('size')) pageSize = rules[ri].style.getPropertyValue('size');
                }
              } catch (e) { }
              window.chrome.webview.postMessage(JSON.stringify({ type: 'bn-print', thermal: thermal, widthMm: width, heightMm: height, pageSize: pageSize, slips: thermal ? rects : [] }));
            } catch (e) { nativePrint(); }
          };
        })();
        """;

    /// <summary>CI self-test: print to PDF files in this folder instead of a printer (no windows are shown).</summary>
    public static string? PdfTestDir;
    public static event Action<string>? PdfWritten;

    /// <summary>Paper length of the driver's roll form ("80 x 210 mm"); the printer stops at the end of the slip.</summary>
    public const double DriverRollLengthMm = 210;

    private sealed record Slip(double X, double Y, double W, double H);
    private sealed record Request(string? Type, bool Thermal, int WidthMm, int HeightMm, string? PageSize, Slip[]? Slips);

    /// <summary>CI self-test: the ESC/POS job written next to the PDF.</summary>
    public const string EscPosTestFile = "thermal.escpos";

    /// <summary>Each slip on the page as an image at the printer's resolution (roll width in dots).</summary>
    private static async Task<List<System.Drawing.Bitmap>> CaptureSlipsAsync(CoreWebView2 core, Request r, int dots)
    {
        var list = new List<System.Drawing.Bitmap>();
        foreach (var sl in r.Slips ?? Array.Empty<Slip>())
        {
            var json = JsonSerializer.Serialize(new
            {
                format = "png", captureBeyondViewport = true, fromSurface = true,
                clip = new { x = sl.X, y = sl.Y, width = sl.W, height = sl.H, scale = dots / sl.W },
            });
            var res = await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", json);
            using var doc = JsonDocument.Parse(res);
            using var ms = new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("data").GetString()!));
            using var tmp = new System.Drawing.Bitmap(ms);
            list.Add(new System.Drawing.Bitmap(tmp));
        }
        return list;
    }

    private static async Task<byte[]?> EscPosJobAsync(CoreWebView2 core, Request r)
    {
        var dots = EscPos.DotsFor(r.WidthMm);
        var slips = await CaptureSlipsAsync(core, r, dots);
        try { return slips.Count == 0 ? null : EscPos.Build(slips, dots); }
        finally { slips.ForEach(b => b.Dispose()); }
    }

    /// <summary>Paper of an A4 / A5 document from its CSS @page size ("A4", "A5 landscape", "210mm 297mm"); A4 when not stated.</summary>
    public static (double WidthMm, double HeightMm) PaperOf(string? cssSize)
    {
        var parts = (cssSize ?? string.Empty).ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        (double W, double H) size = (210, 297);
        var mm = parts.Where(x => x.EndsWith("mm") && double.TryParse(x[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                      .Select(x => double.Parse(x[..^2], System.Globalization.CultureInfo.InvariantCulture)).ToList();
        if (mm.Count == 2) size = (mm[0], mm[1]);
        else if (parts.Contains("a5")) size = (148, 210);
        else if (parts.Contains("a3")) size = (297, 420);
        else if (parts.Contains("letter")) size = (215.9, 279.4);
        if (parts.Contains("landscape") && size.W < size.H) size = (size.H, size.W);
        return size;
    }

    public static async Task AttachAsync(CoreWebView2 core, Form owner)
    {
        await core.AddScriptToExecuteOnDocumentCreatedAsync(Script);
        core.WebMessageReceived += async (_, e) =>
        {
            Request? r;
            try { r = JsonSerializer.Deserialize<Request>(e.TryGetWebMessageAsString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (Exception) { return; }
            if (r?.Type != "bn-print") return;
            try { await PrintAsync(core, owner, r); }
            catch (Exception ex) { MessageBox.Show(owner, "Printing failed: " + ex.Message, "Burhan Nexus HMS", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
    }

    private static CoreWebView2PrintSettings BaseSettings(CoreWebView2 core)
    {
        var s = core.Environment.CreatePrintSettings();
        s.ShouldPrintHeaderAndFooter = false;
        s.ShouldPrintBackgrounds = true;
        s.ScaleFactor = 1.0;
        return s;
    }

    private static async Task PrintAsync(CoreWebView2 core, Form owner, Request r)
    {
        var cfg = PrinterConfig.Load();
        var s = BaseSettings(core);
        if (r.Thermal)
        {
            s.MediaSize = CoreWebView2PrintMediaSize.Custom;
            s.PageWidth = r.WidthMm / 25.4;
            // Update 14: through the Windows driver the page is the driver's standard roll form (80 / 58 x 210 mm) – POS drivers print
            // only up to the last printed line on it, while a custom slip-height page was printed blank by some drivers.
            s.PageHeight = DriverRollLengthMm / 25.4;
            s.MarginTop = s.MarginBottom = s.MarginLeft = s.MarginRight = 0;
            if (PdfTestDir is not null)
            {
                if (await EscPosJobAsync(core, r) is { } test) await File.WriteAllBytesAsync(Path.Combine(PdfTestDir, EscPosTestFile), test);
                await ToPdfAsync(core, s, "thermal");
                return;
            }

            var printer = PrinterConfig.Exists(cfg.ReceiptPrinter) ? cfg.ReceiptPrinter : null;
            var copies = 1;
            if (printer is null)
            {
                using var dlg = new PrintDialogForm(receipt: true, cfg.ReceiptPrinter, r.WidthMm);
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                if (dlg.WantsBrowserDialog) { core.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); return; }
                printer = dlg.Printer;
                copies = dlg.Copies;
                if (dlg.Remember && printer is not null) { cfg.ReceiptPrinter = printer; cfg.Save(); }
            }
            if (printer is null) return;
            // Update 14: straight to the thermal printer as an ESC/POS image – exact slip length, cut, no driver paper size
            // (some POS drivers print a blank or an A4-length page for a custom paper size).
            if (cfg.ReceiptAsImage && await EscPosJobAsync(core, r) is { } job)
            {
                for (var c = 0; c < copies; c++) EscPos.SendRaw(printer, job, "Burhan Nexus HMS receipt");
                return;
            }
            s.PrinterName = printer;
            s.Copies = copies;
            await Report(owner, await core.PrintAsync(s), printer);
            return;
        }

        // A4 / A5 documents: the page's own paper size, margins like the HMS print layouts (about 12 mm), no header / footer.
        var paper = PaperOf(r.PageSize);
        s.MediaSize = CoreWebView2PrintMediaSize.Custom;
        s.PageWidth = paper.WidthMm / 25.4;
        s.PageHeight = paper.HeightMm / 25.4;
        s.MarginTop = s.MarginBottom = s.MarginLeft = s.MarginRight = 12 / 25.4;
        if (PdfTestDir is not null) { await ToPdfAsync(core, s, "a4"); return; }
        using (var dlg = new PrintDialogForm(receipt: false, cfg.A4Printer))
        {
            if (dlg.ShowDialog(owner) != DialogResult.OK) return;
            if (dlg.WantsBrowserDialog) { core.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); return; }
            if (dlg.Printer is null) return;
            if (dlg.Remember) { cfg.A4Printer = dlg.Printer; cfg.Save(); }
            s.PrinterName = dlg.Printer;
            s.Copies = dlg.Copies;
            await Report(owner, await core.PrintAsync(s), dlg.Printer);
        }
    }

    private static Task Report(Form owner, CoreWebView2PrintStatus status, string printer)
    {
        if (status == CoreWebView2PrintStatus.Succeeded) return Task.CompletedTask;
        var why = status == CoreWebView2PrintStatus.PrinterUnavailable ? $"The printer \"{printer}\" is not available (switched off, offline or not connected)." : "The printer reported an error.";
        MessageBox.Show(owner, why + "\n\nCheck the printer and press Print again. To choose another printer: right-click the window title → Printers…",
            "Burhan Nexus HMS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return Task.CompletedTask;
    }

    private static async Task ToPdfAsync(CoreWebView2 core, CoreWebView2PrintSettings s, string name)
    {
        var file = Path.Combine(PdfTestDir!, name + ".pdf");
        await core.PrintToPdfAsync(file, s);
        PdfWritten?.Invoke(file);
    }
}
