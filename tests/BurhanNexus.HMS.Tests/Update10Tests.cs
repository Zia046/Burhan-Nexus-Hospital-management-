using System.Globalization;
using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 10 – central Fees &amp; Services, simplified laboratory, inpatient ledger, ward / NICU billing, discharge clearance.</summary>
public partial class Update10Tests : Phase2TestBase
{
    private async Task<int> CashAsync()
    {
        await using var db = Factory.NewDb();
        return await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
    }

    private async Task<int> MainCounterAsync()
    {
        await using var db = Factory.NewDb();
        return await db.Counters.Where(c => c.Code == "MAIN").Select(c => c.Id).SingleAsync();
    }

    /// <summary>Collects one service for a patient at the cashier and returns the charge.</summary>
    private async Task<Charge> CollectServiceAsync(Browser b, long patientId, int serviceId)
    {
        var before = await LastChargeIdAsync();
        var r = await b.PostFormAsync($"/Finance/Collect?patientId={patientId}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.CounterId", (await MainCounterAsync()).ToString()), P("Input.PaymentMethodId", (await CashAsync()).ToString()),
            P("PatientId", patientId.ToString()), P("Lines[0].ServiceItemId", serviceId.ToString()),
        }, "/Finance/Collect");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Collect: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Charges.AsNoTracking().Where(c => c.Id > before && c.ServiceItemId == serviceId).OrderByDescending(c => c.Id).FirstAsync();
    }

    private async Task<long> LastChargeIdAsync()
    {
        await using var db = Factory.NewDb();
        return await db.Charges.MaxAsync(c => (long?)c.Id) ?? 0;
    }

    private static KeyValuePair<string, string>[] ServiceForm(string code, string name, int? category, string unit, decimal fee, decimal? gov, bool active = true) => new[]
    {
        P("Form.Code", code), P("Form.Name", name), P("Form.Kind", "Procedure"), P("Form.ServiceCategoryId", category?.ToString() ?? ""), P("Form.ChargeUnit", unit),
        P("Form.Applicability", "Both"), P("Form.Fee", fee.ToString(CultureInfo.InvariantCulture)), P("Form.GovernmentFee", gov?.ToString(CultureInfo.InvariantCulture) ?? ""),
        P("Form.IsFree", "false"), P("Form.IsActive", active ? "true" : "false"), P("Form.AllowPartialPayment", "false"),
    };

    // =========================================================================== 1A – Fees & Services

    [Fact]
    public async Task Central_fee_catalogue_categories_fee_history_government_rate_and_permissions()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();

        // Standard categories and services are present (fee 0 until the administrator sets it).
        await using (var db = Factory.NewDb())
        {
            var codes = await db.ServiceCategories.Select(c => c.Code).ToListAsync();
            foreach (var c in new[] { "OPD", "LAB", "IPD", "RAD", "ER", "OT", "PROC", "WARD", "NICU", "OTHER" }) Assert.Contains(c, codes);
            var inc = await db.ServiceItems.Include(s => s.ServiceCategory).SingleAsync(s => s.Code == "SVC-NICU-INC");
            Assert.Equal(("NICU", ChargeUnit.PerDay, 0m, true), (inc.ServiceCategory!.Code, inc.ChargeUnit, inc.DefaultFee, inc.IsActive));
            // Services of other modules are placed in their category (laboratory tests → Laboratory).
            Assert.True(await db.ServiceItems.Where(s => s.Kind == ServiceKind.Laboratory).AllAsync(s => s.ServiceCategory!.Code == "LAB"));
        }
        var list = await admin.GetHtmlAsync("/Fees?q=incubator");
        Assert.Contains("SVC-NICU-INC", list);
        Assert.DoesNotContain("SVC-PR-SUT", list);

        // 35. New category.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Fees/Categories", new[] { P("Form.Code", "physio"), P("Form.Name", "Physiotherapy"), P("Form.IsActive", "true") })).StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Fees/Categories", new[] { P("Form.Code", "PHYSIO2"), P("Form.Name", "physiotherapy"), P("Form.IsActive", "true") })).StatusCode);
        int cat;
        await using (var db = Factory.NewDb()) cat = await db.ServiceCategories.Where(c => c.Code == "PHYSIO").Select(c => c.Id).SingleAsync();

        // 36. New service with fee 500 (government rate 300), charged per session.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Fees/Service", ServiceForm("PHY-1", "Physiotherapy session", cat, "PerSession", 500, 300))).StatusCode);
        int svc;
        await using (var db = Factory.NewDb())
        {
            var s = await db.ServiceItems.SingleAsync(x => x.Code == "PHY-1");
            svc = s.Id;
            Assert.Equal((cat, ChargeUnit.PerSession), (s.ServiceCategoryId!.Value, s.ChargeUnit));
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Fee Catalogue Patient", "Father Fee"));
        var first = await CollectServiceAsync(admin, pid, svc);
        Assert.Equal(500m, first.Fee);

        // 37 / 38. Change the fee: new transactions use 700, the earlier bill keeps 500.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Fees/Service?id={svc}", ServiceForm("PHY-1", "Physiotherapy session", cat, "PerSession", 700, 300)
            .Append(P("Form.FeeNote", "New rates 2026")).ToArray())).StatusCode);
        var second = await CollectServiceAsync(admin, pid, svc);
        Assert.Equal(700m, second.Fee);
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(500m, (await db.Charges.SingleAsync(c => c.Id == first.Id)).Fee);
            Assert.Equal(2, await db.ServiceFees.CountAsync(f => f.ServiceItemId == svc));
        }
        Assert.Contains("New rates 2026", await admin.GetHtmlAsync($"/Fees/Service?id={svc}"));

        // 40. Every department reads the fee from the catalogue; in Government mode the government rate is used.
        using (var scope = Factory.Services.CreateScope())
        {
            var setup = scope.ServiceProvider.GetRequiredService<IFinanceSetupService>();
            var today = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5));
            Assert.Equal(700m, await setup.FeeOnAsync(svc, today));
            await using (var db = Factory.NewDb())
            {
                var cfg = await db.HospitalConfigurations.SingleAsync();
                cfg.HospitalType = HospitalType.Government;
                await db.SaveChangesAsync();
            }
            Assert.Equal(300m, await setup.FeeOnAsync(svc, today));
            await using (var db = Factory.NewDb())
            {
                var cfg = await db.HospitalConfigurations.SingleAsync();
                cfg.HospitalType = HospitalType.Private;
                await db.SaveChangesAsync();
            }
        }

        // 39. Deactivate: the service disappears from the active list, its bills stay.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Fees/Service?id={svc}", ServiceForm("PHY-1", "Physiotherapy session", cat, "PerSession", 700, 300, active: false))).StatusCode);
        Assert.DoesNotContain("PHY-1", await admin.GetHtmlAsync("/Fees"));
        Assert.Contains("PHY-1", await admin.GetHtmlAsync("/Fees?status=inactive"));
        Assert.Contains("data-has-history", await admin.GetHtmlAsync($"/Fees/Service?id={svc}"));
        await using (var db = Factory.NewDb())
        {
            Assert.False((await db.ServiceItems.SingleAsync(s => s.Id == svc)).IsActive);
            Assert.Equal(2, await db.Charges.CountAsync(c => c.ServiceItemId == svc));
        }

        // Permissions are enforced on the server: view-only cannot save; details-only cannot change the fee.
        var viewer = await UserWithAsync(admin, "feeviewer", Perm.DashboardView, Perm.SvcViewFees);
        Assert.Contains("SVC-NICU-INC", await viewer.GetHtmlAsync("/Fees"));
        var denied = await viewer.PostFormAsync("/Fees", ServiceForm("PHY-1", "Hacked", cat, "PerSession", 1, null), $"/Fees/Service?id={svc}");
        Assert.True(denied.StatusCode is HttpStatusCode.Forbidden || denied.Headers.Location?.ToString().Contains("AccessDenied") == true, $"viewer save: {(int)denied.StatusCode}");
        Assert.NotEqual(HttpStatusCode.OK, (await viewer.Http.GetAsync("/Fees/Categories")).StatusCode);
        var editor = await UserWithAsync(admin, "feeeditor", Perm.DashboardView, Perm.SvcManageServices);
        var noFee = await editor.PostFormAsync($"/Fees/Service?id={svc}", ServiceForm("PHY-1", "Physiotherapy (renamed)", cat, "PerSession", 1, null, active: true));
        Assert.Equal(HttpStatusCode.Redirect, noFee.StatusCode); // details saved, fee fields ignored
        await using (var db = Factory.NewDb())
        {
            var s = await db.ServiceItems.SingleAsync(x => x.Id == svc);
            Assert.Equal(("Physiotherapy (renamed)", 700m), (s.Name, s.DefaultFee));
        }
        Assert.Single(await AuditAsync(AuditActions.ServiceCategorySaved));
        Assert.Contains(await AuditAsync(AuditActions.ServiceFeeChanged), a => a.Description!.Contains("500.00 to 700.00"));
    }

    // =========================================================================== 1B – simplified laboratory

    [System.Text.RegularExpressions.GeneratedRegex(@"/Lab/Order/(\d+)")] private static partial System.Text.RegularExpressions.Regex LabOrderRegex();

    private async Task SetFeeAsync(Browser admin, string serviceCode, decimal fee)
    {
        await using var db = Factory.NewDb();
        var s = await db.ServiceItems.SingleAsync(x => x.Code == serviceCode);
        var r = await admin.PostFormAsync($"/Fees/Service?id={s.Id}", ServiceForm(s.Code, s.Name, s.ServiceCategoryId, s.ChargeUnit.ToString(), fee, null)
            .Select(kv => kv.Key == "Form.Kind" ? P("Form.Kind", s.Kind.ToString()) : kv).ToArray());
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Set fee: " + Errors(await r.Content.ReadAsStringAsync()));
    }

    private async Task<HttpResponseMessage> EntryAsync(Browser b, long orderId, long itemId, string handler, params (long ValueId, string Value)[] values)
    {
        uint rv;
        await using (var db = Factory.NewDb()) rv = (await db.LabOrderItems.SingleAsync(i => i.Id == itemId)).RowVersion;
        var fields = new List<KeyValuePair<string, string>> { P("RowVersion", rv.ToString()) };
        if (handler == "submit") fields.Add(P("Mode", "submit"));
        fields.AddRange(values.Select(v => P($"Values[{v.ValueId}]", v.Value)));
        if (handler.StartsWith("review:")) fields.Add(P("Reason", handler[7..]));
        var post = handler.StartsWith("review") ? $"/Lab/Entry/{orderId}?item={itemId}&handler=Review" : $"/Lab/Entry/{orderId}?item={itemId}";
        return await b.PostFormAsync($"/Lab/Order/{orderId}", fields, post);
    }

    [Fact]
    public async Task Lab_visit_is_paid_at_booking_receipt_has_two_copies_saving_verifies_and_review_keeps_history()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        await SetFeeAsync(admin, "LT-FBS", 300);
        await SetFeeAsync(admin, "LT-HBSAG", 500);
        int fbs, hbs;
        await using (var db = Factory.NewDb())
        {
            fbs = await db.LabTests.Where(t => t.Code == "FBS").Select(t => t.Id).SingleAsync();
            hbs = await db.LabTests.Where(t => t.Code == "HBSAG").Select(t => t.Id).SingleAsync();
            Assert.True((await db.LabSettings.SingleAsync()).SimpleWorkflow);
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Simple Lab Patient", "Father Lab"));

        // 1–5. New lab visit: fees come from the catalogue, the payment is recorded with the booking.
        var book = await admin.GetHtmlAsync($"/Lab/Book?patientId={pid}");
        Assert.Contains("data-lab-payment", book);
        var key = Guid.NewGuid().ToString();
        KeyValuePair<string, string>[] Booking(string k, string? amount = null) => new[]
        {
            P("Input.IdempotencyKey", k), P("PatientId", pid.ToString()), P("TestIds", fbs.ToString()), P("TestIds", hbs.ToString()), P("Input.ReferralType", "None"),
            P("Input.CollectPayment", "true"), P("Input.PaymentMethodId", "") , P("Input.AmountReceived", amount ?? ""),
        };
        var cash = (await CashAsync()).ToString();
        var fields = Booking(key).Select(kv => kv.Key == "Input.PaymentMethodId" ? P(kv.Key, cash) : kv).ToArray();
        var r = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", fields);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Lab booking: " + Errors(await r.Content.ReadAsStringAsync()));
        var orderId = long.Parse(LabOrderRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Book?patientId={pid}", fields)).StatusCode); // double submit
        LabOrder order;
        await using (var db = Factory.NewDb())
        {
            order = await db.LabOrders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Values).AsSplitQuery().SingleAsync(o => o.Id == orderId);
            Assert.Equal(1, await db.LabOrders.CountAsync());
            Assert.NotNull(order.BookingReceiptId);
            var charges = await db.Charges.Where(c => c.LabOrderId == orderId).OrderBy(c => c.Fee).ToListAsync();
            Assert.Equal(new[] { 300m, 500m }, charges.Select(c => c.Fee));
            Assert.All(charges, c => Assert.Equal(ChargeStatus.Paid, c.Status));
            var rec = await db.Receipts.SingleAsync();
            Assert.Equal((800m, order.BookingReceiptId!.Value), (rec.AmountPaid, rec.Id));
            Assert.All(order.Items, i => Assert.Equal(LabOrderItemStatus.SampleReceived, i.Status)); // no sample step
        }

        // 6–7. Two copies with the same receipt and visit numbers; reprinting creates nothing.
        var slip = await admin.GetHtmlAsync($"/Print/LabReceipt/{orderId}");
        Assert.Contains("PATIENT COPY", slip);
        Assert.Contains("LABORATORY COPY", slip);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(slip, "data-copy=").Count);
        string receiptNo;
        await using (var db = Factory.NewDb()) receiptNo = (await db.Receipts.SingleAsync()).ReceiptNumber;
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(slip, $"data-receipt=\"{receiptNo}\" data-visit=\"{order.OrderNumber}\"").Count);
        Assert.Contains("PAID", slip);
        await admin.GetHtmlAsync($"/Print/LabReceipt/{orderId}");
        await using (var db = Factory.NewDb()) Assert.Equal((1, 1), (await db.Receipts.CountAsync(), await db.LabOrders.CountAsync()));

        // 8. No sample collection or verification controls; the visit is in Pending Lab Results (searchable by visit number).
        var visit = await admin.GetHtmlAsync($"/Lab/Order/{orderId}");
        Assert.DoesNotContain("data-samples", visit);
        Assert.DoesNotContain("/Lab/Verify/", visit);
        Assert.Contains(order.OrderNumber, await admin.GetHtmlAsync($"/Lab/Pending?q={order.OrderNumber}"));
        Assert.DoesNotContain("Sample Collection", await admin.GetHtmlAsync("/"));

        // 9–12. Enter results; Save Report verifies automatically (no verify step). Incomplete results are refused.
        var fbsItem = order.Items.Single(i => i.TestCode == "FBS");
        var hbsItem = order.Items.Single(i => i.TestCode == "HBSAG");
        var glu = fbsItem.Values.Single();
        var incomplete = await EntryAsync(admin, orderId, fbsItem.Id, "submit", (glu.Id, ""));
        Assert.NotEqual(HttpStatusCode.Redirect, incomplete.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await EntryAsync(admin, orderId, fbsItem.Id, "submit", (glu.Id, "95"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await EntryAsync(admin, orderId, hbsItem.Id, "submit", (hbsItem.Values.Single().Id, "Non-Reactive"))).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var items = await db.LabOrderItems.Where(i => i.LabOrderId == orderId).ToListAsync();
            Assert.All(items, i => Assert.Equal((LabOrderItemStatus.Verified, "admin"), (i.Status, i.VerifiedByUsername)));
            Assert.All(items, i => Assert.Equal(i.ResultEnteredByUsername, i.VerifiedByUsername));
        }
        var entry = await admin.GetHtmlAsync($"/Lab/Entry/{orderId}?item={fbsItem.Id}");
        Assert.DoesNotContain("/Lab/Verify", entry);
        Assert.Contains("data-test=\"review-report\"", entry);

        // 13. Another user with print permission prints the report; without the review permission they cannot edit.
        var printer = await UserWithAsync(admin, "labprinter", Perm.DashboardView, Perm.LabViewResults, Perm.LabPrintReports);
        var report = await printer.GetHtmlAsync($"/Print/LabReport/{orderId}");
        Assert.Contains("data-report-test=\"FBS\"", report);
        Assert.Contains("95", report);
        Assert.NotEqual(HttpStatusCode.OK, (await printer.Http.GetAsync($"/Lab/Entry/{orderId}?item={fbsItem.Id}")).StatusCode);
        var denied = await printer.PostFormAsync($"/Print/LabReport/{orderId}", new[] { P("Values[" + glu.Id + "]", "1"), P("Reason", "x"), P("RowVersion", "0") }, $"/Lab/Entry/{orderId}?item={fbsItem.Id}&handler=Review");
        Assert.True(denied.StatusCode is HttpStatusCode.Forbidden || denied.Headers.Location?.ToString().Contains("AccessDenied") == true, $"printer review returned {(int)denied.StatusCode}");
        // 17. Users with only "Enter Test Results" cannot change a saved report either.
        var tech = await UserWithAsync(admin, "labtech", Perm.DashboardView, Perm.LabEnterResults);
        var techReview = await EntryAsync(tech, orderId, fbsItem.Id, "review:wrong", (glu.Id, "1"));
        Assert.True(techReview.StatusCode is HttpStatusCode.Forbidden || techReview.Headers.Location?.ToString().Contains("AccessDenied") == true, $"tech review returned {(int)techReview.StatusCode}");
        var techEdit = await EntryAsync(tech, orderId, fbsItem.Id, "submit", (glu.Id, "1"));
        Assert.NotEqual(HttpStatusCode.Redirect, techEdit.StatusCode);
        await using (var db = Factory.NewDb()) Assert.Equal("95", (await db.LabResultValues.SingleAsync(v => v.Id == glu.Id)).Value);

        // 14–16. Review report: reason required; the corrected result stays VERIFIED, the original is kept in history.
        Assert.NotEqual(HttpStatusCode.Redirect, (await EntryAsync(admin, orderId, fbsItem.Id, "review", (glu.Id, "110"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await EntryAsync(admin, orderId, fbsItem.Id, "review:Typing error", (glu.Id, "110"))).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var item = await db.LabOrderItems.Include(i => i.Values).SingleAsync(i => i.Id == fbsItem.Id);
            Assert.Equal((LabOrderItemStatus.Verified, 1, "110"), (item.Status, item.RevisionNo, item.Values.Single().Value));
            var rev = await db.LabResultRevisions.SingleAsync(x => x.OrderItemId == fbsItem.Id);
            Assert.Contains("95", rev.Snapshot);
            Assert.Contains("Typing error", rev.Reason);
            var other = await db.LabOrderItems.SingleAsync(i => i.Id == hbsItem.Id); // the unrelated test is not reopened
            Assert.Equal((LabOrderItemStatus.Verified, 0), (other.Status, other.RevisionNo));
        }
        var amended = await printer.GetHtmlAsync($"/Print/LabReport/{orderId}");
        Assert.Contains("data-amended", amended);
        Assert.Contains("110", amended);
        var audit = await AuditAsync(AuditActions.LabResultReviewed);
        Assert.Single(audit);
        Assert.Contains("95", audit[0].OldValues);
        Assert.Contains("110", audit[0].NewValues);

        // A payment that cannot be recorded (partial payment of tests that do not allow it) books nothing.
        var partial = Booking(Guid.NewGuid().ToString(), "100").Select(kv => kv.Key == "Input.PaymentMethodId" ? P(kv.Key, cash) : kv).ToArray();
        var refused = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", partial);
        Assert.NotEqual(HttpStatusCode.Redirect, refused.StatusCode);
        await using (var db = Factory.NewDb()) Assert.Equal((1, 1), (await db.LabOrders.CountAsync(), await db.Receipts.CountAsync()));
    }
}
