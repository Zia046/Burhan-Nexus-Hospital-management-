using System.Globalization;
using System.Net;
using System.Text.Json;
using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 12 – invoice-based bulk stock receiving. Acceptance test: supplier invoice 12322 with 23 medicines (300 units) saved once.</summary>
public partial class Update9Tests
{
    private static readonly string[] AcceptanceMedicines =
    {
        "Inj. Diclofenac", "Tab. Panadol", "Cap. Amoxicillin", "Syrup Brufen", "Tab. Flagyl", "Inj. Ceftriaxone", "Tab. Augmentin", "Syrup Calpol",
        "Tab. Ponstan", "Inj. Dexamethasone", "Tab. Loprin", "Cap. Omeprazole", "Tab. Glucophage", "Inj. Ranitidine", "Syrup Ventolin", "Tab. Disprin",
        "Tab. Arinac", "Inj. Voltral", "Tab. Ciproxin", "Cap. Doxycycline", "Tab. Lasix", "Inj. Gravinate", "Tab. Risek",
    };

    private async Task<int> PricedMedicineAsync(Browser admin, string name, decimal cost, decimal sale)
    {
        var r = await admin.PostFormAsync("/Pharmacy/Medicines/Edit", new[]
        {
            P("Input.Name", name), P("Input.Unit", "Unit"), P("Input.UnitsPerPack", "1"), P("Input.MinimumStock", "0"), P("Input.IsActive", "true"),
            P("Input.PurchasePrice", cost.ToString(CultureInfo.InvariantCulture)), P("Input.SalePrice", sale.ToString(CultureInfo.InvariantCulture)), P("Input.ConfirmDuplicate", "true"),
        });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Medicine: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Medicines.Where(m => m.Name == name).Select(m => m.Id).SingleAsync();
    }

    [Fact]
    public async Task Update12_acceptance_supplier_invoice_12322_with_23_medicines_is_received_in_one_submission()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Suppliers/Edit", new[] { P("Input.Name", "ABC Medical Supplier"), P("Input.IsActive", "true") })).StatusCode);
        int supplier;
        await using (var db = Factory.NewDb()) supplier = await db.Suppliers.Where(s => s.Name == "ABC Medical Supplier").Select(s => s.Id).SingleAsync();
        var meds = new List<(int Id, string Name, decimal Cost, decimal Sale)>();
        for (var i = 0; i < 23; i++)
        {
            var cost = 2m + i; var sale = Math.Round(cost * 1.4m, 2);
            meds.Add((await PricedMedicineAsync(admin, AcceptanceMedicines[i], cost, sale), AcceptanceMedicines[i], cost, sale));
        }

        // The invoice screen: one header, one continuous table, add-row, new medicine from the invoice, one save button.
        var page = await admin.GetHtmlAsync("/Pharmacy/Receive");
        foreach (var marker in new[] { "data-invoice-lines", "data-add-row", "data-new-medicine-open", "Save &amp; receive complete invoice", "data-test=\"invoice-number\"" })
            Assert.Contains(marker, page);
        Assert.DoesNotContain("data-receive-lines", page); // the old search-above-the-table entry is gone

