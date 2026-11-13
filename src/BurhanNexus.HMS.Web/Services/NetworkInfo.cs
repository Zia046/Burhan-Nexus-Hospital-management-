using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BurhanNexus.HMS.Web.Services;

public static class NetworkInfo
{
    /// <summary>LAN IPv4 addresses of the server computer (for "how to connect" information).</summary>
    public static IReadOnlyList<string> LanAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .Distinct()
                .ToList();
        }
        catch (NetworkInformationException)
        {
            return Array.Empty<string>();
        }
    }

    public static string LanAddressesText(HttpRequest request)
    {
        var port = request.Host.Port ?? (request.IsHttps ? 443 : 80);
        var scheme = request.Scheme;
        var list = LanAddresses().Select(ip => $"{scheme}://{ip}:{port}").ToList();
        return list.Count == 0 ? $"{scheme}://{request.Host}" : string.Join("  ·  ", list);
    }
}
