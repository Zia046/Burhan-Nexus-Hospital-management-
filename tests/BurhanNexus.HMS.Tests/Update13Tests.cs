using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 13 – OPD setup with the consultation fee, no default free services, lab New Visit (top 6, today's OPD, live search), purchase-rate batch rule.</summary>
public partial class Update9Tests
{
    [GeneratedRegex("data-frequent=\"1\"")] private static partial Regex Frequent13Regex();

    private async Task<int> DoctorWithFeeAsync(Browser admin, string name, string? fee, bool free = false)
    {
        var f = new List<KeyValuePair<string, string>> { P("DoctorForm.Name", name), P("DoctorForm.IsActive", "true") };
        if (fee is not null) f.Add(P("ConsultationFee", fee));
        if (free) f.Add(P("ConsultationFree", "true"));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Opd/Setup", f, "/Opd/Setup?handler=Doctor")).StatusCode);
        await using var db = Factory.NewDb();
        return await db.Doctors.Where(d => d.Name == name).Select(d => d.Id).SingleAsync();
    }

    private static IEnumerable<KeyValuePair<string, string>> Book13(int doctorId) => new[]
    {
        P("Input.DoctorId", doctorId.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Payment", "Paid"),
    };

    [Fact]
    public async Task Update13_opd_setup_takes_the_consultation_fee_with_the_doctor_and_no_doctor_is_free_by_default()
    {
        await base.SetupAsync("Private", freeUnpricedServices: false);
        var admin = await LoginAdminAsync();

        // OPD Setup: doctor form with the fee, no fee reference table, settings moved to Settings → OPD.
        var setup = await admin.GetHtmlAsync("/Opd/Setup");
        Assert.Contains("data-test=\"doctor-fee\"", setup);
        Assert.Contains("data-test=\"doctor-free\"", setup);
        Assert.DoesNotContain("ServiceForm.Code", setup);
        Assert.Contains("/Settings/Opd", setup);
        var settings = await admin.GetHtmlAsync("/Settings/Opd");
        Assert.Contains("OpdForm", settings);

        var paid = await DoctorWithFeeAsync(admin, "Dr. Fee Thirteen", "1200");
        var free = await DoctorWithFeeAsync(admin, "Dr. Free Thirteen", null, free: true);
        var none = await DoctorWithFeeAsync(admin, "Dr. Nofee Thirteen", null);
        // A fee of 0 without FREE is refused (nothing saved).
        await admin.PostFormAsync("/Opd/Setup", new[] { P("DoctorForm.Name", "Dr. Zero Thirteen"), P("DoctorForm.IsActive", "true"), P("ConsultationFee", "0") }, "/Opd/Setup?handler=Doctor");
        Assert.Contains("not allowed", await admin.GetHtmlAsync("/Opd/Setup"));
        await using (var db = Factory.NewDb())
        {
            var con = await db.ServiceItems.Where(s => s.Kind == ServiceKind.Consultation && s.DoctorId != null).ToListAsync();
            var p = con.Single(s => s.DoctorId == paid);
            Assert.Equal(1200m, p.DefaultFee);
            Assert.False(p.IsFree);
            Assert.True(con.Single(s => s.DoctorId == free).IsFree);
            Assert.DoesNotContain(con, s => s.DoctorId == none);
            Assert.False(await db.Doctors.AnyAsync(d => d.Name == "Dr. Zero Thirteen"));
        }
        setup = await admin.GetHtmlAsync("/Opd/Setup");
        Assert.Contains("1,200.00", setup);
        Assert.Contains("FREE", setup);
        Assert.Contains("Fee not set", setup);

        var pid = await RegisterPatientAsync(admin, PatientFields("Fee Thirteen Patient", "Father Fee"));
        var refused = await admin.PostFormAsync($"/Appointments/Book?patientId={pid}", Book13(none));
        Assert.NotEqual(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Contains("consultation fee of Dr. Nofee Thirteen is not set", Errors(await refused.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Appointments/Book?patientId={pid}", Book13(free))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Appointments/Book?patientId={pid}", Book13(paid))).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var appts = await db.Appointments.AsNoTracking().ToListAsync();
            Assert.Equal(2, appts.Count);
            Assert.Contains(appts, a => a.DoctorId == free && a.PaymentStatus == AppointmentPayment.Free);
            Assert.Contains(appts, a => a.DoctorId == paid && a.PaymentStatus != AppointmentPayment.Free);
        }

        // Editing the fee later updates the same consultation service (no duplicates).
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Opd/Setup", new[]
            { P("DoctorForm.Name", "Dr. Fee Thirteen"), P("DoctorForm.IsActive", "true"), P("ConsultationFee", "1500") }, $"/Opd/Setup?handler=Doctor&id={paid}")).StatusCode);
        await using (var db = Factory.NewDb())
            Assert.Equal(1500m, (await db.ServiceItems.SingleAsync(s => s.Kind == ServiceKind.Consultation && s.DoctorId == paid)).DefaultFee);
    }

    [Fact]
    public async Task Update13_unpriced_lab_test_cannot_be_billed_until_priced_or_marked_free()
    {
        await base.SetupAsync("Private", freeUnpricedServices: false);
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Lab Fee Thirteen", "Father Lab"));
        int fbs, svc;
        await using (var db = Factory.NewDb())
        {
            var t = await db.LabTests.SingleAsync(x => x.Code == "FBS");
            fbs = t.Id; svc = t.ServiceItemId;
            Assert.False((await db.ServiceItems.SingleAsync(s => s.Id == svc)).IsFree); // standard catalogue: "fee not set", not free
        }
        Assert.Contains("Fee not set", await admin.GetHtmlAsync("/Lab/Catalogue"));
        IEnumerable<KeyValuePair<string, string>> Fields() => new[]
            { P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("PatientId", pid.ToString()), P("TestIds", fbs.ToString()), P("Input.ReferralType", "None") };
        var refused = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", Fields());
        Assert.NotEqual(HttpStatusCode.Redirect, refused.StatusCode);
        Assert.Contains("is not set yet", Errors(await refused.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb()) Assert.Equal(0, await db.LabOrders.CountAsync());

        // The administrator marks it FREE explicitly → it can be booked at 0.
        await using (var db = Factory.NewDb()) await db.Database.ExecuteSqlRawAsync($"UPDATE \"ServiceItems\" SET \"IsFree\" = TRUE WHERE \"Id\" = {svc}");
        var ok = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", Fields());
        Assert.True(ok.StatusCode == HttpStatusCode.Redirect, "Free test booking: " + Errors(await ok.Content.ReadAsStringAsync()));
        Assert.Contains("FREE", await admin.GetHtmlAsync("/Lab/Catalogue"));
    }

    [Fact]
    public async Task Update13_lab_new_visit_top_6_tests_payment_below_today_opd_patients_and_live_suggestions()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var a = await RegisterPatientAsync(admin, PatientFields("Zarghuna Thirteen", "Father Zar", "Female"));
        var b = await RegisterPatientAsync(admin, PatientFields("Zarmina Thirteen", "Father Zmn", "Female"));
        string numA;
        await using (var db = Factory.NewDb()) numA = await db.Patients.Where(p => p.Id == a).Select(p => p.PatientNumber).SingleAsync();

        var book = await admin.GetHtmlAsync($"/Lab/Book?patientId={a}");
        Assert.Equal(6, Frequent13Regex().Matches(book).Count);           // only the top 6 are shown
        Assert.Contains("data-test-filter", book);                         // others stay searchable
        var picker = book.IndexOf("data-no-match", StringComparison.Ordinal);
        var payment = book.IndexOf("data-lab-payment", StringComparison.Ordinal);
        Assert.True(picker > 0 && payment > picker, "payment section directly below the test suggestions");
        Assert.True(payment < book.IndexOf("Input_ReferralType", StringComparison.Ordinal), "payment section comes before the referral fields");

        var start = await admin.GetHtmlAsync("/Lab/Book");
        Assert.Contains("data-today-opd", start);
        Assert.Contains($"data-today-patient=\"{numA}\"", start);
        Assert.Contains("Zarmina Thirteen", start);
        Assert.Contains("data-patient-suggest", start);

        using var doc = JsonDocument.Parse(await admin.GetHtmlAsync("/Lab/Book?handler=Suggest&q=Zargh"));
        var hits = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(hits);
        Assert.Equal(a, hits[0].GetProperty("id").GetInt64());
        Assert.Equal(numA, hits[0].GetProperty("number").GetString());
        using var both = JsonDocument.Parse(await admin.GetHtmlAsync("/Lab/Book?handler=Suggest&q=Zar"));
        Assert.Equal(2, both.RootElement.GetArrayLength());
        using var today = JsonDocument.Parse(await admin.GetHtmlAsync("/Lab/Book?handler=Suggest&q="));
        Assert.Contains(today.RootElement.EnumerateArray(), e => e.GetProperty("id").GetInt64() == b);
        Assert.Contains("Zarghuna Thirteen", await admin.GetHtmlAsync($"/Lab/Book?patientId={a}"));
    }

    [Fact]
    public async Task Update13_a_different_purchase_rate_needs_its_own_batch_and_rates_are_never_mixed()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Suppliers/Edit", new[] { P("Input.Name", "Rate Supplier"), P("Input.IsActive", "true") })).StatusCode);
        int supplier;
        await using (var db = Factory.NewDb()) supplier = await db.Suppliers.Where(s => s.Name == "Rate Supplier").Select(s => s.Id).SingleAsync();
        var med = await PricedMedicineAsync(admin, "Tab. Rate Thirteen", 5, 8);
        var exp = Today.AddMonths(18).ToString("yyyy-MM");
        Task<HttpResponseMessage> Receive(string batch, string cost, string reference) => admin.PostFormAsync("/Pharmacy/Receive", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SupplierId", supplier.ToString()), P("Input.ReferenceNumber", reference),
            P("Lines[0].MedicineId", med.ToString()), P("Lines[0].Quantity", "10"), P("Lines[0].Expiry", exp), P("Lines[0].BatchNumber", batch), P("Lines[0].UnitCost", cost),
        });

        // Same rate: batch number optional.
        var same = await Receive("", "5", "R13-1");
        Assert.True(same.StatusCode == HttpStatusCode.Redirect, "same rate: " + Errors(await same.Content.ReadAsStringAsync()));
        // Different rate without a batch number: refused with a clear message.
        var diff = await Receive("", "6", "R13-2");
        Assert.NotEqual(HttpStatusCode.Redirect, diff.StatusCode);
        var msg = Errors(await diff.Content.ReadAsStringAsync());
        Assert.Contains("differs from the current purchase rate", msg);
        Assert.Contains("separate batch number", msg);
        // Different rate with its own batch number: accepted as a separate batch.
        var own = await Receive("NR-6", "6", "R13-3");
        Assert.True(own.StatusCode == HttpStatusCode.Redirect, "own batch: " + Errors(await own.Content.ReadAsStringAsync()));
        // The same batch number at yet another rate is refused – a batch never mixes rates.
        var mixed = await Receive("NR-6", "7", "R13-4");
        Assert.NotEqual(HttpStatusCode.Redirect, mixed.StatusCode);
        Assert.Contains("needs a separate batch number", Errors(await mixed.Content.ReadAsStringAsync()));

        await using (var db = Factory.NewDb())
        {
            var batches = await db.MedicineBatches.Where(x => x.MedicineId == med).OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(2, batches.Count);
            Assert.Equal(5m, batches[0].UnitCost);
            Assert.Equal(6m, batches[1].UnitCost);
            Assert.Equal(2, await db.StockReceipts.CountAsync());
            var stock = await db.StockBalances.Where(s => s.MedicineId == med).ToListAsync();
            Assert.Equal(20, stock.Sum(s => s.Quantity));
            // Valuation stays exact per batch: 10 × 5 + 10 × 6.
            var value = stock.Sum(s => s.Quantity * batches.Single(x => x.Id == s.BatchId).UnitCost!.Value);
            Assert.Equal(110m, value);
        }
        Assert.Contains("needs its own batch number", await admin.GetHtmlAsync("/Pharmacy/Receive"));
    }
}
