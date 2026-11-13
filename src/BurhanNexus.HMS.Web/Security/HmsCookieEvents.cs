using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Infrastructure.Security;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BurhanNexus.HMS.Web.Security;

/// <summary>
/// Validates every authenticated request against the central database: session still open,
/// account active, not timed out. This is what makes logout/deactivation effective on all computers.
/// </summary>
public sealed class HmsCookieEvents : CookieAuthenticationEvents
{
    public const string BackgroundHeader = "X-BNHMS-Background";
    private const string EndReasonKey = "bn:session-end";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var user = context.Principal;
        var userId = user?.GetUserId();
        var sessionId = user?.GetSessionId();
        var stamp = user?.FindFirst(HmsClaims.SecurityStamp)?.Value;
        if (userId is null || sessionId is null || stamp is null)
        {
            await Reject(context, "invalid");
            return;
        }

        var http = context.HttpContext;
        var background = http.Request.Headers.ContainsKey(BackgroundHeader);
        var auth = http.RequestServices.GetRequiredService<IAuthService>();
        var result = await auth.ValidateSessionAsync(userId.Value, sessionId.Value, stamp, touch: !background, http.RequestAborted);
        if (result != SessionCheck.Valid)
            await Reject(context, result == SessionCheck.Expired ? "timeout" : "ended");
    }

    private static async Task Reject(CookieValidatePrincipalContext context, string reason)
    {
        context.RejectPrincipal();
        context.HttpContext.Items[EndReasonKey] = reason;
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsApi(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        var redirect = context.RedirectUri;
        if (context.HttpContext.Items.TryGetValue(EndReasonKey, out var reason) && reason is string r)
            redirect += (redirect.Contains('?') ? "&" : "?") + "reason=" + r;
        context.Response.Redirect(redirect);
        return Task.CompletedTask;
    }

    public override async Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        var audit = context.HttpContext.RequestServices.GetRequiredService<IAuditService>();
        var path = context.Request.Path + context.Request.QueryString;
        await audit.LogAsync(AuditActions.PermissionDenied, "Url", null, $"Access denied: {context.Request.Method} {path}".Length > 900
            ? $"Access denied: {context.Request.Method} {context.Request.Path}" : $"Access denied: {context.Request.Method} {path}", success: false);

        if (IsApi(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        context.Response.Redirect(context.RedirectUri);
    }

    public static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments("/api");
}
