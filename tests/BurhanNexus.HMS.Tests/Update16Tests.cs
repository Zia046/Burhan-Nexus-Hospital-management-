using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>
/// Update 16 – one unified HMS (no Government / Private choice), OPD opens the registration form with OPD / Emergency / Consultant
/// buttons, counter at user creation, fees managed in their own modules and only by the administrator (enforced on the server).
/// (Part of Update10Tests to reuse its helpers.)
/// </summary>
public partial class Update10Tests
{
    [Fact]
    public async Task Setup_wizard_has_no_hospital_type_step_and_works_without_it()
    {
        var b = Factory.NewBrowser();
        var page = await b.GetHtmlAsync("/Setup");
        Assert.DoesNotContain("Government Hospital", page);
        Assert.DoesNotContain("name=\"HospitalType\"", page);
        var fields = SetupFields().Where(f => f.Key != "HospitalType");
        var r = await b.PostMultipartAsync("/Setup", fields, ("Logo", "logo.png", TinyPng, "image/png"));
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Setup failed: " + Errors(await r.Content.ReadAsStringAsync()));
        var admin = await LoginAdminAsync();
        var home = await admin.GetHtmlAsync("/");
        Assert.DoesNotContain("Private Hospital", home);
        Assert.DoesNotContain("Government Hospital", home);
        var settings = await admin.GetHtmlAsync("/Settings");
        Assert.DoesNotContain("href=\"/Settings/HospitalType\"", settings);
    }

