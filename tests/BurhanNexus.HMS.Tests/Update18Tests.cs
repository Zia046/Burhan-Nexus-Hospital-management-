using System.Net;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>
/// Update 18 – Disease Master (registration instead of date of birth, visit-wise history, management, analytics), consultant fee
/// Return / Free by Doctor / Discount on the patient profile, Pay to Hospital, the Finance sidebar item, patients list and lab taskbar.
/// (Part of AppointmentTests to reuse its doctor / booking helpers.)
/// </summary>
public partial class AppointmentTests
{
    private static void Denied18(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode == HttpStatusCode.Forbidden || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied")),
            $"{what} should be denied but returned {(int)r.StatusCode} {r.Headers.Location}");

    private async Task<long> RegisterWithDiseaseAsync(Browser b, string name, string father, string? disease, string gender = "Male", string age = "30", string next = "profile")
    {
        var f = PatientFields(name, father);
        f["Patient.Gender"] = gender;
        f["Patient.Age"] = age;
        f["Patient.AgeUnit"] = "Years";
        f["ConfirmNotDuplicate"] = "true";
        f["Next"] = next;
        if (disease is not null) f["DiseaseName"] = disease;
        var r = await b.PostFormAsync("/Patients/Register", f.Select(x => P(x.Key, x.Value)));
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Register: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Patients.Where(p => p.FullName == name).Select(p => p.Id).SingleAsync();
    }

