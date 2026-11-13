using System.Net;
using System.Net.Sockets;
using BurhanNexus.HMS.Core.Abstractions;

namespace BurhanNexus.HMS.Infrastructure.Printing;

public interface INetworkPrinter
{
    /// <summary>Sends raw ESC/POS bytes to a LAN printer (RAW / JetDirect, usually port 9100).</summary>
    Task<ServiceResult> SendAsync(string host, int port, byte[] data, CancellationToken ct = default);
}

public sealed class NetworkPrinter : INetworkPrinter
{
    public async Task<ServiceResult> SendAsync(string host, int port, byte[] data, CancellationToken ct = default)
    {
        var check = Validate(host, port);
        if (!check.Succeeded) return check;
        try
        {
            using var client = new TcpClient { NoDelay = true, SendTimeout = 5000 };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(IPAddress.Parse(host.Trim()), port, cts.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(data, cts.Token);
            await stream.FlushAsync(cts.Token);
            return ServiceResult.Ok();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            return ServiceResult.Fail($"The printer at {host}:{port} did not respond. Check that it is switched on, connected to the network and that the IP address is correct.");
        }
    }

    /// <summary>Only IP addresses on the local network (private ranges, link-local, loopback) and normal printer ports are allowed.</summary>
    public static ServiceResult Validate(string? host, int port)
    {
        if (port is < 1 or > 65535) return ServiceResult.Fail("Printer port must be between 1 and 65535 (usually 9100).");
        if (!IPAddress.TryParse(host?.Trim(), out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return ServiceResult.Fail("Enter the printer's IPv4 address, e.g. 192.168.1.50.");
        var b = ip.GetAddressBytes();
        var isPrivate = b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)
                        || (b[0] == 169 && b[1] == 254) || b[0] == 127;
        return isPrivate ? ServiceResult.Ok() : ServiceResult.Fail("Only printers on the local network (e.g. 192.168.x.x or 10.x.x.x) can be used.");
    }
}
