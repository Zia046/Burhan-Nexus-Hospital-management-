using System.Diagnostics;

namespace BurhanNexus.Launcher;

/// <summary>Only pages of the HMS server open inside the app; any other web address opens in the normal browser.</summary>
internal static class Navigation
{
    public static bool IsInternal(string? uri, string server)
    {
        if (string.IsNullOrEmpty(uri)) return true;
        if (uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)) return true;
        return Uri.TryCreate(uri, UriKind.Absolute, out var u) && Uri.TryCreate(server, UriKind.Absolute, out var s) &&
               string.Equals(u.GetLeftPart(UriPartial.Authority), s.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }

    public static void OpenExternal(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeMailto)) return;
        try { Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception) { /* no default browser – ignore */ }
    }
}
