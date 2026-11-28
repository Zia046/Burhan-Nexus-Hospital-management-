using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Security;

namespace BurhanNexus.HMS.Tests;

/// <summary>Navigation reorganisation: the administrator keeps 11 pages in the sidebar and finds every other category in the
/// top taskbar; other users keep their permission-based sidebar; every link opens; no page is listed twice.</summary>
public partial class NavigationTests : Phase2TestBase
{
    [GeneratedRegex("href=\"([^\"#]+)\"")] private static partial Regex HrefRegex();

    private static string Block(string html, string startMarker)
    {
        var i = html.IndexOf(startMarker, StringComparison.Ordinal);
        if (i < 0) return "";
        var end = html.IndexOf("</nav>", i, StringComparison.Ordinal);
        return html[i..end];
    }

    private static List<string> Links(string block) => HrefRegex().Matches(block).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();

    private static readonly string[] AdminSidebar =
    {
        "/Patients/Register?next=appointment", "/Appointments", "/Opd", "/Emergency", "/Finance", "/Finance/Services",
        "/Users", "/Departments", "/Audit", "/Settings", "/Backup",
    };

    [Fact]
    public async Task Admin_sidebar_keeps_eleven_pages_and_the_top_taskbar_holds_every_other_category()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var home = await admin.GetHtmlAsync("/");
        var sidebar = Links(Block(home, "data-sidebar-nav=\"admin\""));
        var bar = Block(home, "data-adm-bar");
        var barLinks = Links(bar);

        Assert.Equal(AdminSidebar.OrderBy(x => x), sidebar.OrderBy(x => x));
        foreach (var cat in new[] { "appointments", "patients", "lab", "ipd", "radiology", "pharmacy", "finance", "admin", "system" })
            Assert.Contains($"data-adm-cat=\"{cat}\"", bar);
        foreach (var moved in new[] { "/Appointments/Reports", "/Patients/Register", "/Patients", "/Lab", "/Lab/Settings", "/Ipd", "/Ipd/Nicu", "/Radiology", "/Radiology/Settings",
                     "/Pharmacy", "/Pharmacy/Sale", "/Finance/Collect", "/Finance/Dashboard", "/Roles", "/Users/LoginHistory", "/Settings/License" })
            Assert.Contains(moved, barLinks);
        // No page appears twice – neither inside the taskbar nor in both places.
        var menuLinks = barLinks.Where(l => l != "/").ToList();
        Assert.Equal(menuLinks.Count, menuLinks.Distinct().Count());
        Assert.Empty(menuLinks.Intersect(sidebar));

        // Every link opens (direct access / refresh) and highlights its category.
        foreach (var url in sidebar.Concat(menuLinks).Distinct())
        {
            var r = await admin.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{url} returned {(int)r.StatusCode} {r.Headers.Location}");
        }
        var lab = await admin.GetHtmlAsync("/Lab");
        Assert.Contains("adm-cat active\" data-adm-cat=\"lab\"", lab);
        Assert.Contains("class=\"adm-link active\" href=\"/Lab\"", lab);
        Assert.Contains("class=\"nav-link active\" href=\"/Opd\"", await admin.GetHtmlAsync("/Opd"));
    }

    [Fact]
    public async Task Other_users_keep_their_permission_based_sidebar_without_the_admin_taskbar()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var gov = Block(await admin.GetHtmlAsync("/"), "data-sidebar-nav=\"admin\"");
        Assert.Contains("/Appointments", gov); // Update 16: one HMS – consultant (appointment) tokens everywhere

        var lab = await UserWithAsync(admin, "lab.tech", Perm.DashboardView, Perm.LabDashboard, Perm.LabEnterResults, Perm.FinanceViewOwnAccount);
        var html = await lab.GetHtmlAsync("/");
        Assert.DoesNotContain("data-adm-bar", html);
        var side = Links(Block(html, "data-sidebar-nav=\"user\""));
        Assert.Equal(new[] { "/", "/Lab", "/Lab/Pending", "/Finance" }, side);
        Assert.Contains("<div class=\"nav-section\">Laboratory</div>", html);
        foreach (var hidden in new[] { "/Users", "/Settings", "/Backup", "/Audit", "/Ipd", "/Radiology", "/Pharmacy" })
            Assert.DoesNotContain($"href=\"{hidden}\"", html);
        // Server-side permissions are unchanged.
        var users = await lab.GetAsync("/Users");
        Assert.True(users.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect, "Users page must stay protected");
        Assert.Equal(HttpStatusCode.OK, (await lab.GetAsync("/Lab/Pending")).StatusCode);
    }
}
