using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using BurhanNexus.PrintAgent;

// Burhan Nexus Print Agent – runs on a reception computer and prints ESC/POS slips from the HMS web page
// to a USB / Windows printer (RAW, no print dialog) or to a LAN thermal printer. Listens on 127.0.0.1 only.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.Host.UseWindowsService(o => o.ServiceName = "BurhanNexusPrintAgent");
var port = builder.Configuration.GetValue("Agent:Port", 9123);
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AgentJson.Default));
var allowed = builder.Configuration.GetSection("Agent:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

var app = builder.Build();
const int MaxBytes = 256 * 1024;

// CORS + Chrome "Private Network Access": the HMS page (http://server-ip) calls this agent on 127.0.0.1.
app.Use(async (ctx, next) =>
{
    var origin = ctx.Request.Headers.Origin.ToString();
    if (origin.Length > 0)
    {
        if (!OriginPolicy.IsAllowed(origin, allowed))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new AgentResult(false, $"Origin {origin} is not allowed. Add it to Agent:AllowedOrigins in appsettings.json."), AgentJson.Default.AgentResult);
            return;
        }
        ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
        ctx.Response.Headers["Vary"] = "Origin";
        ctx.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
        if (HttpMethods.IsOptions(ctx.Request.Method))
        {
            ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            ctx.Response.Headers["Access-Control-Max-Age"] = "600";
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }
    }
    await next();
});

app.MapGet("/status", () => new AgentStatus(true, typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0",
    Environment.MachineName, RawPrinter.Supported, RawPrinter.List()));

app.MapPost("/print/raw", (RawPrintRequest r, ILogger<Program> log) =>
{
    if (string.IsNullOrWhiteSpace(r.PrinterName)) return Results.BadRequest(new AgentResult(false, "printerName is required."));
    if (!TryDecode(r.Data, out var bytes, out var error)) return Results.BadRequest(new AgentResult(false, error));
    try
    {
        for (var i = 0; i < Math.Clamp(r.Copies ?? 1, 1, 3); i++)
            RawPrinter.Send(r.PrinterName.Trim(), string.IsNullOrWhiteSpace(r.JobName) ? "Burhan Nexus slip" : r.JobName!, bytes);
        log.LogInformation("Printed {Bytes} bytes to {Printer}", bytes.Length, r.PrinterName);
        return Results.Ok(new AgentResult(true, null));
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "Print to {Printer} failed", r.PrinterName);
        return Results.Json(new AgentResult(false, ex.Message), AgentJson.Default.AgentResult, statusCode: 500);
    }
});

app.MapPost("/print/tcp", async (TcpPrintRequest r, ILogger<Program> log) =>
{
    if (!IPAddress.TryParse(r.Host?.Trim(), out var ip) || !OriginPolicy.IsPrivate(ip) || r.Port is < 1 or > 65535)
        return Results.BadRequest(new AgentResult(false, "A LAN printer IP address (private network) and port are required."));
    if (!TryDecode(r.Data, out var bytes, out var error)) return Results.BadRequest(new AgentResult(false, error));
    try
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(ip, r.Port, cts.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(bytes, cts.Token);
        log.LogInformation("Printed {Bytes} bytes to {Host}:{Port}", bytes.Length, ip, r.Port);
        return Results.Ok(new AgentResult(true, null));
    }
    catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
    {
        return Results.Json(new AgentResult(false, $"Printer {ip}:{r.Port} did not respond."), AgentJson.Default.AgentResult, statusCode: 502);
    }
});

app.Run();

static bool TryDecode(string? data, out byte[] bytes, out string error)
{
    bytes = Array.Empty<byte>();
    error = "";
    if (string.IsNullOrEmpty(data) || data.Length > MaxBytes * 4 / 3 + 8) { error = "data (base64) is missing or too large."; return false; }
    try { bytes = Convert.FromBase64String(data); }
    catch (FormatException) { error = "data must be base64."; return false; }
    if (bytes.Length is 0 or > MaxBytes) { error = "data is empty or too large."; return false; }
    return true;
}

public sealed record RawPrintRequest(string? PrinterName, string? Data, string? JobName, int? Copies);
public sealed record TcpPrintRequest(string? Host, int Port, string? Data, string? JobName);
public sealed record AgentResult(bool Ok, string? Error);
public sealed record AgentStatus(bool Ok, string Version, string Machine, bool RawPrintingSupported, IReadOnlyList<string> Printers);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RawPrintRequest))]
[JsonSerializable(typeof(TcpPrintRequest))]
[JsonSerializable(typeof(AgentResult))]
[JsonSerializable(typeof(AgentStatus))]
internal partial class AgentJson : JsonSerializerContext;

public partial class Program;
