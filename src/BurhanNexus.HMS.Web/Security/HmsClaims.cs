using System.Security.Claims;

namespace BurhanNexus.HMS.Web.Security;

public static class HmsClaims
{
    public const string FullName = "bn:full_name";
    public const string SessionId = "bn:sid";
    public const string SecurityStamp = "bn:stamp";
    public const string MustChangePassword = "bn:mcp";
    public const string SystemAdministrator = "bn:sysadmin";

    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Guid? GetSessionId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionId), out var id) ? id : null;

    public static string GetFullName(this ClaimsPrincipal user) =>
        user.FindFirstValue(FullName) ?? user.Identity?.Name ?? "User";

    public static bool MustChangePasswordFlag(this ClaimsPrincipal user) => user.HasClaim(MustChangePassword, "1");
    public static bool IsSystemAdministrator(this ClaimsPrincipal user) => user.HasClaim(SystemAdministrator, "1");
}
