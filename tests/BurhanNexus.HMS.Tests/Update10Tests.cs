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

        // The "Save corrected report" button must post to this test (regression: the item was lost from the form action).
        var reviewPage = await admin.GetHtmlAsync($"/Lab/Entry/{orderId}?item={fbsItem.Id}&review=true");
        var button = System.Text.RegularExpressions.Regex.Match(reviewPage, "<button[^>]*data-test=\"review-save\"[^>]*>").Value;
        var action = System.Text.RegularExpressions.Regex.Match(button, "formaction=\"([^\"]*)\"").Groups[1].Value.Replace("&amp;", "&");
        Assert.Contains("handler=Review", action);
        Assert.Contains($"item={fbsItem.Id}", action);
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

    // =========================================================================== 1C–1F – inpatient account, admission fee, ward / NICU billing, clearance

    [System.Text.RegularExpressions.GeneratedRegex(@"/Ipd/Admission/(\d+)")] private static partial System.Text.RegularExpressions.Regex AdmissionRegex();

    private async Task<int> SvcAsync(Browser admin, string code, decimal fee)
    {
        await SetFeeAsync(admin, code, fee);
        await using var db = Factory.NewDb();
        return await db.ServiceItems.Where(s => s.Code == code).Select(s => s.Id).SingleAsync();
    }

    private async Task<int> BedAsync(Browser admin, string wardCode, string code)
    {
        int room;
        await using (var db = Factory.NewDb()) room = await db.IpdRooms.Where(r => r.Ward!.Code == wardCode).Select(r => r.Id).FirstAsync();
        var r = await admin.PostFormAsync("/Ipd/Wards", new[] { P("Bed.RoomId", room.ToString()), P("Bed.Code", code), P("Bed.Type", "Standard"), P("Bed.IsActive", "true") }, "/Ipd/Wards?handler=Bed");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Bed: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db2 = Factory.NewDb();
        return await db2.IpdBeds.Where(b => b.Code == code).Select(b => b.Id).SingleAsync();
    }

    private async Task<long> AdmitAsync(Browser admin, long pid, int? bed, params (string, string)[] extra)
    {
        var f = new List<KeyValuePair<string, string>>
        {
            P("PatientId", pid.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"), P("Input.SourceEncounterId", ""),
            P("Input.BedId", bed?.ToString() ?? ""), P("Input.AdmittingDoctorName", "Dr. Ward"), P("Input.AdmissionDiagnosis", "Pneumonia"), P("Input.AttendantName", "Brother"), P("Input.AttendantPhone", "03001234567"),
        };
        f.AddRange(extra.Select(e => P(e.Item1, e.Item2)));
        var r = await admin.PostFormAsync($"/Ipd/Admit?patientId={pid}", f);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Admit: " + Errors(await r.Content.ReadAsStringAsync()));
        return long.Parse(AdmissionRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);
    }

    private static readonly (string, string)[] DischargeFields =
    {
        ("Discharge.Type", "Routine"), ("Discharge.FinalDiagnosis", "Pneumonia, treated"), ("Discharge.ConditionAtDischarge", "Stable"),
        ("Discharge.TreatmentSummary", "Antibiotics."), ("Discharge.FollowUp", "OPD after one week"), ("Discharge.DischargeDoctor", "Dr. Ward"),
    };

    private static Task<HttpResponseMessage> ActAsync(Browser b, long admissionId, string handler, params (string, string)[] fields) =>
        b.PostFormAsync($"/Ipd/Admission/{admissionId}", fields.Select(f => P(f.Item1, f.Item2)), $"/Ipd/Admission/{admissionId}?handler={handler}");

    private async Task<IpdLedgerTotals> TotalsAsync(long adm)
    {
        await using var db = Factory.NewDb();
        var c = await db.Charges.Where(x => x.IpdAdmissionId == adm && x.Status != ChargeStatus.Cancelled).ToListAsync();
        var advance = await db.PatientAdvances.Where(x => x.AdmissionId == adm).SumAsync(x => (decimal?)(x.Amount - x.AppliedAmount - x.RefundedAmount)) ?? 0;
        return new IpdLedgerTotals(c.Sum(x => x.NetAmount), c.Sum(x => x.PaidAmount), advance);
    }

    private sealed record IpdLedgerTotals(decimal Net, decimal Paid, decimal Advance)
    {
        public decimal Due => Net - Paid - Advance;
    }

    [Fact]
    public async Task Bed_days_follow_the_policy_and_never_overlap_after_a_transfer()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "PKT", "PKT");
        DateTime L(int day, int hour) => new DateTime(2026, 11, day, hour, 0, 0, DateTimeKind.Utc).AddHours(-5); // local → UTC
        // Ward 1 from 10 Nov 10:00 to 11 Nov 18:00, then ward 2 until 12 Nov 13:00 (discharge).
        var periods = new List<(DateTime, DateTime?, int)> { (L(10, 10), L(11, 18), 1), (L(11, 18), L(12, 13), 2) };
        var now = L(12, 13);
        var cutoff = Infrastructure.Ipd.IpdService.BedDays(periods, BedChargePolicy.CutoffDay, new TimeOnly(12, 0), 6, now, true, zone);
        // billing days start at 12:00: 9 Nov (10–12), 10 Nov, 11 Nov (ward 2 used 19 h vs ward 1 6 h), 12 Nov (12–13)
        Assert.Equal(new[] { (new DateOnly(2026, 11, 9), 1, 1m), (new DateOnly(2026, 11, 10), 1, 1m), (new DateOnly(2026, 11, 11), 2, 1m), (new DateOnly(2026, 11, 12), 2, 1m) }, cutoff);
        Assert.Equal(cutoff.Count, cutoff.Select(d => d.Day).Distinct().Count()); // one charge per day – no overlap
        var half = Infrastructure.Ipd.IpdService.BedDays(periods, BedChargePolicy.HalfDay, new TimeOnly(12, 0), 6, now, true, zone);
        Assert.Equal(new[] { 0.5m, 1m, 1m, 0.5m }, half.Select(d => d.Quantity));
        var hourly = Infrastructure.Ipd.IpdService.BedDays(periods, BedChargePolicy.Hourly, new TimeOnly(12, 0), 6, now, true, zone);
        Assert.Equal(51m, hourly.Sum(d => d.Quantity)); // 10 Nov 10:00 → 12 Nov 13:00 = 51 hours
        // Before discharge only completed billing days are charged.
        var running = Infrastructure.Ipd.IpdService.BedDays(periods, BedChargePolicy.CutoffDay, new TimeOnly(12, 0), 6, now, false, zone);
        Assert.Equal(3, running.Count);
        // Equipment quantities: per hour (started hours), per day (started days, at least one).
        Assert.Equal(5m, Infrastructure.Ipd.IpdBillingService.UsageQuantity(ChargeUnit.PerHour, L(10, 10), L(10, 14).AddMinutes(30)));
        Assert.Equal(1m, Infrastructure.Ipd.IpdBillingService.UsageQuantity(ChargeUnit.PerDay, L(10, 10), L(10, 11)));
        Assert.Equal(2m, Infrastructure.Ipd.IpdBillingService.UsageQuantity(ChargeUnit.PerDay, L(10, 10), L(11, 11)));
    }

    [Fact]
    public async Task Inpatient_account_collects_every_department_and_discharge_needs_financial_clearance()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var admFee = await SvcAsync(admin, "SVC-IPD-ADM", 1000);
        var bedSvc = await SvcAsync(admin, "SVC-IPD-GEN", 500);
        var incSvc = await SvcAsync(admin, "SVC-NICU-INC", 800);
        await SetFeeAsync(admin, "LT-FBS", 300);
        await using (var db = Factory.NewDb())
        {
            var s = await db.IpdSettings.SingleAsync();
            Assert.True(s.RequireBillClearanceForDischarge); // update 10 default
            s.AdmissionServiceItemId = admFee;
            s.BedChargePolicy = BedChargePolicy.CutoffDay;
            var w = await db.IpdWards.SingleAsync(x => x.Code == "MGW");
            w.BedChargeServiceItemId = bedSvc;
            // radiology starter service (inactive, fee 0) – activate it with a fee for this test
            var rad = await db.RadiologyServices.Include(r => r.ServiceItem).OrderBy(r => r.Id).FirstAsync();
            rad.IsActive = true; rad.ServiceItem!.IsActive = true;
            db.ServiceFees.Add(new ServiceFee { ServiceItemId = rad.ServiceItemId, Fee = 700, EffectiveFrom = DateOnly.MinValue, CreatedUtc = DateTime.UtcNow, CreatedByUsername = "test" });
            await db.SaveChangesAsync();
        }
        var bed1 = await BedAsync(admin, "MGW", "MGW-A1");
        var bed2 = await BedAsync(admin, "MGW", "MGW-A2");
        var pid = await RegisterPatientAsync(admin, PatientFields("Account Patient", "Father Account"));
        var cash = (await CashAsync()).ToString();

        // 18–19. The configured admission fee is shown and received with the admission.
        Assert.Contains("data-admission-fee=\"1000", await admin.GetHtmlAsync($"/Ipd/Admit?patientId={pid}"));
        var adm = await AdmitAsync(admin, pid, bed1, ("FeeAction", "pay"), ("FeeMethodId", cash));
        await using (var db = Factory.NewDb())
        {
            var a = await db.IpdAdmissions.SingleAsync(x => x.Id == adm);
            var fee = await db.Charges.SingleAsync(c => c.Id == a.AdmissionFeeChargeId);
            Assert.Equal((1000m, 1000m, ChargeStatus.Paid), (fee.Fee, fee.PaidAmount, fee.Status));
        }
        // 20. Update 14: ONE copy of the admission form (no patient copy, no signature fields); reprinting creates nothing.
        var form = await admin.GetHtmlAsync($"/Print/Ipd/admission/{adm}");
        Assert.DoesNotContain("PATIENT / ATTENDANT COPY", form);
        Assert.DoesNotContain("HOSPITAL / ADMISSION OFFICE COPY", form);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(form, "data-form-page"));
        Assert.DoesNotContain("data-sign-row", form);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(form, "data-fee-status><strong>PAID"));
        int chargesBefore;
        await using (var db = Factory.NewDb()) chargesBefore = await db.Charges.CountAsync();
        await admin.GetHtmlAsync($"/Print/Ipd/admission/{adm}");
        await using (var db = Factory.NewDb()) Assert.Equal(chargesBefore, await db.Charges.CountAsync());

        // 21 / 43. Pharmacy issue to the admission → on the account, once (repeat issue does not bill twice).
        int med;
        await using (var db = Factory.NewDb())
        {
            var form2 = await db.PharmacyLookups.Where(l => l.Kind == PharmacyLookupKind.DosageForm && l.Name == "Tablet").Select(l => l.Id).SingleAsync();
            _ = form2;
        }
        var medR = await admin.PostFormAsync("/Pharmacy/Medicines/Edit", new[]
        {
            P("Input.Name", "Amoxil Ten"), P("Input.GenericName", "Amoxicillin"), P("Input.Strength", "500 mg"),
            P("Input.DosageFormId", (await DosageFormAsync()).ToString()), P("Input.Unit", "Capsule"), P("Input.UnitsPerPack", "10"), P("Input.MinimumStock", "0"),
            P("Input.IsActive", "true"), P("Input.SalePrice", "20"), P("Input.PurchasePrice", "10"),
        });
        Assert.True(medR.StatusCode == HttpStatusCode.Redirect, "Medicine: " + Errors(await medR.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb()) med = await db.Medicines.Where(m => m.Name == "Amoxil Ten").Select(m => m.Id).SingleAsync();
        var today = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Receive", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SourceName", "Opening stock"), P("Lines[0].MedicineId", med.ToString()), P("Lines[0].BatchNumber", "AMX-1"),
            P("Lines[0].Expiry", today.AddMonths(18).ToString("yyyy-MM")), P("Lines[0].Quantity", "100"), P("Lines[0].UnitCost", "10"),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Medicines/{adm}", new[]
            { P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Priority", "Routine"), P("Input.Lines[0].MedicineId", med.ToString()), P("Input.Lines[0].Quantity", "6") })).StatusCode);
        long reqId, lineId;
        await using (var db = Factory.NewDb()) { var rq = await db.MedicineRequests.Include(x => x.Lines).SingleAsync(); reqId = rq.Id; lineId = rq.Lines.Single().Id; }
        var issue = new[] { P("IdempotencyKey", Guid.NewGuid().ToString()), P($"Issue[{lineId}]", "6") };
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Pharmacy/WardRequest/{reqId}", issue, $"/Pharmacy/WardRequest/{reqId}?handler=Issue")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Pharmacy/WardRequest/{reqId}", issue, $"/Pharmacy/WardRequest/{reqId}?handler=Issue")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var ph = await db.Charges.SingleAsync(c => c.IpdAdmissionId == adm && c.PharmacyIssueId != null);
            Assert.Equal((6m, 20m, 120m), (ph.Quantity, ph.UnitPrice!.Value, ph.Fee));
        }

        // 22. Laboratory for the admitted patient: on the same account, no payment at the lab counter.
        int fbs;
        await using (var db = Factory.NewDb()) fbs = await db.LabTests.Where(t => t.Code == "FBS").Select(t => t.Id).SingleAsync();
        Assert.DoesNotContain("data-lab-payment", await admin.GetHtmlAsync($"/Lab/Book?patientId={pid}&admissionId={adm}"));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Book?patientId={pid}&admissionId={adm}", new[]
            { P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("PatientId", pid.ToString()), P("AdmissionId", adm.ToString()), P("TestIds", fbs.ToString()), P("Input.ReferralType", "None") })).StatusCode);
        // 23. Radiology for the admitted patient.
        int radSvc;
        await using (var db = Factory.NewDb()) radSvc = await db.RadiologyServices.Where(r => r.IsActive).Select(r => r.Id).FirstAsync();
        var radR = await admin.PostFormAsync($"/Radiology/Book?patientId={pid}&admissionId={adm}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("PatientId", pid.ToString()), P("AdmissionId", adm.ToString()), P("VisitChoice", ""), P("Input.ReferralType", "External"),
            P("Input.ExternalDoctorName", "Dr. Ward"), P("Input.ClinicalIndication", "Cough"), P("Input.Priority", "Routine"), P("ServiceIds", radSvc.ToString()),
        });
        Assert.True(radR.StatusCode == HttpStatusCode.Redirect, "Radiology: " + Errors(await radR.Content.ReadAsStringAsync()));

        // 25 / 45. NICU equipment: started, a second period of the same equipment is refused while running, stopped once, charged by usage.
        int inc;
        await using (var db = Factory.NewDb()) inc = await db.NicuEquipment.Where(e => e.Code == "SVC-NICU-INC").Select(e => e.Id).SingleAsync();
        var started = await admin.PostFormAsync($"/Ipd/Equipment/{adm}", new[]
            { P("Start.IdempotencyKey", Guid.NewGuid().ToString()), P("Start.EquipmentId", inc.ToString()), P("Start.Identifier", "INC-01") }, $"/Ipd/Equipment/{adm}?handler=Start");
        Assert.True(started.StatusCode == HttpStatusCode.Redirect, "Equipment start: " + Errors(await started.Content.ReadAsStringAsync()));
        var twice = await admin.PostFormAsync($"/Ipd/Equipment/{adm}", new[]
            { P("Start.IdempotencyKey", Guid.NewGuid().ToString()), P("Start.EquipmentId", inc.ToString()), P("Start.Identifier", "INC-02") }, $"/Ipd/Equipment/{adm}?handler=Start");
        Assert.Contains("already running", Errors(await twice.Content.ReadAsStringAsync()));
        long usage;
        await using (var db = Factory.NewDb()) usage = await db.EquipmentUsages.Where(u => u.AdmissionId == adm).Select(u => u.Id).SingleAsync();
        // 26. Replace: the first period ends, a new one starts – two separate periods, no overlap.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Equipment/{adm}", new[] { P("ReplaceKey", Guid.NewGuid().ToString()), P("NewIdentifier", "INC-03"), P("Reason", "Heater fault") },
            $"/Ipd/Equipment/{adm}?handler=Replace&usageId={usage}")).StatusCode);
        var stopAgain = await admin.PostFormAsync($"/Ipd/Equipment/{adm}", new[] { P("Reason", "again") }, $"/Ipd/Equipment/{adm}?handler=Stop&usageId={usage}");
        Assert.Contains("not charged twice", Errors(await stopAgain.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb())
        {
            var periods = await db.EquipmentUsages.Where(u => u.AdmissionId == adm).OrderBy(u => u.Id).ToListAsync();
            Assert.Equal(2, periods.Count);
            Assert.Equal((EquipmentUsageStatus.Ended, 1m), (periods[0].Status, periods[0].Quantity!.Value)); // a started day
            Assert.True(periods[1].StartUtc >= periods[0].EndUtc);
            var c = await db.Charges.SingleAsync(x => x.Id == periods[0].ChargeId);
            Assert.Equal((800m, 800m), (c.Fee, c.NetAmount));
        }

        // 27. Package including the incubator: the second incubator period is not billed again.
        var pkgSvc = await SvcAsync(admin, "SVC-NICU-OTH", 2500);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Ipd/Packages", new[]
            { P("Form.Code", "INC-PKG"), P("Form.Name", "Incubator package"), P("Form.ServiceItemId", pkgSvc.ToString()), P("Form.IncludedServiceIds", incSvc.ToString()), P("Form.IsActive", "true"), P("Form.Excluded", "Medicines, tests") })).StatusCode);
        int pkg;
        await using (var db = Factory.NewDb()) pkg = await db.ServicePackages.Select(x => x.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Account/{adm}", new[] { P("PackageId", pkg.ToString()) }, $"/Ipd/Account/{adm}?handler=Package")).StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Account/{adm}", new[] { P("PackageId", pkg.ToString()) }, $"/Ipd/Account/{adm}?handler=Package")).StatusCode);

        // 24 / 44. Daily ward charges two days later – posting again adds nothing.
        Factory.Clock.Advance(TimeSpan.FromDays(2));
        admin = await LoginAdminAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await ActAsync(admin, adm, "BedCharges")).StatusCode);
        int bedDays;
        await using (var db = Factory.NewDb()) bedDays = await db.IpdBedCharges.CountAsync(b => b.AdmissionId == adm);
        Assert.True(bedDays >= 2, $"bed days {bedDays}");
        Assert.Equal(HttpStatusCode.Redirect, (await ActAsync(admin, adm, "BedCharges")).StatusCode);
        await using (var db = Factory.NewDb()) Assert.Equal(bedDays, await db.IpdBedCharges.CountAsync(b => b.AdmissionId == adm));
        // Transfer to another bed: still one bed charge per billing day.
        Assert.Equal(HttpStatusCode.Redirect, (await ActAsync(admin, adm, "Transfer", ("BedId", bed2.ToString()), ("Reason", "Closer to the nurses"), ("Receiving", "Nurse Ayesha"))).StatusCode);

        // 28–30. Financial clearance: complete bill by department, outstanding shown.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Clearance/{adm}", Array.Empty<KeyValuePair<string, string>>(), $"/Ipd/Clearance/{adm}?handler=Finalise")).StatusCode);
        var clearance = await admin.GetHtmlAsync($"/Ipd/Clearance/{adm}");
        foreach (var g in new[] { "AdmissionFee", "WardBed", "Pharmacy", "Laboratory", "Radiology", "NicuEquipment", "Package" }) Assert.Contains($"data-row=\"{g}\"", clearance);
        Assert.Contains("data-outstanding-warning", clearance);
        await using (var db = Factory.NewDb())
        {
            var inc2 = await db.EquipmentUsages.Where(u => u.AdmissionId == adm).OrderBy(u => u.Id).Skip(1).SingleAsync();
            Assert.Equal(EquipmentUsageStatus.Ended, inc2.Status); // final bill stopped the running incubator
            var c2 = await db.Charges.SingleAsync(c => c.Id == inc2.ChargeId);
            Assert.Equal(0m, c2.NetAmount);
            Assert.Contains("Included in package", c2.DiscountReason);
            Assert.Contains(await db.Charges.Where(c => c.IpdAdmissionId == adm).Select(c => c.RadiologyOrderId).ToListAsync(), x => x != null);
            Assert.Contains(await db.Charges.Where(c => c.IpdAdmissionId == adm).Select(c => c.LabOrderId).ToListAsync(), x => x != null);
        }
        var t0 = await TotalsAsync(adm);
        Assert.True(t0.Due > 1000, $"due {t0.Due}");

        // Discharge is refused while a balance remains.
        Assert.Equal(HttpStatusCode.Redirect, (await ActAsync(admin, adm, "RequestDischarge", DischargeFields)).StatusCode);
        var blocked = await ActAsync(admin, adm, "Discharge", DischargeFields);
        Assert.Contains("outstanding balance", Errors(await blocked.Content.ReadAsStringAsync()));

        // Advance deposit 500, adjusted against the bill (not counted twice as cash).
        var advKey = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Account/{adm}", new[] { P("IdempotencyKey", advKey), P("Amount", "500"), P("PaymentMethodId", cash) }, $"/Ipd/Account/{adm}?handler=Advance")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Account/{adm}", new[] { P("IdempotencyKey", advKey), P("Amount", "500"), P("PaymentMethodId", cash) }, $"/Ipd/Account/{adm}?handler=Advance")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Account/{adm}", new[] { P("IdempotencyKey", Guid.NewGuid().ToString()) }, $"/Ipd/Account/{adm}?handler=ApplyAdvance")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var adv = await db.PatientAdvances.SingleAsync();
            Assert.Equal((500m, 500m), (adv.Amount, adv.AppliedAmount));
            var applyReceipt = await db.PatientAdvanceEntries.Where(x => x.AdvanceId == adv.Id && x.ReceiptId != null).Select(x => x.ReceiptId!.Value).SingleAsync();
            var received = await db.CashierLedger.Where(e => e.AdvanceId == adv.Id).SingleAsync();
            Assert.Equal((500m, true), (received.Amount, received.AffectsCashInHand));
            Assert.False(await db.CashierLedger.Where(e => e.ReceiptId == applyReceipt).Select(e => e.AffectsCashInHand).SingleAsync()); // the adjustment is not cash again
        }
        var t1 = await TotalsAsync(adm);
        Assert.Equal(t0.Due - 500, t1.Due);

        // 31 / 41. Partial payment (double submit = one receipt), then the rest: balance zero.
        var payKey = Guid.NewGuid().ToString();
        var receipts0 = 0;
        await using (var db = Factory.NewDb()) receipts0 = await db.Receipts.CountAsync();
        var part = new[] { P("IdempotencyKey", payKey), P("Amount", "300"), P("PaymentMethodId", cash) };
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Clearance/{adm}", part, $"/Ipd/Clearance/{adm}?handler=Pay")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Clearance/{adm}", part, $"/Ipd/Clearance/{adm}?handler=Pay")).StatusCode);
        await using (var db = Factory.NewDb()) Assert.Equal(receipts0 + 1, await db.Receipts.CountAsync());
        var t2 = await TotalsAsync(adm);
        Assert.Equal(t1.Due - 300, t2.Due);
        var over = await admin.PostFormAsync($"/Ipd/Clearance/{adm}", new[] { P("IdempotencyKey", Guid.NewGuid().ToString()), P("Amount", (t2.Due + 1).ToString(CultureInfo.InvariantCulture)), P("PaymentMethodId", cash) }, $"/Ipd/Clearance/{adm}?handler=Pay");
        Assert.NotEqual(HttpStatusCode.Redirect, over.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Clearance/{adm}", new[] { P("IdempotencyKey", Guid.NewGuid().ToString()), P("Amount", t2.Due.ToString(CultureInfo.InvariantCulture)), P("PaymentMethodId", cash) }, $"/Ipd/Clearance/{adm}?handler=Pay")).StatusCode);
        Assert.Equal(0m, (await TotalsAsync(adm)).Due);
        Assert.Contains("data-cleared", await admin.GetHtmlAsync($"/Ipd/Clearance/{adm}"));

        // 32–33. Discharge with zero balance; the discharge slip has no outstanding-payment warning.
        Assert.Equal(HttpStatusCode.Redirect, (await ActAsync(admin, adm, "Discharge", DischargeFields)).StatusCode);
        var slip = await admin.GetHtmlAsync($"/Print/Ipd/discharge/{adm}");
        Assert.DoesNotContain("utstanding", slip);

        // 34. Second admission: override needs the permission and a reason, and is audited.
        var pid2 = await RegisterPatientAsync(admin, PatientFields("Override Patient", "Father Override"));
        var adm2 = await AdmitAsync(admin, pid2, null);
        Assert.Equal(HttpStatusCode.Redirect, (await ActAsync(admin, adm2, "RequestDischarge", DischargeFields)).StatusCode);
        Assert.Contains("outstanding balance", Errors(await (await ActAsync(admin, adm2, "Discharge", DischargeFields)).Content.ReadAsStringAsync()));
        var cashier = await UserWithAsync(admin, "ipdcashier", Perm.DashboardView, Perm.IpdViewPatients, Perm.IpdViewAllWards, Perm.FinanceViewClearance, Perm.FinanceReceiveInpatientPayments);
        var noPerm = await cashier.PostFormAsync($"/Ipd/Clearance/{adm2}", new[] { P("Reason", "Poor family") }, $"/Ipd/Clearance/{adm2}?handler=Override");
        Assert.True(noPerm.StatusCode is HttpStatusCode.Forbidden || noPerm.Headers.Location?.ToString().Contains("AccessDenied") == true, $"override without permission: {(int)noPerm.StatusCode}");
        Assert.NotEqual(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Clearance/{adm2}", new[] { P("Reason", "") }, $"/Ipd/Clearance/{adm2}?handler=Override")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Ipd/Clearance/{adm2}", new[] { P("Reason", "Poor family – managing director approved") }, $"/Ipd/Clearance/{adm2}?handler=Override")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ActAsync(admin, adm2, "Discharge", DischargeFields)).StatusCode);
        var audit = await AuditAsync(AuditActions.IpdClearanceOverride);
        Assert.Single(audit);
        Assert.Contains("managing director", audit[0].Description);

        // 46. Department-wise and cashier-wise collections come from the same receipts and agree.
        var rev = await admin.GetHtmlAsync("/Finance/Revenue");
        Assert.Contains("data-reconcile=\"ok\"", rev);
        Assert.Contains("Admission fees", rev);
        Assert.Contains("NICU equipment", rev);

        // 47. Without the financial permissions the account, clearance and reports are refused.
        var nurse = await UserWithAsync(admin, "ipdnurse10", Perm.DashboardView, Perm.IpdViewPatients, Perm.IpdViewAllWards);
        foreach (var url in new[] { $"/Ipd/Account/{adm}", $"/Ipd/Clearance/{adm}", "/Finance/Revenue", $"/Finance/Statement/{pid}" })
        {
            var r = await nurse.Http.GetAsync(url);
            Assert.True(r.StatusCode is HttpStatusCode.Forbidden || r.Headers.Location?.ToString().Contains("AccessDenied") == true, $"{url}: {(int)r.StatusCode}");
        }
        Assert.Contains("data-ledger", await admin.GetHtmlAsync($"/Ipd/Account/{adm}"));
        Assert.Contains("Account Patient", await admin.GetHtmlAsync($"/Finance/Statement/{pid}"));
    }

    private async Task<int> DosageFormAsync()
    {
        await using var db = Factory.NewDb();
        return await db.PharmacyLookups.Where(l => l.Kind == PharmacyLookupKind.DosageForm && l.Name == "Tablet").Select(l => l.Id).SingleAsync();
    }
}
