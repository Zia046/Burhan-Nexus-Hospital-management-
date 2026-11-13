using BurhanNexus.HMS.Infrastructure.Licensing;
using BurhanNexus.HMS.Web.Security;

namespace BurhanNexus.HMS.Web.Middleware;

/// <summary>
/// 1. Users who must change their password are sent to the change-password page.
/// 2. When the licence is not valid, the system becomes READ-ONLY (no data is ever deleted):
///    viewing, backup and licence activation remain available.
/// </summary>
public sealed class RequestPolicyMiddleware
{
    private static readonly string[] ReadOnlyExemptPaths = { "/Account", "/Settings/License", "/Backup", "/Setup" };
    private readonly RequestDelegate _next;

    public RequestPolicyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILicenseService license)
    {
        var path = context.Request.Path;
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (context.User.MustChangePasswordFlag() && !path.StartsWithSegments("/Account")
                && !path.StartsWithSegments("/branding") && !path.StartsWithSegments("/api"))
            {
                context.Response.Redirect("/Account/ChangePassword");
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
                && !ReadOnlyExemptPaths.Any(p => path.StartsWithSegments(p)))
            {
                var status = await license.GetStatusAsync(context.RequestAborted);
                if (status.State is not (Core.Licensing.LicenseState.TrialActive or Core.Licensing.LicenseState.Active
                    or Core.Licensing.LicenseState.NotInitialized))
                {
                    if (path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { error = "Read-only mode: " + status.Message });
                        return;
                    }
                    context.Response.Redirect("/Settings/License?readonly=1");
                    return;
                }
            }
        }
        await _next(context);
    }
}

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "same-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; script-src 'self'; " +
            "font-src 'self'; connect-src 'self' http://127.0.0.1:* http://localhost:*; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";
        if (context.User.Identity?.IsAuthenticated == true)
            h["Cache-Control"] = "no-store";
        return _next(context);
    }
}
