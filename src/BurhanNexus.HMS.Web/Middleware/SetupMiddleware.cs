using BurhanNexus.HMS.Infrastructure.Services;

namespace BurhanNexus.HMS.Web.Middleware;

/// <summary>Sends every request to the setup wizard until the initial setup has been completed.</summary>
public sealed class SetupMiddleware
{
    private readonly RequestDelegate _next;

    public SetupMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISetupService setup)
    {
        var path = context.Request.Path;
        var state = await setup.GetStateAsync(context.RequestAborted);
        var isSetupPath = path.StartsWithSegments("/Setup");
        var alwaysAllowed = path.StartsWithSegments("/health") || path.StartsWithSegments("/branding")
                            || path.StartsWithSegments("/api/system/ping") || path.StartsWithSegments("/Error");

        if (state == SetupState.NotConfigured && !isSetupPath && !alwaysAllowed)
        {
            if (path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { error = "Initial setup has not been completed." });
                return;
            }
            context.Response.Redirect("/Setup");
            return;
        }

        if (state == SetupState.ResetRequested && !isSetupPath && !alwaysAllowed
            && !path.StartsWithSegments("/Account") && context.Request.Method == HttpMethods.Get && !path.StartsWithSegments("/api"))
        {
            context.Response.Redirect("/Setup");
            return;
        }

        await _next(context);
    }
}
