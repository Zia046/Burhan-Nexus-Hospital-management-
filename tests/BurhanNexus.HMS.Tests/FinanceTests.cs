using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Phase 3 – cashier accounts, receipts, deposits, refunds, doctor accounts, closing and security.</summary>
public partial class FinanceTests : Phase2TestBase
{
    [GeneratedRegex(@"/Print/Receipt/(\d+)")] private static partial Regex ReceiptRegex();

    private static readonly string[] CashierPerms =
    {
        Perm.DashboardView, Perm.FinanceViewOwnAccount, Perm.FinanceViewOwnReceipts, Perm.FinanceCollect, Perm.FinanceIssueReceipts,
        Perm.FinanceSubmitDeposit, Perm.FinanceDailyClosing, Perm.PatientsSearch,
    };

    private static string Money(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);

    // ---------------- helpers ----------------

    private async Task<int> CounterAsync(Browser admin, string code, string name)
    {
        var r = await admin.PostFormAsync("/Finance/Counters", new[] { P("Form.Code", code), P("Form.Name", name), P("Form.IsActive", "true") }, "/Finance/Counters?handler=Save");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Counters.Where(c => c.Code == code).Select(c => c.Id).SingleAsync();
    }

    private async Task AssignAsync(Browser admin, int counterId, int userId, int? transferFrom = null)
    {
        var fields = new List<KeyValuePair<string, string>> { P("AssignUserId", userId.ToString()) };
        if (transferFrom is int t) fields.Add(P("TransferFrom", t.ToString()));
        var r = await admin.PostFormAsync($"/Finance/Counters?id={counterId}", fields, $"/Finance/Counters?id={counterId}&handler=Assign");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
    }

    private async Task<int> UserIdAsync(string username)
    {
        await using var db = Factory.NewDb();
        return await db.Users.Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
    }

    private async Task<(Browser B, int Id)> CashierAsync(Browser admin, string username, int counterId, params string[] extra)
    {
        var b = await UserWithAsync(admin, username, CashierPerms.Concat(extra).ToArray());
        var id = await UserIdAsync(username);
        await AssignAsync(admin, counterId, id);
        return (b, id);
    }

