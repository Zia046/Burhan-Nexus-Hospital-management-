using System.Text;
using BurhanNexus.HMS.Core.Domain;

namespace BurhanNexus.HMS.Infrastructure.Printing;

/// <summary>
/// Minimal ESC/POS command builder (Epson standard command set, supported by Xprinter, Rongta, HPRT, Epson TM and
/// generic 58/80 mm printers). Text is sent in code page PC437; characters outside ASCII are replaced.
/// </summary>
public sealed class EscPosBuilder
{
    private readonly List<byte> _bytes = new();

    public static readonly byte[] Init = { 0x1B, 0x40 };            // ESC @
    public static readonly byte[] CodePage437 = { 0x1B, 0x74, 0x00 }; // ESC t 0
    public static readonly byte[] PartialCut = { 0x1D, 0x56, 0x42, 0x03 }; // GS V 66 3 (feed + partial cut)

    public EscPosBuilder Initialize() { _bytes.AddRange(Init); _bytes.AddRange(CodePage437); return this; }
    public EscPosBuilder Align(SlipAlignment a) { _bytes.AddRange(new byte[] { 0x1B, 0x61, (byte)(a == SlipAlignment.Center ? 1 : 0) }); return this; }
    public EscPosBuilder AlignCenter() { _bytes.AddRange(new byte[] { 0x1B, 0x61, 1 }); return this; }
    public EscPosBuilder Bold(bool on) { _bytes.AddRange(new byte[] { 0x1B, 0x45, (byte)(on ? 1 : 0) }); return this; }

    /// <summary>GS ! n – character width/height multiplier 1–8.</summary>
    public EscPosBuilder Size(int width, int height)
    {
        width = Math.Clamp(width, 1, 8);
        height = Math.Clamp(height, 1, 8);
        _bytes.AddRange(new byte[] { 0x1D, 0x21, (byte)(((width - 1) << 4) | (height - 1)) });
        return this;
    }

    public EscPosBuilder Text(string text) { _bytes.AddRange(Encode(text)); return this; }
    public EscPosBuilder Line(string text = "") { Text(text); _bytes.Add(0x0A); return this; }
    public EscPosBuilder Feed(int lines) { _bytes.AddRange(new byte[] { 0x1B, 0x64, (byte)Math.Clamp(lines, 0, 255) }); return this; }
    public EscPosBuilder Cut() { _bytes.AddRange(PartialCut); return this; }
    public EscPosBuilder Raw(params byte[] b) { _bytes.AddRange(b); return this; }

    public byte[] ToArray() => _bytes.ToArray();

    /// <summary>ASCII-safe encoding: printable ASCII kept, common symbols mapped, everything else becomes '?'.</summary>
    public static byte[] Encode(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                >= ' ' and <= '~' => ch,
                '–' or '—' => '-',
                '·' or '•' => '-',
                '‘' or '’' => '\'',
                '“' or '”' => '"',
                '₨' => 'R',
                _ => '?'
            });
        }
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>"Label .......... value" justified to the line width.</summary>
    public static string Justify(string left, string right, int width)
    {
        if (left.Length + right.Length + 1 > width)
        {
            var maxLeft = Math.Max(1, width - right.Length - 1);
            if (left.Length > maxLeft) left = left[..maxLeft];
        }
        var spaces = Math.Max(1, width - left.Length - right.Length);
        return left + new string(' ', spaces) + right;
    }

    public static IEnumerable<string> Wrap(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var w in words)
        {
            var word = w.Length > width ? w[..width] : w;
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) yield return line.ToString();
    }
}

/// <summary>Everything printed on an appointment slip – identical for the browser slip and the ESC/POS slip.</summary>
public sealed record AppointmentSlipData(
    string HospitalName, string? HospitalContact, string DoctorName, string? DoctorQualification, string? DoctorSpecialty,
    int TokenNumber, string PatientName, string PatientNumber, string AgeGender, string DateText, string? TimeText,
    string? FeeText, string? ContactText, string? Message, string AppointmentNumber, string PrintedText, bool IsReprint, bool IsCancelled);

