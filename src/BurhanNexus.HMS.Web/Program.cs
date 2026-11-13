using System.Threading.RateLimiting;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Infrastructure;
using BurhanNexus.HMS.Web.Middleware;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // A Windows Service starts in System32, so use the application folder as content root there.
    ContentRootPath = Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null
});

// Runs as a Windows Service on the hospital server (no effect when started from a console).
builder.Host.UseWindowsService(o => o.ServiceName = "BurhanNexusHMS");
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

// ---- Infrastructure (PostgreSQL, services, data protection) ----
string? licenseKeyOverride = builder.Environment.IsEnvironment("Testing")
    ? builder.Configuration["Testing:LicensePublicKeyPem"]
    : null;
builder.Services.AddHmsInfrastructure(builder.Configuration, licenseKeyOverride);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
builder.Services.AddScoped<UserPermissionContext>();
builder.Services.AddScoped<DisplayFormatter>();
builder.Services.AddScoped<AppointmentSlipFactory>();
builder.Services.AddScoped<ReceiptSlipFactory>();

// ---- Authentication: server-validated cookie sessions ----
builder.Services.AddScoped<HmsCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "BNHMS.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.EventsType = typeof(HmsCookieEvents);
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(o =>
{
    // Everything requires login unless explicitly marked [AllowAnonymous].
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "BNHMS.Antiforgery";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.HeaderName = "X-CSRF-TOKEN";
});

// ---- Login brute-force protection (per computer IP) ----
var loginPerMinute = builder.Configuration.GetValue("Hms:LoginRateLimitPerMinute", 30);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AllowAnonymousToFolder("/Setup");
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddControllers();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

// ---- Database: migrate + seed permissions (never deletes data) ----
var autoMigrate = app.Configuration.GetValue("Hms:AutoMigrate", true);
await app.Services.InitializeHmsDatabaseAsync(autoMigrate);

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler("/Error");
}
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/Error", "?code={0}"));

app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SetupMiddleware>();
app.UseAuthorization();
app.UseMiddleware<RequestPolicyMiddleware>();

app.MapRazorPages();
app.MapControllers();

app.Logger.LogInformation("Burhan Nexus Hospital Management is listening on: {Urls}",
    string.Join(", ", app.Urls.DefaultIfEmpty(app.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://0.0.0.0:5000")));

app.Run();

public partial class Program;
