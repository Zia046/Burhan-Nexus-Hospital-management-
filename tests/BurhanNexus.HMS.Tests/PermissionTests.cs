using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public class PermissionTests : HmsTestBase
{
    [Fact]
    public async Task Restricted_menus_are_hidden_and_direct_access_is_blocked()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var reception = await DepartmentIdAsync("Reception");
        var id = await CreateUserAsync(admin, "reception1", "Recep12345", false, reception);
        await SetPermissionsAsync(admin, id, (Perm.DashboardView, "grant"), ("Patients.View", "grant"));

        // A second computer.
        var pc2 = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(pc2, "reception1", "Recep12345", "Reception-1")).StatusCode);
        var dash = await pc2.GetHtmlAsync("/");
        Assert.DoesNotContain("href=\"/Users\"", dash);
        Assert.DoesNotContain("href=\"/Backup\"", dash);
        Assert.DoesNotContain("href=\"/Settings\"", dash);
        Assert.DoesNotContain("href=\"/Audit\"", dash);
        Assert.DoesNotContain("Pharmacy Management", dash);       // not assigned → not visible at all
        Assert.Contains("Patient Management", dash);              // assigned future module → shown as upcoming
        Assert.DoesNotContain("Registered users", dash);          // no hospital-wide figures

        foreach (var url in new[] { "/Users", "/Users/Create", $"/Users/Permissions/{id}", "/Backup", "/Settings", "/Settings/License", "/Audit", "/Departments", "/Roles" })
        {
            var r = await pc2.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied"),
                $"{url} should be denied but returned {(int)r.StatusCode} {r.Headers.Location}");
        }
        foreach (var api in new[] { "/api/users", "/api/audit", "/api/backups" })
            Assert.Equal(HttpStatusCode.Forbidden, (await pc2.GetAsync(api)).StatusCode);

        // A forged POST to create a user is rejected on the server.
        var token = await pc2.TokenAsync("/");
        var forged = await pc2.Http.PostAsync("/Users/Create", new FormUrlEncodedContent(new[]
        {
            P("FullName", "Hacker"), P("Username", "hacker"), P("Password", "Hacker1234"), P("ConfirmPassword", "Hacker1234"),
            P("__RequestVerificationToken", token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, forged.StatusCode);
        Assert.Contains("/Account/AccessDenied", forged.Headers.Location!.ToString());

        await using var db = Factory.NewDb();
        Assert.False(await db.Users.AnyAsync(u => u.Username == "hacker"));
        Assert.NotEmpty(await AuditAsync(AuditActions.PermissionDenied));
        Assert.Single(await AuditAsync(AuditActions.UserPermissionsChanged));
        Assert.Single(await AuditAsync(AuditActions.UserCreated));

        var me = await (await pc2.GetAsync("/api/me")).Content.ReadAsStringAsync();
        Assert.Contains("Patients.View", me);
        Assert.DoesNotContain("UserManagement.View", me);
    }

    [Fact]
    public async Task Users_cannot_change_their_own_permissions_or_escalate()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var managerId = await CreateUserAsync(admin, "manager1", "Manager123");
        await SetPermissionsAsync(admin, managerId, (Perm.DashboardView, "grant"), (Perm.UsersView, "grant"), (Perm.UsersAdd, "grant"),
            (Perm.UsersAssignPermissions, "grant"));
        var staffId = await CreateUserAsync(admin, "staff1", "Staff12345");

        var manager = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(manager, "manager1", "Manager123")).StatusCode);

        // Own permissions: read-only page and server-side rejection.
        Assert.Contains("You cannot change your own permissions", await manager.GetHtmlAsync($"/Users/Permissions/{managerId}"));
        var self = await manager.PostFormAsync($"/Users/Permissions/{managerId}", new[] { P($"P[{Perm.BackupRestore}]", "grant") });
        Assert.Equal(HttpStatusCode.Redirect, self.StatusCode);
        Assert.Contains("cannot change your own", await manager.GetHtmlAsync($"/Users/Permissions/{managerId}"));

        // Granting a permission the manager does not hold is refused.
        await manager.PostFormAsync($"/Users/Permissions/{staffId}", new[] { P($"P[{Perm.BackupRestore}]", "grant") });
        Assert.Contains("only assign permissions that you have", await manager.GetHtmlAsync($"/Users/Permissions/{staffId}"));

        // Granting one the manager holds works.
        await manager.PostFormAsync($"/Users/Permissions/{staffId}", new[] { P($"P[{Perm.UsersView}]", "grant") });
        await using var db = Factory.NewDb();
        var keys = await db.UserPermissions.Where(p => p.UserId == staffId).Select(p => p.Permission!.Key).ToListAsync();
        Assert.Equal(new[] { Perm.UsersView }, keys);
        Assert.False(await db.UserPermissions.AnyAsync(p => p.UserId == managerId && p.Permission!.Key == Perm.BackupRestore));

        // Cannot touch the primary system administrator.
        var adminId = await db.Users.Where(u => u.IsSystemAdministrator).Select(u => u.Id).SingleAsync();
        await manager.PostFormAsync($"/Users/Permissions/{adminId}", new[] { P($"P[{Perm.UsersView}]", "deny") });
        Assert.False(await db.UserPermissions.AnyAsync(p => p.UserId == adminId));
    }

    [Fact]
    public async Task Responsibilities_grant_permissions_and_deny_overrides()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var save = await admin.PostFormAsync("/Roles/Edit", new[]
        {
            P("Name", "Receptionist"), P("Description", "Front desk"), P("IsActive", "true"),
            P("Keys", Perm.DashboardView), P("Keys", "Patients.View"), P("Keys", "Patients.Add"), P("Keys", "Opd.View")
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        await using var db = Factory.NewDb();
        var roleId = await db.Roles.Where(r => r.Name == "Receptionist").Select(r => r.Id).SingleAsync();

        var id = await CreateUserAsync(admin, "front1", "Front12345");
        var roles = await admin.PostFormAsync($"/Users/Edit/{id}", new[] { P("RoleIds", roleId.ToString()) }, $"/Users/Edit/{id}?handler=Roles");
        Assert.Equal(HttpStatusCode.Redirect, roles.StatusCode);
        await SetPermissionsAsync(admin, id, ("Opd.View", "deny"));

        var user = Factory.NewBrowser();
        await LoginAsync(user, "front1", "Front12345");
        var me = await (await user.GetAsync("/api/me")).Content.ReadAsStringAsync();
        Assert.Contains("Patients.Add", me);
        Assert.DoesNotContain("Opd.View", me);
        Assert.Single(await AuditAsync(AuditActions.UserRolesChanged));
        Assert.Single(await AuditAsync(AuditActions.RoleCreated));
    }

    [Fact]
    public async Task Deactivation_ends_sessions_on_all_computers()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var id = await CreateUserAsync(admin, "lab1", "Lab1234567");
        await SetPermissionsAsync(admin, id, (Perm.DashboardView, "grant"));

        var labPc = Factory.NewBrowser();
        await LoginAsync(labPc, "lab1", "Lab1234567", "Lab-PC");
        Assert.Equal(HttpStatusCode.OK, (await labPc.GetAsync("/")).StatusCode);

        var r = await admin.PostFormAsync($"/Users/Edit/{id}", new[] { P("active", "false") }, $"/Users/Edit/{id}?handler=Activate");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var after = await labPc.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/Account/Login", after.Headers.Location!.ToString());
        var relogin = await LoginAsync(Factory.NewBrowser(), "lab1", "Lab1234567");
        Assert.Contains("inactive", await relogin.Content.ReadAsStringAsync());

        // Reactivate → can log in again; admin cannot deactivate itself.
        await admin.PostFormAsync($"/Users/Edit/{id}", new[] { P("active", "true") }, $"/Users/Edit/{id}?handler=Activate");
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(Factory.NewBrowser(), "lab1", "Lab1234567")).StatusCode);
        Assert.Single(await AuditAsync(AuditActions.UserDeactivated));
        Assert.Single(await AuditAsync(AuditActions.UserActivated));
    }

    [Fact]
    public async Task Departments_assignment_and_password_reset_flow()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var create = await admin.PostFormAsync("/Departments", new[] { P("Name", "Dental"), P("Code", "den") }, "/Departments?handler=Create");
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var dental = await DepartmentIdAsync("Dental");
        var lab = await DepartmentIdAsync("Laboratory");

        var id = await CreateUserAsync(admin, "multi1", "Multi12345", false, dental);
        await admin.PostFormAsync($"/Users/Edit/{id}", new[] { P("DepartmentIds", dental.ToString()), P("DepartmentIds", lab.ToString()) }, $"/Users/Edit/{id}?handler=Departments");
        await using (var db = Factory.NewDb())
            Assert.Equal(2, await db.UserDepartments.CountAsync(d => d.UserId == id));

        await admin.PostFormAsync($"/Users/Edit/{id}", new[] { P("NewPassword", "Reset12345"), P("MustChange", "true") }, $"/Users/Edit/{id}?handler=ResetPassword");
        var user = Factory.NewBrowser();
        var login = await LoginAsync(user, "multi1", "Reset12345");
        Assert.Equal("/Account/ChangePassword", login.Headers.Location?.OriginalString);
        var blocked = await user.GetAsync("/");
        Assert.Equal("/Account/ChangePassword", blocked.Headers.Location?.OriginalString);
        var change = await user.PostFormAsync("/Account/ChangePassword", new[] { P("CurrentPassword", "Reset12345"), P("NewPassword", "Final12345"), P("ConfirmPassword", "Final12345") });
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(Factory.NewBrowser(), "multi1", "Final12345")).StatusCode);
        Assert.Single(await AuditAsync(AuditActions.UserPasswordReset));
        Assert.Single(await AuditAsync(AuditActions.UserDepartmentsChanged));
        Assert.Single(await AuditAsync(AuditActions.DepartmentCreated));
    }

    [Fact]
    public async Task Concurrent_edits_from_two_computers_are_detected()
    {
        await SetupAsync();
        var pc1 = await LoginAdminAsync();
        var id = await CreateUserAsync(pc1, "shared1", "Shared1234");
        var pc2 = await LoginAdminAsync();

        uint version;
        await using (var db = Factory.NewDb()) version = (await db.Users.SingleAsync(u => u.Id == id)).RowVersion;
        IEnumerable<KeyValuePair<string, string>> Form(string name) => new[]
        {
            P("FullName", name), P("Username", "shared1"), P("RowVersion", version.ToString())
        };
        var first = await pc1.PostFormAsync($"/Users/Edit/{id}", Form("Edited On PC1"), $"/Users/Edit/{id}?handler=Profile");
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        await pc2.PostFormAsync($"/Users/Edit/{id}", Form("Edited On PC2"), $"/Users/Edit/{id}?handler=Profile");
        var page = await pc2.GetHtmlAsync($"/Users/Edit/{id}");
        Assert.Contains("changed by another user", page);

        // PC2 sees PC1's change immediately (central database).
        Assert.Contains("Edited On PC1", page);

        // Duplicate usernames are rejected.
        var dup = await pc2.PostFormAsync("/Users/Create", new[] { P("FullName", "Dup"), P("Username", "SHARED1"), P("Password", "Shared1234"), P("ConfirmPassword", "Shared1234") });
        Assert.Contains("already in use", await dup.Content.ReadAsStringAsync());
    }
}
