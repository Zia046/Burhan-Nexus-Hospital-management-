using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Pharmacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>
/// Update 17 – one HMS with a configurable pharmacy: Free / Paid / Subsidy / Discount issue modes (Medicine Issue Configuration),
/// separate permissions, free-issue approval, optional stock classification and issue-mode reports. Doctors list with "Add Doctor",
/// New Appointment opens registration with the patient search at the top.
/// (Part of PharmacyTests to reuse its helpers.)
/// </summary>
public partial class PharmacyTests
{
    private async Task ConfigureAsync(Action<PharmacySettings> change)
    {
        await using var db = Factory.NewDb();
        var s = await db.PharmacySettings.SingleAsync();
        change(s);
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> IssueModeAsync(Browser b, long patientId, long? encounterId, string? mode, IEnumerable<KeyValuePair<string, string>>? extra,
        params (int Med, int Qty, long? Batch, decimal Discount)[] lines)
    {
        var f = LineFields(lines);
        f.Add(P("Input.IdempotencyKey", Guid.NewGuid().ToString()));
        f.Add(P("Input.PatientId", patientId.ToString()));
        if (encounterId is long e) f.Add(P("Input.EncounterId", e.ToString()));
        if (mode is not null) f.Add(P("Input.Mode", mode));
        if (lines.Any(l => l.Discount != 0)) f.Add(P("Input.DiscountReason", "Staff family"));
        if (extra is not null) f.AddRange(extra);
        return await b.PostFormAsync($"/Pharmacy/Issue?patientId={patientId}", f, "/Pharmacy/Issue");
    }

    private async Task<PharmacyReport> PharmReportAsync(string key)
    {
        using var scope = Factory.Services.CreateScope();
        var q = scope.ServiceProvider.GetRequiredService<IPharmacyQueryService>();
        return await q.ReportAsync(key, new PharmacyReportFilter { From = Today.AddDays(-1), To = Today.AddDays(1) }, true);
    }

    private static decimal Col(PharmacyReport r, object?[] row, string column) => Convert.ToDecimal(row[r.Columns.ToList().IndexOf(column)] ?? 0m);

    [Fact]
    public async Task Medicine_issue_configuration_starts_from_the_stored_type_and_is_saved_in_pharmacy_settings()
    {
        await SetupAsync("Government");
        await using (var db = Factory.NewDb())
        {
            var s = await db.PharmacySettings.SingleAsync();
            Assert.True(s.FreeIssueEnabled);
            Assert.False(s.PaidSaleEnabled);
            Assert.Equal(MedicineIssueMode.Free, s.DefaultIssueMode);
        }
        var admin = await LoginAdminAsync();
        var page = await admin.GetHtmlAsync("/Pharmacy/Settings");
        Assert.Contains("Medicine Issue Configuration", page);
        Assert.DoesNotContain("Government hospital", page);
        uint rv;
        await using (var db = Factory.NewDb()) rv = (await db.PharmacySettings.SingleAsync()).RowVersion;
        var r = await admin.PostFormAsync("/Pharmacy/Settings", new[]
        {
            P("RowVersion", rv.ToString()), P("Input.NearExpiryDays", "90"), P("Input.EncounterValidDays", "7"), P("Input.IssueConfigPosted", "true"),
            P("Input.FreeIssueEnabled", "true"), P("Input.PaidSaleEnabled", "true"), P("Input.SubsidyEnabled", "true"), P("Input.DiscountEnabled", "false"),
            P("Input.FreeIssueApprovalRequired", "true"), P("Input.AllowChangeIssueMode", "true"), P("Input.StockClassificationEnabled", "false"), P("Input.DefaultIssueMode", "Free"),
        });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Settings: " + Errors(await r.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb())
        {
            var s = await db.PharmacySettings.SingleAsync();
            Assert.True(s.FreeIssueEnabled && s.PaidSaleEnabled && s.SubsidyEnabled && !s.DiscountEnabled && s.FreeIssueApprovalRequired);
            Assert.Equal(MedicineIssueMode.Free, s.DefaultIssueMode);
        }
        // A default mode that is disabled is refused.
        await using (var db = Factory.NewDb()) rv = (await db.PharmacySettings.SingleAsync()).RowVersion;
        var bad = await admin.PostFormAsync("/Pharmacy/Settings", new[]
        {
            P("RowVersion", rv.ToString()), P("Input.NearExpiryDays", "90"), P("Input.EncounterValidDays", "7"), P("Input.IssueConfigPosted", "true"),
            P("Input.FreeIssueEnabled", "true"), P("Input.DefaultIssueMode", "Paid"),
        });
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("default issue mode must be one of the enabled modes", await bad.Content.ReadAsStringAsync());
        // Retail sale is available once Paid Sale is enabled (no hospital type involved).
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Pharmacy/Sale")).StatusCode);
    }

    [Fact]
    public async Task Free_paid_subsidy_and_discount_in_one_hospital_with_correct_stock_and_money()
    {
        await SetupAsync("Private");
        await ConfigureAsync(s => { s.FreeIssueEnabled = true; s.SubsidyEnabled = true; s.DefaultIssueMode = MedicineIssueMode.Paid; s.AllowChangeIssueMode = true; });
        var admin = await LoginAdminAsync();
        var med = await MedicineAsync(admin, "Amoxicillin", sale: 10, cost: 6);
        await IdAsync(await ReceiveAsync(admin, null, "Local supplier", null, "INV-17", (med, "AMX1", Month(12), 200, 6m)), "Receiving");
        var pid = await RegisterPatientAsync(admin, PatientFields("Mode Patient", "Mode Father"));
        var enc = await RegisterOpdAsync(admin, pid);

        // The issue page shows the enabled modes, the default first and selected.
        var page = await admin.GetHtmlAsync($"/Pharmacy/Issue?patientId={pid}");
        Assert.Contains("data-issue-mode-choice=\"Free\"", page);
        Assert.Contains("data-issue-mode-choice=\"Subsidized\"", page);
        Assert.Contains("data-mode-label>Paid Sale", page);

        // FREE: a reason is required when free is not the default; stock goes down, no charge, value kept for reports.
        var noReason = await IssueModeAsync(admin, pid, enc, "Free", null, (med, 5, null, 0));
        Assert.Equal(HttpStatusCode.OK, noReason.StatusCode);
        Assert.Contains("reason for the free issue", await noReason.Content.ReadAsStringAsync());
        var free = await IdAsync(await IssueModeAsync(admin, pid, enc, "Free", new[] { P("Input.FreeReason", "Deserving patient") }, (med, 5, null, 0)), "Free issue");
        Assert.Equal(195, await StockAsync("MAIN", med));
        await using (var db = Factory.NewDb())
        {
            var i = await db.PatientMedicineIssues.SingleAsync(x => x.Id == free);
            Assert.Equal((MedicineIssueMode.Free, 50m, (decimal?)null, 0m), (i.IssueMode!.Value, i.TotalValue!.Value, i.NetAmount, i.PatientAmount!.Value));
            Assert.Equal(0, await db.Charges.CountAsync(c => c.PharmacyIssueId == free));
        }
        Assert.Contains("FREE MEDICINE ISSUE SLIP", await admin.GetHtmlAsync($"/Print/Pharmacy/ISS/{free}"));

        // PAID (default): one unpaid charge per line.
        var paid = await IdAsync(await IssueModeAsync(admin, pid, enc, null, null, (med, 10, null, 0)), "Paid issue");
        await using (var db = Factory.NewDb())
            Assert.Equal(100m, await db.Charges.Where(c => c.PharmacyIssueId == paid).SumAsync(c => c.NetAmount));

        // SUBSIDY: value 100, patient pays 70, subsidy 30 – all three kept.
        var sub = await IdAsync(await IssueModeAsync(admin, pid, enc, "Subsidized", new[] { P("Input.SubsidyAmount", "30"), P("Input.SubsidyReason", "Zakat fund") }, (med, 10, null, 0)), "Subsidy");
        await using (var db = Factory.NewDb())
        {
            var i = await db.PatientMedicineIssues.SingleAsync(x => x.Id == sub);
            Assert.Equal((100m, 30m, 70m), (i.TotalValue!.Value, i.SubsidyAmount!.Value, i.NetAmount!.Value));
            var charges = await db.Charges.Where(c => c.PharmacyIssueId == sub).ToListAsync();
            Assert.Equal(70m, charges.Sum(c => c.NetAmount));
            Assert.Equal(100m, charges.Sum(c => c.Fee));
        }
        var tooMuch = await IssueModeAsync(admin, pid, enc, "Subsidized", new[] { P("Input.SubsidyAmount", "500") }, (med, 1, null, 0));
        Assert.Contains("subsidy cannot be more", await tooMuch.Content.ReadAsStringAsync());

        // DISCOUNT: original price kept, discount and final amount recorded.
        var disc = await IdAsync(await IssueModeAsync(admin, pid, enc, null, null, (med, 10, null, 5)), "Discount");
        await using (var db = Factory.NewDb())
        {
            var i = await db.PatientMedicineIssues.SingleAsync(x => x.Id == disc);
            Assert.Equal((MedicineIssueMode.Discounted, 100m, 5m, 95m), (i.IssueMode!.Value, i.GrossAmount!.Value, i.DiscountAmount!.Value, i.NetAmount!.Value));
        }

        // A disabled mode cannot be used, also not by direct posting.
        await ConfigureAsync(s => s.SubsidyEnabled = false);
        var disabled = await IssueModeAsync(admin, pid, enc, "Subsidized", new[] { P("Input.SubsidyAmount", "10") }, (med, 1, null, 0));
        Assert.Contains("disabled in the Medicine Issue Configuration", await disabled.Content.ReadAsStringAsync());
        Assert.Equal(165, await StockAsync("MAIN", med));

        // Reports keep free value, paid sales, subsidy and discounts apart; free is never revenue.
        var daily = await PharmReportAsync("pharmacy-daily");
        var total = daily.Totals!;
        Assert.Equal(350m, Col(daily, total, "Total medicine value"));
        Assert.Equal(50m, Col(daily, total, "Free medicine value"));
        Assert.Equal(265m, Col(daily, total, "Paid sales")); // 100 + 70 + 95
        Assert.Equal(30m, Col(daily, total, "Subsidy"));
        Assert.Equal(5m, Col(daily, total, "Discounts"));
        var freeReport = await PharmReportAsync("free-issues");
        Assert.Single(freeReport.Rows);
        Assert.Single((await PharmReportAsync("subsidy-issues")).Rows);
        Assert.Single((await PharmReportAsync("discounts")).Rows);
        Assert.Contains("data-issue-mode=\"Subsidized\"", await admin.GetHtmlAsync($"/Pharmacy/Doc/ISS/{sub}"));
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Mode_rights_change_rights_and_free_issue_approval_are_enforced_on_the_server()
    {
        await SetupAsync("Government");
        await ConfigureAsync(s => { s.PaidSaleEnabled = true; s.FreeIssueApprovalRequired = true; s.AllowChangeIssueMode = true; });
        var admin = await LoginAdminAsync();
        var med = await MedicineAsync(admin, "Metformin");
        await IdAsync(await ReceiveAsync(admin, null, "District Health Office", null, "DHO-17", (med, "MF1", Month(12), 100, null)), "Receiving");
        var pid = await RegisterPatientAsync(admin, PatientFields("Approval Patient", "Approval Father"));
        var enc = await RegisterOpdAsync(admin, pid);
        var pharmacist = await UserWithAsync(admin, "pharm17", PharmacistPerms);

        // No "Change Issue Mode": the default (Free) only.
        var change = await IssueModeAsync(pharmacist, pid, enc, "Paid", null, (med, 1, null, 0));
        Assert.Contains("Change Issue Mode", await change.Content.ReadAsStringAsync());

        // Free issue without "Approve Free Issue": waits for approval, no stock taken, no return possible.
        var pending = await IdAsync(await IssueModeAsync(pharmacist, pid, enc, null, null, (med, 10, null, 0)), "Pending free issue");
        Assert.Equal(100, await StockAsync("MAIN", med));
        await using (var db = Factory.NewDb()) Assert.Equal(FreeIssueApproval.Pending, (await db.PatientMedicineIssues.SingleAsync(i => i.Id == pending)).ApprovalStatus);
        Assert.Contains("NOT APPROVED YET", await admin.GetHtmlAsync($"/Print/Pharmacy/ISS/{pending}"));
        Assert.Contains($"data-pending=", await admin.GetHtmlAsync("/Pharmacy/Approvals"));
        Denied(await pharmacist.GetAsync("/Pharmacy/Approvals"), "Approvals page");
        var selfApprove = await pharmacist.PostFormAsync($"/Pharmacy/Doc/ISS/{pending}", new[] { P("approve", "true") }, $"/Pharmacy/Doc/ISS/{pending}?handler=Decide");
        Denied(selfApprove, "Approve without permission");

        // The administrator approves: now the stock is taken.
        var ok = await admin.PostFormAsync($"/Pharmacy/Doc/ISS/{pending}", new[] { P("approve", "true"), P("note", "Checked") }, $"/Pharmacy/Doc/ISS/{pending}?handler=Decide");
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal(90, await StockAsync("MAIN", med));
        await using (var db = Factory.NewDb())
        {
            var i = await db.PatientMedicineIssues.SingleAsync(x => x.Id == pending);
            Assert.Equal((FreeIssueApproval.Approved, "admin"), (i.ApprovalStatus!.Value, i.ApprovedByUsername));
        }
        Assert.Single(await AuditAsync(AuditActions.PharmFreeIssueApproved));

        // Without the Paid Sale / Subsidy right the mode is refused even when enabled.
        await ConfigureAsync(s => { s.DefaultIssueMode = MedicineIssueMode.Paid; s.FreeIssueApprovalRequired = false; });
        var noSubsidy = await IssueModeAsync(pharmacist, pid, enc, null, new[] { P("Input.SubsidyAmount", "1") }, (med, 1, null, 0));
        Assert.Contains("only used with Partial Payment", await noSubsidy.Content.ReadAsStringAsync());
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Stock_classification_keeps_free_and_sale_stock_apart()
    {
        await SetupAsync("Private");
        await ConfigureAsync(s => { s.FreeIssueEnabled = true; s.StockClassificationEnabled = true; });
        var admin = await LoginAdminAsync();
        var med = await MedicineAsync(admin, "Ibuprofen", sale: 4, cost: 2);
        Assert.Contains("data-stock-class", await admin.GetHtmlAsync("/Pharmacy/Receive"));
        async Task ReceiveClassAsync(string batch, string cls, int qty, string expiry)
        {
            var f = new List<KeyValuePair<string, string>>
            {
                P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SourceName", "Supply " + batch), P("Input.ReferenceNumber", "REF-" + batch), P("Input.StockClass", cls),
                P("Lines[0].MedicineId", med.ToString()), P("Lines[0].BatchNumber", batch), P("Lines[0].Expiry", expiry), P("Lines[0].Quantity", qty.ToString()), P("Lines[0].UnitCost", "2"),
            };
            await IdAsync(await admin.PostFormAsync("/Pharmacy/Receive", f), "Receive " + batch);
        }
        // The free batch expires first: a paid issue must still skip it.
        await ReceiveClassAsync("GOVT1", "FreeDistribution", 50, Month(6));
        await ReceiveClassAsync("SALE1", "Sale", 50, Month(12));
        await using (var db = Factory.NewDb())
            Assert.Equal(MedicineStockClass.FreeDistribution, (await db.MedicineBatches.SingleAsync(b => b.BatchNumber == "GOVT1")).StockClass);
        var pid = await RegisterPatientAsync(admin, PatientFields("Class Patient", "Class Father"));
        var enc = await RegisterOpdAsync(admin, pid);
        await IdAsync(await IssueModeAsync(admin, pid, enc, null, null, (med, 20, null, 0)), "Paid from sale stock");
        Assert.Equal(50, await StockAsync("MAIN", med, "GOVT1"));
        Assert.Equal(30, await StockAsync("MAIN", med, "SALE1"));
        await IdAsync(await IssueModeAsync(admin, pid, enc, "Free", new[] { P("Input.FreeReason", "Government supply") }, (med, 20, null, 0)), "Free from free stock");
        Assert.Equal(30, await StockAsync("MAIN", med, "GOVT1"));
        Assert.Equal(30, await StockAsync("MAIN", med, "SALE1"));
        // Choosing a restricted batch directly is refused.
        var wrong = await IssueModeAsync(admin, pid, enc, "Free", new[] { P("Input.FreeReason", "x") }, (med, 1, await BatchAsync(med, "SALE1"), 0));
        Assert.Contains("Sale Stock and cannot be used", await wrong.Content.ReadAsStringAsync());
        // A batch can be re-classified (audited).
        var sale1 = await BatchAsync(med, "SALE1");
        var re = await admin.PostFormAsync($"/Pharmacy/Medicines/{med}", new[] { P("stockClass", "General") }, $"/Pharmacy/Medicines/{med}?handler=BatchClass&batchId={sale1}");
        Assert.Equal(HttpStatusCode.Redirect, re.StatusCode);
        await using (var db = Factory.NewDb()) Assert.Null((await db.MedicineBatches.SingleAsync(b => b.Id == sale1)).StockClass);
        Assert.Single(await AuditAsync(AuditActions.PharmStockClassChanged));
        await AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Doctors_list_adds_and_edits_doctors_with_their_consultation_fee()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        Assert.Contains("data-add-doctor", await admin.GetHtmlAsync("/Appointments"));
        Assert.Contains("data-doctor-form", await admin.GetHtmlAsync("/Appointments/Doctors?new=true"));
        var noFee = await admin.PostFormAsync("/Appointments/Doctors?new=true", new[] { P("Form.Name", "Dr. Fee Missing"), P("Form.IsActive", "true") }, "/Appointments/Doctors");
        Assert.Equal(HttpStatusCode.OK, noFee.StatusCode);
        Assert.Contains("Enter the consultation fee", await noFee.Content.ReadAsStringAsync());

        var r = await admin.PostFormAsync("/Appointments/Doctors?new=true", new[]
        {
            P("Form.Name", "Dr. Ayesha Noor"), P("Fee", "800"), P("Form.Specialty", "Gynaecology"), P("Form.ContactNumber", "0300-1234567"), P("Form.IsActive", "true"),
        }, "/Appointments/Doctors");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Add doctor: " + Errors(await r.Content.ReadAsStringAsync()));
        int doctorId;
        await using (var db = Factory.NewDb())
        {
            var d = await db.Doctors.SingleAsync(x => x.Name == "Dr. Ayesha Noor");
            doctorId = d.Id;
            Assert.Equal("0300-1234567", d.ContactNumber);
            var svc = await db.ServiceItems.SingleAsync(s => s.DoctorId == d.Id && s.Kind == ServiceKind.Consultation);
            Assert.Equal(800m, await db.ServiceFees.Where(f => f.ServiceItemId == svc.Id).OrderByDescending(f => f.Id).Select(f => f.Fee).FirstAsync());
        }
        var list = await admin.GetHtmlAsync("/Appointments/Doctors");
        Assert.Contains("Dr. Ayesha Noor", list);
        // The fee is offered automatically when booking.
        var pid = await RegisterPatientAsync(admin, PatientFields("Fee Patient", "Fee Father"));
        Assert.Contains("data-fee=\"800", await admin.GetHtmlAsync($"/Appointments/Book?patientId={pid}"));

        // Duplicates are refused.
        var dup = await admin.PostFormAsync("/Appointments/Doctors?new=true", new[] { P("Form.Name", "dr ayesha noor"), P("Fee", "500"), P("Form.IsActive", "true") }, "/Appointments/Doctors");
        Assert.Contains("already exists", await dup.Content.ReadAsStringAsync());

        // Edit: new fee (new appointments only) and inactive → not offered for new appointments.
        var edit = await admin.PostFormAsync($"/Appointments/Doctors?edit={doctorId}", new[]
        {
            P("Form.Name", "Dr. Ayesha Noor"), P("Fee", "900"), P("Form.Specialty", "Gynaecology"), P("Form.ContactNumber", "0300-7654321"), P("Form.IsActive", "false"),
        }, $"/Appointments/Doctors?edit={doctorId}");
        Assert.True(edit.StatusCode == HttpStatusCode.Redirect, "Edit doctor: " + Errors(await edit.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb())
        {
            var d = await db.Doctors.SingleAsync(x => x.Id == doctorId);
            Assert.False(d.IsActive);
            Assert.Equal("0300-7654321", d.ContactNumber);
        }
        Assert.DoesNotContain("Dr. Ayesha Noor", await admin.GetHtmlAsync($"/Appointments/Book?patientId={pid}"));
    }

    [Fact]
    public async Task New_appointment_opens_registration_with_the_patient_search_at_the_top()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        Assert.Contains("href=\"/Patients/Register?next=appointment\"", await admin.GetHtmlAsync("/"));
        var book = await admin.GetAsync("/Appointments/Book");
        Assert.Equal(HttpStatusCode.Redirect, book.StatusCode);
        Assert.Equal("/Patients/Register?next=appointment", book.Headers.Location!.OriginalString);
        var form = await admin.GetHtmlAsync("/Patients/Register?next=appointment");
        Assert.True(form.IndexOf("data-patient-find", StringComparison.Ordinal) < form.IndexOf("data-patient-form", StringComparison.Ordinal), "search must be above the form");
        Assert.Contains("data-patient-search-button", form);

        var pid = await RegisterPatientAsync(admin, PatientFields("Searchable Person", "Search Father"));
        string number;
        await using (var db = Factory.NewDb()) number = await db.Patients.Where(p => p.Id == pid).Select(p => p.PatientNumber).SingleAsync();
        foreach (var q in new[] { "Searchable", number })
        {
            var found = await admin.GetHtmlAsync($"/Patients/Register?next=appointment&q={Uri.EscapeDataString(q)}");
            Assert.Contains($"href=\"/Appointments/Book?patientId={pid}\"", found);
        }
        var none = await admin.GetHtmlAsync("/Patients/Register?next=appointment&q=Nobody%20Here");
        Assert.Contains("data-no-match", none);
        Assert.Contains("value=\"Nobody Here\"", none);

        // Register & New Appointment goes straight to the doctor token booking.
        var f = PatientFields("Brand New Patient", "New Father");
        f["Next"] = "appointment"; f["ConfirmNotDuplicate"] = "true";
        var reg = await admin.PostFormAsync("/Patients/Register?next=appointment", f.Select(x => P(x.Key, x.Value)), "/Patients/Register");
        Assert.Equal(HttpStatusCode.Redirect, reg.StatusCode);
        Assert.StartsWith("/Appointments/Book?patientId=", reg.Headers.Location!.OriginalString);
    }
}