public static class ThermalSlipComposer
{
    /// <summary>Builds the ESC/POS byte stream of an appointment slip for the given printer profile.</summary>
    public static byte[] Appointment(AppointmentSlipData d, PrinterProfile p)
    {
        var w = p.CharsPerLine;
        var sep = new string('-', w);
        var b = new EscPosBuilder().Initialize();
        var body = p.FontSize == SlipFontSize.Large ? 2 : 1;

        b.AlignCenter().Bold(true).Size(2, 2);
        foreach (var l in EscPosBuilder.Wrap(d.HospitalName, w / 2)) b.Line(l);
        b.Size(1, 1).Bold(false);
        if (!string.IsNullOrWhiteSpace(d.HospitalContact)) b.Line(d.HospitalContact);
        b.Line(sep);

        b.AlignCenter().Bold(true).Size(body, body).Line(d.DoctorName).Bold(false).Size(1, 1);
        if (!string.IsNullOrWhiteSpace(d.DoctorQualification)) b.Line(d.DoctorQualification);
        if (!string.IsNullOrWhiteSpace(d.DoctorSpecialty)) b.Line(d.DoctorSpecialty);
        b.Line(sep);

        b.AlignCenter().Line("TOKEN NO.").Bold(true).Size(p.TokenSize, p.TokenSize).Line(d.TokenNumber.ToString()).Size(1, 1).Bold(false);
        if (d.IsCancelled) b.Bold(true).Line("*** CANCELLED ***").Bold(false);
        b.Line(sep);

        b.Align(p.Alignment).Bold(p.BoldText).Size(body, body);
        foreach (var l in EscPosBuilder.Wrap(d.PatientName, w / body)) b.Line(l);
        b.Size(1, 1).Bold(false);
        b.Align(SlipAlignment.Left);
        b.Line(EscPosBuilder.Justify("Patient ID:", d.PatientNumber, w));
        b.Line(EscPosBuilder.Justify("Age / Gender:", d.AgeGender, w));
        b.Line(EscPosBuilder.Justify("Date:", d.DateText, w));
        if (!string.IsNullOrWhiteSpace(d.TimeText)) b.Line(EscPosBuilder.Justify("Time:", d.TimeText, w));
        if (!string.IsNullOrWhiteSpace(d.FeeText)) b.Bold(p.BoldText).Line(EscPosBuilder.Justify("Consultation fee:", d.FeeText, w)).Bold(false);
        b.Line(EscPosBuilder.Justify("Appt. no.:", d.AppointmentNumber, w));
        b.Line(sep);
        b.AlignCenter();
        if (!string.IsNullOrWhiteSpace(d.Message)) b.Bold(true).Line(d.Message).Bold(false);
        if (!string.IsNullOrWhiteSpace(d.ContactText)) foreach (var l in EscPosBuilder.Wrap(d.ContactText, w)) b.Line(l);
        b.Line(d.PrintedText + (d.IsReprint ? " (REPRINT)" : ""));
        b.Feed(3);
        if (p.AutoCut) b.Cut();
        var one = b.ToArray();
        return Enumerable.Repeat(one, Math.Clamp(p.Copies, 1, 3)).SelectMany(x => x).ToArray();
    }

    public static byte[] TestPage(string hospitalName, PrinterProfile p)
    {
        var w = p.CharsPerLine;
        var b = new EscPosBuilder().Initialize().AlignCenter().Bold(true).Line(hospitalName).Bold(false)
            .Line("Burhan Nexus HMS - printer test").Line(new string('-', w))
            .Line($"Paper {p.PaperWidthMm} mm - {w} characters per line")
            .Align(SlipAlignment.Left).Line(new string('1', w)).Line(EscPosBuilder.Justify("LEFT", "RIGHT", w))
            .AlignCenter().Line("TOKEN").Bold(true).Size(p.TokenSize, p.TokenSize).Line("88").Size(1, 1).Bold(false)
            .Line(new string('-', w)).Line("If you can read this, the printer works.").Feed(3);
        if (p.AutoCut) b.Cut();
        return b.ToArray();
    }
}
