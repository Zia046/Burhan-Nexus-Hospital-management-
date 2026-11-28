using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Phase 5 – pharmacy: Government/Private rules, batches & FEFO, expiry, issue, retail sale, transfers, adjustments, returns, finance, security.</summary>
public partial class PharmacyTests : Phase2TestBase
{
    [GeneratedRegex(@"/Pharmacy/(?:Doc/[A-Z]+|Medicines|Suppliers)/(\d+)")] private static partial Regex IdRegex();

    private static string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);
    private static readonly KeyValuePair<string, string>[] None = Array.Empty<KeyValuePair<string, string>>();

    private static async Task<long> IdAsync(HttpResponseMessage r, string what)
    {
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, $"{what} failed: " + Errors(await r.Content.ReadAsStringAsync()));
        var m = IdRegex().Match(r.Headers.Location!.OriginalString);
        Assert.True(m.Success, $"Unexpected redirect {r.Headers.Location}");
        return long.Parse(m.Groups[1].Value);
    }

    private static void Denied(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied")),
            $"{what} should be refused but returned {(int)r.StatusCode} {r.Headers.Location}");

    /// <summary>Today in the hospital time zone, as the application sees it.</summary>
    private DateOnly Today => DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5));
    private string Month(int monthsAhead) => Today.AddMonths(monthsAhead).ToString("yyyy-MM");

    private async Task<int> MedicineAsync(Browser admin, string name, string strength = "500 mg", decimal? sale = null, decimal? cost = null, int minimum = 0, bool confirm = false)
    {
        int form;
        await using (var db = Factory.NewDb()) form = await db.PharmacyLookups.Where(l => l.Kind == PharmacyLookupKind.DosageForm && l.Name == "Tablet").Select(l => l.Id).SingleAsync();
        var fields = new List<KeyValuePair<string, string>>
        {
            P("Input.Name", name), P("Input.GenericName", name + " generic"), P("Input.Strength", strength), P("Input.DosageFormId", form.ToString()), P("Input.Unit", "Tablet"),
            P("Input.UnitsPerPack", "10"), P("Input.MinimumStock", minimum.ToString()), P("Input.IsActive", "true"), P("Input.AllowDiscount", "true"), P("Input.MaxDiscountPercent", "10"),
        };
        if (sale is decimal s) fields.Add(P("Input.SalePrice", N(s)));
        if (cost is decimal c) fields.Add(P("Input.PurchasePrice", N(c)));
        if (confirm) fields.Add(P("Input.ConfirmDuplicate", "true"));
        return (int)await IdAsync(await admin.PostFormAsync("/Pharmacy/Medicines/Edit", fields), "Medicine");
    }

    private static async Task<HttpResponseMessage> ReceiveAsync(Browser b, int? supplierId, string? source, Guid? key, string? reference, params (int Med, string Batch, string Expiry, int Qty, decimal? Cost)[] lines)
    {
        var f = new List<KeyValuePair<string, string>> { P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()) };
        if (supplierId is int s) f.Add(P("Input.SupplierId", s.ToString()));
        if (source is not null) f.Add(P("Input.SourceName", source));
        if (reference is not null) f.Add(P("Input.ReferenceNumber", reference));
        for (var i = 0; i < lines.Length; i++)
        {
            f.Add(P($"Lines[{i}].MedicineId", lines[i].Med.ToString()));
            f.Add(P($"Lines[{i}].BatchNumber", lines[i].Batch));
            f.Add(P($"Lines[{i}].Expiry", lines[i].Expiry));
            f.Add(P($"Lines[{i}].Quantity", lines[i].Qty.ToString()));
            if (lines[i].Cost is decimal c) f.Add(P($"Lines[{i}].UnitCost", N(c)));
        }
        return await b.PostFormAsync("/Pharmacy/Receive", f);
    }

    private static List<KeyValuePair<string, string>> LineFields(params (int Med, int Qty, long? Batch, decimal Discount)[] lines)
    {
        var f = new List<KeyValuePair<string, string>>();
        for (var i = 0; i < lines.Length; i++)
        {
            f.Add(P($"Input.Lines[{i}].MedicineId", lines[i].Med.ToString()));
            f.Add(P($"Input.Lines[{i}].Quantity", lines[i].Qty.ToString()));
            if (lines[i].Batch is long b) f.Add(P($"Input.Lines[{i}].BatchId", b.ToString()));
            if (lines[i].Discount != 0) f.Add(P($"Input.Lines[{i}].Discount", N(lines[i].Discount)));
            f.Add(P($"Input.Lines[{i}].Instructions", "1 tablet three times a day"));
        }
        return f;
    }

    private static async Task<HttpResponseMessage> IssueAsync(Browser b, long patientId, long? encounterId, Guid? key, params (int Med, int Qty, long? Batch, decimal Discount)[] lines)
    {
        var f = LineFields(lines);
        f.Add(P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()));
        f.Add(P("Input.PatientId", patientId.ToString()));
        if (encounterId is long e) f.Add(P("Input.EncounterId", e.ToString()));
        if (lines.Any(l => l.Discount != 0)) f.Add(P("Input.DiscountReason", "Staff family"));
        return await b.PostFormAsync($"/Pharmacy/Issue?patientId={patientId}", f, "/Pharmacy/Issue");
    }

    private async Task<HttpResponseMessage> SellAsync(Browser b, int counterId, Guid? key, params (int Med, int Qty, long? Batch, decimal Discount)[] lines)
    {
        int method;
        await using (var db = Factory.NewDb()) method = await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
        var f = LineFields(lines);
        f.Add(P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()));
        f.Add(P("Input.CustomerName", "Walk-in Ahmed"));
        f.Add(P("Input.CounterId", counterId.ToString()));
        f.Add(P("Input.PaymentMethodId", method.ToString()));
        if (lines.Any(l => l.Discount != 0)) f.Add(P("Input.DiscountReason", "Regular customer"));
        return await b.PostFormAsync("/Pharmacy/Sale", f);
    }

    private static async Task<HttpResponseMessage> TransferAsync(Browser b, int from, int to, Guid? key, params (int Med, int Qty, long? Batch, decimal Discount)[] lines)
    {
        var f = LineFields(lines);
        f.Add(P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()));
        f.Add(P("Input.FromLocationId", from.ToString()));
        f.Add(P("Input.ToLocationId", to.ToString()));
        f.Add(P("Input.Reference", "OT-REQ-1"));
        return await b.PostFormAsync($"/Pharmacy/Transfer?from={from}", f, "/Pharmacy/Transfer");
    }

    private async Task<int> LocationAsync(string code)
    {
        await using var db = Factory.NewDb();
        return await db.StockLocations.Where(l => l.Code == code).Select(l => l.Id).SingleAsync();
    }

    private async Task<long> BatchAsync(int medicineId, string batch)
    {
        await using var db = Factory.NewDb();
        return await db.MedicineBatches.Where(b => b.MedicineId == medicineId && b.BatchNumber == batch).Select(b => b.Id).SingleAsync();
    }

    private async Task<int> StockAsync(string location, int medicineId, string? batch = null)
    {
        await using var db = Factory.NewDb();
        return await db.StockBalances.Where(s => s.Location!.Code == location && s.MedicineId == medicineId && (batch == null || s.Batch!.BatchNumber == batch)).SumAsync(s => (int?)s.Quantity) ?? 0;
    }

    /// <summary>Every balance equals the sum of its inventory transactions (no stock change without a transaction).</summary>
    private async Task AssertLedgerConsistentAsync()
    {
        await using var db = Factory.NewDb();
        var balances = await db.StockBalances.AsNoTracking().ToListAsync();
        var sums = await db.InventoryTransactions.GroupBy(t => new { t.LocationId, t.BatchId }).Select(g => new { g.Key.LocationId, g.Key.BatchId, Sum = g.Sum(x => x.Quantity) }).ToListAsync();
        Assert.Equal(sums.Count, balances.Count);
        foreach (var b in balances)
            Assert.Equal(sums.Single(s => s.LocationId == b.LocationId && s.BatchId == b.BatchId).Sum, b.Quantity);
    }

    private async Task<int> CounterAsync(Browser admin, string code, string name)
    {
        var r = await admin.PostFormAsync("/Finance/Counters", new[] { P("Form.Code", code), P("Form.Name", name), P("Form.IsActive", "true") }, "/Finance/Counters?handler=Save");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Counters.Where(c => c.Code == code).Select(c => c.Id).SingleAsync();
    }

    private static readonly string[] PharmacistPerms =
    {
        Perm.DashboardView, Perm.PharmDashboard, Perm.PharmViewCatalogue, Perm.PharmIssueToPatients, Perm.PharmReceiveStock, Perm.PharmTransferStock,
        Perm.PharmViewStockReports, Perm.PharmAdjustStock, Perm.PharmProcessReturns, Perm.PatientsSearch
    };

    // =====================================================================================

    [Fact]
    public async Task Government_pharmacy_hides_prices_and_issues_only_against_registered_visits()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var para = await MedicineAsync(admin, "Paracetamol", sale: 99, cost: 50);
        await using (var db = Factory.NewDb())
        {
            var m = await db.Medicines.SingleAsync(x => x.Id == para);
            Assert.Null(m.SalePrice); // prices posted in Government mode are ignored
            Assert.Null(m.PurchasePrice);
            Assert.Matches(@"^MED-\d{6}$", m.Code);
        }
        Assert.DoesNotContain("data-price-fields", await admin.GetHtmlAsync($"/Pharmacy/Medicines/Edit/{para}"));
        Assert.DoesNotContain("Cost / unit", await admin.GetHtmlAsync("/Pharmacy/Receive"));

        // Prices are refused on receiving; stock comes in batch-wise from a source.
        var priced = await ReceiveAsync(admin, null, "District Health Office", null, "DHO-1", (para, "B001", Month(6), 500, 2.5m));
        Assert.Equal(HttpStatusCode.OK, priced.StatusCode);
        Assert.Contains("Purchase and sale prices are not used", await priced.Content.ReadAsStringAsync()); // update 17
        var grn = await IdAsync(await ReceiveAsync(admin, null, "District Health Office", null, "DHO-1",
            (para, "B001", Month(6), 500, null), (para, "B002", Month(12), 300, null), (para, "B003", Month(18), 200, null)), "Receiving");
        Assert.Equal(1000, await StockAsync("MAIN", para));
        await using (var db = Factory.NewDb())
        {
            var r = await db.StockReceipts.Include(x => x.Items).SingleAsync(x => x.Id == grn);
            Assert.Equal(HospitalType.Government, r.Mode);
            Assert.Null(r.NetAmount);
            Assert.All(r.Items, i => Assert.Null(i.UnitCost));
            Assert.Equal(3, await db.MedicineBatches.CountAsync(b => b.MedicineId == para));
        }

        // No visit → no medicine (Government).
        var pid = await RegisterPatientAsync(admin, PatientFields("Govt Pharmacy Patient", "Govt Father"));
        var noVisit = await IssueAsync(admin, pid, null, null, (para, 10, null, 0));
        Assert.Equal(HttpStatusCode.OK, noVisit.StatusCode);
        Assert.Contains("registered visit", await noVisit.Content.ReadAsStringAsync());
        Assert.Equal(1000, await StockAsync("MAIN", para));

        // Against the OPD visit: FEFO takes B001 first, then B002 – recorded batch by batch.
        var enc = await RegisterOpdAsync(admin, pid);
        var key = Guid.NewGuid();
        var iss = await IdAsync(await IssueAsync(admin, pid, enc, key, (para, 600, null, 0)), "Issue");
        var again = await IdAsync(await IssueAsync(admin, pid, enc, key, (para, 600, null, 0)), "Repeated issue");
        Assert.Equal(iss, again);
        Assert.Equal(0, await StockAsync("MAIN", para, "B001"));
        Assert.Equal(200, await StockAsync("MAIN", para, "B002"));
        Assert.Equal(200, await StockAsync("MAIN", para, "B003"));
        await using (var db = Factory.NewDb())
        {
            var issue = await db.PatientMedicineIssues.Include(x => x.Items).SingleAsync();
            Assert.Equal(enc, issue.EncounterId);
            Assert.Null(issue.NetAmount);
            Assert.Equal(new[] { ("B001", 500), ("B002", 100) }, issue.Items.OrderBy(i => i.BatchNumber).Select(i => (i.BatchNumber, i.Quantity)).ToArray());
            Assert.All(issue.Items, i => { Assert.Null(i.UnitPrice); Assert.Null(i.ChargeId); });
            Assert.Equal(0, await db.Charges.CountAsync(c => c.PharmacyIssueId != null));
        }

        // More than available is refused.
        var tooMuch = await IssueAsync(admin, pid, enc, null, (para, 401, null, 0));
        Assert.Contains("Only 400", await tooMuch.Content.ReadAsStringAsync());

        // Retail sales and commercial reports do not exist in Government mode – also not by direct posting or SQL.
        Denied(await admin.GetAsync("/Pharmacy/Sale"), "Retail sale page");
        Denied(await admin.PostFormAsync("/Pharmacy/Issue", None, "/Pharmacy/Sale"), "Retail sale post");
        Denied(await admin.GetAsync("/Pharmacy/Reports?report=gross-profit"), "Profit report");
        Assert.DoesNotContain("data-kpi=\"sales-amount\"", await admin.GetHtmlAsync("/Pharmacy"));
        await using (var db = Factory.NewDb())
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"PharmacySales\" (\"Number\",\"IdempotencyKey\",\"LocationId\",\"GrossAmount\",\"DiscountAmount\",\"NetAmount\",\"CounterId\",\"BusinessDate\",\"CreatedUtc\") " +
                "VALUES ('X', gen_random_uuid(), 1, 1, 0, 1, 1, CURRENT_DATE, now())"));
            Assert.Contains("only available when medicine prices are used", ex.Message + ex.InnerException?.Message); // update 17
        }

        // Patient profile and pharmacy history show what was issued.
        Assert.Contains("data-profile-medicines", await admin.GetHtmlAsync($"/Patients/Profile/{pid}"));
        var history = await admin.GetHtmlAsync($"/Pharmacy/PatientHistory/{pid}");
        Assert.Contains("B001", history);
        Assert.Contains("1 tablet three times a day", await admin.GetHtmlAsync($"/Print/Pharmacy/ISS/{iss}"));
        Assert.Single(await AuditAsync(AuditActions.PharmMedicineIssued));
        Assert.Single(await AuditAsync(AuditActions.PharmDuplicateBlocked));
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Expired_stock_is_never_issued_and_fefo_override_needs_permission()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var amox = await MedicineAsync(admin, "Amoxicillin", "250 mg");
        await IdAsync(await ReceiveAsync(admin, null, "Store", null, null, (amox, "E-SOON", Month(1), 50, null), (amox, "E-LATE", Month(12), 50, null)), "Receiving");
        Assert.Contains("already expired", await (await ReceiveAsync(admin, null, "Store", null, null, (amox, "E-OLD", Month(-2), 10, null))).Content.ReadAsStringAsync());

        var pid = await RegisterPatientAsync(admin, PatientFields("Fefo Patient", "Fefo Father"));
        var enc = await RegisterOpdAsync(admin, pid);
        var late = await BatchAsync(amox, "E-LATE");
        var soon = await BatchAsync(amox, "E-SOON");

        // A pharmacist without "Select another batch" must take the earliest-expiring batch.
        var pharmacist = await UserWithAsync(admin, "fefo.pharm", PharmacistPerms);
        var blocked = await IssueAsync(pharmacist, pid, enc, null, (amox, 5, late, 0));
        Assert.Contains("expires first", await blocked.Content.ReadAsStringAsync());
        var ok = await IdAsync(await IssueAsync(admin, pid, enc, null, (amox, 5, late, 0)), "Override issue");
        await using (var db = Factory.NewDb())
            Assert.True((await db.PatientMedicineIssueItems.SingleAsync(i => i.IssueId == ok)).FefoOverridden);
        Assert.Single(await AuditAsync(AuditActions.PharmFefoOverride));

        // Time passes and the first batch expires (simulated in the database – a 30-day trial would end if the clock moved months):
        // it is never issued, not counted as available, and shown in the alerts.
        await using (var db = Factory.NewDb())
            await db.Database.ExecuteSqlRawAsync("UPDATE \"MedicineBatches\" SET \"ExpiryDate\" = {0} WHERE \"Id\" = {1}", Today.AddDays(-1), soon);
        var enc2 = await RegisterOpdAsync(admin, pid);
        Assert.Contains("expired", await (await IssueAsync(admin, pid, enc2, null, (amox, 1, soon, 0))).Content.ReadAsStringAsync());
        Assert.Contains("Only 45", await (await IssueAsync(admin, pid, enc2, null, (amox, 46, null, 0))).Content.ReadAsStringAsync());
        await IdAsync(await IssueAsync(pharmacist, pid, enc2, null, (amox, 45, null, 0)), "Issue from the valid batch");
        Assert.Equal(50, await StockAsync("MAIN", amox, "E-SOON"));
        Assert.Equal(0, await StockAsync("MAIN", amox, "E-LATE"));
        Assert.Contains("data-kpi=\"expired\">1<", await admin.GetHtmlAsync("/Pharmacy"));
        Assert.Contains("E-SOON", await admin.GetHtmlAsync("/Pharmacy/Reports?report=expired"));

        // Government: an old visit is not enough (visit moved 10 days back; default validity 7 days).
        await using (var db = Factory.NewDb())
            await db.Database.ExecuteSqlRawAsync("UPDATE \"PatientEncounters\" SET \"EncounterDate\" = {0} WHERE \"Id\" = {1}", Today.AddDays(-10), enc2);
        Assert.Contains("older than", await (await IssueAsync(admin, pid, enc2, null, (amox, 1, null, 0))).Content.ReadAsStringAsync());

        // The expired batch is written off through an approved adjustment.
        await using (var db = Factory.NewDb()) Assert.Equal(0, await db.StockAdjustments.CountAsync());
        var adj = await admin.PostFormAsync("/Pharmacy/Adjustments", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.LocationId", (await LocationAsync("MAIN")).ToString()), P("Input.BatchId", soon.ToString()),
            P("Input.Increase", "false"), P("Input.Quantity", "50"), P("Input.Reason", "Expired"), P("Input.Note", "Expired batch destroyed"),
        });
        Assert.Equal(HttpStatusCode.Redirect, adj.StatusCode);
        Assert.Equal(0, await StockAsync("MAIN", amox));
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Private_purchase_retail_sale_receipt_returns_and_supplier_account()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "PHC", "Pharmacy Counter");
        var supplier = await IdAsync(await admin.PostFormAsync("/Pharmacy/Suppliers/Edit", new[] { P("Input.Name", "Karachi Pharma Distributors"), P("Input.Phone", "0300-1234567"), P("Input.IsActive", "true") }), "Supplier");
        var med = await MedicineAsync(admin, "Ibuprofen", "400 mg", sale: 10, cost: 6);
        Assert.Contains("data-price-fields", await admin.GetHtmlAsync($"/Pharmacy/Medicines/Edit/{med}"));

        // Purchase with supplier invoice → supplier account; the same invoice cannot be entered twice.
        var grn = await IdAsync(await ReceiveAsync(admin, (int)supplier, null, null, "INV-1001", (med, "P1", Month(12), 100, 6m)), "Purchase");
        var dupInvoice = await ReceiveAsync(admin, (int)supplier, null, null, "INV-1001", (med, "P2", Month(12), 10, 6m));
        Assert.Contains("already received", await dupInvoice.Content.ReadAsStringAsync());
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(600m, (await db.StockReceipts.SingleAsync(r => r.Id == grn)).NetAmount);
            Assert.Equal(600m, await db.SupplierLedger.Where(e => e.SupplierId == supplier).SumAsync(e => e.Amount));
        }

        // Retail sale: 10 × 10.00 − 5.00 discount (max 10 %) → Phase 3 receipt, cashier ledger, stock −10. A double click charges once.
        var tooBig = await SellAsync(admin, counter, null, (med, 10, null, 11m));
        Assert.Contains("must be between 0 and 10.00", await tooBig.Content.ReadAsStringAsync());
        var key = Guid.NewGuid();
        var sale = await IdAsync(await SellAsync(admin, counter, key, (med, 10, null, 5m)), "Sale");
        Assert.Equal(sale, await IdAsync(await SellAsync(admin, counter, key, (med, 10, null, 5m)), "Repeated sale"));
        Assert.Equal(90, await StockAsync("MAIN", med));
        long receiptId;
        await using (var db = Factory.NewDb())
        {
            var s = await db.PharmacySales.Include(x => x.Items).SingleAsync();
            Assert.Equal(95m, s.NetAmount);
            Assert.Equal(10m, s.Items.Single().UnitPrice);
            Assert.Equal(6m, s.Items.Single().UnitCost);
            var receipt = await db.Receipts.Include(r => r.Lines).SingleAsync(r => r.Id == s.ReceiptId);
            receiptId = receipt.Id;
            Assert.Equal(95m, receipt.AmountPaid);
            Assert.Equal("Walk-in Ahmed", receipt.PayerName);
            Assert.Null(receipt.PatientId);
            Assert.Equal(1, await db.Receipts.CountAsync());
            Assert.All(await db.Charges.Where(c => c.PharmacySaleId == s.Id).ToListAsync(), c => Assert.Equal(ChargeStatus.Paid, c.Status));
            Assert.Equal(95m, await db.CashierLedger.Where(e => e.ReceiptId == receipt.Id).SumAsync(e => e.Amount));
        }
        var slip = await admin.GetHtmlAsync($"/Print/Pharmacy/SAL/{sale}");
        Assert.Contains("PHARMACY RECEIPT", slip);
        Assert.Contains("10 × 10.00", slip);

        // A later price change does not touch the old sale.
        await MedicineEditPriceAsync(admin, med, 12);
        await using (var db = Factory.NewDb()) Assert.Equal(10m, (await db.PharmacySaleItems.SingleAsync()).UnitPrice);
        Assert.Single(await AuditAsync(AuditActions.PharmMedicinePriceChanged), a => a.Description!.Contains("10.00 → 12.00"));

        // Returns: 4 undamaged back to stock (refund 38.00 waits for approval), 6 damaged recorded only, then nothing left to return.
        int method;
        await using (var db = Factory.NewDb()) method = await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
        long itemId;
        await using (var db = Factory.NewDb()) itemId = await db.PharmacySaleItems.Select(i => i.Id).SingleAsync();
        async Task<HttpResponseMessage> Return(int qty, bool restock) => await admin.PostFormAsync($"/Pharmacy/Returns/Sale/{sale}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Reason", "Customer returned"), P("Input.RefundPaymentMethodId", method.ToString()),
            P("Input.Lines[0].ItemId", itemId.ToString()), P("Input.Lines[0].Quantity", qty.ToString()), P("Input.Lines[0].Restock", restock ? "true" : "false"),
        });
        Assert.Equal(HttpStatusCode.Redirect, (await Return(4, true)).StatusCode);
        Assert.Equal(94, await StockAsync("MAIN", med));
        Assert.Equal(HttpStatusCode.Redirect, (await Return(6, false)).StatusCode);
        Assert.Equal(94, await StockAsync("MAIN", med));
        Assert.Contains("can still be returned", await (await Return(1, true)).Content.ReadAsStringAsync());
        await using (var db = Factory.NewDb())
        {
            var refunds = await db.Refunds.Where(r => r.ReceiptId == receiptId).OrderBy(r => r.Id).ToListAsync();
            Assert.Equal(new[] { 38m, 57m }, refunds.Select(r => r.Amount).ToArray());
            Assert.All(refunds, r => Assert.Equal(RefundStatus.Pending, r.Status)); // paid out only after approval in Finance → Refunds
            Assert.Equal(10, (await db.PharmacySaleItems.SingleAsync()).ReturnedQuantity);
        }

        // Supplier payment (not more than owed) and supplier return.
        int bank;
        await using (var db = Factory.NewDb()) bank = await db.PaymentMethods.Where(m => !m.RequiresReference).OrderBy(m => m.Id).Select(m => m.Id).FirstAsync();
        async Task<HttpResponseMessage> Pay(decimal amount) => await admin.PostFormAsync($"/Pharmacy/Suppliers/{supplier}", new[]
        {
            P("Payment.IdempotencyKey", Guid.NewGuid().ToString()), P("Payment.Amount", N(amount)), P("Payment.PaymentMethodId", bank.ToString()),
        }, $"/Pharmacy/Suppliers/{supplier}?handler=Pay");
        Assert.Contains("cannot exceed", await (await Pay(700)).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Pay(400)).StatusCode);
        var p1 = await BatchAsync(med, "P1");
        var srt = await admin.PostFormAsync("/Pharmacy/Returns/Supplier", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SupplierId", supplier.ToString()), P("Input.ReferenceInvoice", "INV-1001"), P("Input.Reason", "Near expiry"),
            P("Input.Lines[0].BatchId", p1.ToString()), P("Input.Lines[0].Quantity", "10"),
        });
        await IdAsync(srt, "Supplier return");
        Assert.Equal(84, await StockAsync("MAIN", med));
        await using (var db = Factory.NewDb()) Assert.Equal(140m, await db.SupplierLedger.Where(e => e.SupplierId == supplier).SumAsync(e => e.Amount));

        // Gross profit: a second sale of 5 × 12.00 at cost 6.00 → profit 30.00 (the fully returned first sale adds nothing).
        await IdAsync(await SellAsync(admin, counter, null, (med, 5, null, 0)), "Second sale");
        var profit = await admin.GetHtmlAsync("/Pharmacy/Reports?report=gross-profit&period=today");
        Assert.Contains("60.00", profit);
        Assert.Contains("30.00", profit);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Pharmacy/Reports?handler=Export&format=xlsx&report=supplier-balances&period=today")).StatusCode);
        Assert.Single(await AuditAsync(AuditActions.PharmExported));
        Assert.Contains("data-kpi=\"sales-amount\"", await admin.GetHtmlAsync("/Pharmacy?period=today"));
        await AssertLedgerConsistentAsync();
    }

    private async Task MedicineEditPriceAsync(Browser admin, int med, decimal sale)
    {
        await using var db = Factory.NewDb();
        var m = await db.Medicines.SingleAsync(x => x.Id == med);
        var r = await admin.PostFormAsync($"/Pharmacy/Medicines/Edit/{med}", new[]
        {
            P("Input.Name", m.Name), P("Input.GenericName", m.GenericName ?? ""), P("Input.Strength", m.Strength ?? ""), P("Input.DosageFormId", m.DosageFormId?.ToString() ?? ""),
            P("Input.Unit", m.Unit), P("Input.UnitsPerPack", m.UnitsPerPack.ToString()), P("Input.MinimumStock", m.MinimumStock.ToString()), P("Input.IsActive", "true"),
            P("Input.SalePrice", N(sale)), P("Input.PurchasePrice", N(m.PurchasePrice ?? 0)), P("Input.AllowDiscount", "true"), P("Input.MaxDiscountPercent", "10"),
        });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Private_patient_issue_is_charged_at_the_cash_counter_and_returns_correct_the_bill()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var counter = await CounterAsync(admin, "CAS", "Cash Counter");
        var med = await MedicineAsync(admin, "Omeprazole", "20 mg", sale: 20, cost: 12);
        await IdAsync(await ReceiveAsync(admin, null, "Opening stock", null, null, (med, "O1", Month(10), 50, 12m)), "Receiving");
        var pid = await RegisterPatientAsync(admin, PatientFields("Private Pharmacy Patient", "Private Father"));

        // Private: registered patient without a visit is allowed; each line becomes an unpaid Phase 3 charge.
        var iss = await IdAsync(await IssueAsync(admin, pid, null, null, (med, 3, null, 0)), "Issue");
        long chargeId;
        await using (var db = Factory.NewDb())
        {
            var c = await db.Charges.SingleAsync(x => x.PharmacyIssueId == iss);
            chargeId = c.Id;
            Assert.Equal(60m, c.NetAmount);
            Assert.Equal(ChargeStatus.Unpaid, c.Status);
            Assert.Equal(pid, c.PatientId);
        }
        var collectPage = await admin.GetHtmlAsync($"/Finance/Collect?patientId={pid}&pharmacyIssueId={iss}");
        Assert.Contains("Omeprazole", collectPage);
        Assert.Contains("value=\"60.00\"", collectPage.Replace("value=\"60\"", "value=\"60.00\""));

        // Pay at the cashier, then return one tablet → refund request of 20.00.
        int method;
        await using (var db = Factory.NewDb()) method = await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
        var paid = await admin.PostFormAsync($"/Finance/Collect?patientId={pid}&pharmacyIssueId={iss}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.CounterId", counter.ToString()), P("Input.PaymentMethodId", method.ToString()),
            P("PatientId", pid.ToString()), P("Balances[0].ChargeId", chargeId.ToString()), P("Balances[0].PayNow", "60"),
        }, "/Finance/Collect");
        Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        long itemId;
        await using (var db = Factory.NewDb()) itemId = await db.PatientMedicineIssueItems.Where(i => i.IssueId == iss).Select(i => i.Id).SingleAsync();
        var ret = await admin.PostFormAsync($"/Pharmacy/Returns/Issue/{iss}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Reason", "Patient stopped the medicine"), P("Input.RefundPaymentMethodId", method.ToString()),
            P("Input.Lines[0].ItemId", itemId.ToString()), P("Input.Lines[0].Quantity", "1"), P("Input.Lines[0].Restock", "true"),
        });
        Assert.Equal(HttpStatusCode.Redirect, ret.StatusCode);
        Assert.Equal(48, await StockAsync("MAIN", med));
        await using (var db = Factory.NewDb()) Assert.Equal(20m, (await db.Refunds.SingleAsync()).Amount);

        // Unpaid issue, partial return → the charge is cancelled and the kept medicines are billed again (no money changes hands).
        var iss2 = await IdAsync(await IssueAsync(admin, pid, null, null, (med, 3, null, 0)), "Second issue");
        long item2;
        await using (var db = Factory.NewDb()) item2 = await db.PatientMedicineIssueItems.Where(i => i.IssueId == iss2).Select(i => i.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Pharmacy/Returns/Issue/{iss2}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Reason", "Doctor reduced the dose"),
            P("Input.Lines[0].ItemId", item2.ToString()), P("Input.Lines[0].Quantity", "1"), P("Input.Lines[0].Restock", "true"),
        })).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var charges = await db.Charges.Where(c => c.PharmacyIssueId == iss2).OrderBy(c => c.Id).ToListAsync();
            Assert.Equal(new[] { (60m, ChargeStatus.Cancelled), (40m, ChargeStatus.Unpaid) }, charges.Select(c => (c.NetAmount, c.Status)).ToArray());
            Assert.Equal(1, await db.Refunds.CountAsync());
        }
        Assert.Equal(46, await StockAsync("MAIN", med));
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Minor_OT_transfer_moves_stock_exactly_once_and_departments_keep_their_own_balance()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var tt = await MedicineAsync(admin, "Tetanus Toxoid", "0.5 ml");
        await IdAsync(await ReceiveAsync(admin, null, "EPI store", null, null, (tt, "TT1", Month(8), 100, null)), "Receiving");
        var main = await LocationAsync("MAIN");
        var ot = await LocationAsync("MOT");

        var key = Guid.NewGuid();
        var trf = await IdAsync(await TransferAsync(admin, main, ot, key, (tt, 30, null, 0)), "Transfer");
        Assert.Equal(trf, await IdAsync(await TransferAsync(admin, main, ot, key, (tt, 30, null, 0)), "Repeated transfer"));
        Assert.Equal(70, await StockAsync("MAIN", tt));
        Assert.Equal(30, await StockAsync("MOT", tt));
        await using (var db = Factory.NewDb())
        {
            var moves = await db.InventoryTransactions.Where(t => t.DocumentType == "TRF").OrderBy(t => t.Id).ToListAsync();
            Assert.Equal(new[] { (InventoryTxnType.TransferOut, main, -30), (InventoryTxnType.TransferIn, ot, 30) }, moves.Select(m => (m.Type, m.LocationId, m.Quantity)).ToArray());
        }
        Assert.Contains("Only 70", await (await TransferAsync(admin, main, ot, null, (tt, 71, null, 0))).Content.ReadAsStringAsync());
        Assert.Contains("TRANSFER", await admin.GetHtmlAsync($"/Print/Pharmacy/TRF/{trf}"));

        // Patient issues come from the main pharmacy only – the OT balance is never reduced per patient.
        var pid = await RegisterPatientAsync(admin, PatientFields("OT Patient", "OT Father"));
        var enc = await PostEmergencyAsync(admin, pid, ("Visit.BriefComplaint", "Cut on hand"));
        await IdAsync(await IssueAsync(admin, pid, EncounterId(enc), null, (tt, 1, null, 0)), "Emergency issue");
        Assert.Equal(69, await StockAsync("MAIN", tt));
        Assert.Equal(30, await StockAsync("MOT", tt));

        // The administrator adds another department store and moves stock from OT to it.
        var add = await admin.PostFormAsync("/Pharmacy/Settings", new[] { P("location.Code", "DRS"), P("location.Name", "Dressing Room"), P("location.IsActive", "true"), P("location.SortOrder", "30") },
            "/Pharmacy/Settings?handler=Location");
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        var drs = await LocationAsync("DRS");
        await IdAsync(await TransferAsync(admin, ot, drs, null, (tt, 10, null, 0)), "OT → Dressing Room");
        Assert.Equal(20, await StockAsync("MOT", tt));
        Assert.Equal(10, await StockAsync("DRS", tt));

        // A user with only "View Department Stock" sees the departments but not the main pharmacy.
        var otViewer = await UserWithAsync(admin, "ot.viewer", Perm.DashboardView, Perm.PharmViewDepartmentStock);
        var html = await otViewer.GetHtmlAsync("/Pharmacy/Stock");
        Assert.Contains("Minor OT|TT1", html);
        Assert.DoesNotContain("Main Pharmacy|TT1", html);
        Denied(await otViewer.GetAsync($"/Pharmacy/Stock?location={main}"), "Main stock for department viewer");
        Denied(await otViewer.PostFormAsync("/Pharmacy/Stock", None, "/Pharmacy/Transfer"), "Transfer without permission");
        Assert.Contains("Minor OT", await admin.GetHtmlAsync("/Pharmacy/Reports?report=ot-transfers&period=today"));
        Assert.Equal(2, (await AuditAsync(AuditActions.PharmTransfer)).Count);
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Stock_adjustments_need_a_reason_and_approval_and_history_cannot_be_rewritten()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var med = await MedicineAsync(admin, "Cetirizine", "10 mg", sale: 5, cost: 2);
        await IdAsync(await ReceiveAsync(admin, null, "Opening", null, null, (med, "C1", Month(9), 100, 2m)), "Receiving");
        var main = await LocationAsync("MAIN");
        var batch = await BatchAsync(med, "C1");
        var clerk = await UserWithAsync(admin, "adj.clerk", PharmacistPerms);
        async Task<HttpResponseMessage> Adjust(Browser b, bool increase, int qty, string reason, string note) => await b.PostFormAsync("/Pharmacy/Adjustments", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.LocationId", main.ToString()), P("Input.BatchId", batch.ToString()), P("Input.Increase", increase ? "true" : "false"),
            P("Input.Quantity", qty.ToString()), P("Input.Reason", reason), P("Input.Note", note),
        });
        Assert.Contains("mandatory", await (await Adjust(clerk, false, 5, "Damaged", "")).Content.ReadAsStringAsync());
        Assert.Contains("only reduce", await (await Adjust(clerk, true, 5, "Expired", "found")).Content.ReadAsStringAsync());
        Assert.Contains("Only 100", await (await Adjust(clerk, false, 101, "Lost", "missing")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Adjust(clerk, false, 5, "Damaged", "Strip torn")).StatusCode);
        Assert.Equal(100, await StockAsync("MAIN", med)); // pending – not applied yet
        long id;
        await using (var db = Factory.NewDb()) id = await db.StockAdjustments.Where(a => a.Status == StockAdjustmentStatus.Pending).Select(a => a.Id).SingleAsync();
        Denied(await clerk.PostFormAsync("/Pharmacy/Adjustments", new[] { P("approve", "true") }, $"/Pharmacy/Adjustments?handler=Decide&id={id}"), "Self approval without permission");
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Adjustments", new[] { P("approve", "true") }, $"/Pharmacy/Adjustments?handler=Decide&id={id}")).StatusCode);
        Assert.Equal(95, await StockAsync("MAIN", med));
        await using (var db = Factory.NewDb())
        {
            var a = await db.StockAdjustments.SingleAsync(x => x.Id == id);
            Assert.Equal((StockAdjustmentStatus.Approved, 100, 95, "admin"), (a.Status, a.QuantityBefore, a.QuantityAfter!.Value, a.DecidedByUsername));
        }
        // Rejection needs a note; a decided adjustment cannot be decided again.
        Assert.Equal(HttpStatusCode.Redirect, (await Adjust(clerk, true, 3, "PhysicalCount", "Counted 98")).StatusCode);
        long id2;
        await using (var db = Factory.NewDb()) id2 = await db.StockAdjustments.Where(a => a.Status == StockAdjustmentStatus.Pending).Select(a => a.Id).SingleAsync();
        await admin.PostFormAsync("/Pharmacy/Adjustments", new[] { P("approve", "false") }, $"/Pharmacy/Adjustments?handler=Decide&id={id2}");
        await using (var db = Factory.NewDb()) Assert.Equal(StockAdjustmentStatus.Pending, (await db.StockAdjustments.SingleAsync(x => x.Id == id2)).Status);
        await admin.PostFormAsync("/Pharmacy/Adjustments", new[] { P("approve", "false"), P("note", "Recount was wrong") }, $"/Pharmacy/Adjustments?handler=Decide&id={id2}");
        await using (var db = Factory.NewDb()) Assert.Equal(StockAdjustmentStatus.Rejected, (await db.StockAdjustments.SingleAsync(x => x.Id == id2)).Status);
        Assert.Equal(95, await StockAsync("MAIN", med));

        // The database protects the history: no update/delete of transactions, no negative stock, no deleting balances or documents.
        await using (var db = Factory.NewDb())
        {
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"InventoryTransactions\" SET \"Quantity\" = 1"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"InventoryTransactions\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"StockBalances\" SET \"Quantity\" = -1"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"StockBalances\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"StockReceipts\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"StockAdjustments\""));
        }
        Assert.Single(await AuditAsync(AuditActions.PharmAdjustmentApproved));
        Assert.Single(await AuditAsync(AuditActions.PharmAdjustmentRejected));
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Pharmacy_permissions_duplicate_warning_and_price_control()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var para = await MedicineAsync(admin, "Panadol", sale: 3, cost: 2);

        // Similar medicine → warning with the existing record; saving anyway needs a confirmation. Nothing is merged.
        int form;
        await using (var db = Factory.NewDb()) form = await db.PharmacyLookups.Where(l => l.Kind == PharmacyLookupKind.DosageForm && l.Name == "Tablet").Select(l => l.Id).SingleAsync();
        var dup = await admin.PostFormAsync("/Pharmacy/Medicines/Edit", new[] { P("Input.Name", "PANADOL"), P("Input.Strength", "500mg"), P("Input.DosageFormId", form.ToString()), P("Input.Unit", "Tablet"), P("Input.IsActive", "true") });
        Assert.Equal(HttpStatusCode.OK, dup.StatusCode);
        Assert.Contains("data-similar-row", await dup.Content.ReadAsStringAsync());
        await MedicineAsync(admin, "PANADOL", "500mg", confirm: true);
        await using (var db = Factory.NewDb()) Assert.Equal(2, await db.Medicines.CountAsync());

        // Users only reach what they are allowed to – checked on the server.
        var nobody = await UserWithAsync(admin, "pharm.none", Perm.DashboardView);
        foreach (var url in new[] { "/Pharmacy", "/Pharmacy/Medicines", "/Pharmacy/Issue", "/Pharmacy/Sale", "/Pharmacy/Receive", "/Pharmacy/Transfer", "/Pharmacy/Stock",
                     "/Pharmacy/Adjustments", "/Pharmacy/Suppliers", "/Pharmacy/Reports", "/Pharmacy/Settings", "/Pharmacy/Documents", "/Pharmacy/Pick?q=pan" })
            Denied(await nobody.GetAsync(url), "No-permission user " + url);
        var viewer = await UserWithAsync(admin, "pharm.viewer", Perm.DashboardView, Perm.PharmViewCatalogue);
        Assert.Contains("Panadol", await viewer.GetHtmlAsync("/Pharmacy/Medicines"));
        Assert.DoesNotContain("Sale price", await viewer.GetHtmlAsync("/Pharmacy/Medicines"));
        Denied(await viewer.PostFormAsync("/Pharmacy/Medicines", new[] { P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SourceName", "X"),
            P("Lines[0].MedicineId", para.ToString()), P("Lines[0].BatchNumber", "V1"), P("Lines[0].Expiry", Month(6)), P("Lines[0].Quantity", "1") }, "/Pharmacy/Receive"), "Receive by viewer");
        Denied(await viewer.PostFormAsync("/Pharmacy/Medicines", new[] { P("Input.Name", "Hack") }, $"/Pharmacy/Medicines/Edit/{para}"), "Edit by viewer");
        Denied(await viewer.GetAsync("/Pharmacy/Reports?report=gross-profit"), "Financial report by viewer");

        // An editor without "Manage Medicine Prices" cannot change prices, even by posting them.
        var editor = await UserWithAsync(admin, "pharm.editor", Perm.DashboardView, Perm.PharmViewCatalogue, Perm.PharmEditMedicine);
        var r = await editor.PostFormAsync($"/Pharmacy/Medicines/Edit/{para}", new[]
        {
            P("Input.Name", "Panadol"), P("Input.Strength", "500 mg"), P("Input.DosageFormId", form.ToString()), P("Input.Unit", "Tablet"), P("Input.IsActive", "true"),
            P("Input.SalePrice", "0.01"), P("Input.PurchasePrice", "0.01"), P("Input.ConfirmDuplicate", "true"),
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        await using (var db = Factory.NewDb())
        {
            var m = await db.Medicines.SingleAsync(x => x.Id == para);
            Assert.Equal((3m, 2m), (m.SalePrice!.Value, m.PurchasePrice!.Value));
        }

        // A pharmacist without "Apply Discount" / "Manage Retail Sales" cannot discount or sell.
        var pharmacist = await UserWithAsync(admin, "pharm.user", PharmacistPerms);
        Denied(await pharmacist.GetAsync("/Pharmacy/Sale"), "Retail sale without permission");
        await IdAsync(await ReceiveAsync(admin, null, "Opening", null, null, (para, "PN1", Month(6), 20, 2m)), "Receiving");
        var pid = await RegisterPatientAsync(admin, PatientFields("Discount Patient", "Discount Father"));
        Assert.Contains("not allowed to give discounts", await (await IssueAsync(pharmacist, pid, null, null, (para, 2, null, 0.5m))).Content.ReadAsStringAsync());

        // Lookups and suppliers, catalogue changes audited.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Lookups", new[] { P("kind", "Manufacturer"), P("name", "GSK"), P("isActive", "true"), P("sortOrder", "1") })).StatusCode);
        await using (var db = Factory.NewDb()) Assert.True(await db.PharmacyLookups.AnyAsync(l => l.Kind == PharmacyLookupKind.Manufacturer && l.Name == "GSK"));
        Assert.Equal(3, (await AuditAsync(AuditActions.PharmMedicineSaved)).Count);
        Assert.Contains("saved despite similar-medicine warning", string.Join(" ", (await AuditAsync(AuditActions.PharmMedicineSaved)).Select(a => a.Description)));
    }

    [Fact]
    public async Task Pharmacy_reports_open_and_previous_phases_still_work()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var med = await MedicineAsync(admin, "Metformin", sale: 4, cost: 2, minimum: 50);
        await IdAsync(await ReceiveAsync(admin, null, "Opening", null, null, (med, "M1", Month(2), 20, 2m)), "Receiving");
        Assert.Contains("data-low=", await admin.GetHtmlAsync("/Pharmacy"));
        foreach (var key in new[] { "stock", "batches", "low-stock", "near-expiry", "expired", "receiving", "issuance", "transfers", "adjustments", "supplier-returns",
                     "physical-difference", "patient-issues", "opd-issues", "emergency-issues", "daily-issues", "medicine-issues", "department-stock", "ot-transfers",
                     "department-transfers", "sales-daily", "sales-monthly", "sales-medicine", "purchases", "supplier-balances", "sales-returns", "purchase-returns", "gross-profit", "valuation" })
        {
            var res = await admin.GetAsync($"/Pharmacy/Reports?report={key}&period=month");
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"Report {key}: {(int)res.StatusCode}");
        }
        Assert.Contains("Metformin", await admin.GetHtmlAsync("/Pharmacy/Reports?report=low-stock"));
        Assert.Contains("M1", await admin.GetHtmlAsync("/Pharmacy/Reports?report=near-expiry"));
        var csv = await admin.GetAsync("/Pharmacy/Reports?handler=Export&format=csv&report=valuation&period=month");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Contains("40.00", (await csv.Content.ReadAsStringAsync()).Replace("40.0000", "40.00"));

        // Earlier modules still answer.
        foreach (var url in new[] { "/", "/Patients", "/Opd", "/Finance/Collect", "/Finance/Receipts", "/Lab", "/Lab/Catalogue" })
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        Assert.Contains("Pharmacy", await admin.GetHtmlAsync("/"));
    }
}
