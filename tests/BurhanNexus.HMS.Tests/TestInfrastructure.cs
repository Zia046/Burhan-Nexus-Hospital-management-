using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BurhanNexus.HMS.Tests;

/// <summary>Adjustable clock so tests can simulate trial expiry and session timeouts.</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset? _fixed;
    private TimeSpan _offset = TimeSpan.Zero;
    public override DateTimeOffset GetUtcNow() => (_fixed ?? DateTimeOffset.UtcNow) + _offset;
    public void Advance(TimeSpan by) => _offset += by;
}

public sealed class TestKeys
{
    public static readonly TestKeys Instance = new();
    public string PublicPem { get; }
    public string PrivatePem { get; }

    private TestKeys()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublicPem = ec.ExportSubjectPublicKeyInfoPem();
        PrivatePem = ec.ExportPkcs8PrivateKeyPem();
    }
}

/// <summary>Starts the real application against a fresh, uniquely named PostgreSQL database.</summary>
public sealed class HmsFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; }
    public string ConnectionString { get; }
    public string DataDir { get; }
    public string BackupDir { get; }
    public TestClock Clock { get; } = new();

    public HmsFactory(string? databaseName = null, string? dataDir = null, string? backupDir = null)
    {
        var baseCs = Environment.GetEnvironmentVariable("HMS_TEST_PG")
                     ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";
        DatabaseName = databaseName ?? "hms_test_" + Guid.NewGuid().ToString("N")[..12];
        ConnectionString = new NpgsqlConnectionStringBuilder(baseCs) { Database = DatabaseName, Pooling = true }.ConnectionString;
        var root = Path.Combine(Path.GetTempPath(), "bnhms-tests");
        DataDir = dataDir ?? Path.Combine(root, DatabaseName, "data");
        BackupDir = backupDir ?? Path.Combine(root, DatabaseName, "backups");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:HmsDatabase", ConnectionString);
        builder.UseSetting("Hms:DataDirectory", DataDir);
        builder.UseSetting("Hms:BackupDirectory", BackupDir);
        builder.UseSetting("Hms:LoginRateLimitPerMinute", "100000");
        builder.UseSetting("Testing:LicensePublicKeyPem", TestKeys.Instance.PublicPem);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    /// <summary>A browser-like client (keeps cookies, does not follow redirects automatically).</summary>
    public Browser NewBrowser() => new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }));

    public HmsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HmsDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task DropDatabaseAsync()
    {
        NpgsqlConnection.ClearAllPools();
        var admin = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString;
        await using var conn = new NpgsqlConnection(admin);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}

/// <summary>Simulates one computer's browser: cookies + antiforgery-aware form posts.</summary>
public sealed partial class Browser
{
    public HttpClient Http { get; }
    public Browser(HttpClient http) => Http = http;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);

    public async Task<string> GetHtmlAsync(string url)
    {
        var r = await Http.GetAsync(url);
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"GET {url} returned {(int)r.StatusCode} {r.Headers.Location}");
        return await r.Content.ReadAsStringAsync();
    }

    public async Task<string> TokenAsync(string pageUrl)
    {
        var html = await GetHtmlAsync(pageUrl);
        var m = TokenRegex().Match(html);
        Assert.True(m.Success, $"No antiforgery token on {pageUrl}");
        return WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    /// <summary>GETs the page (for the antiforgery token) then posts the form.</summary>
    public async Task<HttpResponseMessage> PostFormAsync(string pageUrl, IEnumerable<KeyValuePair<string, string>> fields, string? postUrl = null)
    {
        var token = await TokenAsync(pageUrl);
        var all = fields.Append(new("__RequestVerificationToken", token)).ToList();
        return await Http.PostAsync(postUrl ?? pageUrl, new FormUrlEncodedContent(all));
    }

    public async Task<HttpResponseMessage> PostMultipartAsync(string pageUrl, IEnumerable<KeyValuePair<string, string>> fields,
        (string Name, string FileName, byte[] Content, string ContentType)? file = null, string? postUrl = null)
    {
        var token = await TokenAsync(pageUrl);
        var content = new MultipartFormDataContent();
        foreach (var (k, v) in fields) content.Add(new StringContent(v), k);
        content.Add(new StringContent(token), "__RequestVerificationToken");
        if (file is { } f)
        {
            var bytes = new ByteArrayContent(f.Content);
            bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(f.ContentType);
            content.Add(bytes, f.Name, f.FileName);
        }
        return await Http.PostAsync(postUrl ?? pageUrl, content);
    }
}

public static class F
{
    public static KeyValuePair<string, string> P(string k, string v) => new(k, v);
}
