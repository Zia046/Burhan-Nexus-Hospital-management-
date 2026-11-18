using System.Net;
using System.Text.Json;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 11 – ward request alert, IPD medicine issuance pay now / later, supplier invoice entry.</summary>
public partial class Update9Tests
{
    private static async Task<int?> WardAlertAsync(Browser b)
    {
        var json = await b.GetHtmlAsync("/api/alerts");
        using var doc = JsonDocument.Parse(json);
        var w = doc.RootElement.GetProperty("wardRequests");
        return w.ValueKind == JsonValueKind.Null ? null : w.GetInt32();
    }

    [Fact]
    public async Task Update11_ward_request_alert_stays_until_issued_and_ipd_issue_pay_now_or_later()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var ceft = await MedicineAsync(admin, "Ceftriaxone Eleven");
        await ReceiveAsync(admin, ceft, 50);
        var pid = await RegisterPatientAsync(admin, PatientFields("Ward Pay Patient", "Father Pay"));
        var admit = await admin.PostFormAsync($"/Ipd/Admit?patientId={pid}", new[]
        {
            P("PatientId", pid.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"),
            P("Input.SourceEncounterId", ""), P("Input.BedId", ""), P("Input.AdmittingDoctorName", "Dr. Ward"), P("Input.AdmissionDiagnosis", "Pneumonia"),
            P("Input.AttendantName", "Brother"), P("Input.AttendantPhone", "03001234567"),
        });
        var adm = await IdAsync(AdmissionRegex(), admit, "Admission");
        Assert.Equal(0, await WardAlertAsync(admin));

        var nurse = await StaffAsync(admin, "nurse11", Perm.DashboardView, Perm.IpdViewPatients, Perm.IpdRequestMedicines);
        Assert.Equal(HttpStatusCode.Redirect, (await nurse.PostFormAsync($"/Ipd/Medicines/{adm}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Priority", "Routine"), P("Input.Lines[0].MedicineId", ceft.ToString()), P("Input.Lines[0].Quantity", "10"),
        })).StatusCode);
        long reqId, lineId;
        await using (var db = Factory.NewDb())
        {
            var r = await db.MedicineRequests.Include(x => x.Lines).SingleAsync();
            (reqId, lineId) = (r.Id, r.Lines.Single().Id);
        }
        // Red alert with the count; opening the list does not clear it; a user without the pharmacy task gets no count.
        Assert.Equal(1, await WardAlertAsync(admin));
        await admin.GetHtmlAsync("/Pharmacy/WardRequests");
        Assert.Equal(1, await WardAlertAsync(admin));
        Assert.Null(await WardAlertAsync(nurse));

        // Issue 4 with "pay now" 15 of 40 (partial) → receipt linked to that issuance; the rest outstanding; still pending (partly issued).
        int cash;
        await using (var db = Factory.NewDb()) cash = await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
        var form = await admin.GetHtmlAsync($"/Pharmacy/WardRequest/{reqId}");
        Assert.Contains("data-ipd-medicine-payment", form);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Pharmacy/WardRequest/{reqId}", new[]
        {
            P("IdempotencyKey", Guid.NewGuid().ToString()), P($"Issue[{lineId}]", "4"), P("PayAction", "pay"), P("PayAmount", "15"), P("PayMethodId", cash.ToString()),
        }, $"/Pharmacy/WardRequest/{reqId}?handler=Issue")).StatusCode);
        Assert.Equal(1, await WardAlertAsync(admin));
        await using (var db = Factory.NewDb())
        {
            var issue = await db.PatientMedicineIssues.SingleAsync();
            Assert.Equal(adm, issue.IpdAdmissionId);
            var charges = await db.Charges.Where(c => c.PharmacyIssueId == issue.Id).ToListAsync();
            Assert.All(charges, c => Assert.Equal(adm, c.IpdAdmissionId));
            Assert.Equal((40m, 15m), (charges.Sum(c => c.NetAmount), charges.Sum(c => c.PaidAmount)));
            Assert.All(charges, c => Assert.NotEqual(ChargeStatus.Paid, c.Status));
            Assert.Equal(1, await db.ReceiptLines.CountAsync(l => charges.Select(c => c.Id).Contains(l.ChargeId)));
        }
        Assert.Single(await AuditAsync(AuditActions.IpdMedicinePaid));

        // Issue the remaining 6 without payment → outstanding on the IPD account, never marked paid; the alert clears.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Pharmacy/WardRequest/{reqId}", new[]
        {
            P("IdempotencyKey", Guid.NewGuid().ToString()), P($"Issue[{lineId}]", "6"), P("PayAction", "later"),
        }, $"/Pharmacy/WardRequest/{reqId}?handler=Issue")).StatusCode);
        Assert.Equal(0, await WardAlertAsync(admin));
        await using (var db = Factory.NewDb())
        {
            var second = await db.PatientMedicineIssues.OrderByDescending(i => i.Id).FirstAsync();
            var charges = await db.Charges.Where(c => c.PharmacyIssueId == second.Id).ToListAsync();
            Assert.Equal((60m, 0m), (charges.Sum(c => c.NetAmount), charges.Sum(c => c.PaidAmount)));
            Assert.All(charges, c => Assert.Equal(ChargeStatus.Unpaid, c.Status));
            Assert.Equal(40, await db.StockBalances.Where(s => s.MedicineId == ceft).SumAsync(s => s.Quantity));
            Assert.Equal(2, await db.Charges.CountAsync(c => c.IpdAdmissionId == adm && c.PharmacyIssueId != null)); // no duplicate charges
        }
    }

    [Fact]
    public async Task Update11_supplier_invoice_optional_batch_auto_prices_duplicate_invoice_refused_and_history()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var amox = await MedicineAsync(admin, "Amoxil Eleven");
        var pan = await MedicineAsync(admin, "Panadol Eleven");
        int supplier;
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Suppliers/Edit", new[] { P("Input.Name", "City Distributors"), P("Input.IsActive", "true") })).StatusCode);
        await using (var db = Factory.NewDb()) supplier = await db.Suppliers.Where(s => s.Name == "City Distributors").Select(s => s.Id).SingleAsync();
        var page = await admin.GetHtmlAsync("/Pharmacy/Receive");
        Assert.Contains("data-receive-summary", page);
        Assert.Contains("data-add-row", page);
        // The medicine picker carries the purchase and sale price for auto-fill.
        Assert.Contains("\"cost\":5", await admin.GetHtmlAsync("/Pharmacy/Pick?q=Amoxil"));

        var exp = Today.AddMonths(12).ToString("yyyy-MM");
        KeyValuePair<string, string>[] Invoice(string key, string inv) => new[]
        {
            P("Input.IdempotencyKey", key), P("Input.SupplierId", supplier.ToString()), P("Input.ReferenceNumber", inv), P("Input.InvoiceDate", Today.ToString("yyyy-MM-dd")),
            P("Lines[0].MedicineId", amox.ToString()), P("Lines[0].BatchNumber", ""), P("Lines[0].Expiry", exp), P("Lines[0].Quantity", "30"), P("Lines[0].UnitCost", "5"),
            P("Lines[1].MedicineId", pan.ToString()), P("Lines[1].BatchNumber", "PN-1"), P("Lines[1].Expiry", exp), P("Lines[1].Quantity", "20"), P("Lines[1].UnitCost", "4"),
        };
        var key = Guid.NewGuid().ToString();
        var r = await admin.PostFormAsync("/Pharmacy/Receive", Invoice(key, "INV-77"));
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Invoice: " + Errors(await r.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Receive", Invoice(key, "INV-77"))).StatusCode); // double submit
        // The same supplier invoice number again (any case) is refused; nothing is added.
        var dup = await admin.PostFormAsync("/Pharmacy/Receive", Invoice(Guid.NewGuid().ToString(), "inv-77"));
        Assert.Contains("already received", Errors(await dup.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.StockReceipts.CountAsync());
            Assert.Equal(30, await db.StockBalances.Where(s => s.MedicineId == amox).SumAsync(s => s.Quantity));
            Assert.StartsWith("AUTO-", await db.MedicineBatches.Where(b => b.MedicineId == amox).Select(b => b.BatchNumber).SingleAsync());
        }
        // A bad line rolls back the whole invoice – no partial stock.
        var bad = Invoice(Guid.NewGuid().ToString(), "INV-78").Select(kv => kv.Key == "Lines[1].Quantity" ? P(kv.Key, "0") : kv).ToArray();
        Assert.NotEqual(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Receive", bad)).StatusCode);
        await using (var db = Factory.NewDb()) Assert.Equal(30, await db.StockBalances.Where(s => s.MedicineId == amox).SumAsync(s => s.Quantity));
        Assert.Contains("INV-77", await admin.GetHtmlAsync("/Pharmacy/Documents?type=receipts&q=INV-77"));
    }
}
