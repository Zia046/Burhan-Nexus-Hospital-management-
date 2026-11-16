using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 9 – laboratory standard catalogue (ranges, culture, pregnancy), nursing medicine demand, signatures.</summary>
public partial class Update9Tests : Phase2TestBase
{
    [GeneratedRegex(@"/Lab/Order/(\d+)")] private static partial Regex OrderRegex();
    [GeneratedRegex(@"/Ipd/Admission/(\d+)")] private static partial Regex AdmissionRegex();

    private DateOnly Today => DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5));

    private static async Task<long> IdAsync(Regex rx, HttpResponseMessage r, string what)
    {
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, $"{what} failed: " + Errors(await r.Content.ReadAsStringAsync()));
        var m = rx.Match(r.Headers.Location!.OriginalString);
        Assert.True(m.Success, $"{what}: unexpected redirect {r.Headers.Location}");
        return long.Parse(m.Groups[1].Value);
    }

    private static void Denied(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied")),
            $"{what} should be refused but returned {(int)r.StatusCode} {r.Headers.Location}");

    // =========================================================================== Laboratory

    [Fact]
    public async Task Standard_lab_catalogue_is_added_once_active_free_and_marked_for_review()
    {
        await SetupAsync("Private");
        await using (var db = Factory.NewDb())
        {
            Assert.True(await db.LabCategories.CountAsync() >= 25, "at least 25 laboratory disciplines");
            var tests = await db.LabTests.Include(t => t.ServiceItem).Include(t => t.Parameters).ThenInclude(p => p.Ranges).AsSplitQuery().ToListAsync();
            Assert.True(tests.Count >= 140, $"standard catalogue has {tests.Count} tests");
            Assert.All(tests, t => Assert.True(t.IsActive && t.RequiresReview && t.ServiceItem!.DefaultFee == 0, t.Code));
            Assert.Equal(tests.Count, tests.Select(t => t.Code).Distinct().Count());
            var cbc = tests.Single(t => t.Code == "CBC-FULL");
            var hb = cbc.Parameters.Single(p => p.Name == "Haemoglobin");
            Assert.Equal("g/dL", hb.Unit);
            Assert.Contains(hb.Ranges, r => r.Gender == Gender.Male && r.Low == 13.0m);
            Assert.Contains(hb.Ranges, r => r.IsPregnancy);
            Assert.Contains(tests.Single(t => t.Code == "BG").Parameters, p => p.ResultType == LabResultType.Options && p.Options!.Contains("AB"));
            Assert.Contains(tests.Single(t => t.Code == "HBSAG").Parameters, p => p.Options!.Contains("Reactive"));
            Assert.Contains(tests.Single(t => t.Code == "URNCS").Parameters, p => p.ResultType == LabResultType.Culture);
            Assert.Equal(1, (await db.LabSettings.SingleAsync()).StandardCatalogueVersion);
        }
        // Running the seeder again (every start) adds nothing and never changes the laboratory's own edits.
        int count;
        await using (var db = Factory.NewDb())
        {
            var t = await db.LabTests.SingleAsync(x => x.Code == "ESR");
            t.Name = "ESR (our method)";
            await db.SaveChangesAsync();
            count = await db.LabTests.CountAsync();
        }
        await using (var db = Factory.NewDb())
        {
            await LabCatalogService.SeedAsync(db, DateTime.UtcNow);
            Assert.Equal(count, await db.LabTests.CountAsync());
            Assert.Equal("ESR (our method)", (await db.LabTests.SingleAsync(x => x.Code == "ESR")).Name);
        }
        var admin = await LoginAdminAsync();
        var list = await admin.GetHtmlAsync("/Lab/Catalogue");
        Assert.Contains("data-review-required", list);
    }

    [Fact]
    public async Task Pregnancy_ranges_dropdowns_and_culture_sensitivity_are_entered_and_flagged()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Pregnant Lab Patient", "Husband Khan", "Female"));
        int fbs, culture, hbs;
        await using (var db = Factory.NewDb())
        {
            fbs = await db.LabTests.Where(t => t.Code == "FBS").Select(t => t.Id).SingleAsync();
            culture = await db.LabTests.Where(t => t.Code == "URNCS").Select(t => t.Id).SingleAsync();
            hbs = await db.LabTests.Where(t => t.Code == "HBSAG").Select(t => t.Id).SingleAsync();
        }
        var book = await admin.GetHtmlAsync($"/Lab/Book?patientId={pid}");
        Assert.Contains("data-pregnant", book);
        var r = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("PatientId", pid.ToString()), P("TestIds", fbs.ToString()), P("TestIds", culture.ToString()),
            P("TestIds", hbs.ToString()), P("Input.Pregnant", "true"),
        });
        var orderId = await IdAsync(OrderRegex(), r, "Lab booking");
        LabOrder order;
        await using (var db = Factory.NewDb()) order = await db.LabOrders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Values).Include(o => o.Samples).AsSplitQuery().SingleAsync(o => o.Id == orderId);
        Assert.True(order.PatientPregnant);
        foreach (var s in order.Samples)
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Order/{orderId}", Array.Empty<KeyValuePair<string, string>>(), $"/Lab/Order/{orderId}?handler=Collect&sampleId={s.Id}&receive=true")).StatusCode);
        await using (var db = Factory.NewDb()) order = await db.LabOrders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Values).AsSplitQuery().SingleAsync(o => o.Id == orderId);

        // Pregnancy range (fasting glucose ≤ 92) is the one snapshotted; 95 is HIGH for this patient.
        var fbsItem = order.Items.Single(i => i.TestCode == "FBS");
        var glu = fbsItem.Values.Single();
        Assert.Equal(92m, glu.RefHigh);
        var entry = await admin.GetHtmlAsync($"/Lab/Entry/{orderId}?item={fbsItem.Id}");
        Assert.Contains("mg/dL", entry);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Entry/{orderId}?item={fbsItem.Id}", new[]
            { P("Mode", "submit"), P("RowVersion", fbsItem.RowVersion.ToString()), P($"Values[{glu.Id}]", "95") })).StatusCode);

        // Qualitative dropdown: Reactive is flagged abnormal; free text outside the list is refused.
        var hbsItem = order.Items.Single(i => i.TestCode == "HBSAG");
        var hv = hbsItem.Values.Single();
        Assert.Contains("Non-Reactive", await admin.GetHtmlAsync($"/Lab/Entry/{orderId}?item={hbsItem.Id}"));
        var bad = await admin.PostFormAsync($"/Lab/Entry/{orderId}?item={hbsItem.Id}", new[] { P("Mode", "submit"), P("RowVersion", hbsItem.RowVersion.ToString()), P($"Values[{hv.Id}]", "maybe") });
        Assert.NotEqual(HttpStatusCode.Redirect, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Entry/{orderId}?item={hbsItem.Id}", new[]
            { P("Mode", "submit"), P("RowVersion", hbsItem.RowVersion.ToString()), P($"Values[{hv.Id}]", "Reactive") })).StatusCode);

        // Culture: organism + antibiotic sensitivity rows (S / I / R).
        var cItem = order.Items.Single(i => i.TestCode == "URNCS");
        var cv = cItem.Values.Single(v => v.ResultType == LabResultType.Culture);
        var count = cItem.Values.Single(v => v.ResultType == LabResultType.Numeric);
        var page = await admin.GetHtmlAsync($"/Lab/Entry/{orderId}?item={cItem.Id}");
        Assert.Contains("lab-antibiotics", page);
        var noSir = await admin.PostFormAsync($"/Lab/Entry/{orderId}?item={cItem.Id}", new[]
        {
            P("Mode", "submit"), P("RowVersion", cItem.RowVersion.ToString()), P($"Values[{cv.Id}]", "Escherichia coli"), P($"Values[{count.Id}]", "100000"),
            P("Culture[0].ValueId", cv.Id.ToString()), P("Culture[0].Antibiotic", "Ciprofloxacin"),
        });
        Assert.Contains("choose S, I or R", Errors(await noSir.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Entry/{orderId}?item={cItem.Id}", new[]
        {
            P("Mode", "submit"), P("RowVersion", cItem.RowVersion.ToString()), P($"Values[{cv.Id}]", "Escherichia coli"), P($"Values[{count.Id}]", "100000"),
            P("Culture[0].ValueId", cv.Id.ToString()), P("Culture[0].Antibiotic", "Ciprofloxacin"), P("Culture[0].Result", "Resistant"),
            P("Culture[1].ValueId", cv.Id.ToString()), P("Culture[1].Antibiotic", "Nitrofurantoin"), P("Culture[1].Result", "Sensitive"), P("Culture[1].Mic", "18 mm"),
        })).StatusCode);

        await using (var db = Factory.NewDb())
        {
            var vals = await db.LabResultValues.AsNoTracking().Where(v => v.Id == glu.Id || v.Id == hv.Id || v.Id == cv.Id).ToDictionaryAsync(v => v.Id);
            Assert.Equal(LabFlag.High, vals[glu.Id].Flag);
            Assert.Equal(LabFlag.Abnormal, vals[hv.Id].Flag);
            Assert.Equal(LabFlag.Abnormal, vals[cv.Id].Flag);
            var sens = await db.LabCultureResults.Where(c => c.ResultValueId == cv.Id).OrderBy(c => c.SortOrder).ToListAsync();
            Assert.Equal(new[] { ("Ciprofloxacin", SensitivityResult.Resistant), ("Nitrofurantoin", SensitivityResult.Sensitive) }, sens.Select(s => (s.Antibiotic, s.Interpretation)));
            Assert.All(sens, s => Assert.Equal("Escherichia coli", s.Organism));
        }
        // Saving again replaces the lines (no duplicates).
        await using (var db = Factory.NewDb()) cItem = await db.LabOrderItems.AsNoTracking().SingleAsync(i => i.Id == cItem.Id);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Lab/Entry/{orderId}?item={cItem.Id}", new[]
        {
            P("Mode", "submit"), P("RowVersion", cItem.RowVersion.ToString()), P($"Values[{cv.Id}]", "Escherichia coli"), P($"Values[{count.Id}]", "100000"),
            P("Culture[0].ValueId", cv.Id.ToString()), P("Culture[0].Antibiotic", "Ciprofloxacin"), P("Culture[0].Result", "Resistant"),
        })).StatusCode);
        await using (var db = Factory.NewDb()) Assert.Equal(1, await db.LabCultureResults.CountAsync(c => c.ResultValueId == cv.Id));
        Assert.Contains("Resistant", await admin.GetHtmlAsync($"/Lab/Entry/{orderId}?item={cItem.Id}"));
    }

    // =========================================================================== Nursing medicine demand

    private async Task<int> MedicineAsync(Browser admin, string name)
    {
        int form;
        await using (var db = Factory.NewDb()) form = await db.PharmacyLookups.Where(l => l.Kind == PharmacyLookupKind.DosageForm && l.Name == "Tablet").Select(l => l.Id).SingleAsync();
        var r = await admin.PostFormAsync("/Pharmacy/Medicines/Edit", new[]
        {
            P("Input.Name", name), P("Input.GenericName", name + " generic"), P("Input.Strength", "500 mg"), P("Input.DosageFormId", form.ToString()), P("Input.Unit", "Tablet"),
            P("Input.UnitsPerPack", "10"), P("Input.MinimumStock", "0"), P("Input.IsActive", "true"), P("Input.SalePrice", "10"), P("Input.PurchasePrice", "5"),
        });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Medicine: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db2 = Factory.NewDb();
        return await db2.Medicines.Where(m => m.Name == name).Select(m => m.Id).SingleAsync();
    }

    private async Task ReceiveAsync(Browser admin, int med, int qty)
    {
        var r = await admin.PostFormAsync("/Pharmacy/Receive", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SourceName", "Opening stock"), P("Lines[0].MedicineId", med.ToString()), P("Lines[0].BatchNumber", "B-" + med),
            P("Lines[0].Expiry", Today.AddMonths(18).ToString("yyyy-MM")), P("Lines[0].Quantity", qty.ToString()), P("Lines[0].UnitCost", "5"),
        });
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Receive: " + Errors(await r.Content.ReadAsStringAsync()));
    }

    private async Task<int> StockAsync(int medicineId)
    {
        await using var db = Factory.NewDb();
        return await db.StockBalances.Where(s => s.MedicineId == medicineId).SumAsync(s => (int?)s.Quantity) ?? 0;
    }

    private async Task<Browser> StaffAsync(Browser admin, string username, params string[] perms)
    {
        var id = await CreateUserAsync(admin, username, "Usr#Pass2026", false);
        await SetPermissionsAsync(admin, id, perms.Select(p => (p, "grant")).ToArray());
        var b = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(b, username, "Usr#Pass2026")).StatusCode);
        return b;
    }

    [Fact]
    public async Task Nurse_requests_medicines_and_pharmacy_issues_partly_with_stock_deducted_once()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var ceft = await MedicineAsync(admin, "Ceftriaxone Nine");
        var para = await MedicineAsync(admin, "Paracetamol Nine");
        await ReceiveAsync(admin, ceft, 50);
        var pid = await RegisterPatientAsync(admin, PatientFields("Ward Medicine Patient", "Father Ward"));
        var admit = await admin.PostFormAsync($"/Ipd/Admit?patientId={pid}", new[]
        {
            P("PatientId", pid.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"),
            P("Input.SourceEncounterId", ""), P("Input.BedId", ""), P("Input.AdmittingDoctorName", "Dr. Ward"), P("Input.AdmissionDiagnosis", "Pneumonia"),
            P("Input.AttendantName", "Brother"), P("Input.AttendantPhone", "03001234567"),
        });
        var adm = await IdAsync(AdmissionRegex(), admit, "Admission");

        var nurse = await StaffAsync(admin, "nurse9", Perm.DashboardView, Perm.IpdViewPatients, Perm.IpdRequestMedicines);
        var form = await nurse.GetHtmlAsync($"/Ipd/Medicines/{adm}");
        Assert.Contains("data-medicine-request", form);
        var key = Guid.NewGuid().ToString();
        KeyValuePair<string, string>[] Req(string k) => new[]
        {
            P("Input.IdempotencyKey", k), P("Input.Priority", "Urgent"), P("Input.Notes", "Start tonight"),
            P("Input.Lines[0].MedicineId", ceft.ToString()), P("Input.Lines[0].Quantity", "10"), P("Input.Lines[0].Instructions", "1 g IV BD"),
            P("Input.Lines[1].MedicineId", para.ToString()), P("Input.Lines[1].Quantity", "6"),
        };
        Assert.Equal(HttpStatusCode.Redirect, (await nurse.PostFormAsync($"/Ipd/Medicines/{adm}", Req(key))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await nurse.PostFormAsync($"/Ipd/Medicines/{adm}", Req(key))).StatusCode); // double click
        // Same medicines again while still open → refused as a duplicate.
        var dup = await nurse.PostFormAsync($"/Ipd/Medicines/{adm}", Req(Guid.NewGuid().ToString()));
        Assert.Contains("already requested", Errors(await dup.Content.ReadAsStringAsync()));
        MedicineRequest req;
        await using (var db = Factory.NewDb())
        {
            req = await db.MedicineRequests.Include(r => r.Lines).SingleAsync();
            Assert.Equal((MedicineRequestStatus.Pending, MedicineRequestPriority.Urgent, 2), (req.Status, req.Priority, req.Lines.Count));
            Assert.StartsWith("MRQ-", req.Number);
        }
        Assert.Equal(50, await StockAsync(ceft)); // nothing deducted on request

        // The nurse cannot open the pharmacy queue; a user without the IPD permission cannot request.
        Denied(await nurse.Http.GetAsync("/Pharmacy/WardRequests"), "Ward queue for a nurse");
        var clerk = await StaffAsync(admin, "clerk9", Perm.DashboardView, Perm.IpdViewPatients);
        Denied(await clerk.Http.GetAsync($"/Ipd/Medicines/{adm}"), "Request page without permission");

        // Pharmacy: partial issue of 4, the paracetamol is not available.
        var pharm = await StaffAsync(admin, "pharm9", Perm.DashboardView, Perm.PharmProcessWardRequests);
        Assert.Contains(req.Number, await pharm.GetHtmlAsync("/Pharmacy/WardRequests"));
        var ceftLine = req.Lines.Single(l => l.MedicineId == ceft);
        var paraLine = req.Lines.Single(l => l.MedicineId == para);
        Assert.Equal(HttpStatusCode.Redirect, (await pharm.PostFormAsync($"/Pharmacy/WardRequest/{req.Id}", new[] { P("Note", "Out of stock"), P("decision", "Unavailable") },
            $"/Pharmacy/WardRequest/{req.Id}?handler=Decide&lineId={paraLine.Id}")).StatusCode);
        var issueKey = Guid.NewGuid().ToString();
        var issue = new[] { P("IdempotencyKey", issueKey), P($"Issue[{ceftLine.Id}]", "4") };
        Assert.Equal(HttpStatusCode.Redirect, (await pharm.PostFormAsync($"/Pharmacy/WardRequest/{req.Id}", issue, $"/Pharmacy/WardRequest/{req.Id}?handler=Issue")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await pharm.PostFormAsync($"/Pharmacy/WardRequest/{req.Id}", issue, $"/Pharmacy/WardRequest/{req.Id}?handler=Issue")).StatusCode); // repeat
        Assert.Equal(46, await StockAsync(ceft));
        await using (var db = Factory.NewDb())
        {
            var r = await db.MedicineRequests.Include(x => x.Lines).SingleAsync();
            Assert.Equal(MedicineRequestStatus.PartlyIssued, r.Status);
            Assert.Equal((4, MedicineRequestLineStatus.PartlyIssued), r.Lines.Where(l => l.Id == ceftLine.Id).Select(l => (l.IssuedQuantity, l.Status)).Single());
            Assert.Equal(MedicineRequestLineStatus.Unavailable, r.Lines.Single(l => l.Id == paraLine.Id).Status);
            var iss = await db.PatientMedicineIssues.SingleAsync();
            Assert.Equal((adm, pid), (iss.IpdAdmissionId!.Value, iss.PatientId));
        }
        // More than the remainder is refused; the remaining 6 completes the request.
        var over = await pharm.PostFormAsync($"/Pharmacy/WardRequest/{req.Id}", new[] { P("IdempotencyKey", Guid.NewGuid().ToString()), P($"Issue[{ceftLine.Id}]", "7") },
            $"/Pharmacy/WardRequest/{req.Id}?handler=Issue");
        Assert.NotEqual(HttpStatusCode.Redirect, over.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await pharm.PostFormAsync($"/Pharmacy/WardRequest/{req.Id}", new[] { P("IdempotencyKey", Guid.NewGuid().ToString()), P($"Issue[{ceftLine.Id}]", "6") },
            $"/Pharmacy/WardRequest/{req.Id}?handler=Issue")).StatusCode);
        Assert.Equal(40, await StockAsync(ceft));
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(MedicineRequestStatus.Completed, (await db.MedicineRequests.SingleAsync()).Status);
            Assert.Equal(2, await db.PatientMedicineIssues.CountAsync());
            Assert.Equal(2, await db.MedicineRequestEvents.CountAsync(e => e.Event == "Issued"));
        }
        // History is visible to the nurse; a new request for the same medicine is allowed once the old one is issued.
        var hist = await nurse.GetHtmlAsync($"/Ipd/Medicines/{adm}");
        Assert.Contains("Completed", hist);
        Assert.Single((await AuditAsync(AuditActions.MedicineRequestCreated)).Where(x => x.Success));
        Assert.Equal(2, (await AuditAsync(AuditActions.MedicineRequestIssued)).Count);
    }

    // =========================================================================== Signatures

    [Fact]
    public async Task Discharge_slip_shows_only_the_doctors_signature_and_no_patient_signatures()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Signature Patient", "Father Sign"));
        var admit = await admin.PostFormAsync($"/Ipd/Admit?patientId={pid}", new[]
        {
            P("PatientId", pid.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"),
            P("Input.SourceEncounterId", ""), P("Input.BedId", ""), P("Input.AdmittingDoctorName", "Dr. Sign"), P("Input.AdmissionDiagnosis", "Fever"),
            P("Input.AttendantName", "Brother"), P("Input.AttendantPhone", "03001234567"),
        });
        var adm = await IdAsync(AdmissionRegex(), admit, "Admission");
        var form = await admin.GetHtmlAsync($"/Print/Ipd/admission/{adm}");
        Assert.DoesNotContain("Patient / attendant", form);
        Assert.DoesNotContain("Patient's signature", form);
        var discharge = await admin.GetHtmlAsync($"/Print/Ipd/discharge/{adm}");
        Assert.Contains("data-doctor-signature", discharge);
        Assert.DoesNotContain("Patient / attendant", discharge);
        Assert.DoesNotContain("Attendant's signature", discharge);
    }
}
