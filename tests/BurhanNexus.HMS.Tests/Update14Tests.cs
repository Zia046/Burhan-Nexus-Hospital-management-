using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>
/// Update 14 – thermal lab / registration receipts, one visit discount on the lab booking, single admission form without signatures,
/// ward kept after saving beds, admission fee at admission, NICU admission weight in kg. (Part of Update10Tests to reuse its helpers.)
/// </summary>
public partial class Update10Tests
{
    [Fact]
    public void Visit_discount_is_spread_exactly_over_the_tests()
    {
        Assert.Equal(new[] { 80m, 120m }, LabService.SpreadDiscount(200m, new[] { 400m, 600m }));
        var odd = LabService.SpreadDiscount(100m, new[] { 300m, 300m, 300m });
        Assert.Equal(100m, odd.Sum());
        Assert.All(odd, d => Assert.Equal(d, decimal.Round(d, 2)));
        Assert.Equal(new[] { 400m, 600m }, LabService.SpreadDiscount(1000m, new[] { 400m, 600m })); // 100 % free
        Assert.Equal(new[] { 0m, 0m }, LabService.SpreadDiscount(0m, new[] { 400m, 600m }));
        var small = LabService.SpreadDiscount(0.05m, new[] { 1m, 1m, 1m });
        Assert.Equal(0.05m, small.Sum());
    }

    [Fact]
    public async Task Lab_booking_takes_one_discount_with_separate_cash_received_and_the_receipt_is_thermal()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        await SetFeeAsync(admin, "LT-FBS", 400);
        await SetFeeAsync(admin, "LT-HBSAG", 600);
        int fbs, hbs;
        await using (var db = Factory.NewDb())
        {
            fbs = await db.LabTests.Where(t => t.Code == "FBS").Select(t => t.Id).SingleAsync();
            hbs = await db.LabTests.Where(t => t.Code == "HBSAG").Select(t => t.Id).SingleAsync();
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Discount Lab Patient", "Father Disc"));
        var page = await admin.GetHtmlAsync($"/Lab/Book?patientId={pid}");
        Assert.Contains("name=\"Input.TotalDiscount\"", page);       // dedicated discount box
        Assert.Contains("data-lab-net", page);                       // net payable
        Assert.Contains("Cash received", page);                      // separate cash box
        Assert.DoesNotContain("name=\"Discounts[", page);             // no per-test boxes any more
        var cash = (await CashAsync()).ToString();
        KeyValuePair<string, string>[] Booking(string discount, string received, string reason = "") => new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("PatientId", pid.ToString()), P("TestIds", fbs.ToString()), P("TestIds", hbs.ToString()),
            P("Input.ReferralType", "None"), P("Input.CollectPayment", "true"), P("Input.PaymentMethodId", cash),
            P("Input.TotalDiscount", discount), P("Input.DiscountReason", reason), P("Input.AmountReceived", received),
            P("Discounts[" + fbs + "]", ""), // an empty old-style box no longer gives "The value '' is invalid"
        };

