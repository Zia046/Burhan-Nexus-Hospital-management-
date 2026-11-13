using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;

namespace BurhanNexus.HMS.Web.Security;

/// <summary>Supplies user and workstation details of the current HTTP request to services (for auditing).</summary>
public sealed partial class HttpRequestContext : IRequestContext
{
    public const string WorkstationCookie = "BNHMS.Workstation";
    public const string WorkstationItem = "bn:workstation";
    private readonly IHttpContextAccessor _accessor;

    public HttpRequestContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Http => _accessor.HttpContext;

    public int? UserId => Http?.User.GetUserId();

    public string? Username => Http is null ? "SYSTEM" : Http.User.Identity?.IsAuthenticated == true ? Http.User.Identity.Name : null;

    public string? IpAddress => Http?.Connection.RemoteIpAddress?.MapToIPv4().ToString();

    public string? Workstation
    {
        get
        {
            if (Http is null) return Environment.MachineName;
            var name = SanitizeWorkstation(Http.Items[WorkstationItem] as string ?? Http.Request.Cookies[WorkstationCookie]);
            return name ?? IpAddress;
        }
    }

    public string? UserAgent => Http?.Request.Headers.UserAgent.ToString();

    public static string? SanitizeWorkstation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = AllowedChars().Replace(value.Trim(), string.Empty);
        if (cleaned.Length == 0) return null;
        return cleaned.Length > 50 ? cleaned[..50] : cleaned;
    }

    [GeneratedRegex(@"[^A-Za-z0-9 _\-\.]")]
    private static partial Regex AllowedChars();
}