    private async Task<int> ServiceAsync(Browser admin, string code, string name, decimal fee, string kind = "Laboratory", bool partial = false, int? doctorId = null)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            P("Form.Code", code), P("Form.Name", name), P("Form.Kind", kind), P("Form.Fee", fee.ToString(CultureInfo.InvariantCulture)),
            P("Form.IsFree", fee == 0 ? "true" : "false"), P("Form.AllowPartialPayment", partial ? "true" : "false"), P("Form.IsActive", "true"),
        };
        if (doctorId is int d) fields.Add(P("Form.DoctorId", d.ToString()));
        var r = await admin.PostFormAsync("/Finance/Services", fields);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.ServiceItems.Where(s => s.Code == code).Select(s => s.Id).SingleAsync();
    }

    private async Task<int> DoctorAsync(Browser admin, string name, string shareType, decimal value, decimal fee)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Opd/Setup", new[] { P("DoctorForm.Name", name), P("DoctorForm.IsActive", "true") }, "/Opd/Setup?handler=Doctor")).StatusCode);
        int id;
        await using (var db = Factory.NewDb()) id = await db.Doctors.Where(d => d.Name == name).Select(d => d.Id).SingleAsync();
        await ServiceAsync(admin, "CON-" + id, "Consultation " + name, fee, "Consultation", doctorId: id);
        await ShareAsync(admin, id, shareType, value);
        return id;
    }

    private static async Task ShareAsync(Browser admin, int doctorId, string shareType, decimal value)
    {
        var r = await admin.PostFormAsync($"/Finance/Doctors/Account/{doctorId}",
            new[] { P("ShareType", shareType), P("ShareValue", value.ToString(CultureInfo.InvariantCulture)) }, $"/Finance/Doctors/Account/{doctorId}?handler=Share");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
    }

    private async Task<int> MethodAsync(string code)
    {
        await using var db = Factory.NewDb();
        return await db.PaymentMethods.Where(m => m.Code == code).Select(m => m.Id).SingleAsync();
    }

    private sealed record Line(int ServiceId, decimal? PayNow = null, decimal Discount = 0, string? Reason = null, int? DoctorId = null);

    private async Task<HttpResponseMessage> PostCollectAsync(Browser b, long? patientId, int counterId, IEnumerable<Line> lines, string method = "CASH",
        Guid? key = null, IEnumerable<(long ChargeId, decimal Pay)>? balances = null, string? reference = null, string? payer = null)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()), P("Input.CounterId", counterId.ToString()),
            P("Input.PaymentMethodId", (await MethodAsync(method)).ToString()),
        };
        if (patientId is long pid) fields.Add(P("PatientId", pid.ToString()));
        if (payer is not null) { fields.Add(P("Input.PayerName", payer)); fields.Add(P("WalkIn", "true")); }
        if (reference is not null) fields.Add(P("Input.PaymentReference", reference));
        var i = 0;
        foreach (var l in lines)
        {
            fields.Add(P($"Lines[{i}].ServiceItemId", l.ServiceId.ToString()));
            if (l.PayNow is decimal pay) fields.Add(P($"Lines[{i}].PayNow", pay.ToString(CultureInfo.InvariantCulture)));
            if (l.Discount != 0) fields.Add(P($"Lines[{i}].Discount", l.Discount.ToString(CultureInfo.InvariantCulture)));
            if (l.Reason is not null) fields.Add(P($"Lines[{i}].DiscountReason", l.Reason));
            if (l.DoctorId is int d) fields.Add(P($"Lines[{i}].DoctorId", d.ToString()));
            i++;
        }
        var j = 0;
        foreach (var (chargeId, pay) in balances ?? Array.Empty<(long, decimal)>())
        {
            fields.Add(P($"Balances[{j}].ChargeId", chargeId.ToString()));
            fields.Add(P($"Balances[{j}].PayNow", pay.ToString(CultureInfo.InvariantCulture)));
            j++;
        }
        var page = patientId is long p2 ? $"/Finance/Collect?patientId={p2}" : "/Finance/Collect?walkIn=true";
        return await b.PostFormAsync(page, fields, "/Finance/Collect");
    }

    private static async Task<long> ReceiptIdAsync(HttpResponseMessage r)
    {
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Collection failed: " + Errors(await r.Content.ReadAsStringAsync()));
        var m = ReceiptRegex().Match(r.Headers.Location!.OriginalString);
        Assert.True(m.Success, "Unexpected redirect " + r.Headers.Location);
        return long.Parse(m.Groups[1].Value);
    }

    private async Task<long> CollectAsync(Browser b, long? patientId, int counterId, params Line[] lines) =>
        await ReceiptIdAsync(await PostCollectAsync(b, patientId, counterId, lines));

    private async Task<Receipt> ReceiptAsync(long id)
    {
        await using var db = Factory.NewDb();
        return await db.Receipts.Include(r => r.Lines).Include(r => r.Prints).SingleAsync(r => r.Id == id);
    }

    private async Task<decimal> OutstandingAsync(int userId)
    {
        await using var db = Factory.NewDb();
        return await db.CashierLedger.Where(e => e.UserId == userId && e.AffectsCashInHand).SumAsync(e => (decimal?)e.Amount) ?? 0;
    }

    private async Task<decimal> DoctorPayableAsync(int doctorId)
    {
        await using var db = Factory.NewDb();
        return await db.DoctorLedger.Where(e => e.DoctorId == doctorId).SumAsync(e => (decimal?)e.Amount) ?? 0;
    }

    private static async Task<HttpResponseMessage> AjaxAsync(Browser b, string pageUrl, string handlerUrl, params KeyValuePair<string, string>[] fields)
    {
        var token = await b.TokenAsync(pageUrl);
        return await b.Http.PostAsync(handlerUrl, new FormUrlEncodedContent(fields.Append(P("__RequestVerificationToken", token))));
    }

    private static void Denied(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode == HttpStatusCode.Forbidden || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied")),
            $"{what} should be denied but returned {(int)r.StatusCode} {r.Headers.Location}");

    private async Task<long> DepositIdAsync(string username, decimal amount)
    {
        await using var db = Factory.NewDb();
        return await db.CashDeposits.Where(d => d.Username == username && d.Amount == amount).OrderByDescending(d => d.Id).Select(d => d.Id).FirstAsync();
    }

    private async Task<HttpResponseMessage> SubmitDepositAsync(Browser b, decimal amount) =>
        await b.PostFormAsync("/Finance", new[] { P("DepositAmount", amount.ToString(CultureInfo.InvariantCulture)), P("DepositNotes", "cash") }, "/Finance?handler=Deposit");

    private async Task<HttpResponseMessage> ConfirmAsync(Browser admin, long depositId, decimal? received, string remarks = "Counted and received") =>
        await admin.PostFormAsync("/Finance/Deposits",
            new[] { P("ReceivedAmount", received?.ToString(CultureInfo.InvariantCulture) ?? ""), P("Remarks", remarks) }, $"/Finance/Deposits?handler=Confirm&id={depositId}");

    // =====================================================================================

    [Fact]
    public async Task Each_cashier_has_a_separate_account_and_cannot_see_others()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var opd = await CounterAsync(admin, "OPD", "OPD Reception Counter");
        var lab = await CounterAsync(admin, "LAB", "Laboratory Counter");
        var er = await CounterAsync(admin, "ER", "Emergency Counter");
        var svc = await ServiceAsync(admin, "LAB-1000", "Test Rs 1000", 1000);
        var pid = await RegisterPatientAsync(admin, PatientFields("Account Patient", "Account Father"));
        var (a, aId) = await CashierAsync(admin, "cashier.a", opd);
        var (b, bId) = await CashierAsync(admin, "cashier.b", lab);
        var (c, cId) = await CashierAsync(admin, "cashier.c", er);

        var a1 = await CollectAsync(a, pid, opd, new Line(svc), new Line(svc), new Line(svc));
        var a2 = await CollectAsync(a, pid, opd, new Line(svc), new Line(svc));
        var b1 = await CollectAsync(b, pid, lab, new Line(svc), new Line(svc), new Line(svc));
        var c1 = await CollectAsync(c, pid, er, new Line(svc), new Line(svc));

        Assert.Equal(aId, (await ReceiptAsync(a1)).CashierUserId);
        Assert.Equal(opd, (await ReceiptAsync(a2)).CounterId);
        Assert.Equal(bId, (await ReceiptAsync(b1)).CashierUserId);
        Assert.Equal(cId, (await ReceiptAsync(c1)).CashierUserId);
        Assert.Equal(5000m, await OutstandingAsync(aId));
        Assert.Equal(3000m, await OutstandingAsync(bId));
        Assert.Equal(2000m, await OutstandingAsync(cId));

        // Each sees only his own account.
        var aPage = await a.GetHtmlAsync("/Finance");
        Assert.Contains($"data-kpi=\"outstanding\">Rs. {Money(5000)}", aPage);
        Assert.DoesNotContain("cashier.b", aPage);
        var bPage = await b.GetHtmlAsync("/Finance");
        Assert.Contains($"data-kpi=\"outstanding\">Rs. {Money(3000)}", bPage);
        Denied(await b.GetAsync($"/Finance?userId={aId}"), "B opening A's account");
        Denied(await c.GetAsync($"/Finance?userId={bId}"), "C opening B's account");
        Denied(await b.GetAsync($"/Finance/Receipts/Details/{a1}"), "B opening A's receipt");
        Denied(await b.GetAsync($"/Print/Receipt/{a1}"), "B printing A's receipt");
        Denied(await a.GetAsync("/Finance/Dashboard"), "cashier opening hospital dashboard");
        Denied(await a.GetAsync("/Finance/Cashiers"), "cashier opening all accounts");
        Denied(await a.GetAsync("/Finance/Reports"), "cashier opening reports");
        var bReceipts = await b.GetHtmlAsync("/Finance/Receipts?period=year");
        Assert.Contains((await ReceiptAsync(b1)).ReceiptNumber, bReceipts);
        Assert.DoesNotContain((await ReceiptAsync(a1)).ReceiptNumber, bReceipts);
        Assert.DoesNotContain("data-nav=\"finance-dashboard\"", await a.GetHtmlAsync("/"));

        // A cashier can only use his assigned counter.
        var wrong = await PostCollectAsync(a, pid, lab, new[] { new Line(svc) });
        Assert.Contains("not assigned", Errors(await wrong.Content.ReadAsStringAsync()));

        // Admin sees all accounts and the hospital total.
        var all = await admin.GetHtmlAsync("/Finance/Cashiers");
        Assert.Contains("cashier.a", all);
        Assert.Contains("cashier.b", all);
        Assert.Contains("cashier.c", all);
        Assert.Contains($"Rs. {Money(5000)}", await admin.GetHtmlAsync($"/Finance?userId={aId}"));
        var dash = await admin.GetHtmlAsync("/Finance/Dashboard");
        Assert.Contains($"data-kpi=\"collected\">Rs. {Money(10000)}", dash);
        Assert.Contains("OPD Reception Counter", dash);
        Assert.Contains("Laboratory Counter", dash);

        // Transfer: B moves from Laboratory to OPD counter.
        await AssignAsync(admin, opd, bId, transferFrom: lab);
        await using (var db = Factory.NewDb())
        {
            Assert.False(await db.CounterAssignments.AnyAsync(x => x.UserId == bId && x.CounterId == lab && x.EndedUtc == null));
            Assert.True(await db.CounterAssignments.AnyAsync(x => x.UserId == bId && x.CounterId == lab && x.EndedUtc != null));
            Assert.True(await db.CounterAssignments.AnyAsync(x => x.UserId == bId && x.CounterId == opd && x.EndedUtc == null));
        }
        await CollectAsync(b, pid, opd, new Line(svc));
        Assert.Equal(4000m, await OutstandingAsync(bId));
        Assert.Contains("cashier.b", (await AuditAsync(AuditActions.CounterAssigned)).Last().Description!);
    }

    [Fact]
    public async Task Deposits_reduce_outstanding_only_when_confirmed_by_someone_else()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var svc = await ServiceAsync(admin, "LAB-10K", "Package 10000", 10000);
        var pid = await RegisterPatientAsync(admin, PatientFields("Deposit Patient", "Deposit Father"));
        var (a, aId) = await CashierAsync(admin, "cashier.dep", counter);
        await CollectAsync(a, pid, counter, new Line(svc));
        Assert.Equal(10000m, await OutstandingAsync(aId));

        Assert.Equal(HttpStatusCode.Redirect, (await SubmitDepositAsync(a, 7000)).StatusCode);
        Assert.Equal(10000m, await OutstandingAsync(aId)); // still pending
        var dep = await DepositIdAsync("cashier.dep", 7000);
        // The cashier cannot confirm (no permission – backend enforced).
        Denied(await a.PostFormAsync("/Finance", new[] { P("Remarks", "self") }, $"/Finance/Deposits?handler=Confirm&id={dep}"), "cashier confirming");
        Assert.Contains("Remarks", Errors(await (await ConfirmAsync(admin, dep, null, "")).Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, (await ConfirmAsync(admin, dep, 7000)).StatusCode);
        Assert.Equal(3000m, await OutstandingAsync(aId));
        var page = await a.GetHtmlAsync("/Finance");
        Assert.Contains($"data-kpi=\"outstanding\">Rs. {Money(3000)}", page);
        Assert.Contains($"data-kpi=\"deposited\">Rs. {Money(7000)}", page);
        Assert.Contains($"data-kpi=\"valid\">Rs. {Money(10000)}", page);

        // Over-deposit is refused; a rejected deposit changes nothing.
        Assert.Contains("at most", Errors(await (await SubmitDepositAsync(a, 3500)).Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await SubmitDepositAsync(a, 1000)).StatusCode);
        var dep2 = await DepositIdAsync("cashier.dep", 1000);
        Assert.Contains("reason", Errors(await (await admin.PostFormAsync("/Finance/Deposits", new[] { P("Remarks", "") }, $"/Finance/Deposits?handler=Reject&id={dep2}")).Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Finance/Deposits", new[] { P("Remarks", "Cash not handed over") }, $"/Finance/Deposits?handler=Reject&id={dep2}")).StatusCode);
        Assert.Equal(3000m, await OutstandingAsync(aId));
        await using (var db = Factory.NewDb()) Assert.Equal(DepositStatus.Rejected, (await db.CashDeposits.SingleAsync(d => d.Id == dep2)).Status);

        // Partial receipt of a deposit: declared 2000, Admin counts 1500.
        Assert.Equal(HttpStatusCode.Redirect, (await SubmitDepositAsync(a, 2000)).StatusCode);
        var dep3 = await DepositIdAsync("cashier.dep", 2000);
        Assert.Equal(HttpStatusCode.Redirect, (await ConfirmAsync(admin, dep3, 1500, "Only 1500 in the envelope")).StatusCode);
        Assert.Equal(1500m, await OutstandingAsync(aId));
        Assert.Contains("already", Errors(await (await ConfirmAsync(admin, dep3, 1500)).Content.ReadAsStringAsync()));

        // Admin cannot confirm his own deposit.
        var (_, adminId) = (admin, await UserIdAsync(AdminUser));
        await CollectAsync(admin, pid, counter, new Line(svc));
        Assert.Equal(HttpStatusCode.Redirect, (await SubmitDepositAsync(admin, 500)).StatusCode);
        var own = await DepositIdAsync(AdminUser, 500);
        Assert.Contains("own deposit", Errors(await (await ConfirmAsync(admin, own, 500)).Content.ReadAsStringAsync()));
        Assert.Equal(10000m, await OutstandingAsync(adminId));

        Assert.Equal(4, (await AuditAsync(AuditActions.DepositSubmitted)).Count);
        Assert.Equal(2, (await AuditAsync(AuditActions.DepositConfirmed)).Count);
        Assert.Single(await AuditAsync(AuditActions.DepositRejected));
    }

    [Fact]
    public async Task Receipts_are_unique_reprints_keep_the_number_and_cancellation_keeps_history()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var paid = await ServiceAsync(admin, "XR-1", "Chest X-Ray", 300, "XRay");
        var free = await ServiceAsync(admin, "ER-FREE", "Emergency Registration Free", 0, "EmergencyRegistration");
        var pid = await RegisterPatientAsync(admin, PatientFields("Receipt Patient", "Receipt Father"));
        var (cash, cashId) = await CashierAsync(admin, "cashier.r", counter);

        var r1 = await CollectAsync(cash, pid, counter, new Line(paid));
        var r2 = await CollectAsync(cash, pid, counter, new Line(free));
        var r3 = await ReceiptIdAsync(await PostCollectAsync(cash, null, counter, new[] { new Line(paid) }, payer: "Walk-in Person"));
        var receipt1 = await ReceiptAsync(r1);
        var receipt2 = await ReceiptAsync(r2);
        Assert.Equal(300m, receipt1.AmountPaid);
        Assert.Equal(0m, receipt2.AmountPaid); // proof of a free service
        Assert.Equal(3, new[] { receipt1.ReceiptNumber, receipt2.ReceiptNumber, (await ReceiptAsync(r3)).ReceiptNumber }.Distinct().Count());
        Assert.Matches(@"^RCP-\d{4}-\d{6}$", receipt1.ReceiptNumber);

        // Preview shows the required receipt data.
        var html = await cash.GetHtmlAsync($"/Print/Receipt/{r1}?width=58");
        foreach (var expected in new[] { "Burhan Test Hospital", receipt1.ReceiptNumber, "Receipt Patient", "Chest X-Ray", "OPD Counter", "Cash", "w58" }) Assert.Contains(expected, html);
        Assert.Contains("format=a4", html);
        Assert.Contains("Chest X-Ray", await cash.GetHtmlAsync($"/Print/Receipt/{r1}?format=a4"));

        // Print, then reprint: same receipt, copy 2; cashier without Reprint permission is refused.
        var first = JsonDocument.Parse(await (await AjaxAsync(cash, $"/Print/Receipt/{r1}", $"/Print/Receipt/{r1}?handler=Record", P("width", "80"))).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, first.GetProperty("copy").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await AjaxAsync(cash, $"/Print/Receipt/{r1}", $"/Print/Receipt/{r1}?handler=Record", P("width", "80"))).StatusCode);
        var again = JsonDocument.Parse(await (await AjaxAsync(admin, $"/Print/Receipt/{r1}", $"/Print/Receipt/{r1}?handler=Record", P("width", "58"))).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, again.GetProperty("copy").GetInt32());
        Assert.Contains("(REPRINT)", await admin.GetHtmlAsync($"/Print/Receipt/{r1}"));
        var reprinted = await ReceiptAsync(r1);
        Assert.Equal(receipt1.ReceiptNumber, reprinted.ReceiptNumber);
        Assert.Equal(2, reprinted.Prints.Count);
        await using (var db = Factory.NewDb()) Assert.Equal(3, await db.Receipts.CountAsync());
        var esc = JsonDocument.Parse(await (await AjaxAsync(admin, $"/Print/Receipt/{r1}", $"/Print/Receipt/{r1}?handler=EscPos")).Content.ReadAsStringAsync()).RootElement;
        var text = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(esc.GetProperty("data").GetString()!));
        Assert.Contains(receipt1.ReceiptNumber, text);
        Assert.Contains("PAID", text);

        // Double submission with the same key: one receipt, one ledger entry.
        var key = Guid.NewGuid();
        var d1 = await ReceiptIdAsync(await PostCollectAsync(cash, pid, counter, new[] { new Line(paid) }, key: key));
        var d2 = await ReceiptIdAsync(await PostCollectAsync(cash, pid, counter, new[] { new Line(paid) }, key: key));
        Assert.Equal(d1, d2);
        await using (var db = Factory.NewDb()) Assert.Single(await db.CashierLedger.Where(e => e.ReceiptId == d1).ToListAsync());
        Assert.NotEmpty(await AuditAsync(AuditActions.DuplicatePaymentBlocked));
        Assert.Equal(900m, await OutstandingAsync(cashId));

        // Cancellation: cashier may not, admin with reason may; receipt stays, number not reused.
        Denied(await cash.PostFormAsync($"/Finance/Receipts/Details/{r1}", new[] { P("CancelReason", "x") }, $"/Finance/Receipts/Details/{r1}?handler=Cancel"), "cashier cancelling");
        Assert.Contains("reason", Errors(await (await admin.PostFormAsync($"/Finance/Receipts/Details/{r1}", new[] { P("CancelReason", "") }, $"/Finance/Receipts/Details/{r1}?handler=Cancel")).Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Finance/Receipts/Details/{r1}", new[] { P("CancelReason", "Wrong patient selected") }, $"/Finance/Receipts/Details/{r1}?handler=Cancel")).StatusCode);
        var cancelled = await ReceiptAsync(r1);
        Assert.Equal(ReceiptStatus.Cancelled, cancelled.Status);
        Assert.Equal(receipt1.ReceiptNumber, cancelled.ReceiptNumber);
        Assert.Equal(600m, await OutstandingAsync(cashId));
        await using (var db = Factory.NewDb())
        {
            Assert.Single(await db.CashierLedger.Where(e => e.ReceiptId == r1 && e.EntryType == CashierEntryType.ReceiptCancellation && e.Amount == -300).ToListAsync());
            Assert.Equal(ChargeStatus.Cancelled, (await db.Charges.SingleAsync(c => c.OriginReceiptId == r1)).Status);
            // The database itself refuses to delete or alter financial history.
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync($"DELETE FROM \"Receipts\" WHERE \"Id\" = {r1}"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync($"UPDATE \"Receipts\" SET \"AmountPaid\" = 1 WHERE \"Id\" = {r2}"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"CashierLedger\""));
        }
        var details = await admin.GetHtmlAsync($"/Finance/Receipts/Details/{r1}");
        Assert.Contains("Wrong patient selected", details);
        Assert.Contains("Cancelled", details);
        var next = await CollectAsync(cash, pid, counter, new Line(paid));
        Assert.True(string.CompareOrdinal((await ReceiptAsync(next)).ReceiptNumber, receipt1.ReceiptNumber) > 0);
        Assert.Contains(receipt1.ReceiptNumber, await admin.GetHtmlAsync("/Finance/Receipts?period=year&status=cancelled"));
        Assert.Single(await AuditAsync(AuditActions.ReceiptCancelled));
    }

    [Fact]
    public async Task Fee_changes_affect_only_new_receipts_and_partial_payments_track_the_balance()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var us = await ServiceAsync(admin, "US-1", "Ultrasound Abdomen", 500, "Ultrasound");
        var surgery = await ServiceAsync(admin, "PROC-1", "Minor Procedure", 1000, "Procedure", partial: true);
        var pid = await RegisterPatientAsync(admin, PatientFields("Fee Patient", "Fee Father"));

        var old = await CollectAsync(admin, pid, counter, new Line(us));
        var change = await admin.PostFormAsync($"/Finance/Services?id={us}", new[]
        {
            P("Form.Code", "US-1"), P("Form.Name", "Ultrasound Abdomen"), P("Form.Kind", "Ultrasound"), P("Form.Fee", "650"), P("Form.IsFree", "false"),
            P("Form.AllowPartialPayment", "false"), P("Form.IsActive", "true"), P("Form.FeeNote", "New rates"),
        });
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        var newer = await CollectAsync(admin, pid, counter, new Line(us));
        Assert.Equal(500m, (await ReceiptAsync(old)).AmountPaid);
        Assert.Equal(650m, (await ReceiptAsync(newer)).AmountPaid);
        Assert.Contains("New rates", await admin.GetHtmlAsync($"/Finance/Services?id={us}"));
        Assert.Single(await AuditAsync(AuditActions.ServiceFeeChanged));
        // Back-dated fee changes are refused.
        var backdated = await admin.PostFormAsync($"/Finance/Services?id={us}", new[]
        {
            P("Form.Code", "US-1"), P("Form.Name", "Ultrasound Abdomen"), P("Form.Kind", "Ultrasound"), P("Form.Fee", "700"), P("Form.IsFree", "false"),
            P("Form.IsActive", "true"), P("Form.FeeEffectiveFrom", DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd")),
        });
        Assert.Contains("back-dated", Errors(await backdated.Content.ReadAsStringAsync()));

        // Non-partial service must be paid in full.
        var notPartial = await PostCollectAsync(admin, pid, counter, new[] { new Line(us, PayNow: 100) });
        Assert.Contains("paid in full", Errors(await notPartial.Content.ReadAsStringAsync()));

        // Partial payment: 400 of 1000 → balance 600, then pay the balance with a second receipt.
        var part1 = await CollectAsync(admin, pid, counter, new Line(surgery, PayNow: 400));
        var rec1 = await ReceiptAsync(part1);
        Assert.Equal(400m, rec1.AmountPaid);
        Assert.Equal(600m, rec1.BalanceAfter);
        long chargeId;
        await using (var db = Factory.NewDb())
        {
            var charge = await db.Charges.SingleAsync(c => c.ServiceItemId == surgery);
            chargeId = charge.Id;
            Assert.Equal(ChargeStatus.PartiallyPaid, charge.Status);
            Assert.Equal(600m, charge.NetAmount - charge.PaidAmount);
        }
        Assert.Contains("600.00", await admin.GetHtmlAsync($"/Print/Receipt/{part1}"));
        var over = await PostCollectAsync(admin, pid, counter, Array.Empty<Line>(), balances: new[] { (chargeId, 700m) });
        Assert.Contains("only 600.00 is outstanding", Errors(await over.Content.ReadAsStringAsync()));
        Assert.Contains("CHG-", await admin.GetHtmlAsync($"/Finance/Collect?patientId={pid}"));
        var part2 = await ReceiptIdAsync(await PostCollectAsync(admin, pid, counter, Array.Empty<Line>(), balances: new[] { (chargeId, 600m) }));
        Assert.NotEqual(part1, part2);
        Assert.Equal(600m, (await ReceiptAsync(part2)).AmountPaid);
        await using (var db = Factory.NewDb()) Assert.Equal(ChargeStatus.Paid, (await db.Charges.SingleAsync(c => c.Id == chargeId)).Status);

        // Discount needs permission and a reason.
        var noReason = await PostCollectAsync(admin, pid, counter, new[] { new Line(us, Discount: 50) });
        Assert.Contains("reason for the discount", Errors(await noReason.Content.ReadAsStringAsync()));
        var disc = await CollectAsync(admin, pid, counter, new Line(us, Discount: 50, Reason: "Staff family"));
        Assert.Equal(600m, (await ReceiptAsync(disc)).AmountPaid);
        var (cash, _) = await CashierAsync(admin, "no.discount", counter);
        var forbiddenDiscount = await PostCollectAsync(cash, pid, counter, new[] { new Line(us, Discount: 50, Reason: "x") });
        Assert.Contains("not allowed to give discounts", Errors(await forbiddenDiscount.Content.ReadAsStringAsync()));
        // Bank transfer needs a reference and does not count as cash in hand.
        var bank = await PostCollectAsync(admin, pid, counter, new[] { new Line(us) }, method: "BANK");
        Assert.Contains("reference", Errors(await bank.Content.ReadAsStringAsync()));
        var bankOk = await ReceiptIdAsync(await PostCollectAsync(admin, pid, counter, new[] { new Line(us) }, method: "BANK", reference: "IBFT-778"));
        await using (var db = Factory.NewDb()) Assert.False((await db.CashierLedger.SingleAsync(e => e.ReceiptId == bankOk)).AffectsCashInHand);
    }

    [Fact]
    public async Task Refunds_are_separate_from_cancellation_and_cannot_exceed_the_paid_amount()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var doc = await DoctorAsync(admin, "Dr. Refund", "FullFeeToDoctor", 0, 1000);
        var lab = await ServiceAsync(admin, "LAB-CBC", "CBC", 800);
        var pid = await RegisterPatientAsync(admin, PatientFields("Refund Patient", "Refund Father"));
        var (cash, cashId) = await CashierAsync(admin, "cashier.ref", counter, Perm.FinanceProcessRefunds);
        int consult;
        await using (var db = Factory.NewDb()) consult = await db.ServiceItems.Where(s => s.DoctorId == doc && s.Kind == ServiceKind.Consultation).Select(s => s.Id).SingleAsync();

        var r = await CollectAsync(cash, pid, counter, new Line(consult), new Line(lab));
        Assert.Equal(1800m, await OutstandingAsync(cashId));
        Assert.Equal(1000m, await DoctorPayableAsync(doc));

        // Cashier can request but not approve; request above the paid amount is refused.
        var tooMuch = await cash.PostFormAsync($"/Finance/Receipts/Details/{r}", new[] { P("RefundAmount", "1900"), P("RefundMethodId", (await MethodAsync("CASH")).ToString()), P("RefundReason", "x") }, $"/Finance/Receipts/Details/{r}?handler=Refund");
        Assert.Contains("cannot exceed", Errors(await tooMuch.Content.ReadAsStringAsync()));
        Assert.Contains("reason", Errors(await (await cash.PostFormAsync($"/Finance/Receipts/Details/{r}", new[] { P("RefundAmount", "100"), P("RefundMethodId", (await MethodAsync("CASH")).ToString()), P("RefundReason", "") }, $"/Finance/Receipts/Details/{r}?handler=Refund")).Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, (await cash.PostFormAsync($"/Finance/Receipts/Details/{r}", new[] { P("RefundAmount", "500"), P("RefundMethodId", (await MethodAsync("CASH")).ToString()), P("RefundReason", "Doctor left early"), P("ApproveNow", "true") }, $"/Finance/Receipts/Details/{r}?handler=Refund")).StatusCode);
        long refundId;
        await using (var db = Factory.NewDb())
        {
            var refund = await db.Refunds.SingleAsync();
            refundId = refund.Id;
            Assert.Equal(RefundStatus.Pending, refund.Status); // no approve permission → stays pending
        }
        Assert.Equal(1800m, await OutstandingAsync(cashId));
        Denied(await cash.PostFormAsync($"/Finance/Receipts/Details/{r}", Array.Empty<KeyValuePair<string, string>>(), $"/Finance/Refunds?handler=Approve&id={refundId}"), "cashier approving refund");
        // Pending 500 counts against what is refundable.
        var stillTooMuch = await admin.PostFormAsync($"/Finance/Receipts/Details/{r}", new[] { P("RefundAmount", "1400"), P("RefundMethodId", (await MethodAsync("CASH")).ToString()), P("RefundReason", "x") }, $"/Finance/Receipts/Details/{r}?handler=Refund");
        Assert.Contains("cannot exceed", Errors(await stillTooMuch.Content.ReadAsStringAsync()));

        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Finance/Refunds", new[] { P("Note", "OK") }, $"/Finance/Refunds?handler=Approve&id={refundId}")).StatusCode);
        Assert.Equal(1300m, await OutstandingAsync(cashId));
        Assert.Equal(500m, await DoctorPayableAsync(doc)); // first line (consultation) refunded 500 → doctor share reversed 500
        var receipt = await ReceiptAsync(r);
        Assert.Equal(ReceiptStatus.Valid, receipt.Status);
        Assert.Equal(500m, receipt.RefundedAmount);

        // Full refund of the rest (1300) by admin.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Finance/Receipts/Details/{r}", new[] { P("RefundAmount", "1300"), P("RefundMethodId", (await MethodAsync("CASH")).ToString()), P("RefundReason", "Patient not served"), P("ApproveNow", "true") }, $"/Finance/Receipts/Details/{r}?handler=Refund")).StatusCode);
        Assert.Equal(0m, await OutstandingAsync(cashId));
        Assert.Equal(0m, await DoctorPayableAsync(doc));
        receipt = await ReceiptAsync(r);
        Assert.Equal(1800m, receipt.RefundedAmount);
        Assert.All(receipt.Lines, l => Assert.Equal(l.Amount, l.RefundedAmount));
        // Nothing left to refund; a refunded receipt cannot be cancelled.
        Assert.Contains("cannot exceed", Errors(await (await admin.PostFormAsync($"/Finance/Receipts/Details/{r}", new[] { P("RefundAmount", "1"), P("RefundMethodId", (await MethodAsync("CASH")).ToString()), P("RefundReason", "x") }, $"/Finance/Receipts/Details/{r}?handler=Refund")).Content.ReadAsStringAsync()));
        Assert.Contains("refund", Errors(await (await admin.PostFormAsync($"/Finance/Receipts/Details/{r}", new[] { P("CancelReason", "x") }, $"/Finance/Receipts/Details/{r}?handler=Cancel")).Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, (await AuditAsync(AuditActions.RefundCompleted)).Count);
        Assert.Equal(2, (await AuditAsync(AuditActions.RefundRequested)).Count);
        var dash = await admin.GetHtmlAsync("/Finance/Dashboard");
        Assert.Contains($"data-kpi=\"refunds\">Rs. {Money(1800)}", dash);
        Assert.Contains($"data-kpi=\"net\">Rs. {Money(0)}", dash);
    }

    [Fact]
    public async Task Doctor_accounts_follow_each_fee_arrangement_and_settlements()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var dA = await DoctorAsync(admin, "Dr. Full Fee", "FullFeeToDoctor", 0, 1000);
        var dB = await DoctorAsync(admin, "Dr. Commission", "HospitalCommissionPercent", 30, 2000);
        var dC = await DoctorAsync(admin, "Dr. Fixed", "DoctorFixedAmount", 500, 1500);
        var dD = await DoctorAsync(admin, "Dr. Percent", "DoctorPercent", 40, 1200);
        var pid = await RegisterPatientAsync(admin, PatientFields("Doctor Fee Patient", "Fee Father"));
        async Task<int> Consult(int d)
        {
            await using var db = Factory.NewDb();
            return await db.ServiceItems.Where(s => s.DoctorId == d && s.Kind == ServiceKind.Consultation).Select(s => s.Id).SingleAsync();
        }
        await CollectAsync(admin, pid, counter, new Line(await Consult(dA)), new Line(await Consult(dA)));
        await CollectAsync(admin, pid, counter, new Line(await Consult(dB)));
        await CollectAsync(admin, pid, counter, new Line(await Consult(dC)), new Line(await Consult(dC)));
        await CollectAsync(admin, pid, counter, new Line(await Consult(dD)));

        Assert.Equal(2000m, await DoctorPayableAsync(dA));
        Assert.Equal(1400m, await DoctorPayableAsync(dB));
        Assert.Equal(1000m, await DoctorPayableAsync(dC));
        Assert.Equal(480m, await DoctorPayableAsync(dD));
        var list = await admin.GetHtmlAsync("/Finance/Doctors");
        Assert.Contains("Hospital commission 30%", list);
        var accountB = await admin.GetHtmlAsync($"/Finance/Doctors/Account/{dB}");
        Assert.Contains($"data-kpi=\"collected\">Rs. {Money(2000)}", accountB);
        Assert.Contains($"data-kpi=\"hospital\">Rs. {Money(600)}", accountB);

        // Partial payment to A, full payment to B, overpayment refused.
        async Task<HttpResponseMessage> Pay(int d, decimal amount, string method = "CASH", string? reference = null) =>
            await admin.PostFormAsync($"/Finance/Doctors/Account/{d}", new[]
            {
                P("Pay.Amount", amount.ToString(CultureInfo.InvariantCulture)), P("Pay.PaymentMethodId", (await MethodAsync(method)).ToString()),
                P("Pay.PaymentReference", reference ?? ""), P("Pay.Notes", "Weekly payment"),
            }, $"/Finance/Doctors/Account/{d}?handler=Pay");
        Assert.Equal(HttpStatusCode.Redirect, (await Pay(dA, 500)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Pay(dB, 1400, "BANK", "TRX-55")).StatusCode);
        Assert.Contains("At most 1,500.00", Errors(await (await Pay(dA, 1600)).Content.ReadAsStringAsync()));
        Assert.Contains("Nothing is payable", Errors(await (await Pay(dB, 1)).Content.ReadAsStringAsync()));
        Assert.Equal(1500m, await DoctorPayableAsync(dA));
        Assert.Equal(0m, await DoctorPayableAsync(dB));
        var accountA = await admin.GetHtmlAsync($"/Finance/Doctors/Account/{dA}");
        Assert.Contains($"data-kpi=\"paid\">Rs. {Money(500)}", accountA);
        Assert.Contains($"data-kpi=\"payable\">Rs. {Money(1500)}", accountA);
        Assert.Contains("Weekly payment", accountA);
        Assert.Contains("TRX-55", await admin.GetHtmlAsync($"/Finance/Doctors/Account/{dB}"));
        Assert.Equal(2, (await AuditAsync(AuditActions.DoctorPaid)).Count);
        await using (var db = Factory.NewDb()) Assert.All(await db.DoctorSettlements.ToListAsync(), s => Assert.Equal(AdminUser, s.RecordedByUsername));

        // Changing an arrangement only affects new consultations.
        await ShareAsync(admin, dD, "DoctorPercent", 50);
        await CollectAsync(admin, pid, counter, new Line(await Consult(dD)));
        Assert.Equal(480m + 600m, await DoctorPayableAsync(dD));
        Assert.Contains("Pending doctor payments", await admin.GetHtmlAsync("/Finance/Dashboard"));
        // Invalid arrangement refused; the doctor-wise token system is untouched by finance.
        var bad = await admin.PostFormAsync($"/Finance/Doctors/Account/{dD}", new[] { P("ShareType", "DoctorPercent"), P("ShareValue", "150") }, $"/Finance/Doctors/Account/{dD}?handler=Share");
        Assert.Contains("between 0 and 100", Errors(await bad.Content.ReadAsStringAsync()));
        Assert.Contains("At most", Errors(await (await Pay(dD, 5000)).Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Daily_closing_matches_the_transactions_and_never_resets_them()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var opd = await CounterAsync(admin, "OPD", "OPD Counter");
        var lab = await CounterAsync(admin, "LAB", "Lab Counter");
        var svc = await ServiceAsync(admin, "LAB-500", "Test 500", 500);
        var pid = await RegisterPatientAsync(admin, PatientFields("Closing Patient", "Closing Father"));
        var (a, aId) = await CashierAsync(admin, "close.a", opd);
        var (b, _) = await CashierAsync(admin, "close.b", lab);
        await CollectAsync(a, pid, opd, new Line(svc), new Line(svc));                                   // 1000 cash
        await ReceiptIdAsync(await PostCollectAsync(a, pid, opd, new[] { new Line(svc) }, method: "CARD", reference: "C-1")); // 500 non-cash
        var toCancel = await CollectAsync(a, pid, opd, new Line(svc));                                   // 500 cash then cancelled
        await admin.PostFormAsync($"/Finance/Receipts/Details/{toCancel}", new[] { P("CancelReason", "Duplicate") }, $"/Finance/Receipts/Details/{toCancel}?handler=Cancel");
        await CollectAsync(b, pid, lab, new Line(svc));                                                  // 500 cash at lab
        Assert.Equal(HttpStatusCode.Redirect, (await SubmitDepositAsync(a, 600)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ConfirmAsync(admin, await DepositIdAsync("close.a", 600), 600)).StatusCode);

        int ledgerBefore;
        await using (var db = Factory.NewDb()) ledgerBefore = await db.CashierLedger.CountAsync();
        var own = await a.GetHtmlAsync("/Finance/Closing");
        Assert.Contains($"data-k=\"cash\">Rs. {Money(1500)}", own);
        Assert.Contains($"data-k=\"noncash\">Rs. {Money(500)}", own);
        Assert.Contains($"data-k=\"cancelled\">− Rs. {Money(500)}", own);
        Assert.Contains($"data-k=\"confirmed\">− Rs. {Money(600)}", own);
        Assert.Contains($"data-k=\"closing\">Rs. {Money(400)}", own);
        Assert.Equal(400m, await OutstandingAsync(aId));
        Assert.Equal(HttpStatusCode.Redirect, (await a.PostFormAsync("/Finance/Closing", new[] { P("Notes", "All OK") })).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var closing = await db.DailyCashClosings.SingleAsync();
            Assert.Equal(400m, closing.ClosingBalance);
            Assert.Equal(ClosingScope.Cashier, closing.Scope);
            Assert.Equal(ledgerBefore, await db.CashierLedger.CountAsync()); // nothing reset
        }
        Assert.Equal(400m, await OutstandingAsync(aId));

        // Cashiers cannot close others / counters / the hospital.
        Denied(await a.GetAsync("/Finance/Closing?scope=Hospital"), "cashier hospital closing");
        Denied(await a.GetAsync("/Finance/Closing?scope=Counter&id=" + lab), "cashier counter closing");
        Denied(await b.GetAsync($"/Finance/Closing?scope=Cashier&id={aId}"), "B closing A");

        var counter = await admin.GetHtmlAsync($"/Finance/Closing?scope=Counter&id={lab}");
        Assert.Contains($"data-k=\"cash\">Rs. {Money(500)}", counter);
        var hospital = await admin.GetHtmlAsync("/Finance/Closing?scope=Hospital");
        Assert.Contains($"data-k=\"cash\">Rs. {Money(2000)}", hospital);
        Assert.Contains($"data-k=\"closing\">Rs. {Money(900)}", hospital);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Finance/Closing?scope=Hospital", new[] { P("Notes", "Day end") }, "/Finance/Closing?scope=Hospital")).StatusCode);

        // A discrepancy is fixed by an adjustment with a reason (never by editing history).
        Assert.Contains("reason", Errors(await (await admin.PostFormAsync("/Finance/Adjustments", new[] { P("Account", $"C:{aId}"), P("Amount", "-100"), P("Reason", "") })).Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Finance/Adjustments", new[] { P("Account", $"C:{aId}"), P("Amount", "-100"), P("Reason", "Counted short, approved write-off") })).StatusCode);
        Assert.Equal(300m, await OutstandingAsync(aId));
        var adminId = await UserIdAsync(AdminUser);
        Assert.Contains("own account", Errors(await (await admin.PostFormAsync("/Finance/Adjustments", new[] { P("Account", $"C:{adminId}"), P("Amount", "-1"), P("Reason", "x") })).Content.ReadAsStringAsync()));
        Denied(await a.PostFormAsync("/Finance", new[] { P("Account", $"C:{aId}"), P("Amount", "-300"), P("Reason", "me") }, "/Finance/Adjustments"), "cashier adjusting own balance");
        Assert.Equal(300m, await OutstandingAsync(aId));
        Assert.Single(await AuditAsync(AuditActions.AdjustmentRecorded));
        Assert.Equal(2, (await AuditAsync(AuditActions.DailyClosingCreated)).Count);
    }

    [Fact]
    public async Task Unauthorised_users_cannot_reach_finance_and_concurrent_payments_stay_unique()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var svc = await ServiceAsync(admin, "LAB-100", "Test 100", 100);
        var pid = await RegisterPatientAsync(admin, PatientFields("Security Patient", "Security Father"));
        var nobody = await UserWithAsync(admin, "no.finance", Perm.DashboardView, Perm.PatientsSearch);
        foreach (var url in new[] { "/Finance", "/Finance/Collect", "/Finance/Receipts", "/Finance/Dashboard", "/Finance/Reports", "/Finance/Deposits", "/Finance/Refunds",
                     "/Finance/Doctors", "/Finance/Counters", "/Finance/Services", "/Finance/Closing", "/Finance/Adjustments", "/Finance/Settings", "/Finance/Cashiers" })
            Denied(await nobody.GetAsync(url), url);
        var forgedToken = await nobody.TokenAsync("/");
        Denied(await nobody.Http.PostAsync("/Finance/Collect", new FormUrlEncodedContent(new[]
        {
            P("__RequestVerificationToken", forgedToken), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.CounterId", counter.ToString()),
            P("Input.PaymentMethodId", (await MethodAsync("CASH")).ToString()), P("PatientId", pid.ToString()), P("Lines[0].ServiceItemId", svc.ToString()),
        })), "forged collection");
        await using (var db0 = Factory.NewDb()) Assert.Empty(await db0.Receipts.ToListAsync());
        Assert.DoesNotContain("data-nav=\"finance-collect\"", await nobody.GetHtmlAsync("/"));
        // Fee changes need "Manage Service Fees".
        var (cash, cashId) = await CashierAsync(admin, "sec.cash", counter);
        Denied(await cash.PostFormAsync("/Finance", new[] { P("Form.Code", "LAB-100"), P("Form.Name", "Test"), P("Form.Fee", "1") }, $"/Finance/Services?id={svc}"), "cashier changing fee");
        Denied(await cash.GetAsync("/Finance/Reports?handler=Export&format=xlsx"), "cashier export");

        // 12 simultaneous payments from two cashiers: unique receipt numbers, correct totals.
        var (cash2, cash2Id) = await CashierAsync(admin, "sec.cash2", counter);
        var t1 = await cash.TokenAsync($"/Finance/Collect?patientId={pid}");
        var t2 = await cash2.TokenAsync($"/Finance/Collect?patientId={pid}");
        var method = await MethodAsync("CASH");
        var sameKey = Guid.NewGuid();
        var tasks = Enumerable.Range(0, 16).Select(i =>
        {
            var (b, t) = i % 2 == 0 ? (cash, t1) : (cash2, t2);
            var key = i >= 12 ? sameKey : Guid.NewGuid(); // the last 4 are the same double-submitted payment
            return b.Http.PostAsync("/Finance/Collect", new FormUrlEncodedContent(new[]
            {
                P("__RequestVerificationToken", t), P("Input.IdempotencyKey", key.ToString()), P("Input.CounterId", counter.ToString()),
                P("Input.PaymentMethodId", method.ToString()), P("PatientId", pid.ToString()), P("Lines[0].ServiceItemId", svc.ToString()),
            }));
        }).ToList();
        foreach (var r in await Task.WhenAll(tasks)) await ReceiptIdAsync(r);
        await using (var db = Factory.NewDb())
        {
            var numbers = await db.Receipts.Select(r => r.ReceiptNumber).ToListAsync();
            Assert.Equal(13, numbers.Count);
            Assert.Equal(13, numbers.Distinct().Count());
            Assert.Equal(1300m, await db.CashierLedger.Where(e => e.EntryType == CashierEntryType.Collection).SumAsync(e => e.Amount));
        }
        Assert.Equal(1300m, await OutstandingAsync(cashId) + await OutstandingAsync(cash2Id));

        // Excel export for admin is a real .xlsx (zip) with the transactions sheet.
        var xlsx = await admin.GetAsync("/Finance/Reports?period=today&handler=Export&format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        using (var zip = new ZipArchive(await xlsx.Content.ReadAsStreamAsync()))
        {
            Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
            using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            Assert.Contains("RCP-", await sheet.ReadToEndAsync());
        }
        var csv = await (await admin.GetAsync("/Finance/Reports?period=today&handler=Export&format=csv")).Content.ReadAsStringAsync();
        Assert.Contains("Test 100", csv);
        Assert.Equal(2, (await AuditAsync(AuditActions.FinanceExported)).Count);
        Assert.NotEmpty(await AuditAsync(AuditActions.PaymentCollected));
    }

    [Fact]
    public async Task Appointment_and_opd_visits_can_be_billed_without_changing_tokens()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "OPD", "OPD Counter");
        var doc = await DoctorAsync(admin, "Dr. Billing Link", "HospitalCommissionPercent", 20, 900);
        var pid = await RegisterPatientAsync(admin, PatientFields("Billing Link Patient", "Link Father"));
        var book = await admin.PostFormAsync($"/Appointments/Book?patientId={pid}", new[] { P("Input.DoctorId", doc.ToString()) });
        Assert.Equal(HttpStatusCode.Redirect, book.StatusCode);
        Appointment appt;
        await using (var db = Factory.NewDb()) appt = await db.Appointments.SingleAsync();
        Assert.Equal(1, appt.TokenNumber);
        Assert.Contains("Collect fee", await admin.GetHtmlAsync($"/Appointments/Details/{appt.Id}"));

        var form = await admin.GetHtmlAsync($"/Finance/Collect?encounterId={appt.EncounterId}");
        Assert.Contains("Visit OPD-", form);
        Assert.Contains("Billing Link Patient", form);
        var token = await admin.TokenAsync($"/Finance/Collect?encounterId={appt.EncounterId}");
        int consult;
        await using (var db = Factory.NewDb()) consult = await db.ServiceItems.Where(s => s.DoctorId == doc).Select(s => s.Id).SingleAsync();
        Assert.Contains($"value=\"{consult}\" data-fee=\"900.00\"", form);
        var r = await admin.Http.PostAsync("/Finance/Collect", new FormUrlEncodedContent(new[]
        {
            P("__RequestVerificationToken", token), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.CounterId", counter.ToString()),
            P("Input.PaymentMethodId", (await MethodAsync("CASH")).ToString()), P("PatientId", pid.ToString()), P("EncounterId", appt.EncounterId.ToString()),
            P("Lines[0].ServiceItemId", consult.ToString()), P("Lines[0].DoctorId", doc.ToString()), P("Lines[0].EncounterId", appt.EncounterId.ToString()),
        }));
        var rid = await ReceiptIdAsync(r);
        await using (var db = Factory.NewDb())
        {
            var charge = await db.Charges.SingleAsync();
            Assert.Equal(appt.EncounterId, charge.EncounterId);
            Assert.Equal(720m, charge.DoctorShareAmount);
            Assert.Equal(1, (await db.Appointments.SingleAsync()).TokenNumber);
        }
        Assert.Equal(720m, await DoctorPayableAsync(doc));
        Assert.Contains("already billed", await admin.GetHtmlAsync($"/Finance/Collect?encounterId={appt.EncounterId}"));
        Assert.Contains("Dr. Billing Link", await admin.GetHtmlAsync($"/Print/Receipt/{rid}"));
        // The thermal appointment slip is unchanged.
        Assert.Contains("TOKEN NO.", await admin.GetHtmlAsync($"/Print/Appointment/{appt.Id}"));
    }
}
