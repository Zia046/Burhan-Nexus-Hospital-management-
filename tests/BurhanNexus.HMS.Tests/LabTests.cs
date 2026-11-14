using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Phase 4 – laboratory: catalogue, booking, samples, result entry, verification, reports, billing and security.</summary>
public partial class LabTests : Phase2TestBase
{
    [GeneratedRegex(@"/Lab/Order/(\d+)")] private static partial Regex OrderRegex();

    private static string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);

    // ---------------- helpers ----------------

    /// <summary>Creates an active test with one numeric parameter through the catalogue screens.</summary>
    private async Task<(int TestId, int ParamId)> LabTestAsync(Browser admin, string code, string name, decimal price, string param = "Glucose",
        string unit = "mg/dL", decimal? low = 70, decimal? high = 110, bool partial = false, string sample = "SER")
    {
        int cat, smp;
        await using (var db = Factory.NewDb())
        {
            cat = await db.LabCategories.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();
            smp = await db.LabSampleTypes.Where(s => s.Code == sample).Select(s => s.Id).SingleAsync();
        }
        var r = await admin.PostFormAsync("/Lab/Catalogue/Test", new[]
        {
            P("Input.Code", code), P("Input.Name", name), P("Input.CategoryId", cat.ToString()), P("Input.SampleTypeId", smp.ToString()),
            P("Input.IsActive", "true"), P("Input.IsFree", price == 0 ? "true" : "false"), P("Input.Price", N(price)),
            P("Input.AllowPartialPayment", partial ? "true" : "false"), P("Input.Method", "Enzymatic"),
        });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
        int testId;
        await using (var db = Factory.NewDb()) testId = await db.LabTests.Where(t => t.Code == code).Select(t => t.Id).SingleAsync();
        var paramId = await ParameterAsync(admin, testId, param, unit);
        if (low is not null || high is not null) await RangeAsync(admin, testId, paramId, low, high);
        return (testId, paramId);
    }

    private async Task<int> ParameterAsync(Browser admin, int testId, string name, string unit, int? id = null)
    {
        var r = await admin.PostFormAsync($"/Lab/Catalogue/Test/{testId}", new[]
        {
            P("Param.Name", name), P("Param.Unit", unit), P("Param.ResultType", "Numeric"), P("Param.Decimals", "1"), P("Param.SortOrder", "10"), P("Param.IsActive", "true"),
        }, $"/Lab/Catalogue/Test/{testId}?handler=Parameter" + (id is int i ? $"&parameterId={i}" : ""));
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.LabTestParameters.Where(p => p.LabTestId == testId && p.Name == name).Select(p => p.Id).SingleAsync();
    }

    private async Task RangeAsync(Browser admin, int testId, int paramId, decimal? low, decimal? high, string? gender = null)
    {
        var fields = new List<KeyValuePair<string, string>> { P("Range.Low", low is null ? "" : N(low.Value)), P("Range.High", high is null ? "" : N(high.Value)) };
        if (gender is not null) fields.Add(P("Range.Gender", gender));
        var r = await admin.PostFormAsync($"/Lab/Catalogue/Test/{testId}", fields, $"/Lab/Catalogue/Test/{testId}?handler=Range&parameterId={paramId}");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
    }

    private static async Task<HttpResponseMessage> PostBookAsync(Browser b, long patientId, IEnumerable<int> testIds, Guid? key = null, params (string, string)[] extra)
    {
        var fields = new List<KeyValuePair<string, string>> { P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()), P("PatientId", patientId.ToString()) };
        fields.AddRange(testIds.Select(t => P("TestIds", t.ToString())));
        fields.AddRange(extra.Select(e => P(e.Item1, e.Item2)));
        return await b.PostFormAsync($"/Lab/Book?patientId={patientId}", fields);
    }

    private static async Task<long> OrderIdAsync(HttpResponseMessage r)
    {
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Booking failed: " + Errors(await r.Content.ReadAsStringAsync()));
        var m = OrderRegex().Match(r.Headers.Location!.OriginalString);
        Assert.True(m.Success, "Unexpected redirect " + r.Headers.Location);
        return long.Parse(m.Groups[1].Value);
    }

    private async Task<long> BookAsync(Browser b, long patientId, params int[] testIds) => await OrderIdAsync(await PostBookAsync(b, patientId, testIds));

    private async Task<LabOrder> OrderAsync(long id)
    {
        await using var db = Factory.NewDb();
        return await db.LabOrders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Values).Include(o => o.Samples).AsSplitQuery().SingleAsync(o => o.Id == id);
    }

    private async Task<LabOrderItem> ItemAsync(long orderId, string code)
    {
        await using var db = Factory.NewDb();
        return await db.LabOrderItems.AsNoTracking().Include(i => i.Values).SingleAsync(i => i.LabOrderId == orderId && i.TestCode == code);
    }

    private static async Task CollectAllAsync(Browser b, LabOrder o)
    {
        foreach (var s in o.Samples.Where(s => s.Status == LabSampleStatus.Pending))
        {
            var r = await b.PostFormAsync($"/Lab/Order/{o.Id}", Array.Empty<KeyValuePair<string, string>>(), $"/Lab/Order/{o.Id}?handler=Collect&sampleId={s.Id}&receive=true");
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        }
    }

    private async Task<HttpResponseMessage> EnterAsync(Browser b, long orderId, string code, string value, string mode = "submit", string? comment = null)
    {
        var item = await ItemAsync(orderId, code);
        var fields = new List<KeyValuePair<string, string>> { P("Mode", mode), P("RowVersion", item.RowVersion.ToString()) };
        fields.AddRange(item.Values.Select(v => P($"Values[{v.Id}]", value)));
        if (comment is not null) fields.Add(P("Comment", comment));
        return await b.PostFormAsync($"/Lab/Entry/{orderId}?item={item.Id}", fields);
    }

    private async Task<HttpResponseMessage> VerifyAsync(Browser b, long orderId, string code)
    {
        var item = await ItemAsync(orderId, code);
        return await b.PostFormAsync($"/Lab/Verify/{orderId}", Array.Empty<KeyValuePair<string, string>>(), $"/Lab/Verify/{orderId}?handler=Verify&itemId={item.Id}");
    }

    private static async Task<HttpResponseMessage> PrintAsync(Browser b, long orderId, params long[] itemIds) =>
        await b.PostFormAsync($"/Print/LabReport/{orderId}", itemIds.Select(i => P("ItemIds", i.ToString())), $"/Print/LabReport/{orderId}?handler=Record");

    private async Task<int> CounterAsync(Browser admin, string code, string name)
    {
        var r = await admin.PostFormAsync("/Finance/Counters", new[] { P("Form.Code", code), P("Form.Name", name), P("Form.IsActive", "true") }, "/Finance/Counters?handler=Save");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Counters.Where(c => c.Code == code).Select(c => c.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> PayBalanceAsync(Browser b, long patientId, long orderId, int counterId, long chargeId, decimal amount, Guid? key = null)
    {
        int method;
        await using (var db = Factory.NewDb()) method = await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
        return await b.PostFormAsync($"/Finance/Collect?patientId={patientId}&labOrderId={orderId}", new[]
        {
            P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()), P("Input.CounterId", counterId.ToString()), P("Input.PaymentMethodId", method.ToString()),
            P("PatientId", patientId.ToString()), P("LabOrderId", orderId.ToString()), P("Balances[0].ChargeId", chargeId.ToString()), P("Balances[0].PayNow", N(amount)),
        }, "/Finance/Collect");
    }

    private static void Denied(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode == HttpStatusCode.Forbidden || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied")),
            $"{what} should be denied but returned {(int)r.StatusCode} {r.Headers.Location}");

    private static readonly string[] TechPerms = { Perm.DashboardView, Perm.LabDashboard, Perm.LabCollectSamples, Perm.LabEnterResults, Perm.LabViewResults };
    private static readonly string[] VerifierPerms = { Perm.DashboardView, Perm.LabViewResults, Perm.LabVerifyResults, Perm.LabApproveReports, Perm.LabPrintReports };

    // =====================================================================================

    [Fact]
    public async Task Complete_workflow_from_booking_to_released_report()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var (glu, _) = await LabTestAsync(admin, "GLU", "Blood Glucose (Random)", 500);
        var pid = await RegisterPatientAsync(admin, PatientFields("Lab Workflow Patient", "Lab Father"));
        var tech = await UserWithAsync(admin, "lab.tech", TechPerms);
        var verifier = await UserWithAsync(admin, "lab.pathologist", VerifierPerms);

        // Booking + duplicate submission with the same key → one order, one charge.
        var key = Guid.NewGuid();
        var id = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { glu }, key, ("Input.ReferralType", "External"), ("Input.ExternalDoctorName", "Dr. Outside")));
        var again = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { glu }, key));
        Assert.Equal(id, again);
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.LabOrders.CountAsync());
            var charge = await db.Charges.SingleAsync(c => c.LabOrderId == id);
            Assert.Equal(500m, charge.NetAmount);
            Assert.Equal(ChargeStatus.Unpaid, charge.Status);
            Assert.Matches(@"^LAB-\d{4}-\d{6}$", (await db.LabOrders.SingleAsync()).OrderNumber);
        }
        var order = await OrderAsync(id);
        Assert.Single(order.Samples);
        Assert.Equal(LabOrderItemStatus.SamplePending, order.Items.Single().Status);

        // Order slip and pending list.
        Assert.Contains(order.OrderNumber, await admin.GetHtmlAsync($"/Print/LabSlip/{id}"));
        Assert.Contains("data-pending-test=\"GLU\"", await tech.GetHtmlAsync("/Lab/Pending"));

        // No final report before verification.
        var item = order.Items.Single();
        await PrintAsync(admin, id, item.Id);
        await using (var db = Factory.NewDb()) Assert.Equal(0, await db.LabReportPrints.CountAsync());
        Assert.Contains("data-no-final", await admin.GetHtmlAsync($"/Print/LabReport/{id}"));

        // Sample collection, then technician enters a high value.
        await CollectAllAsync(tech, order);
        Assert.Equal(LabOrderItemStatus.SampleReceived, (await ItemAsync(id, "GLU")).Status);
        var entry = await tech.GetHtmlAsync($"/Lab/Entry/{id}");
        Assert.Contains("mg/dL", entry);
        Assert.Contains("70", entry);
        var saved = await EnterAsync(tech, id, "GLU", "180", comment: "Non-fasting sample");
        Assert.True(saved.StatusCode == HttpStatusCode.Redirect, Errors(await saved.Content.ReadAsStringAsync()));
        var entered = await ItemAsync(id, "GLU");
        Assert.Equal(LabOrderItemStatus.AwaitingVerification, entered.Status);
        Assert.Equal(LabFlag.High, entered.Values.Single().Flag);
        Assert.Equal(180m, entered.Values.Single().NumericValue);
        Assert.DoesNotContain("data-pending-test=\"GLU\"", await tech.GetHtmlAsync("/Lab/Pending"));
        Assert.Contains("data-verify-test=\"GLU\"", await verifier.GetHtmlAsync("/Lab/Verify"));

        // Verification and the released report.
        var v = await VerifyAsync(verifier, id, "GLU");
        Assert.True(v.StatusCode == HttpStatusCode.Redirect, Errors(await v.Content.ReadAsStringAsync()));
        var verified = await ItemAsync(id, "GLU");
        Assert.Equal(LabOrderItemStatus.Verified, verified.Status);
        Assert.Equal("lab.pathologist", verified.VerifiedByUsername);

        var preview = await verifier.GetHtmlAsync($"/Print/LabReport/{id}");
        Assert.Contains("PREVIEW", preview);
        Assert.Contains("Lab Workflow Patient", preview);
        Assert.Contains("Dr. Outside", preview);
        var printed = await PrintAsync(verifier, id, item.Id);
        Assert.Equal(HttpStatusCode.Redirect, printed.StatusCode);
        Assert.Contains("print=True", printed.Headers.Location!.OriginalString);
        Assert.Equal(LabOrderItemStatus.Released, (await ItemAsync(id, "GLU")).Status);
        var final = await verifier.GetHtmlAsync($"/Print/LabReport/{id}");
        Assert.DoesNotContain("lr-watermark", final);
        Assert.Contains("180", final);
        Assert.Contains("Non-fasting sample", final);
        Assert.Contains("Verified by", final);

        // Reprint needs its own permission.
        Denied(await PrintAsync(verifier, id, item.Id), "Reprint without permission");
        Assert.Equal(HttpStatusCode.Redirect, (await PrintAsync(admin, id, item.Id)).StatusCode);
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(new[] { 1, 2 }, await db.LabReportPrints.OrderBy(p => p.CopyNumber).Select(p => p.CopyNumber).ToArrayAsync());
            Assert.Equal(1, await db.Charges.CountAsync(c => c.LabOrderId == id));
        }

        // Previous results remain accessible; audit trail written.
        Assert.Contains("180", await admin.GetHtmlAsync($"/Lab/Patient/{pid}"));
        Assert.Contains(order.OrderNumber, await admin.GetHtmlAsync($"/Lab/Results?q={order.OrderNumber}"));
        Assert.Single(await AuditAsync(AuditActions.LabOrderBooked));
        Assert.Single(await AuditAsync(AuditActions.LabResultVerified));
        Assert.Single(await AuditAsync(AuditActions.LabReportPrinted));
        Assert.Single(await AuditAsync(AuditActions.LabReportReprinted));
    }

    [Fact]
    public async Task Definition_changes_never_alter_booked_orders_and_ranges_follow_gender()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var (hb, hbParam) = await LabTestAsync(admin, "HBX", "Haemoglobin", 0, "Haemoglobin", "g/dL", low: null, high: null, sample: "BLD");
        await RangeAsync(admin, hb, hbParam, 13, 17, "Male");
        await RangeAsync(admin, hb, hbParam, 12, 15, "Female");
        var man = await RegisterPatientAsync(admin, PatientFields("Male Lab Patient", "Father One", "Male"));
        var woman = await RegisterPatientAsync(admin, PatientFields("Female Lab Patient", "Father Two", "Female"));

        var first = await BookAsync(admin, man, hb);
        var her = await BookAsync(admin, woman, hb);
        var m1 = (await ItemAsync(first, "HBX")).Values.Single();
        Assert.Equal(13m, m1.RefLow);
        Assert.Equal(17m, m1.RefHigh);
        var f1 = (await ItemAsync(her, "HBX")).Values.Single();
        Assert.Equal(12m, f1.RefLow);
        Assert.Equal(15m, f1.RefHigh);

        // Change the definition: rename the parameter and replace the male range.
        await ParameterAsync(admin, hb, "Hb (renamed)", "g/L", hbParam);
        int maleRange;
        await using (var db = Factory.NewDb()) maleRange = await db.LabReferenceRanges.Where(r => r.ParameterId == hbParam && r.Gender == Gender.Male).Select(r => r.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Catalogue/Test/{hb}", Array.Empty<KeyValuePair<string, string>>(),
            $"/Lab/Catalogue/Test/{hb}?handler=DeleteRange&rangeId={maleRange}")).StatusCode);
        await RangeAsync(admin, hb, hbParam, 130, 170, "Male");

        var second = await BookAsync(admin, man, hb);
        var old = await ItemAsync(first, "HBX");
        var now = await ItemAsync(second, "HBX");
        Assert.Equal("Haemoglobin", old.Values.Single().Name);
        Assert.Equal("g/dL", old.Values.Single().Unit);
        Assert.Equal(13m, old.Values.Single().RefLow);
        Assert.Equal("Hb (renamed)", now.Values.Single().Name);
        Assert.Equal(130m, now.Values.Single().RefLow);
        Assert.True(now.DefinitionVersion > old.DefinitionVersion);

        // A free test is billed at zero and immediately settled.
        await using (var db = Factory.NewDb())
            Assert.All(await db.Charges.Where(c => c.LabOrderId != null).ToListAsync(), c => Assert.Equal(ChargeStatus.Paid, c.Status));
    }

    [Fact]
    public async Task Verification_rules_rejection_correction_and_bulk_approval()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var (a, _) = await LabTestAsync(admin, "TA", "Test A", 100);
        var (b, _) = await LabTestAsync(admin, "TB", "Test B", 100);
        var pid = await RegisterPatientAsync(admin, PatientFields("Verify Patient", "Verify Father"));
        // A technician who may also verify – but never their own result (hospital default).
        var tech = await UserWithAsync(admin, "tech.verifier", TechPerms.Append(Perm.LabVerifyResults).Append(Perm.LabVerifyOwnResults).ToArray());
        var path = await UserWithAsync(admin, "path.one", VerifierPerms);

        var id = await BookAsync(admin, pid, a, b);
        await CollectAllAsync(tech, await OrderAsync(id));
        Assert.Equal(HttpStatusCode.Redirect, (await EnterAsync(tech, id, "TA", "95")).StatusCode);

        // Invalid number is refused on submit.
        var bad = await EnterAsync(tech, id, "TB", "abc");
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.NotEqual(LabOrderItemStatus.AwaitingVerification, (await ItemAsync(id, "TB")).Status);

        // Self-verification is blocked.
        await VerifyAsync(tech, id, "TA");
        Assert.Equal(LabOrderItemStatus.AwaitingVerification, (await ItemAsync(id, "TA")).Status);

        // Reject needs a reason, then returns the test to the technician.
        var ta = await ItemAsync(id, "TA");
        Assert.Equal(HttpStatusCode.OK, (await path.PostFormAsync($"/Lab/Verify/{id}", new[] { P("Reason", "") }, $"/Lab/Verify/{id}?handler=Reject&itemId={ta.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await path.PostFormAsync($"/Lab/Verify/{id}", new[] { P("Reason", "Repeat – haemolysed?") }, $"/Lab/Verify/{id}?handler=Reject&itemId={ta.Id}")).StatusCode);
        var back = await ItemAsync(id, "TA");
        Assert.Equal(LabOrderItemStatus.InProgress, back.Status);
        Assert.Equal("Repeat – haemolysed?", back.CorrectionReason);
        Assert.Contains("data-pending-test=\"TA\"", await tech.GetHtmlAsync("/Lab/Pending"));

        // Correction (needs "Edit Unverified Results") and bulk verification.
        Denied(await EnterAsync(tech, id, "TA", "97"), "Correction without Edit Unverified Results");
        var editor = await UserWithAsync(admin, "tech.editor", TechPerms.Append(Perm.LabEditResults).ToArray());
        Assert.Equal(HttpStatusCode.Redirect, (await EnterAsync(editor, id, "TA", "97")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await EnterAsync(tech, id, "TB", "88")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await path.PostFormAsync($"/Lab/Verify/{id}", Array.Empty<KeyValuePair<string, string>>(), $"/Lab/Verify/{id}?handler=VerifyAll")).StatusCode);
        var o = await OrderAsync(id);
        Assert.All(o.Items, i => Assert.Equal(LabOrderItemStatus.Verified, i.Status));
        Assert.Equal(97m, o.Items.Single(i => i.TestCode == "TA").Values.Single().NumericValue);
        Assert.Single(await AuditAsync(AuditActions.LabResultRejected));
        Assert.Equal(2, (await AuditAsync(AuditActions.LabResultVerified)).Count);
    }

    [Fact]
    public async Task Verified_results_are_protected_and_withdrawal_keeps_revision_history()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var (t, _) = await LabTestAsync(admin, "CRE", "Creatinine", 300, "Creatinine", "mg/dL", 0.6m, 1.2m);
        var pid = await RegisterPatientAsync(admin, PatientFields("Revision Patient", "Revision Father"));
        var tech = await UserWithAsync(admin, "rev.tech", TechPerms);
        var id = await BookAsync(admin, pid, t);
        await CollectAllAsync(tech, await OrderAsync(id));
        Assert.Equal(HttpStatusCode.Redirect, (await EnterAsync(tech, id, "CRE", "2.4")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await VerifyAsync(admin, id, "CRE")).StatusCode);
        var item = await ItemAsync(id, "CRE");
        Assert.Equal(LabOrderItemStatus.Verified, item.Status);

        // The database refuses to change a verified value, even with direct SQL.
        await using (var db = Factory.NewDb())
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync($"UPDATE \"LabResultValues\" SET \"Value\" = '1.0' WHERE \"Id\" = {item.Values.Single().Id}"));
        await using (var db = Factory.NewDb())
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync($"DELETE FROM \"LabOrderItems\" WHERE \"Id\" = {item.Id}"));

        // Entry is closed for verified results; withdrawal needs permission and a reason.
        Assert.Equal(HttpStatusCode.OK, (await EnterAsync(tech, id, "CRE", "1.0")).StatusCode);
        Assert.Equal("2.4", (await ItemAsync(id, "CRE")).Values.Single().Value);
        Denied(await tech.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "typo") }, $"/Lab/Order/{id}?handler=Withdraw&itemId={item.Id}"), "Withdraw without permission");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "") }, $"/Lab/Order/{id}?handler=Withdraw&itemId={item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "Wrong patient sample value") }, $"/Lab/Order/{id}?handler=Withdraw&itemId={item.Id}")).StatusCode);
        var reopened = await ItemAsync(id, "CRE");
        Assert.Equal(LabOrderItemStatus.InProgress, reopened.Status);
        Assert.Null(reopened.VerifiedUtc);
        await using (var db = Factory.NewDb())
        {
            var rev = await db.LabResultRevisions.SingleAsync(r => r.OrderItemId == item.Id);
            Assert.Equal(1, rev.RevisionNo);
            Assert.Contains("2.4", rev.Snapshot);
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync($"DELETE FROM \"LabResultRevisions\" WHERE \"Id\" = {rev.Id}"));
        }
        var editor = await UserWithAsync(admin, "rev.editor", TechPerms.Append(Perm.LabEditResults).ToArray());
        Assert.Equal(HttpStatusCode.Redirect, (await EnterAsync(editor, id, "CRE", "1.1")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await VerifyAsync(admin, id, "CRE")).StatusCode);
        var html = await admin.GetHtmlAsync($"/Lab/Order/{id}");
        Assert.Contains("data-revisions", html);
        Assert.Single(await AuditAsync(AuditActions.LabResultWithdrawn));
    }

    [Fact]
    public async Task Laboratory_fees_use_the_phase3_cashier_without_duplicates()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var (t, _) = await LabTestAsync(admin, "LIP", "Lipid Profile", 1200);
        var (p, _) = await LabTestAsync(admin, "PRT", "Partial Allowed Test", 1000, partial: true);
        var counter = await CounterAsync(admin, "LABC", "Lab Cash Counter");
        var pid = await RegisterPatientAsync(admin, PatientFields("Billing Lab Patient", "Billing Father"));
        var id = await BookAsync(admin, pid, t, p);
        long full, part;
        await using (var db = Factory.NewDb())
        {
            full = await db.Charges.Where(c => c.LabOrderId == id && c.ServiceName == "Lipid Profile").Select(c => c.Id).SingleAsync();
            part = await db.Charges.Where(c => c.LabOrderId == id && c.ServiceName == "Partial Allowed Test").Select(c => c.Id).SingleAsync();
        }

        // The cash counter shows the laboratory balance.
        Assert.Contains("Lipid Profile", await admin.GetHtmlAsync($"/Finance/Collect?patientId={pid}&labOrderId={id}"));

        // Partial payment is refused for a test that requires full payment.
        var refused = await PayBalanceAsync(admin, pid, id, counter, full, 500);
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("must be paid in full", await refused.Content.ReadAsStringAsync());

        // Full payment, then a repeated submission with the same key → still one receipt.
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Redirect, (await PayBalanceAsync(admin, pid, id, counter, full, 1200, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PayBalanceAsync(admin, pid, id, counter, full, 1200, key)).StatusCode);
        // Paying the settled charge again is refused.
        Assert.Equal(HttpStatusCode.OK, (await PayBalanceAsync(admin, pid, id, counter, full, 1200)).StatusCode);
        // Partial payment where allowed.
        Assert.Equal(HttpStatusCode.Redirect, (await PayBalanceAsync(admin, pid, id, counter, part, 400)).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var charges = await db.Charges.Where(c => c.LabOrderId == id).ToDictionaryAsync(c => c.Id);
            Assert.Equal(ChargeStatus.Paid, charges[full].Status);
            Assert.Equal(1200m, charges[full].PaidAmount);
            Assert.Equal(ChargeStatus.PartiallyPaid, charges[part].Status);
            Assert.Equal(400m, charges[part].PaidAmount);
            Assert.Equal(2, await db.Receipts.CountAsync());
            Assert.Equal(1600m, await db.ReceiptLines.SumAsync(l => l.Amount));
        }

        // Laboratory statistics read the same receipts (no separate laboratory ledger).
        var stats = await admin.GetHtmlAsync("/Lab/Reports?period=today");
        Assert.Contains("1,600.00", stats);
        var xlsx = await admin.GetAsync("/Lab/Reports?handler=Export&format=xlsx&period=today");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Single(await AuditAsync(AuditActions.LabExported));

        // Policy: release only fully paid tests.
        var tech = await UserWithAsync(admin, "bill.tech", TechPerms);
        await CollectAllAsync(tech, await OrderAsync(id));
        Assert.Equal(HttpStatusCode.Redirect, (await EnterAsync(tech, id, "PRT", "50")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await VerifyAsync(admin, id, "PRT")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var s = await db.LabSettings.SingleAsync();
            s.RequirePaymentBeforeRelease = true;
            await db.SaveChangesAsync();
        }
        var item = await ItemAsync(id, "PRT");
        await PrintAsync(admin, id, item.Id);
        Assert.Equal(LabOrderItemStatus.Verified, (await ItemAsync(id, "PRT")).Status);
        Assert.Equal(HttpStatusCode.Redirect, (await PayBalanceAsync(admin, pid, id, counter, part, 600)).StatusCode);
        await PrintAsync(admin, id, item.Id);
        Assert.Equal(LabOrderItemStatus.Released, (await ItemAsync(id, "PRT")).Status);
    }

    [Fact]
    public async Task Sample_rejection_and_test_cancellation_need_reasons()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var (a, _) = await LabTestAsync(admin, "SA", "Serum A", 200);
        var (b, _) = await LabTestAsync(admin, "SB", "Serum B", 300);
        var pid = await RegisterPatientAsync(admin, PatientFields("Sample Patient", "Sample Father"));
        var id = await BookAsync(admin, pid, a, b);
        var order = await OrderAsync(id);
        var sample = Assert.Single(order.Samples); // both serum tests share one sample
        Assert.All(order.Items, i => Assert.Equal(sample.Id, i.SampleId));
        await CollectAllAsync(admin, order);

        var reject = $"/Lab/Order/{id}?handler=RejectSample&sampleId={sample.Id}";
        Assert.Equal(HttpStatusCode.OK, (await admin.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "") }, reject)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "Haemolysed") }, reject)).StatusCode);
        order = await OrderAsync(id);
        Assert.Equal(2, order.Samples.Count);
        Assert.Equal(LabSampleStatus.Rejected, order.Samples.Single(s => s.Id == sample.Id).Status);
        var replacement = order.Samples.Single(s => s.Id != sample.Id);
        Assert.Equal(LabSampleStatus.Pending, replacement.Status);
        Assert.All(order.Items, i => { Assert.Equal(LabOrderItemStatus.SamplePending, i.Status); Assert.Equal(replacement.Id, i.SampleId); });

        var sb = order.Items.Single(i => i.TestCode == "SB");
        var cancel = $"/Lab/Order/{id}?handler=CancelItem&itemId={sb.Id}";
        Assert.Equal(HttpStatusCode.OK, (await admin.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "") }, cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "Doctor changed the request") }, cancel)).StatusCode);
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(LabOrderItemStatus.Cancelled, (await db.LabOrderItems.SingleAsync(i => i.Id == sb.Id)).Status);
            Assert.Equal(ChargeStatus.Cancelled, (await db.Charges.SingleAsync(c => c.Id == sb.ChargeId)).Status);
        }
        Assert.Single(await AuditAsync(AuditActions.LabSampleRejected));
        Assert.Single(await AuditAsync(AuditActions.LabTestCancelled));
    }

    [Fact]
    public async Task Laboratory_permissions_are_enforced_on_the_server()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var (t, _) = await LabTestAsync(admin, "PER", "Permission Test", 750);
        var pid = await RegisterPatientAsync(admin, PatientFields("Permission Patient", "Permission Father"));
        var id = await BookAsync(admin, pid, t);
        var tech = await UserWithAsync(admin, "perm.tech", TechPerms);
        var booker = await UserWithAsync(admin, "perm.booker", Perm.DashboardView, Perm.LabBook, Perm.LabRegisterPatient, Perm.PatientsSearch);
        var nobody = await UserWithAsync(admin, "perm.none", Perm.DashboardView);

        foreach (var url in new[] { "/Lab", "/Lab/Book", "/Lab/Pending", "/Lab/Verify", "/Lab/Results", "/Lab/Catalogue", "/Lab/Settings", "/Lab/Reports", $"/Lab/Order/{id}", $"/Print/LabReport/{id}" })
            Denied(await nobody.GetAsync(url), "No-permission user " + url);
        foreach (var url in new[] { "/Lab/Catalogue", "/Lab/Settings", "/Lab/Reports", "/Lab/Book", "/Lab/Verify", $"/Print/LabReport/{id}" })
            Denied(await tech.GetAsync(url), "Technician " + url);
        var perItem = await ItemAsync(id, "PER");
        Denied(await tech.PostFormAsync($"/Lab/Order/{id}", Array.Empty<KeyValuePair<string, string>>(), $"/Lab/Verify/{id}?handler=Verify&itemId={perItem.Id}"), "Technician verification");
        Denied(await tech.PostFormAsync($"/Lab/Order/{id}", new[] { P("Reason", "x") }, $"/Lab/Order/{id}?handler=CancelItem&itemId={(await ItemAsync(id, "PER")).Id}"), "Technician cancel");
        Denied(await booker.GetAsync($"/Lab/Entry/{id}"), "Booker result entry");

        // Fees are hidden without "View Laboratory Financial Information"; prices need "Manage Laboratory Fees".
        var book = await booker.GetHtmlAsync($"/Lab/Book?patientId={pid}");
        Assert.Contains("Permission Test", book);
        Assert.DoesNotContain("data-lab-total", book);
        Assert.DoesNotContain("data-test=\"lab-collect\"", await booker.GetHtmlAsync($"/Lab/Order/{id}"));
        var cataloguer = await UserWithAsync(admin, "perm.catalogue", Perm.DashboardView, Perm.LabManageCatalogue);
        int cat, smp;
        await using (var db = Factory.NewDb())
        {
            cat = await db.LabCategories.Select(c => c.Id).FirstAsync();
            smp = await db.LabSampleTypes.Select(s => s.Id).FirstAsync();
        }
        Assert.Equal(HttpStatusCode.Redirect, (await cataloguer.PostFormAsync("/Lab/Catalogue/Test", new[]
        {
            P("Input.Code", "NOFEE"), P("Input.Name", "Priced Without Permission"), P("Input.CategoryId", cat.ToString()), P("Input.SampleTypeId", smp.ToString()),
            P("Input.IsActive", "true"), P("Input.Price", "9999"),
        })).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var created = await db.LabTests.Include(x => x.ServiceItem).SingleAsync(x => x.Code == "NOFEE");
            Assert.Equal(0m, await db.ServiceFees.Where(f => f.ServiceItemId == created.ServiceItemId).OrderByDescending(f => f.Id).Select(f => (decimal?)f.Fee).FirstOrDefaultAsync() ?? 0m);
            Assert.Equal(0m, created.ServiceItem!.DefaultFee);
        }
        Denied(await cataloguer.PostFormAsync($"/Lab/Catalogue/Test/{t}", new[] { P("Range.Low", "1"), P("Range.High", "2") },
            $"/Lab/Catalogue/Test/{t}?handler=Range&parameterId=1"), "Ranges without permission");

        // Lab-only registration permission is enough to register a patient from the laboratory.
        var reg = await PostRegisterAsync(booker, PatientFields("Lab Registered Patient", "Lab Reg Father"));
        Assert.Equal(HttpStatusCode.Redirect, reg.StatusCode);

        // Booked orders cannot be deleted at database level.
        await using (var db = Factory.NewDb())
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync($"DELETE FROM \"LabOrders\" WHERE \"Id\" = {id}"));
    }

    [Fact]
    public async Task Lab_dashboard_counts_and_previous_phases_still_work()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var (t, _) = await LabTestAsync(admin, "DSH", "Dashboard Test", 0);
        var pid = await RegisterPatientAsync(admin, PatientFields("Dashboard Patient", "Dashboard Father"));
        await BookAsync(admin, pid, t);
        await BookAsync(admin, pid, t);
        var html = await admin.GetHtmlAsync("/Lab?period=today");
        Assert.Contains("data-kpi=\"tests\">2<", html);
        Assert.Contains("data-kpi=\"sample-pending\">2<", html);
        Assert.Contains("Laboratory", await admin.GetHtmlAsync("/"));

        // OPD registration (Phase 2) and the cashier (Phase 3) are untouched.
        var enc = await RegisterOpdAsync(admin, pid);
        Assert.True(enc > 0);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Finance/Collect")).StatusCode);
    }
}