        // Existing purchase and sale prices are loaded automatically for every medicine.
        foreach (var m in meds)
        {
            using var doc = JsonDocument.Parse(await admin.GetHtmlAsync("/Pharmacy/Pick?q=" + Uri.EscapeDataString(m.Name)));
            var hit = doc.RootElement.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == m.Id);
            Assert.Equal(m.Cost, hit.GetProperty("cost").GetDecimal());
            Assert.Equal(m.Sale, hit.GetProperty("price").GetDecimal());
        }

        // 23 rows (22 × 13 + 14 = 300 units) + two untouched empty rows, as the screen posts them. The sale price column carries the
        // unchanged medicine price – that is not a price change.
        var exp = Today.AddMonths(18).ToString("yyyy-MM");
        var key = Guid.NewGuid().ToString();
        var f = new List<KeyValuePair<string, string>>
        {
            P("Input.IdempotencyKey", key), P("Input.SupplierId", supplier.ToString()), P("Input.ReferenceNumber", "12322"), P("Input.InvoiceDate", Today.ToString("yyyy-MM-dd")),
        };
        for (var i = 0; i < 25; i++)
        {
            if (i >= 23) { f.Add(P($"Lines[{i}].MedicineId", "0")); f.Add(P($"Lines[{i}].Quantity", "")); continue; }
            var m = meds[i];
            f.AddRange(new[]
            {
                P($"Lines[{i}].MedicineId", m.Id.ToString()), P($"Lines[{i}].MedicineName", m.Name), P($"Lines[{i}].Quantity", (i == 22 ? 14 : 13).ToString()),
                P($"Lines[{i}].Expiry", exp), P($"Lines[{i}].BatchNumber", i % 2 == 0 ? $"B12322-{i + 1}" : ""),
                P($"Lines[{i}].UnitCost", m.Cost.ToString(CultureInfo.InvariantCulture)), P($"Lines[{i}].SalePrice", m.Sale.ToString(CultureInfo.InvariantCulture)),
            });
        }
        var r = await admin.PostFormAsync("/Pharmacy/Receive", f);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Invoice 12322: " + Errors(await r.Content.ReadAsStringAsync()));
        var docUrl = r.Headers.Location!.OriginalString;
        Assert.StartsWith("/Pharmacy/Doc/GRN/", docUrl);
        // A second click on Save never adds stock twice.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Receive", f)).StatusCode);

        await using (var db = Factory.NewDb())
        {
            var receipt = await db.StockReceipts.Include(x => x.Items).SingleAsync();
            Assert.Equal("12322", receipt.ReferenceNumber);
            Assert.Equal(supplier, receipt.SupplierId);
            Assert.Equal(23, receipt.Items.Count);
            Assert.Equal(300, receipt.Items.Sum(i => i.Quantity));
            Assert.Equal(meds.Select(m => m.Id).OrderBy(x => x), receipt.Items.Select(i => i.MedicineId).OrderBy(x => x));
            Assert.Equal(meds.Sum(m => m.Cost * (m == meds[22] ? 14 : 13)), receipt.NetAmount);
            for (var i = 0; i < 23; i++)
            {
                var m = meds[i];
                Assert.Equal(i == 22 ? 14 : 13, await db.StockBalances.Where(s => s.MedicineId == m.Id).SumAsync(s => s.Quantity));
                var med = await db.Medicines.SingleAsync(x => x.Id == m.Id);
                Assert.Equal(m.Sale, med.SalePrice); // sale prices preserved
            }
            var ids = meds.Select(m => m.Id).ToList();
            Assert.Equal(0, await db.MedicinePriceChanges.CountAsync(h => ids.Contains(h.MedicineId) && h.BatchId != null && h.NewSalePrice != h.OldSalePrice));
        }

        // History: search by invoice number, supplier and date; open the complete invoice; print it.
        var hist = await admin.GetHtmlAsync("/Pharmacy/Invoices?q=12322");
        Assert.Contains("data-invoice=\"12322\"", hist);
        Assert.Contains("data-items>23<", hist);
        Assert.Contains("data-qty>300<", hist);
        Assert.Contains("data-invoice=\"12322\"", await admin.GetHtmlAsync($"/Pharmacy/Invoices?supplierId={supplier}"));
        Assert.Contains("data-invoice=\"12322\"", await admin.GetHtmlAsync($"/Pharmacy/Invoices?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}"));
        Assert.DoesNotContain("data-invoice=\"12322\"", await admin.GetHtmlAsync($"/Pharmacy/Invoices?from={Today.AddDays(1):yyyy-MM-dd}"));
        var view = await admin.GetHtmlAsync(docUrl);
        Assert.Contains("data-grn-items>23<", view);
        Assert.Contains("data-grn-qty>300<", view);
        foreach (var m in meds) Assert.Contains(m.Name, view);
        var print = await admin.GetHtmlAsync(docUrl.Replace("/Pharmacy/Doc/GRN/", "/Print/SupplierInvoice/"));
        foreach (var m in meds) Assert.Contains(m.Name, print);
    }

    [Fact]
    public async Task Update12_invoice_rows_are_validated_together_and_a_new_medicine_can_be_created_from_the_invoice()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var a = await PricedMedicineAsync(admin, "Row Check Alpha", 5, 8);
        var b = await PricedMedicineAsync(admin, "Row Check Beta", 6, 9);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Suppliers/Edit", new[] { P("Input.Name", "Row Supplier"), P("Input.IsActive", "true") })).StatusCode);
        int supplier;
        await using (var db = Factory.NewDb()) supplier = await db.Suppliers.Where(s => s.Name == "Row Supplier").Select(s => s.Id).SingleAsync();
        var exp = Today.AddMonths(12).ToString("yyyy-MM");
        KeyValuePair<string, string>[] Head(string inv) => new[] { P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SupplierId", supplier.ToString()), P("Input.ReferenceNumber", inv) };

        // No invoice number with a supplier; a row with a quantity but no medicine; a row without a quantity → nothing is received.
        var noNumber = await admin.PostFormAsync("/Pharmacy/Receive", new[] { P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SupplierId", supplier.ToString()),
            P("Lines[0].MedicineId", a.ToString()), P("Lines[0].Quantity", "5"), P("Lines[0].Expiry", exp) });
        Assert.Contains("invoice number", Errors(await noNumber.Content.ReadAsStringAsync()));
        var bad = await admin.PostFormAsync("/Pharmacy/Receive", Head("R-1").Concat(new[]
        {
            P("Lines[0].MedicineId", a.ToString()), P("Lines[0].MedicineName", "Row Check Alpha"), P("Lines[0].Quantity", "5"), P("Lines[0].Expiry", exp),
            P("Lines[1].MedicineId", "0"), P("Lines[1].MedicineName", "Typo medicine"), P("Lines[1].Quantity", "7"),
            P("Lines[2].MedicineId", b.ToString()), P("Lines[2].MedicineName", "Row Check Beta"), P("Lines[2].Expiry", exp),
        }).ToArray());
        var badText = Errors(await bad.Content.ReadAsStringAsync());
        Assert.Contains("Row 2", badText);
        Assert.Contains("Row 3", badText);
        await using (var db = Factory.NewDb()) Assert.Equal(0, await db.StockReceipts.CountAsync());

        // A medicine missing from the catalogue is created from the invoice (JSON) and can be received in the same invoice.
        var created = await admin.PostFormAsync("/Pharmacy/Receive", new[] { P("Name", "Brandnew Twelve"), P("Unit", "Vial"), P("PurchasePrice", "40"), P("SalePrice", "55") },
            "/Pharmacy/Receive?handler=NewMedicine");
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        var newId = json.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(40m, json.RootElement.GetProperty("cost").GetDecimal());
        var ok = await admin.PostFormAsync("/Pharmacy/Receive", Head("R-2").Concat(new[]
        {
            P("Lines[0].MedicineId", a.ToString()), P("Lines[0].Quantity", "5"), P("Lines[0].Expiry", exp), P("Lines[0].UnitCost", "5"),
            P("Lines[1].MedicineId", newId.ToString()), P("Lines[1].Quantity", "10"), P("Lines[1].Expiry", exp), P("Lines[1].UnitCost", "40"),
        }).ToArray());
        Assert.True(ok.StatusCode == HttpStatusCode.Redirect, "Invoice R-2: " + Errors(await ok.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb()) Assert.Equal(10, await db.StockBalances.Where(s => s.MedicineId == newId).SumAsync(s => s.Quantity));

        // A user without "Add Medicine" cannot create medicines from the invoice.
        var clerk = await StaffAsync(admin, "receiver12", BurhanNexus.HMS.Core.Security.Perm.DashboardView, BurhanNexus.HMS.Core.Security.Perm.PharmReceiveStock);
        Assert.DoesNotContain("data-new-medicine-open", await clerk.GetHtmlAsync("/Pharmacy/Receive"));
        var denied = await clerk.PostFormAsync("/Pharmacy/Receive", new[] { P("Name", "Sneaky Med") }, "/Pharmacy/Receive?handler=NewMedicine");
        Assert.NotEqual(HttpStatusCode.OK, denied.StatusCode);
        await using (var db = Factory.NewDb()) Assert.False(await db.Medicines.AnyAsync(m => m.Name == "Sneaky Med"));
    }
}