    private async Task<DiseaseAnalytics> AnalyticsAsync(DiseaseAnalyticsFilter f)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDiseaseService>().AnalyticsAsync(f);
    }

    [Fact]
    public async Task Registration_has_a_disease_field_instead_of_date_of_birth_and_keeps_a_visit_wise_history()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var form = await admin.GetHtmlAsync("/Patients/Register");
        Assert.DoesNotContain("name=\"Patient.DateOfBirth\"", form);
        Assert.Contains("data-disease-picker", form);
        Assert.Contains("name=\"Patient.Age\"", form);

        // Optional: no disease → nothing recorded.
        var none = await RegisterWithDiseaseAsync(admin, "No Disease Patient", "Father A", null);
        // A new disease typed by an authorised user is added to the Disease Master at once.
        var p1 = await RegisterWithDiseaseAsync(admin, "Rti Patient One", "Father B", "Respiratory Tract Infection (RTI)");
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(0, await db.PatientDiseases.CountAsync(x => x.PatientId == none));
            var d = await db.Diseases.SingleAsync();
            Assert.Equal("Respiratory Tract Infection (RTI)", d.Name);
            Assert.Equal(1, await db.PatientDiseases.CountAsync(x => x.PatientId == p1 && x.DiseaseId == d.Id));
        }
        // Different spelling / case is the same disease (no duplicate).
        await RegisterWithDiseaseAsync(admin, "Rti Patient Two", "Father C", "respiratory tract infection rti", gender: "Female", age: "8");
        await using (var db = Factory.NewDb()) Assert.Equal(1, await db.Diseases.CountAsync());
        Assert.Contains("<option value=\"Respiratory Tract Infection (RTI)\"", await admin.GetHtmlAsync("/Patients/Register"));

        // A user without "Add Diseases" may only pick existing diseases.
        var clerk = await UserWithAsync(admin, "regclerk", Perm.DashboardView, Perm.PatientsAdd, Perm.PatientsView);
        var f = PatientFields("Clerk Patient", "Clerk Father");
        f["ConfirmNotDuplicate"] = "true"; f["DiseaseName"] = "Brand New Disease";
        var refused = await clerk.PostFormAsync("/Patients/Register", f.Select(x => P(x.Key, x.Value)));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("not in the Disease Master", await refused.Content.ReadAsStringAsync());
        await using (var db = Factory.NewDb()) Assert.False(await db.Patients.AnyAsync(p => p.FullName == "Clerk Patient"));
        await RegisterWithDiseaseAsync(clerk, "Clerk Patient", "Clerk Father", "RESPIRATORY tract infection (rti)");

        // Later visit: a further record – the earlier one is kept (visit-wise).
        Factory.Clock.Advance(TimeSpan.FromDays(3));
        var enc = await admin.PostFormAsync($"/Opd/Register?patientId={p1}", new[] { P("DiseaseName", "Respiratory Tract Infection (RTI)") }, $"/Opd/Register?patientId={p1}");
        Assert.Equal(HttpStatusCode.Redirect, enc.StatusCode);
        await using (var db = Factory.NewDb())
        {
            var rows = await db.PatientDiseases.Where(x => x.PatientId == p1).OrderBy(x => x.VisitDate).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Null(rows[0].EncounterId);
            Assert.NotNull(rows[1].EncounterId);
            Assert.True(rows[1].VisitDate > rows[0].VisitDate);
        }
        var profile = await admin.GetHtmlAsync($"/Patients/Profile/{p1}");
        Assert.Contains("data-profile-diseases", profile);

        // Analytics: unique patients once, repeat visits counted, gender and age from the records.
        var all = await AnalyticsAsync(new DiseaseAnalyticsFilter());
        Assert.Equal((3, 4, 2, 1), (all.UniquePatients, all.Visits, all.Male, all.Female));
        Assert.Equal(1, all.AgeGroups.Single(g => g.Label == "6–12 years").Patients);
        Assert.Equal(2, all.AgeGroups.Single(g => g.Label == "19–35 years").Patients);
        var female = await AnalyticsAsync(new DiseaseAnalyticsFilter { Gender = Gender.Female });
        Assert.Equal((1, 1), (female.UniquePatients, female.Visits));
        var adults = await AnalyticsAsync(new DiseaseAnalyticsFilter { MinAge = 19, MaxAge = 65 });
        Assert.Equal((2, 3), (adults.UniquePatients, adults.Visits));
        var today = await TodayAsync();
        var recent = await AnalyticsAsync(new DiseaseAnalyticsFilter { From = today, To = today });
        Assert.Equal((1, 1), (recent.UniquePatients, recent.Visits));

        var page = await admin.GetHtmlAsync("/Diseases/Analytics?disease=" + Uri.EscapeDataString("respiratory tract infection (rti)"));
        Assert.Contains("data-unique-patients", page);
        Assert.Contains(">3</strong>", page);
        var x = await admin.GetAsync("/Diseases/Analytics?handler=Export&disease=" + Uri.EscapeDataString("Respiratory Tract Infection (RTI)"));
        Assert.Equal(HttpStatusCode.OK, x.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", x.Content.Headers.ContentType!.MediaType);
        Assert.Contains("data-disease-not-found", await admin.GetHtmlAsync("/Diseases/Analytics?disease=Unknown%20Fever"));
    }

    [Fact]
    public async Task Disease_master_adds_edits_searches_and_deletes_or_deactivates_used_diseases()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        Assert.Contains("href=\"/Diseases\"", await admin.GetHtmlAsync("/"));
        async Task<HttpResponseMessage> SaveAsync(string? edit, params KeyValuePair<string, string>[] f) =>
            await admin.PostFormAsync(edit is null ? "/Diseases?new=true" : $"/Diseases?edit={edit}", f, edit is null ? "/Diseases" : $"/Diseases?edit={edit}");
        Assert.Equal(HttpStatusCode.Redirect, (await SaveAsync(null, P("Form.Name", "Typhoid Fever"), P("Form.Code", "A01.0"), P("Form.IsActive", "true"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SaveAsync(null, P("Form.Name", "Malaria"), P("Form.IsActive", "true"))).StatusCode);
        var dup = await SaveAsync(null, P("Form.Name", "typhoid  FEVER"), P("Form.IsActive", "true"));
        Assert.Contains("already exists", await dup.Content.ReadAsStringAsync());
        int typhoid, malaria;
        await using (var db = Factory.NewDb())
        {
            typhoid = await db.Diseases.Where(d => d.Name == "Typhoid Fever").Select(d => d.Id).SingleAsync();
            malaria = await db.Diseases.Where(d => d.Name == "Malaria").Select(d => d.Id).SingleAsync();
        }
        uint rv;
        await using (var db = Factory.NewDb()) rv = (await db.Diseases.SingleAsync(d => d.Id == typhoid)).RowVersion;
        Assert.Equal(HttpStatusCode.Redirect, (await SaveAsync(typhoid.ToString(), P("Form.Name", "Enteric (Typhoid) Fever"), P("Form.Code", "A01.0"),
            P("Form.IsActive", "true"), P("RowVersion", rv.ToString()))).StatusCode);
        var list = await admin.GetHtmlAsync("/Diseases?q=enteric");
        Assert.Contains("Enteric (Typhoid) Fever", list);
        Assert.DoesNotContain(">Malaria<", list);
        Assert.Contains("data-live-filter", list);

        // Used disease → deactivated + hidden (records kept); unused → deleted.
        var pid = await RegisterWithDiseaseAsync(admin, "Typhoid Patient", "Father T", "Enteric (Typhoid) Fever");
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Diseases", Array.Empty<KeyValuePair<string, string>>(), $"/Diseases?handler=Delete&id={typhoid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Diseases", Array.Empty<KeyValuePair<string, string>>(), $"/Diseases?handler=Delete&id={malaria}")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var t = await db.Diseases.SingleAsync(d => d.Id == typhoid);
            Assert.True(t.IsDeleted && !t.IsActive);
            Assert.False(await db.Diseases.AnyAsync(d => d.Id == malaria));
            Assert.Equal(1, await db.PatientDiseases.CountAsync(p => p.PatientId == pid && p.DiseaseId == typhoid));
        }
        Assert.DoesNotContain("Enteric (Typhoid) Fever\"", await admin.GetHtmlAsync("/Patients/Register"));
        Assert.Contains("Enteric (Typhoid) Fever", await admin.GetHtmlAsync($"/Patients/Profile/{pid}"));
        // Users without the Disease permissions cannot change the master (server side).
        var clerk = await UserWithAsync(admin, "disclerk", Perm.DashboardView, Perm.PatientsView);
        Denied18(await clerk.GetAsync("/Diseases"), "Diseases without permission");
    }

    [Fact]
    public async Task Consultation_fee_return_free_by_doctor_and_discount_keep_the_financial_history()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var doc = await AddDoctorAsync(admin, "Dr. Fee Actions", "Medicine", fee: 1000);
        var p1 = await RegisterWithDiseaseAsync(admin, "Fee Return Patient", "Father F1", null);
        var p2 = await RegisterWithDiseaseAsync(admin, "Fee Discount Patient", "Father F2", null);
        var p3 = await RegisterWithDiseaseAsync(admin, "Fee Free Patient", "Father F3", null);
        var a1 = await BookAsync(admin, p1, doc);
        var a2 = await BookAsync(admin, p2, doc);
        var a3 = await BookAsync(admin, p3, doc);
        var profile = await admin.GetHtmlAsync($"/Patients/Profile/{p1}");
        Assert.Contains("data-return-fee", profile);
        Assert.Contains("data-discount-form", profile);
        Assert.Contains("data-free-form", profile);

        // Return Fee: partial, reason required, never more than what is left (no double refund).
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Patients/Profile/{p1}", new[] { P("appointmentId", a1.ToString()), P("amount", "400"), P("reason", "") }, $"/Patients/Profile/{p1}?handler=ReturnFee")).StatusCode);
        await using (var db = Factory.NewDb()) Assert.False(await db.Refunds.AnyAsync(r => r.ConsultationAppointmentId == a1));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Patients/Profile/{p1}", new[] { P("appointmentId", a1.ToString()), P("amount", "400"), P("reason", "Doctor not available") }, $"/Patients/Profile/{p1}?handler=ReturnFee")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Patients/Profile/{p1}", new[] { P("appointmentId", a1.ToString()), P("amount", "700"), P("reason", "Again") }, $"/Patients/Profile/{p1}?handler=ReturnFee")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var refunds = await db.Refunds.Where(r => r.ConsultationAppointmentId == a1).ToListAsync();
            Assert.Equal(400m, Assert.Single(refunds).Amount); // the 700 was refused (only 600 left)
            Assert.Equal(RefundStatus.Pending, refunds[0].Status);
            Assert.True(await db.Receipts.AnyAsync(r => r.Status == ReceiptStatus.Valid)); // the original payment is never deleted
        }
        Assert.Contains("data-consultation-refund=\"Pending\"", await admin.GetHtmlAsync($"/Patients/Profile/{p1}"));

        // Discount 20 %: original 1000, discount 200, final 800 (already paid → the 200 is refunded).
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Patients/Profile/{p2}", new[]
            { P("appointmentId", a2.ToString()), P("discountType", "Percent"), P("value", "20"), P("reason", "Staff relative") }, $"/Patients/Profile/{p2}?handler=Discount")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var a = await db.Appointments.SingleAsync(x => x.Id == a2);
            Assert.Equal(("Percent", 20m, 200m, 1000m), (a.DiscountType, a.DiscountValue!.Value, a.DiscountAmount!.Value, a.DiscountOriginalFee!.Value));
            Assert.Equal(200m, (await db.Refunds.SingleAsync(r => r.Id == a.DiscountRefundId)).Amount);
            // The doctor's fee configuration is not changed.
            Assert.Equal(1000m, await db.ServiceFees.Where(f => f.ServiceItem!.DoctorId == doc).OrderByDescending(f => f.Id).Select(f => f.Fee).FirstAsync());
        }
        var again = await admin.PostFormAsync($"/Patients/Profile/{p2}", new[] { P("appointmentId", a2.ToString()), P("discountType", "Fixed"), P("value", "100"), P("reason", "x") }, $"/Patients/Profile/{p2}?handler=Discount");
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        await using (var db = Factory.NewDb()) Assert.Single(await db.Refunds.Where(r => r.ConsultationAppointmentId == a2).ToListAsync());

        // Free by Doctor after payment: recorded with doctor and reason; the collected fee goes back through a refund.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Patients/Profile/{p3}", new[] { P("appointmentId", a3.ToString()), P("reason", "Poor patient") }, $"/Patients/Profile/{p3}?handler=FreeByDoctor")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var a = await db.Appointments.SingleAsync(x => x.Id == a3);
            Assert.Equal((AppointmentPayment.FreeByDoctor, "Dr. Fee Actions", "Poor patient"), (a.PaymentStatus, a.WaiverDoctorName, a.WaiverReason));
            Assert.NotNull(a.WaiverRefundId);
        }
        Assert.Contains("data-free-by-doctor", await admin.GetHtmlAsync($"/Patients/Profile/{p3}"));
    }

    [Fact]
    public async Task Pay_to_hospital_settles_outstanding_accounts_and_the_admin_sidebar_shows_finance()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var home = await admin.GetHtmlAsync("/");
        Assert.Contains("href=\"/Finance/Dashboard\"", home);
        Assert.DoesNotContain(">My Financial Account<", home.Split("data-sidebar-nav=\"admin\"")[1].Split("</nav>")[0]);
        var doc = await AddDoctorAsync(admin, "Dr. Settle", "Medicine", fee: 500);
        var pid = await RegisterWithDiseaseAsync(admin, "Settle Patient", "Father S", null);
        await BookAsync(admin, pid, doc);
        int adminId;
        await using (var db = Factory.NewDb()) adminId = await db.Users.Where(u => u.IsSystemAdministrator).Select(u => u.Id).SingleAsync();
        async Task<decimal> OutstandingAsync() { await using var db = Factory.NewDb(); return await db.CashierLedger.Where(e => e.UserId == adminId && e.AffectsCashInHand).SumAsync(e => (decimal?)e.Amount) ?? 0; }
        Assert.Equal(500m, await OutstandingAsync());

        var dash = await admin.GetHtmlAsync("/Finance/Dashboard");
        Assert.Contains("data-finance-options", dash);
        Assert.Contains("data-admin-outstanding", dash);
        var page = await admin.GetHtmlAsync("/Finance/PayToHospital");
        Assert.Contains("data-account=\"admin\"", page);
        var r = await admin.PostFormAsync("/Finance/PayToHospital", new[] { P("UserIds", adminId.ToString()), P("Notes", "End of day") });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Pay: " + Errors(await r.Content.ReadAsStringAsync()));
        Assert.Equal(0m, await OutstandingAsync());
        await using (var db = Factory.NewDb())
        {
            var st = await db.HospitalSettlements.SingleAsync();
            Assert.Equal((500m, 500m), (st.Amount, st.OutstandingBefore));
            Assert.Equal(1, await db.CashierLedger.CountAsync(e => e.SettlementId == st.Id && e.EntryType == CashierEntryType.HospitalSettlement));
        }
        // Nothing left: a second settlement is refused (no duplicate transaction).
        var again = await admin.PostFormAsync("/Finance/PayToHospital", new[] { P("UserIds", adminId.ToString()) });
        Assert.Contains("nothing outstanding", await again.Content.ReadAsStringAsync());

        // Laboratory: CBC configuration is in the Test Catalogue, not in the taskbar.
        Assert.DoesNotContain("href=\"/Lab/Cbc\"", await admin.GetHtmlAsync("/Lab"));
        Assert.Contains("href=\"/Lab/Cbc\"", await admin.GetHtmlAsync("/Lab/Catalogue"));
        // Registered patients list fits the screen (columns wrap instead of scrolling).
        Assert.Contains("table-fit", await admin.GetHtmlAsync("/Patients?q=Settle"));
    }
}
