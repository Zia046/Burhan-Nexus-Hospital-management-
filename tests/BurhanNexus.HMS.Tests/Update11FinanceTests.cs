using System.Globalization;
using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 11 – doctor account: daily OPD, handed to doctor / handed to Admin kept apart, payable carried forward, own-account restriction, prints.</summary>
public partial class FinanceTests
{
    [Fact]
    public async Task Update11_doctor_account_handover_to_doctor_and_to_admin_with_vouchers_and_own_account_only()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var dA = await DoctorAsync(admin, "Dr. Handover", "FullFeeToDoctor", 0, 1000);
        var dB = await DoctorAsync(admin, "Dr. Other", "FullFeeToDoctor", 0, 500);
        var pid = await RegisterPatientAsync(admin, PatientFields("Handover Patient", "Handover Father"));
        async Task<int> Consult(int d)
        {
            await using var db = Factory.NewDb();
            return await db.ServiceItems.Where(s => s.DoctorId == d && s.Kind == ServiceKind.Consultation).Select(s => s.Id).SingleAsync();
        }
        await CollectAsync(admin, pid, counter, new Line(await Consult(dA)), new Line(await Consult(dA)), new Line(await Consult(dA)));
        await CollectAsync(admin, pid, counter, new Line(await Consult(dB)));
        Assert.Equal(3000m, await DoctorPayableAsync(dA));

        async Task<HttpResponseMessage> Hand(Browser b, int d, decimal amount, bool toAdmin, string? receivedBy = null) =>
            await b.PostFormAsync($"/Finance/Doctors/Account/{d}", new[]
            {
                P("Pay.Amount", amount.ToString(CultureInfo.InvariantCulture)), P("Pay.PaymentMethodId", (await MethodAsync("CASH")).ToString()),
                P("Pay.ToAdmin", toAdmin ? "true" : "false"), P("Pay.ReceivedBy", receivedBy ?? ""), P("Pay.Notes", toAdmin ? "Kept by admin" : "Evening handover"),
            }, $"/Finance/Doctors/Account/{d}?handler=Pay");

        // Handed to doctor 1000, handed to Admin 1500 → 500 stays payable for the next day (no double counting).
        Assert.Equal(HttpStatusCode.Redirect, (await Hand(admin, dA, 1000, false)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Hand(admin, dA, 1500, true, "Admin Burhan")).StatusCode);
        Assert.Contains("At most 500.00", Errors(await (await Hand(admin, dA, 600, true)).Content.ReadAsStringAsync()));
        Assert.Equal(500m, await DoctorPayableAsync(dA));
        await using (var db = Factory.NewDb())
        {
            var s = await db.DoctorSettlements.Where(x => x.DoctorId == dA).OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(2, s.Count);
            Assert.Equal((DoctorSettlementKind.ToDoctor, DoctorSettlementKind.ToAdmin), (s[0].Kind, s[1].Kind));
            Assert.StartsWith("DPY-", s[0].SettlementNumber);
            Assert.StartsWith("DHA-", s[1].SettlementNumber);
            Assert.NotEqual(s[0].SettlementNumber, s[1].SettlementNumber);
            Assert.Equal("Admin Burhan", s[1].ReceivedBy);
            Assert.Equal(1, await db.DoctorLedger.CountAsync(e => e.DoctorId == dA && e.EntryType == DoctorEntryType.HandedToAdmin && e.Amount == -1500m));
        }
        var acc = await admin.GetHtmlAsync($"/Finance/Doctors/Account/{dA}");
        Assert.Contains($"data-kpi=\"paid\">Rs. {Money(1000)}", acc);
        Assert.Contains($"data-kpi=\"to-admin\">Rs. {Money(1500)}", acc);
        Assert.Contains($"data-kpi=\"payable\">Rs. {Money(500)}", acc);
        Assert.Contains($"data-day=\"collected\">Rs. {Money(3000)}", acc);
        Assert.Contains("data-doctor-opd", acc);
        Assert.Contains("data-settlement-kind=\"ToAdmin\"", acc);
        Assert.Contains("data-handed-admin", await admin.GetHtmlAsync("/Finance/Doctors"));
        Assert.Single(await AuditAsync(AuditActions.DoctorHandedToAdmin));
        Assert.Single(await AuditAsync(AuditActions.DoctorPaid));

        // Prints: daily OPD list, settlement report and voucher.
        long voucher;
        await using (var db = Factory.NewDb()) voucher = await db.DoctorSettlements.Where(x => x.Kind == DoctorSettlementKind.ToAdmin).Select(x => x.Id).SingleAsync();
        Assert.Contains("data-opd-list", await admin.GetHtmlAsync($"/Print/Doctor/{dA}"));
        Assert.Contains("data-settlement-summary", await admin.GetHtmlAsync($"/Print/Doctor/{dA}?from=2020-01-01&to=2099-12-31"));
        var v = await admin.GetHtmlAsync($"/Print/Doctor/{dA}?voucher={voucher}");
        Assert.Contains("data-voucher=\"DHA-", v);
        Assert.Contains("Admin Burhan", v);

        // A doctor sees only his own account; he cannot hand over money or change the arrangement.
        var docUser = await UserWithAsync(admin, "drhandover", Perm.DashboardView, Perm.FinanceViewOwnDoctorAccount);
        await using (var db = Factory.NewDb())
        {
            var d = await db.Doctors.SingleAsync(x => x.Id == dA);
            d.UserId = await UserIdAsync("drhandover");
            await db.SaveChangesAsync();
        }
        Assert.Equal($"/Finance/Doctors/Account/{dA}", (await docUser.Http.GetAsync("/Finance/Doctors")).Headers.Location!.OriginalString);
        var own = await docUser.GetHtmlAsync($"/Finance/Doctors/Account/{dA}");
        Assert.Contains("data-doctor-day", own);
        Assert.DoesNotContain("data-doctor-handover", own);
        Denied(await docUser.Http.GetAsync($"/Finance/Doctors/Account/{dB}"), "Another doctor's account");
        Denied(await docUser.Http.GetAsync($"/Print/Doctor/{dB}"), "Another doctor's print");
        Assert.Contains("data-opd-list", await docUser.GetHtmlAsync($"/Print/Doctor/{dA}"));
        Denied(await Hand(docUser, dA, 100, false), "Doctor paying himself");
        Assert.Equal(500m, await DoctorPayableAsync(dA));
    }
}
