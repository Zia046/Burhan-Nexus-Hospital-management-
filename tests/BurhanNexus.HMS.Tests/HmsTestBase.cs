using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public abstract partial class HmsTestBase : IAsyncLifetime
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "Admin12345";
    public static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    protected HmsFactory Factory { get; set; } = null!;

    public virtual Task InitializeAsync()
    {
        Factory = new HmsFactory();
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Factory.DropDatabaseAsync();
    }

    protected static IEnumerable<KeyValuePair<string, string>> SetupFields(string hospitalType = "Private", string admin = AdminUser, string password = AdminPassword) => new[]
    {
        P("HospitalName", "Burhan Test Hospital"), P("HospitalType", hospitalType), P("RegistrationNumber", "REG-001"),
        P("Address", "Main Road"), P("City", "Peshawar"), P("Province", "Khyber Pakhtunkhwa"),
        P("ContactNumber", "091-1234567"), P("Email", "info@test-hospital.pk"), P("Website", "www.test-hospital.pk"),
        P("AdminFullName", "System Admin"), P("AdminUsername", admin), P("AdminPassword", password), P("AdminConfirmPassword", password),
        P("CurrencyCode", "PKR"), P("TimeZoneId", "Asia/Karachi"), P("DateFormat", "dd/MM/yyyy"), P("TimeFormat", "hh:mm tt"),
        P("DefaultLanguage", "en"), P("ReceiptPaperSize", "80mm"), P("ReportPaperSize", "A4")
    };

    protected async Task<Browser> SetupAsync(string hospitalType = "Private")
    {
        var b = Factory.NewBrowser();
        var r = await b.PostMultipartAsync("/Setup", SetupFields(hospitalType), ("Logo", "logo.png", TinyPng, "image/png"));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Account/Login", r.Headers.Location?.OriginalString);
        return b;
    }

    protected async Task<HttpResponseMessage> LoginAsync(Browser b, string username, string password, string? workstation = null)
    {
        var fields = new List<KeyValuePair<string, string>> { P("Username", username), P("Password", password) };
        if (workstation is not null) fields.Add(P("Workstation", workstation));
        return await b.PostFormAsync("/Account/Login", fields);
    }

    protected async Task<Browser> LoginAdminAsync()
    {
        var b = Factory.NewBrowser();
        var r = await LoginAsync(b, AdminUser, AdminPassword, "Server-PC");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/", r.Headers.Location?.OriginalString);
        return b;
    }

    [GeneratedRegex(@"/Users/Edit/(\d+)")] private static partial Regex EditIdRegex();

    protected async Task<int> CreateUserAsync(Browser admin, string username, string password, bool mustChange = false, params int[] departmentIds)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            P("FullName", "User " + username), P("Username", username), P("Designation", "Staff"),
            P("Password", password), P("ConfirmPassword", password), P("MustChangePassword", mustChange ? "true" : "false")
        };
        fields.AddRange(departmentIds.Select(d => P("DepartmentIds", d.ToString())));
        var r = await admin.PostFormAsync("/Users/Create", fields);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Create user failed: " + await r.Content.ReadAsStringAsync());
        return int.Parse(EditIdRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);
    }

    protected async Task SetPermissionsAsync(Browser admin, int userId, params (string Key, string Value)[] values)
    {
        var r = await admin.PostFormAsync($"/Users/Permissions/{userId}", values.Select(v => P($"P[{v.Key}]", v.Value)));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var page = await admin.GetHtmlAsync($"/Users/Permissions/{userId}");
        Assert.Contains("Permissions saved", page);
    }

    protected async Task<int> DepartmentIdAsync(string name)
    {
        await using var db = Factory.NewDb();
        return await db.Departments.Where(d => d.Name == name).Select(d => d.Id).SingleAsync();
    }

    protected async Task<List<AuditLog>> AuditAsync(string action)
    {
        await using var db = Factory.NewDb();
        return await db.AuditLogs.Where(a => a.Action == action).OrderBy(a => a.Id).ToListAsync();
    }
}
