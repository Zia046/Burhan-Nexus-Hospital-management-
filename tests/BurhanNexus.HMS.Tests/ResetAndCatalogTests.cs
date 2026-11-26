using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public class ResetAndCatalogTests : HmsTestBase
{
    [Fact]
    public void Permission_catalogue_is_consistent()
    {
        Assert.Equal(PermissionCatalog.All.Count, PermissionCatalog.AllKeys.Count);
        foreach (var module in new[] { "Patients", "Opd", "Emergency", "MinorOt", "Doctors", "Reports" })
            foreach (var action in new[] { "View", "Add", "Edit", "Delete", "Print", "ExportPdf", "Verify", "Approve", "Cancel", "Refund", "FinancialAccess", "SettingsAccess" })
                Assert.Contains($"{module}.{action}", PermissionCatalog.AllKeys);
        // Phase 3: Finance became a real module with its own, specific financial permissions.
        var finance = PermissionCatalog.FindModule("Finance")!;
        Assert.True(finance.IsAvailable);
        Assert.True(finance.IsFinancialModule);
        Assert.Equal(31, finance.Actions.Count); // update 10: inpatient payments, advances, clearance; update 11: own doctor account, hand to admin, doctor reports
        // Phase 4: Laboratory became a real module with its own specific permissions.
        var lab = PermissionCatalog.FindModule("Laboratory")!;
        Assert.True(lab.IsAvailable);
        Assert.Equal(28, lab.Actions.Count); // update 10: payments, receipts, review
        Assert.Contains(Perm.LabVerifyOwnResults, PermissionCatalog.AllKeys);
        // Phase 5: Pharmacy became a real module.
        var pharm = PermissionCatalog.FindModule("Pharmacy")!;
        Assert.True(pharm.IsAvailable);
        Assert.Equal(26, pharm.Actions.Count); // update 9: ward medicine requests
        Assert.Contains(Perm.PharmApproveAdjustments, PermissionCatalog.AllKeys);
        // Phase 6: X-Ray and Ultrasound became one real Radiology module.
        var rad = PermissionCatalog.FindModule("Radiology")!;
        Assert.True(rad.IsAvailable);
        Assert.Equal(28, rad.Actions.Count); // Update 15: + View ECG Worklist
        Assert.Null(PermissionCatalog.FindModule("XRay"));
        Assert.Contains(Perm.RadVerifyReport, PermissionCatalog.AllKeys);
        // Phase 7: IPD, maternity and NICU.
        var ipd = PermissionCatalog.FindModule("Ipd")!;
        Assert.True(ipd.IsAvailable);
        Assert.Equal(47, ipd.Actions.Count); // update 10: ledger, statements, admission fee, ward charges, equipment, packages
        // Phase 8: Operation Theatre (the Minor OT stays a pharmacy department location – no module of its own).
        var ot = PermissionCatalog.FindModule("Ot")!;
        Assert.True(ot.IsAvailable);
        Assert.Equal(26, ot.Actions.Count);
        Assert.Contains(Perm.OtAuthorizeException, PermissionCatalog.AllKeys);
        foreach (var key in typeof(Perm).GetFields().Select(f => (string)f.GetValue(null)!))
            Assert.Contains(key, PermissionCatalog.AllKeys);
    }

    [Fact]
    public async Task Only_primary_admin_can_rerun_setup_and_data_is_kept()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var staffId = await CreateUserAsync(admin, "keep.me", "Keep#Pass2026");
        await SetPermissionsAsync(admin, staffId, (Perm.DashboardView, "grant"), (Perm.SettingsView, "grant"));

        var staff = Factory.NewBrowser();
        await LoginAsync(staff, "keep.me", "Keep#Pass2026");
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync("/Settings/ResetSetup")).Headers.Location!.ToString());

        var wrong = await admin.PostFormAsync("/Settings/ResetSetup", new[] { P("Password", "nope-nope1"), P("Confirmation", "RESET") });
        Assert.Equal("/Settings/ResetSetup", wrong.Headers.Location?.OriginalString);

        var reset = await admin.PostFormAsync("/Settings/ResetSetup", new[] { P("Password", AdminPassword), P("Confirmation", "RESET") });
        Assert.Equal("/Setup", reset.Headers.Location?.OriginalString);

        // Other users see "setup in progress"; the administrator re-runs the wizard without creating a new admin.
        Assert.Equal("/Setup", (await staff.GetAsync("/")).Headers.Location?.OriginalString);
        Assert.Contains("Setup in progress", await staff.GetHtmlAsync("/Setup"));
        var html = await admin.GetHtmlAsync("/Setup");
        Assert.Contains("Re-run Setup Wizard", html);
        Assert.Contains("Burhan Test Hospital", html);

        var fields = SetupFields().Where(f => !f.Key.StartsWith("Admin") && f.Key != "HospitalName").Append(P("HospitalName", "Renamed After Reset"));
        var done = await admin.PostMultipartAsync("/Setup", fields);
        Assert.True(done.StatusCode == HttpStatusCode.Redirect, Errors(await done.Content.ReadAsStringAsync()));

        await using var db = Factory.NewDb();
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal("Renamed After Reset", (await db.HospitalSettings.SingleAsync()).HospitalName);
        Assert.Single(await AuditAsync(AuditActions.SetupReset));
        Assert.Equal(2, (await AuditAsync(AuditActions.SetupCompleted)).Count);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Background_dashboard_refresh_does_not_keep_an_idle_session_alive()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        for (var i = 0; i < 4; i++)
        {
            Factory.Clock.Advance(TimeSpan.FromMinutes(10));
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
            req.Headers.Add("X-BNHMS-Background", "1");
            await admin.Http.SendAsync(req);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/dashboard/summary")).StatusCode);
    }
}
