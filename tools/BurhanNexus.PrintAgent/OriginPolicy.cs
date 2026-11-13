using System.Net;
using System.Net.Sockets;

namespace BurhanNexus.PrintAgent;

/// <summary>Only the hospital's own HMS server may use the agent: configured origins, or (if none configured) origins on the local network.</summary>
public static class OriginPolicy
{
    public static bool IsAllowed(string origin, IReadOnlyCollection<string> allowed)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return false;
        if (allowed.Count > 0)
            return allowed.Any(a => string.Equals(a.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        if (uri.IsLoopback) return true;
        if (IPAddress.TryParse(uri.Host, out var ip)) return IsPrivate(ip);
        // Single-label computer names on the LAN (e.g. http://hms-server:5000)
        return !uri.Host.Contains('.');
    }

    public static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
    }
}
