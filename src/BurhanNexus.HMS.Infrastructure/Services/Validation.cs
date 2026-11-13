using System.Net.Mail;
using System.Text.RegularExpressions;

namespace BurhanNexus.HMS.Infrastructure.Services;

public static partial class Validation
{
    [GeneratedRegex(@"^[A-Za-z0-9._-]{3,64}$")] private static partial Regex UsernameRegex();
    [GeneratedRegex(@"^[0-9+()\-\s]{5,50}$")] private static partial Regex PhoneRegex();

    public static bool IsValidUsername(string s) => UsernameRegex().IsMatch(s.Trim());
    public static bool IsValidPhone(string s) => PhoneRegex().IsMatch(s.Trim());

    public static bool IsValidEmail(string s)
    {
        s = s.Trim();
        if (s.Length > 200 || s.Contains(' ')) return false;
        return MailAddress.TryCreate(s, out var addr) && addr.Address == s && s.Contains('.');
    }

    public static bool IsValidUrl(string s)
    {
        s = s.Trim();
        if (!s.Contains("://")) s = "https://" + s;
        return Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps) && u.Host.Contains('.');
    }

    /// <summary>Returns the image MIME type if the bytes are a PNG, JPEG, GIF or WebP image; otherwise null.</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return "image/gif";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }
}

/// <summary>Allowed values for preference drop-downs (validated server-side).</summary>
public static class Options
{
    public static readonly string[] Currencies = { "PKR", "USD", "AED", "SAR", "GBP", "EUR" };
    public static readonly string[] TimeZones = { "Asia/Karachi", "Asia/Dubai", "Asia/Riyadh", "Asia/Kabul", "Europe/London", "UTC" };
    public static readonly string[] DateFormats = { "dd/MM/yyyy", "dd-MM-yyyy", "dd-MMM-yyyy", "MM/dd/yyyy", "yyyy-MM-dd" };
    public static readonly string[] TimeFormats = { "hh:mm tt", "HH:mm" };
    public static readonly Dictionary<string, string> Languages = new() { ["en"] = "English", ["ur"] = "Urdu (اردو)" };
    public static readonly string[] ReceiptPaperSizes = { "80mm", "58mm", "A5", "A4" };
    public static readonly string[] ReportPaperSizes = { "A4", "Letter", "Legal", "A5" };
    public static readonly string[] Orientations = { "Portrait", "Landscape" };
    public static readonly string[] Provinces =
    {
        "Khyber Pakhtunkhwa", "Punjab", "Sindh", "Balochistan", "Islamabad Capital Territory", "Gilgit-Baltistan", "Azad Jammu & Kashmir"
    };
}