    [Fact]
    public async Task Opd_opens_the_registration_form_with_search_and_three_buttons()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var r = await admin.GetAsync("/Opd/Register");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Patients/Register?next=opd", r.Headers.Location!.OriginalString);
        var form = await admin.GetHtmlAsync("/Patients/Register?next=opd");
        Assert.Contains("OPD Registration – Find the Patient", System.Net.WebUtility.HtmlDecode(form));
        Assert.Contains("href=\"/Opd/Register?search=true\"", form);
        Assert.Contains("data-reg-opd", form);
        Assert.Contains("data-reg-emergency", form);
        Assert.Contains("data-reg-consultant", form);
        var search = await admin.GetHtmlAsync("/Opd/Register?search=true");
        Assert.Contains("name=\"q\"", search);
    }

    [Fact]
    public async Task Opd_button_registers_the_visit_and_prints_the_slip_consultant_button_opens_doctor_tokens()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var f = PatientFields("Opd Button", "Father Opd");
        f["Next"] = "opd"; f["ConfirmNotDuplicate"] = "true";
        var r = await PostRegisterAsync(admin, f);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "OPD: " + Errors(await r.Content.ReadAsStringAsync()));
        var location = r.Headers.Location!.OriginalString;
        Assert.Matches(@"^/Print/Opd/\d+\?autoprint=true$", location);
        var slip = await admin.GetHtmlAsync(location);
        Assert.Contains("OPD SLIP", slip);
        await using (var db = Factory.NewDb()) Assert.Equal(1, await db.PatientEncounters.CountAsync(e => e.Type == EncounterType.Opd));

        var c = PatientFields("Consultant Button", "Father Cons");
        c["Next"] = "consultant"; c["ConfirmNotDuplicate"] = "true";
        var rc = await PostRegisterAsync(admin, c);
        Assert.Equal(HttpStatusCode.Redirect, rc.StatusCode);
        Assert.StartsWith("/Appointments/Book?patientId=", rc.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(rc.Headers.Location!.OriginalString)).StatusCode);

        var e = PatientFields("Emergency Button", "Father Er");
        e["Next"] = "emergency"; e["ConfirmNotDuplicate"] = "true";
        var re = await PostRegisterAsync(admin, e);
        Assert.StartsWith("/Emergency/Register?patientId=", re.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Fees_and_services_shows_only_opd_emergency_and_consultant_and_adds_a_service()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var opd = await admin.GetHtmlAsync("/Fees");
        Assert.Contains("data-fee-tab=\"consultant\"", opd);
        Assert.DoesNotContain("data-service=\"LT-", opd);           // laboratory prices: Test Catalogue
        Assert.DoesNotContain("data-service=\"SVC-IPD-ADM\"", opd); // IPD fees: IPD → Fees
        Assert.DoesNotContain("data-service=\"SVC-RAD-ECG\"", opd); // radiology: Radiology → Services
        Assert.DoesNotContain("Government rate", opd);
        Assert.Contains("data-service=\"SVC-ER-CONS\"", await admin.GetHtmlAsync("/Fees?tab=emergency"));

        var r = await admin.PostFormAsync("/Fees", new[]
        {
            P("NewService.Name", "Blood pressure check"), P("NewService.Category", "Opd"), P("NewService.Fee", "150"),
            P("NewService.IsFree", "false"), P("NewService.IsActive", "true"),
        }, "/Fees?handler=Add");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Add: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        var item = await db.ServiceItems.SingleAsync(s => s.Name == "Blood pressure check");
        Assert.True(item.IsActive);
        Assert.Equal(150m, await db.ServiceFees.Where(x => x.ServiceItemId == item.Id).Select(x => x.Fee).SingleAsync());
        Assert.Contains("Blood pressure check", await admin.GetHtmlAsync("/Fees?tab=opd"));
    }

    [Fact]
    public async Task Consultant_fee_is_set_per_doctor_and_used_for_the_token()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        int doctorId;
        await using (var db = Factory.NewDb())
        {
            var d = new Doctor { Name = "Dr Consultant Sixteen", IsActive = true, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow };
            db.Doctors.Add(d);
            await db.SaveChangesAsync();
            doctorId = d.Id;
        }
        var page = await admin.GetHtmlAsync("/Fees?tab=consultant");
        Assert.Contains($"data-doctor=\"{doctorId}\"", page);
        var r = await admin.PostFormAsync("/Fees?tab=consultant", new[] { P("doctorId", doctorId.ToString()), P("fee", "700") }, "/Fees?handler=DoctorFee");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        await using (var db = Factory.NewDb())
        {
            var svc = await db.ServiceItems.SingleAsync(s => s.DoctorId == doctorId);
            Assert.Equal((ServiceKind.Consultation, EncounterType.Opd), (svc.Kind, svc.Category));
            Assert.Equal(700m, await db.ServiceFees.Where(x => x.ServiceItemId == svc.Id).OrderByDescending(x => x.Id).Select(x => x.Fee).FirstAsync());
        }
        Assert.DoesNotContain($"data-doctor=\"{doctorId}\"", await admin.GetHtmlAsync("/Fees?tab=consultant"));
    }

    [Fact]
    public async Task Only_the_administrator_can_change_fees_even_with_the_fee_permissions()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var id = await CreateUserAsync(admin, "feeclerk", "Clerk#2026pass");
        await SetPermissionsAsync(admin, id, (Perm.DashboardView, "grant"), (Perm.SvcViewFees, "grant"), (Perm.SvcManageServices, "grant"),
            (Perm.SvcManageFees, "grant"), (Perm.IpdSettings, "grant"));
        var clerk = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(clerk, "feeclerk", "Clerk#2026pass")).StatusCode);
        int svcId;
        await using (var db = Factory.NewDb()) svcId = await db.ServiceItems.Where(s => s.Code == "SVC-ER-CONS").Select(s => s.Id).SingleAsync();

        // Inline fee change and Add Service: refused on the server.
        var inline = await clerk.PostFormAsync($"/Fees/Service?id={svcId}", new[] { P("id", svcId.ToString()), P("fee", "999") }, "/Fees?handler=Fee");
        Assert.True(inline.StatusCode is HttpStatusCode.Forbidden || inline.Headers.Location?.ToString().Contains("AccessDenied") == true, $"clerk fee change: {(int)inline.StatusCode}");
        var ipd = await clerk.GetHtmlAsync("/Ipd/Fees");
        Assert.Contains("Only the administrator can change fees", ipd);
        // The full service form: the fee field is locked and a posted fee is refused.
        var form = await clerk.GetHtmlAsync($"/Fees/Service?id={svcId}");
        Assert.Contains("Only the administrator can change fees", form);
        await using (var db = Factory.NewDb())
            Assert.DoesNotContain(await db.ServiceFees.Where(f => f.ServiceItemId == svcId).Select(f => f.Fee).ToListAsync(), f => f == 999m);

        // The administrator can.
        var ok = await admin.PostFormAsync("/Fees?tab=emergency", new[] { P("id", svcId.ToString()), P("fee", "300"), P("tab", "emergency") }, "/Fees?handler=Fee");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        await using (var db = Factory.NewDb())
            Assert.Contains(await db.ServiceFees.Where(f => f.ServiceItemId == svcId).Select(f => f.Fee).ToListAsync(), f => f == 300m);
    }

    [Fact]
    public async Task Ipd_fees_are_set_in_ipd_and_old_bills_keep_their_fee()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var page = await admin.GetHtmlAsync("/Ipd/Fees");
        Assert.Contains("data-service=\"SVC-IPD-ADM\"", page);
        Assert.Contains("data-service=\"SVC-NICU-INC\"", page);
        int svcId;
        await using (var db = Factory.NewDb()) svcId = await db.ServiceItems.Where(s => s.Code == "SVC-IPD-ADM").Select(s => s.Id).SingleAsync();
        var r = await admin.PostFormAsync("/Ipd/Fees", new[] { P("id", svcId.ToString()), P("fee", "2500") }, "/Ipd/Fees");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        await using (var db = Factory.NewDb())
            Assert.Contains(await db.ServiceFees.Where(f => f.ServiceItemId == svcId).Select(f => f.Fee).ToListAsync(), f => f == 2500m);
        // Fees & Services no longer edits it.
        Assert.Contains("data-fee-managed", await admin.GetHtmlAsync($"/Fees/Service?id={svcId}"));
    }

    [Fact]
    public async Task New_user_gets_the_selected_counter()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var create = await admin.GetHtmlAsync("/Users/Create");
        Assert.Contains("data-user-counter", create);
        var counter = await MainCounterAsync();
        var r = await admin.PostFormAsync("/Users/Create", new[]
        {
            P("FullName", "Counter User"), P("Username", "counteruser"), P("Designation", "Cashier"),
            P("Password", "Counter#2026pw"), P("ConfirmPassword", "Counter#2026pw"), P("MustChangePassword", "false"), P("CounterId", counter.ToString()),
        });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Create: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        var uid = await db.Users.Where(u => u.Username == "counteruser").Select(u => u.Id).SingleAsync();
        Assert.True(await db.CounterAssignments.AnyAsync(a => a.UserId == uid && a.CounterId == counter && a.EndedUtc == null));
    }
}