        // More discount than the total → refused, nothing booked.
        var tooMuch = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", Booking("1200", ""));
        Assert.Equal(HttpStatusCode.OK, tooMuch.StatusCode);
        Assert.Contains("more than the total", Errors(await tooMuch.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb()) Assert.Equal(0, await db.LabOrders.CountAsync());

        // Total 1000 − discount 200 = net 800, cash 800 – the reason is optional; the "value '' is invalid" error is gone.
        var r = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", Booking("200", "800"));
        var body = r.StatusCode == HttpStatusCode.Redirect ? "" : await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Lab booking with discount: " + Errors(body));
        Assert.DoesNotContain("is invalid", body);
        var orderId = long.Parse(LabOrderRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);
        await using (var db = Factory.NewDb())
        {
            var charges = await db.Charges.Where(c => c.LabOrderId == orderId).OrderBy(c => c.Fee).ToListAsync();
            Assert.Equal(new[] { 400m, 600m }, charges.Select(c => c.Fee));
            Assert.Equal(new[] { 80m, 120m }, charges.Select(c => c.Discount));     // saved in the database
            Assert.Equal(800m, charges.Sum(c => c.NetAmount));
            Assert.All(charges, c => Assert.Equal(ChargeStatus.Paid, c.Status));
            Assert.Equal(800m, (await db.Receipts.SingleAsync()).AmountPaid);
        }

        // Receipt: thermal roll page only (no A4 stylesheet), shows the discount and the paid amount; the visit page prints it directly.
        var receipt = await admin.GetHtmlAsync($"/Print/LabReceipt/{orderId}");
        Assert.Contains("thermal-body w80", receipt);
        Assert.DoesNotContain("lab-report.css", receipt);
        Assert.Contains("Discount", receipt);
        Assert.Contains("200.00", receipt);
        Assert.Contains("800.00", receipt);
        Assert.Contains("data-autoprint=\"1\"", await admin.GetHtmlAsync($"/Print/LabReceipt/{orderId}?autoprint=true"));
        Assert.Contains($"/Print/LabReceipt/{orderId}?autoprint=true", await admin.GetHtmlAsync($"/Lab/Order/{orderId}"));
        Assert.Contains("thermal-body w58", await admin.GetHtmlAsync($"/Print/LabReceipt/{orderId}?width=58"));

        // Without the discount permission the discount box is not shown and a posted discount is refused.
        var clerk = await UserWithAsync(admin, "labclerk14", Core.Security.Perm.LabBook, Core.Security.Perm.LabViewFinancial, Core.Security.Perm.LabCollectPayments);
        Assert.DoesNotContain("Input.TotalDiscount", await clerk.GetHtmlAsync($"/Lab/Book?patientId={pid}"));
        var denied = await clerk.PostFormAsync($"/Lab/Book?patientId={pid}", Booking("100", ""));
        Assert.Contains("not allowed to give discounts", Errors(await denied.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Registration_prints_a_thermal_receipt_and_the_A4_A5_slip_stays_available()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Thermal Receipt Patient", "Gul Khan"));
        var r = await admin.PostFormAsync("/Patients/Register", new[] { P("Visit.ChiefComplaint", "Fever") }, $"/Opd/Register?patientId={pid}");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var location = r.Headers.Location!.OriginalString;
        Assert.StartsWith("/Print/Registration/", location);
        var html = await admin.GetHtmlAsync(location);
        Assert.Contains("thermal-body w80", html);
        Assert.Contains("REGISTRATION RECEIPT", html);
        Assert.Contains("Thermal Receipt Patient", html);
        Assert.DoesNotContain("@page", html);
        Assert.DoesNotContain("A4 portrait", html);
        var visit = long.Parse(Regex.Match(location, @"/Print/Registration/(\d+)").Groups[1].Value);
        Assert.Contains($"/Print/Opd/{visit}", html);                                  // the doctor's A5 / A4 slip is one click away
        Assert.Contains($"/Print/Registration/{visit}", await admin.GetHtmlAsync($"/Print/Opd/{visit}"));
        Assert.Contains("thermal-body w58", await admin.GetHtmlAsync($"/Print/Registration/{visit}?width=58"));

        var er = await PostEmergencyAsync(admin, pid, ("Visit.BriefComplaint", "Chest pain"));
        Assert.Equal(HttpStatusCode.Redirect, er.StatusCode);
        var erHtml = await admin.GetHtmlAsync(er.Headers.Location!.OriginalString);
        Assert.Contains("EMERGENCY REGISTRATION", erHtml);
        Assert.Contains("thermal-body", erHtml);
    }

    [Fact]
    public async Task Admission_form_is_one_copy_without_signatures_and_shows_the_fee_entered_at_admission()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var bed = await BedAsync(admin, "MGW", "MGW-14");
        var pid = await RegisterPatientAsync(admin, PatientFields("Admission Fee Patient", "Father Adm"));
        // No admission fee configured in Fees & Services – the fee box is still shown and the fee can be entered.
        var admit = await admin.GetHtmlAsync($"/Ipd/Admit?patientId={pid}");
        Assert.Contains("name=\"Input.AdmissionFeeAmount\"", admit);
        var adm = await AdmitAsync(admin, pid, bed, ("Input.AdmissionFeeAmount", "1500"));
        await using (var db = Factory.NewDb())
        {
            var a = await db.IpdAdmissions.SingleAsync(x => x.Id == adm);
            Assert.NotNull(a.AdmissionFeeChargeId);
            var fee = await db.Charges.SingleAsync(c => c.Id == a.AdmissionFeeChargeId);
            Assert.Equal((1500m, 0m, 1500m), (fee.Fee, fee.Discount, fee.NetAmount));
            Assert.Equal("SVC-IPD-ADM", (await db.ServiceItems.SingleAsync(s => s.Id == fee.ServiceItemId)).Code);
            Assert.Equal(1, await db.Charges.CountAsync(c => c.IpdAdmissionId == adm && c.ServiceItemId == fee.ServiceItemId)); // its own charge, not mixed
        }
        var form = await admin.GetHtmlAsync($"/Print/Ipd/admission/{adm}");
        Assert.Single(Regex.Matches(form, "data-form-page"));
        Assert.DoesNotContain("PATIENT / ATTENDANT COPY", form);
        Assert.DoesNotContain("f-sign-row", form);
        Assert.Contains("data-admission-fee", form);
        Assert.Contains("1,500.00", form);

        // A configured fee is pre-filled and can be changed at admission; the changed amount is what is saved.
        var admFee = await SvcAsync(admin, "SVC-IPD-ADM", 1000);
        await using (var db = Factory.NewDb()) { (await db.IpdSettings.SingleAsync()).AdmissionServiceItemId = admFee; await db.SaveChangesAsync(); }
        var pid2 = await RegisterPatientAsync(admin, PatientFields("Admission Fee Patient Two", "Father Two"));
        Assert.Contains("value=\"1000\"", await admin.GetHtmlAsync($"/Ipd/Admit?patientId={pid2}"));
        var adm2 = await AdmitAsync(admin, pid2, null, ("Input.AdmissionFeeAmount", "750.50"), ("FeeAction", "pay"), ("FeeMethodId", (await CashAsync()).ToString()));
        await using (var db = Factory.NewDb())
        {
            var a = await db.IpdAdmissions.SingleAsync(x => x.Id == adm2);
            var fee = await db.Charges.SingleAsync(c => c.Id == a.AdmissionFeeChargeId);
            Assert.Equal((750.50m, 750.50m, ChargeStatus.Paid), (fee.Fee, fee.PaidAmount, fee.Status));
        }
        Assert.Contains("data-fee-status><strong>PAID", await admin.GetHtmlAsync($"/Print/Ipd/admission/{adm2}"));
        // Negative fee refused.
        var pid3 = await RegisterPatientAsync(admin, PatientFields("Admission Fee Patient Three", "Father Three"));
        var bad = await admin.PostFormAsync($"/Ipd/Admit?patientId={pid3}", new[]
        {
            P("PatientId", pid3.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"),
            P("Input.AdmittingDoctorName", "Dr. Ward"), P("Input.AdmissionDiagnosis", "Fever"), P("Input.AdmissionFeeAmount", "-5"),
        });
        Assert.Contains("valid admission fee", Errors(await bad.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Saving_a_bed_keeps_the_selected_ward_and_room()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        int room, ward;
        await using (var db = Factory.NewDb())
        {
            var nicuRoom = await db.IpdRooms.Where(r => r.Ward!.Code == "NICU").OrderBy(r => r.Id).FirstAsync();
            (room, ward) = (nicuRoom.Id, nicuRoom.WardId);
        }
        var r = await admin.PostFormAsync("/Ipd/Wards", new[] { P("Bed.RoomId", room.ToString()), P("Bed.Code", "NICU-K1"), P("Bed.Type", "Incubator"), P("Bed.IsActive", "true") }, "/Ipd/Wards?handler=Bed");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var location = r.Headers.Location!.OriginalString;
        Assert.Contains($"inroom={room}", location);
        var html = await admin.GetHtmlAsync(location);
        Assert.Matches($"<option value=\"{room}\" selected=\"selected\">", html);       // the bed form stays on the NICU room
        Assert.Matches($"<option value=\"{ward}\" selected=\"selected\">", html);       // the bed list shows that ward
        Assert.Contains("NICU-K1", html);
        Assert.DoesNotContain("data-room-row=\"" + await FirstRoomOfOtherWardAsync(ward) + "\"", html);
        // Deactivating ("removing") a bed also returns to the same room.
        int bedId;
        await using (var db = Factory.NewDb()) bedId = await db.IpdBeds.Where(b => b.Code == "NICU-K1").Select(b => b.Id).SingleAsync();
        var off = await admin.PostFormAsync($"/Ipd/Wards?bed={bedId}", new[] { P("Bed.RoomId", room.ToString()), P("Bed.Code", "NICU-K1"), P("Bed.Type", "Incubator"), P("Bed.IsActive", "false") },
            $"/Ipd/Wards?handler=Bed&bed={bedId}");
        Assert.Equal(HttpStatusCode.Redirect, off.StatusCode);
        Assert.Contains($"inroom={room}", off.Headers.Location!.OriginalString);
    }

    private async Task<int> FirstRoomOfOtherWardAsync(int ward)
    {
        await using var db = Factory.NewDb();
        return await db.IpdRooms.Where(r => r.WardId != ward).OrderBy(r => r.Id).Select(r => r.Id).FirstAsync();
    }

    [Fact]
    public async Task Nicu_admission_weight_is_entered_and_shown_in_kg_with_decimals()
    {
        Assert.Equal("3.25 kg", BurhanNexus.HMS.Web.Services.DisplayFormatter.Kg(3250));
        Assert.Equal("0.8 kg", BurhanNexus.HMS.Web.Services.DisplayFormatter.Kg(800));
        Assert.Equal("3.4 kg", BurhanNexus.HMS.Web.Services.DisplayFormatter.Kg(3400));
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var page = await admin.GetHtmlAsync("/Ipd/Nicu/Admit?patientId=" + await RegisterPatientAsync(admin, PatientFields("Baby Of Test", "Father Baby")));
        Assert.Contains("Admission weight (kg)", page);
        Assert.DoesNotContain("Admission weight (g)", page);
        var i = 0;
        foreach (var (kg, grams) in new[] { ("3.4", 3400), ("2.9", 2900), ("1.5", 1500), ("0.8", 800), ("3.25", 3250) })
        {
            i++;
            var cot = await BedAsync(admin, "NICU", $"NICU-W{i}");
            var baby = await RegisterPatientAsync(admin, PatientFields($"Baby Weight {i}", $"Father Weight {i}"));
            var r = await admin.PostFormAsync($"/Ipd/Nicu/Admit?patientId={baby}", new[]
            {
                P("PatientId", baby.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Source", "ExternalReferral"), P("Input.BedId", cot.ToString()),
                P("Input.Reason", "Low birth weight"), P("Input.AdmissionWeightKg", kg),
            });
            Assert.True(r.StatusCode == HttpStatusCode.Redirect, $"NICU {kg} kg: " + Errors(await r.Content.ReadAsStringAsync()));
            var adm = long.Parse(AdmissionRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);
            await using (var db = Factory.NewDb()) Assert.Equal(grams, (await db.NicuDetails.SingleAsync(n => n.AdmissionId == adm)).AdmissionWeightGrams); // exact, no rounding
            Assert.Contains($"{kg} kg", await admin.GetHtmlAsync($"/Ipd/Admission/{adm}?tab=nicu"));
            Assert.Contains($"{kg} kg", await admin.GetHtmlAsync($"/Print/Ipd/nicu-admission/{adm}"));
            Assert.Contains($"admission weight {kg} kg", await admin.GetHtmlAsync($"/Print/Ipd/nicu-discharge/{adm}"));
        }
        // More than 3 decimals would need rounding → refused instead of rounded.
        var cotX = await BedAsync(admin, "NICU", "NICU-WX");
        var babyX = await RegisterPatientAsync(admin, PatientFields("Baby Weight X", "Father Weight X"));
        var rx = await admin.PostFormAsync($"/Ipd/Nicu/Admit?patientId={babyX}", new[]
        {
            P("PatientId", babyX.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Source", "ExternalReferral"), P("Input.BedId", cotX.ToString()),
            P("Input.Reason", "Observation"), P("Input.AdmissionWeightKg", "3.2555"),
        });
        Assert.Contains("at most 3 decimals", Errors(await rx.Content.ReadAsStringAsync()));
    }
}
