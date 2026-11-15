using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure;
using BurhanNexus.HMS.Infrastructure.Radiology;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Phase 6 – radiology (X-Ray &amp; Ultrasound): booking, queues, examination, reporting, verification, release, printing,
/// attachments, Phase 3 billing, Government / Private rules, permissions, audit, concurrency and backups of files.</summary>
public partial class RadiologyTests : Phase2TestBase
{
    [GeneratedRegex(@"/Radiology/Order/(\d+)")] private static partial Regex OrderRegex();

    private static string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);
    private static readonly KeyValuePair<string, string>[] None = Array.Empty<KeyValuePair<string, string>>();
    private DateOnly Today => DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5));

    private static void Denied(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied")),
            $"{what} should be refused but returned {(int)r.StatusCode} {r.Headers.Location}");

    private static async Task Ok(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, $"{what} failed: " + Errors(await r.Content.ReadAsStringAsync()));

    private async Task<int> ServiceAsync(Browser admin, string code, string name, string modality, decimal fee, int? template = null)
    {
        var f = new List<KeyValuePair<string, string>>
        {
            P("Input.Code", code), P("Input.Name", name), P("Input.Modality", modality), P("Input.BodyPart", "Chest"), P("Input.DurationMinutes", "15"),
            P("Input.IsActive", "true"), P("Input.IsFree", fee == 0 ? "true" : "false"), P("Input.Fee", N(fee)), P("Input.AllowPartialPayment", "true"),
        };
        if (template is int t) f.Add(P("Input.DefaultTemplateId", t.ToString()));
        await Ok(await admin.PostFormAsync("/Radiology/Services/Edit", f), "Service");
        await using var db = Factory.NewDb();
        return await db.RadiologyServices.Where(s => s.Code == code).Select(s => s.Id).SingleAsync();
    }

    private static async Task<HttpResponseMessage> PostBookAsync(Browser b, long patientId, IEnumerable<int> services, Guid? key = null, long? visit = null, params (string, string)[] extra)
    {
        var f = new List<KeyValuePair<string, string>>
        {
            P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()), P("PatientId", patientId.ToString()), P("VisitChoice", visit?.ToString() ?? ""),
            P("Input.ReferralType", "External"), P("Input.ExternalDoctorName", "Dr. Handwritten Advice"), P("Input.ClinicalIndication", "Cough for 2 weeks"), P("Input.Priority", "Routine"),
        };
        f.AddRange(services.Select(s => P("ServiceIds", s.ToString())));
        f.AddRange(extra.Select(e => P(e.Item1, e.Item2)));
        return await b.PostFormAsync($"/Radiology/Book?patientId={patientId}", f);
    }

    private static async Task<long> OrderIdAsync(HttpResponseMessage r)
    {
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Booking failed: " + Errors(await r.Content.ReadAsStringAsync()));
        var m = OrderRegex().Match(r.Headers.Location!.OriginalString);
        Assert.True(m.Success, "Unexpected redirect " + r.Headers.Location);
        return long.Parse(m.Groups[1].Value);
    }

    private async Task<RadiologyExam> ExamAsync(long orderId, string code)
    {
        await using var db = Factory.NewDb();
        return await db.RadiologyExams.AsNoTracking().SingleAsync(e => e.OrderId == orderId && e.ServiceCode == code);
    }

    private static Task<HttpResponseMessage> StepAsync(Browser b, long examId, string handler, params (string, string)[] fields) =>
        b.PostFormAsync($"/Radiology/Exam/{examId}", fields.Select(x => P(x.Item1, x.Item2)), $"/Radiology/Exam/{examId}?handler={handler}");

    private async Task PerformAsync(Browser b, long examId)
    {
        await Ok(await StepAsync(b, examId, "Arrive"), "Arrive");
        await Ok(await StepAsync(b, examId, "Start"), "Start");
        await Ok(await StepAsync(b, examId, "Complete", ("TechnicalNotes", "Good inspiration"), ("PerformedBy", "Tech Ali")), "Complete");
    }

    private async Task<HttpResponseMessage> ReportAsync(Browser b, long examId, bool submit, string findings = "Both lung fields are clear.", string impression = "Normal chest.")
    {
        uint rv;
        await using (var db = Factory.NewDb()) rv = (await db.RadiologyExams.AsNoTracking().SingleAsync(e => e.Id == examId)).RowVersion;
        var f = new[]
        {
            P("RowVersion", rv.ToString()), P("Input.Technique", "PA view"), P("Input.Findings", findings), P("Input.Impression", impression),
            P("Input.ReportingDoctorName", "Dr. Sana Radiologist"), P("Input.ReportingDoctorDesignation", "Consultant Radiologist"), P("Input.ClinicalIndication", "Cough"),
        };
        return await b.PostFormAsync($"/Radiology/Report/{examId}", f, $"/Radiology/Report/{examId}" + (submit ? "?handler=Submit" : ""));
    }

    private static Task<HttpResponseMessage> PrintAsync(Browser b, long examId, string format = "A4") =>
        b.PostFormAsync($"/Print/RadiologyReport/{examId}", new[] { P("Format", format) }, $"/Print/RadiologyReport/{examId}?handler=Record");

    private async Task<int> CounterAsync(Browser admin, string code, string name)
    {
        var r = await admin.PostFormAsync("/Finance/Counters", new[] { P("Form.Code", code), P("Form.Name", name), P("Form.IsActive", "true") }, "/Finance/Counters?handler=Save");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Counters.Where(c => c.Code == code).Select(c => c.Id).SingleAsync();
    }

    private static readonly string[] RadiologistPerms =
    {
        Perm.DashboardView, Perm.RadDashboard, Perm.RadViewXRay, Perm.RadViewUltrasound, Perm.RadEnterReport, Perm.RadEditDraft, Perm.RadSubmitReport,
        Perm.RadVerifyReport, Perm.RadReleaseReport, Perm.RadPrintReport, Perm.RadViewHistory
    };

    // =====================================================================================

    [Fact]
    public async Task Private_booking_queue_report_verification_release_print_and_billing()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var cxr = await ServiceAsync(admin, "XR-CXR", "Chest X-Ray PA", "XRay", 800);
        var usg = await ServiceAsync(admin, "US-ABDO", "Ultrasound Abdomen", "Ultrasound", 1500);
        await using (var db = Factory.NewDb())
        {
            // The catalogue is data, not code: a starter catalogue (inactive, free) and templates were seeded; new services are Phase 3 services.
            Assert.True(await db.RadiologyServices.CountAsync(s => !s.IsActive) >= 18);
            Assert.True(await db.RadiologyTemplates.CountAsync() >= 5);
            var item = await db.ServiceItems.SingleAsync(s => s.Code == "RAD-XR-CXR");
            Assert.Equal(ServiceKind.XRay, item.Kind);
            Assert.True((await db.RadiologySettings.SingleAsync()).AllowBookingWithoutVisit); // Private default
        }

        // One booking with an X-Ray and an Ultrasound from the doctor's handwritten advice (no doctor login, no visit needed in Private mode).
        var pid = await RegisterPatientAsync(admin, PatientFields("Radiology Patient", "Rad Father"));
        var key = Guid.NewGuid();
        var order = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { cxr, usg }, key));
        Assert.Equal(order, await OrderIdAsync(await PostBookAsync(admin, pid, new[] { cxr, usg }, key))); // double click → same order
        var x = await ExamAsync(order, "XR-CXR");
        var u = await ExamAsync(order, "US-ABDO");
        Assert.Equal((1, 1), (x.TokenNumber, u.TokenNumber)); // separate X-Ray and Ultrasound queues
        Assert.Equal(RadiologyExamStatus.Booked, x.Status);
        await using (var db = Factory.NewDb())
        {
            var o = await db.RadiologyOrders.SingleAsync(o => o.Id == order);
            Assert.Matches(@"^RAD-\d{4}-\d{6}$", o.OrderNumber);
            Assert.Equal("Dr. Handwritten Advice", o.ExternalDoctorName);
            var charges = await db.Charges.Where(c => c.RadiologyOrderId == order).OrderBy(c => c.Id).ToListAsync();
            Assert.Equal(new[] { 800m, 1500m }, charges.Select(c => c.NetAmount).ToArray());
            Assert.Equal(new[] { ServiceKind.XRay, ServiceKind.Ultrasound }, charges.Select(c => c.ServiceKind).ToArray());
            Assert.All(charges, c => Assert.Equal(ChargeStatus.Unpaid, c.Status));
            Assert.Equal(1, await db.RadiologyOrders.CountAsync());
        }
        Assert.Single(await AuditAsync(AuditActions.RadDuplicateBlocked));
        var p2 = await RegisterPatientAsync(admin, PatientFields("Second Rad Patient", "Second Father"));
        var order2 = await OrderIdAsync(await PostBookAsync(admin, p2, new[] { cxr }));
        Assert.Equal(2, (await ExamAsync(order2, "XR-CXR")).TokenNumber);

        // Worklists: X-Ray and Ultrasound separately.
        var xrayList = await admin.GetHtmlAsync("/Radiology/Worklist/XRay");
        Assert.Contains("Radiology Patient", xrayList);
        Assert.Contains("Second Rad Patient", xrayList);
        Assert.DoesNotContain("Ultrasound Abdomen", xrayList);
        Assert.Contains("Ultrasound Abdomen", await admin.GetHtmlAsync("/Radiology/Worklist/Ultrasound"));

        // Report before the examination is refused; then the technician's steps.
        Assert.Contains("not been completed", await (await ReportAsync(admin, x.Id, false)).Content.ReadAsStringAsync());
        await PerformAsync(admin, x.Id);
        x = await ExamAsync(order, "XR-CXR");
        Assert.Equal(RadiologyExamStatus.ExamCompleted, x.Status);
        Assert.Equal("Tech Ali", x.PerformedByName);
        Assert.NotNull(x.CompletedUtc);

        // Draft → submit. A final report cannot be printed yet.
        await Ok(await ReportAsync(admin, x.Id, false), "Draft");
        Assert.Equal(RadiologyExamStatus.ReportEntered, (await ExamAsync(order, "XR-CXR")).Status);
        Assert.Contains("data-no-final", await admin.GetHtmlAsync($"/Print/RadiologyReport/{x.Id}"));
        Assert.Equal(HttpStatusCode.Redirect, (await PrintAsync(admin, x.Id)).StatusCode);
        await using (var db = Factory.NewDb()) Assert.Equal(0, await db.RadiologyReportPrints.CountAsync());
        await Ok(await ReportAsync(admin, x.Id, true), "Submit");
        Assert.Equal(RadiologyExamStatus.AwaitingVerification, (await ExamAsync(order, "XR-CXR")).Status);
        Assert.Contains("XR-CXR", await admin.GetHtmlAsync("/Radiology/Verify"));

        // The author cannot verify their own report (policy off) – another radiologist verifies.
        var own = await StepAsync(admin, x.Id, "Verify");
        Assert.Contains("another authorised person", await own.Content.ReadAsStringAsync());
        var radiologist = await UserWithAsync(admin, "dr.sana", RadiologistPerms);
        await Ok(await StepAsync(radiologist, x.Id, "Verify"), "Verify");
        x = await ExamAsync(order, "XR-CXR");
        Assert.Equal(RadiologyExamStatus.Verified, x.Status);
        Assert.Equal("dr.sana", x.VerifiedByUsername);

        // Release & print (A4), then reprint: copies are recorded, no new order / report is created.
        var preview = await admin.GetHtmlAsync($"/Print/RadiologyReport/{x.Id}");
        Assert.Contains("PREVIEW", preview);
        Assert.Contains("Release &amp; print", preview);
        await Ok(await PrintAsync(admin, x.Id), "Print");
        Assert.Equal(RadiologyExamStatus.Released, (await ExamAsync(order, "XR-CXR")).Status);
        var printed = await admin.GetHtmlAsync($"/Print/RadiologyReport/{x.Id}");
        Assert.Contains("Both lung fields are clear.", printed);
        Assert.Contains("Dr. Sana Radiologist", printed);
        Assert.Contains("Verified by", printed);
        Assert.Contains("data-report-header", printed);
        await Ok(await PrintAsync(admin, x.Id, "PDF"), "Reprint as PDF");
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(new[] { 1, 2 }, await db.RadiologyReportPrints.Where(p => p.ExamId == x.Id).OrderBy(p => p.CopyNumber).Select(p => p.CopyNumber).ToArrayAsync());
            Assert.Equal(3, await db.RadiologyExams.CountAsync());
            Assert.Equal(2, await db.RadiologyOrders.CountAsync());
        }
        Assert.Single(await AuditAsync(AuditActions.RadReportPrinted));
        Assert.Single(await AuditAsync(AuditActions.RadReportReprinted));

        // Letterhead mode: the software header can be switched off.
        var settings = await admin.GetHtmlAsync("/Radiology/Settings");
        uint rv;
        await using (var db = Factory.NewDb()) rv = (await db.RadiologySettings.SingleAsync()).RowVersion;
        await Ok(await admin.PostFormAsync("/Radiology/Settings", new[]
        {
            P("RowVersion", rv.ToString()), P("Input.PrintHeader", "false"), P("Input.LetterheadTopMarginMm", "50"), P("Input.ShowVerifiedBy", "true"),
            P("Input.AllowBookingWithoutVisit", "true"), P("Input.VisitValidDays", "7"), P("Input.MaxUploadMb", "25"), P("Input.ReportFooter", "Correlate clinically."),
        }), "Settings");
        var letterhead = await admin.GetHtmlAsync($"/Print/RadiologyReport/{x.Id}");
        Assert.DoesNotContain("data-report-header", letterhead);
        Assert.Contains("data-letterhead-space", letterhead);
        Assert.Contains("Correlate clinically.", letterhead);

        // Phase 3: the fee is collected at the cash counter (no radiology ledger); manual collection of the service is refused.
        var counter = await CounterAsync(admin, "RADC", "Radiology Counter");
        int method;
        long xCharge;
        await using (var db = Factory.NewDb())
        {
            method = await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
            xCharge = x.ChargeId!.Value;
        }
        Assert.Contains("data-rad-collect", (await admin.GetHtmlAsync($"/Radiology/Exam/{u.Id}")).Replace("data-test=\"rad-collect\"", "data-rad-collect"));
        var manual = await admin.PostFormAsync($"/Finance/Collect?patientId={pid}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.CounterId", counter.ToString()), P("Input.PaymentMethodId", method.ToString()), P("PatientId", pid.ToString()),
            P("Lines[0].ServiceItemId", (await ServiceItemIdAsync("RAD-XR-CXR")).ToString()),
        }, "/Finance/Collect");
        Assert.Contains("billed when the examination is booked", await manual.Content.ReadAsStringAsync());
        await Ok(await admin.PostFormAsync($"/Finance/Collect?patientId={pid}&radiologyOrderId={order}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.CounterId", counter.ToString()), P("Input.PaymentMethodId", method.ToString()),
            P("PatientId", pid.ToString()), P("RadiologyOrderId", order.ToString()), P("Balances[0].ChargeId", xCharge.ToString()), P("Balances[0].PayNow", "500"),
        }, "/Finance/Collect"), "Partial payment");
        await using (var db = Factory.NewDb())
        {
            var c = await db.Charges.SingleAsync(c => c.Id == xCharge);
            Assert.Equal(500m, c.PaidAmount);
            Assert.Equal(ChargeStatus.PartiallyPaid, c.Status);
            Assert.Equal(1, await db.Receipts.CountAsync());
        }
        Assert.Contains("PartiallyPaid", await admin.GetHtmlAsync($"/Radiology/Order/{order}"));

        // Dashboard, history, profile and revenue report.
        var dash = await admin.GetHtmlAsync("/Radiology");
        Assert.Contains("data-kpi=\"xray\">2<", dash);
        Assert.Contains("data-kpi=\"ultrasound\">1<", dash);
        Assert.Contains("data-kpi=\"revenue\"", dash);
        Assert.Contains("Chest X-Ray PA", await admin.GetHtmlAsync($"/Radiology/Patient/{pid}"));
        Assert.Contains("data-profile-radiology", await admin.GetHtmlAsync($"/Patients/Profile/{pid}"));
        Assert.Contains("500.00", await admin.GetHtmlAsync("/Radiology/Reports?report=revenue&period=today"));
        Assert.Single(await AuditAsync(AuditActions.RadReportVerified));
        Assert.Equal(2, (await AuditAsync(AuditActions.RadOrderBooked)).Count);
    }

    private async Task<int> ServiceItemIdAsync(string code)
    {
        await using var db = Factory.NewDb();
        return await db.ServiceItems.Where(s => s.Code == code).Select(s => s.Id).SingleAsync();
    }

    [Fact]
    public async Task Rejection_correction_amendment_and_history_are_preserved()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        int template;
        await using (var db = Factory.NewDb()) template = await db.RadiologyTemplates.Where(t => t.Name == "Chest X-Ray – normal").Select(t => t.Id).SingleAsync();
        var cxr = await ServiceAsync(admin, "XR-CHEST", "Chest X-Ray", "XRay", 0, template);
        var pid = await RegisterPatientAsync(admin, PatientFields("Correction Patient", "Correction Father"));
        var order = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { cxr }));
        var x = await ExamAsync(order, "XR-CHEST");
        await using (var db = Factory.NewDb()) Assert.Equal(ChargeStatus.Paid, (await db.Charges.SingleAsync(c => c.Id == x.ChargeId)).Status); // free service
        await PerformAsync(admin, x.Id);

        // A new report starts from the service's default template; nothing else is forced.
        Assert.Contains("Trachea is central.", await admin.GetHtmlAsync($"/Radiology/Report/{x.Id}"));
        var empty = await ReportAsync(admin, x.Id, true, findings: "", impression: "");
        Assert.Contains("Enter the findings or the impression", await empty.Content.ReadAsStringAsync());

        var reporter = await UserWithAsync(admin, "rad.reporter", Perm.DashboardView, Perm.RadViewXRay, Perm.RadEnterReport, Perm.RadSubmitReport);
        var verifier = await UserWithAsync(admin, "rad.verifier", RadiologistPerms);
        await Ok(await ReportAsync(reporter, x.Id, true, impression: "Right lower zone consolidation."), "Submit");

        // Rejection needs a reason; the rejected text is kept as a revision and the report returns for correction.
        Assert.Contains("reason", await (await StepAsync(verifier, x.Id, "Reject", ("Reason", ""))).Content.ReadAsStringAsync());
        await Ok(await StepAsync(verifier, x.Id, "Reject", ("Reason", "Wrong side – check the film")), "Reject");
        x = await ExamAsync(order, "XR-CHEST");
        Assert.Equal(RadiologyExamStatus.ReportPending, x.Status);
        Assert.Equal("Wrong side – check the film", x.CorrectionReason);
        Assert.Contains("XR-CHEST", await reporter.GetHtmlAsync("/Radiology/Reporting"));
        // The reporter (without "Edit Draft Report") may still correct their own report.
        await Ok(await ReportAsync(reporter, x.Id, true, impression: "Left lower zone consolidation."), "Corrected");
        await Ok(await StepAsync(verifier, x.Id, "Verify"), "Verify again");

        // A verified report is never overwritten silently: direct edits are refused (page, service and database).
        Assert.Contains("verified and cannot be changed", await (await ReportAsync(reporter, x.Id, false, impression: "Changed")).Content.ReadAsStringAsync());
        await using (var db = Factory.NewDb())
        {
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"RadiologyExams\" SET \"Impression\" = 'tampered' WHERE \"Id\" = {0}", x.Id));
            Assert.Contains("cannot be changed directly", ex.Message + ex.InnerException?.Message);
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"RadiologyExamEvents\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"RadiologyExams\" WHERE \"Id\" = {0}", x.Id));
        }

        // Amendment: needs a reason and the permission, keeps the verified text, and must be verified again.
        Denied(await StepAsync(reporter, x.Id, "Amend", ("Reason", "Typo")), "Amend without permission");
        await Ok(await StepAsync(admin, x.Id, "Amend", ("Reason", "Size of the lesion added")), "Amend");
        x = await ExamAsync(order, "XR-CHEST");
        Assert.Equal(RadiologyExamStatus.ReportPending, x.Status);
        Assert.Null(x.VerifiedByUserId);
        await Ok(await ReportAsync(reporter, x.Id, true, impression: "Left lower zone consolidation, 3 cm."), "Amended report");
        await Ok(await StepAsync(verifier, x.Id, "Verify"), "Verify amended");
        await Ok(await StepAsync(verifier, x.Id, "Release"), "Release");
        await using (var db = Factory.NewDb())
        {
            var revisions = await db.RadiologyReportRevisions.Where(r => r.ExamId == x.Id).OrderBy(r => r.RevisionNo).ToListAsync();
            Assert.Equal(2, revisions.Count);
            Assert.Contains("Right lower zone", revisions[0].Snapshot);
            Assert.Contains("Left lower zone consolidation.", revisions[1].Snapshot);
            Assert.Equal(RadiologyExamStatus.Verified, revisions[1].PreviousStatus);
            Assert.Equal("Left lower zone consolidation, 3 cm.", (await db.RadiologyExams.SingleAsync(e => e.Id == x.Id)).Impression);
        }
        var page = await admin.GetHtmlAsync($"/Radiology/Exam/{x.Id}");
        Assert.Contains("data-revisions", page);
        Assert.Contains("Size of the lesion added", page);

        // Templates edited later never change saved reports.
        await Ok(await admin.PostFormAsync($"/Radiology/Templates/Edit/{template}", new[]
        {
            P("Input.Name", "Chest X-Ray – normal"), P("Input.Modality", "XRay"), P("Input.Findings", "NEW TEMPLATE TEXT"), P("Input.Impression", "Normal"), P("Input.IsActive", "true"),
        }), "Template");
        Assert.DoesNotContain("NEW TEMPLATE TEXT", await admin.GetHtmlAsync($"/Print/RadiologyReport/{x.Id}"));

        // Cancelling: a verified examination cannot be cancelled; an open one needs a reason and stays in history; its unpaid charge is cancelled.
        Assert.Contains("cannot be cancelled", await (await StepAsync(admin, x.Id, "Cancel", ("Reason", "No"))).Content.ReadAsStringAsync());
        var paid = await ServiceAsync(admin, "XR-KNEE2", "Knee X-Ray", "XRay", 600);
        var o2 = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { paid }));
        var k = await ExamAsync(o2, "XR-KNEE2");
        Assert.Contains("reason", await (await StepAsync(admin, k.Id, "Cancel", ("Reason", ""))).Content.ReadAsStringAsync());
        await Ok(await StepAsync(admin, k.Id, "Cancel", ("Reason", "Patient left")), "Cancel");
        await using (var db = Factory.NewDb())
        {
            var e = await db.RadiologyExams.SingleAsync(e => e.Id == k.Id);
            Assert.Equal(RadiologyExamStatus.Cancelled, e.Status);
            Assert.Equal("Patient left", e.CancelReason);
            Assert.Equal(ChargeStatus.Cancelled, (await db.Charges.SingleAsync(c => c.Id == e.ChargeId)).Status);
            var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"RadiologyExams\" SET \"Status\" = 'Booked' WHERE \"Id\" = {0}", k.Id));
            Assert.Contains("cannot be re-opened", ex.Message + ex.InnerException?.Message);
        }
        Assert.Contains("Patient left", await admin.GetHtmlAsync("/Radiology/Reports?report=cancelled&period=today"));
        Assert.Single(await AuditAsync(AuditActions.RadReportRejected));
        Assert.Single(await AuditAsync(AuditActions.RadReportAmended));
        Assert.Single(await AuditAsync(AuditActions.RadExamCancelled));
    }

    [Fact]
    public async Task Government_bookings_need_a_visit_and_permissions_are_enforced_on_the_server()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        await using (var db = Factory.NewDb()) Assert.False((await db.RadiologySettings.SingleAsync()).AllowBookingWithoutVisit); // Government default
        var cxr = await ServiceAsync(admin, "XR-G", "Chest X-Ray", "XRay", 0);
        var usg = await ServiceAsync(admin, "US-G", "Ultrasound KUB", "Ultrasound", 0);
        var pid = await RegisterPatientAsync(admin, PatientFields("Government Rad Patient", "Gov Father"));

        var noVisit = await PostBookAsync(admin, pid, new[] { cxr });
        Assert.Equal(HttpStatusCode.OK, noVisit.StatusCode);
        Assert.Contains("only allow bookings against a registered visit", await noVisit.Content.ReadAsStringAsync());
        var visit = await RegisterOpdAsync(admin, pid);
        var order = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { cxr, usg }, visit: visit));
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(visit, (await db.RadiologyOrders.SingleAsync(o => o.Id == order)).EncounterId);
            Assert.All(await db.Charges.Where(c => c.RadiologyOrderId == order).ToListAsync(), c => { Assert.Equal(0m, c.NetAmount); Assert.Equal(ChargeStatus.Paid, c.Status); });
            await db.Database.ExecuteSqlRawAsync("UPDATE \"PatientEncounters\" SET \"EncounterDate\" = {0} WHERE \"Id\" = {1}", Today.AddDays(-10), visit);
        }
        Assert.Contains("older than", await (await PostBookAsync(admin, pid, new[] { cxr }, visit: visit)).Content.ReadAsStringAsync());
        var other = await RegisterPatientAsync(admin, PatientFields("Other Gov Patient", "Other Father"));
        Assert.Contains("does not belong", await (await PostBookAsync(admin, other, new[] { cxr }, visit: visit)).Content.ReadAsStringAsync());

        var x = await ExamAsync(order, "XR-G");
        var u = await ExamAsync(order, "US-G");

        // X-Ray technician: only the X-Ray queue and examination steps.
        var xtech = await UserWithAsync(admin, "xray.tech", Perm.DashboardView, Perm.RadViewXRay, Perm.RadStartExam, Perm.RadCompleteExam);
        var nav = await xtech.GetHtmlAsync("/Radiology/Worklist/XRay");
        Assert.Contains("Government Rad Patient", nav);
        Assert.DoesNotContain("/Radiology/Verify\"", nav);
        Assert.DoesNotContain("/Radiology/Book\"", nav);
        Denied(await xtech.GetAsync("/Radiology/Worklist/Ultrasound"), "Ultrasound worklist");
        Denied(await xtech.GetAsync($"/Radiology/Exam/{u.Id}"), "Ultrasound examination");
        Denied(await xtech.GetAsync("/Radiology/Book"), "Booking page");
        Denied(await xtech.PostFormAsync("/Radiology/Worklist/XRay", None, $"/Radiology/Exam/{x.Id}?handler=Verify"), "Verify");
        Denied(await xtech.PostFormAsync("/Radiology/Worklist/XRay", None, $"/Radiology/Exam/{x.Id}?handler=Cancel"), "Cancel");
        Denied(await xtech.PostFormAsync("/Radiology/Worklist/XRay", None, $"/Radiology/Worklist/Ultrasound?handler=Start&examId={u.Id}"), "Start ultrasound");
        Denied(await xtech.GetAsync($"/Radiology/Report/{x.Id}"), "Report page");
        Denied(await xtech.GetAsync("/Radiology/Settings"), "Settings");
        Denied(await xtech.GetAsync("/Radiology/Reports"), "Statistics");
        Assert.Equal(HttpStatusCode.Redirect, (await xtech.PostFormAsync("/Radiology/Worklist/XRay", None, $"/Radiology/Worklist/XRay?handler=Start&examId={x.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await xtech.PostFormAsync("/Radiology/Worklist/XRay", new[] { P("PerformedBy", "Tech Gul") }, $"/Radiology/Worklist/XRay?handler=Complete&examId={x.Id}")).StatusCode);
        Assert.Equal(RadiologyExamStatus.ExamCompleted, (await ExamAsync(order, "XR-G")).Status);

        // Reception: books, but cannot run examinations or see fees.
        var reception = await UserWithAsync(admin, "rad.reception", Perm.DashboardView, Perm.RadCreateBooking, Perm.PatientsSearch);
        Assert.DoesNotContain("data-price", (await reception.GetHtmlAsync($"/Radiology/Book?patientId={pid}")).Replace("data-price=\"0\"", ""));
        Denied(await reception.PostFormAsync($"/Radiology/Book?patientId={pid}", None, $"/Radiology/Exam/{u.Id}?handler=Start"), "Start");
        Assert.Contains("discount", (await (await PostBookAsync(reception, pid, new[] { usg }, visit: await RegisterOpdAsync(admin, pid), extra: ($"Discounts[{usg}]", "10"))).Content.ReadAsStringAsync()).ToLowerInvariant());

        // Verification queue only for verifiers; a viewer without radiology rights sees nothing.
        var viewer = await UserWithAsync(admin, "no.radiology", Perm.DashboardView);
        Denied(await viewer.GetAsync("/Radiology"), "Dashboard");
        Denied(await viewer.GetAsync($"/Radiology/Patient/{pid}"), "History");
        Denied(await viewer.GetAsync($"/Print/RadiologyReport/{x.Id}"), "Report print");
        Assert.DoesNotContain("Radiology Dashboard", await viewer.GetHtmlAsync("/"));
        Assert.NotEmpty(await AuditAsync(AuditActions.RadExamStatus));
    }

    [Fact]
    public async Task Attachments_are_stored_as_files_with_permissions_and_survive_backup_archives()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var usg = await ServiceAsync(admin, "US-OBS1", "Obstetric Ultrasound", "Ultrasound", 1200);
        var pid = await RegisterPatientAsync(admin, PatientFields("Attachment Patient", "Attach Husband", "Female"));
        var order = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { usg }));
        var u = await ExamAsync(order, "US-OBS1");

        var bad = await admin.PostMultipartAsync($"/Radiology/Exam/{u.Id}", new[] { P("Description", "notes") },
            ("Upload", "notes.txt", System.Text.Encoding.UTF8.GetBytes("plain text pretending to be an image"), "image/png"), $"/Radiology/Exam/{u.Id}?handler=Upload");
        Assert.Contains("Only JPG, PNG, PDF or DICOM", await bad.Content.ReadAsStringAsync());
        await Ok(await admin.PostMultipartAsync($"/Radiology/Exam/{u.Id}", new[] { P("Description", "Fetal image") },
            ("Upload", "fetus.png", TinyPng, "image/png"), $"/Radiology/Exam/{u.Id}?handler=Upload"), "Upload PNG");
        var dicom = new byte[200];
        "DICM"u8.ToArray().CopyTo(dicom, 128);
        await Ok(await admin.PostMultipartAsync($"/Radiology/Exam/{u.Id}", None, ("Upload", "scan.dcm", dicom, "application/octet-stream"), $"/Radiology/Exam/{u.Id}?handler=Upload"), "Upload DICOM");

        RadiologyAttachment png;
        await using (var db = Factory.NewDb())
        {
            var files = await db.RadiologyAttachments.OrderBy(a => a.Id).ToListAsync();
            Assert.Equal(2, files.Count);
            png = files[0];
            Assert.Equal((RadiologyFileKind.Image, "image/png", pid), (png.Kind, png.ContentType, png.PatientId));
            Assert.Equal(RadiologyFileKind.Dicom, files[1].Kind);
            Assert.Equal(64, png.Sha256.Length);
            Assert.DoesNotContain("fetus", png.StoredPath); // stored under a random name
        }
        var r = await admin.GetAsync($"/Radiology/Attachment/{png.Id}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(TinyPng, await r.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/dicom", (await admin.GetAsync($"/Radiology/Attachment/{png.Id + 1}")).Content.Headers.ContentType!.MediaType);
        Assert.NotEmpty(await AuditAsync(AuditActions.RadAttachmentViewed));

        // Permissions: viewing and managing files are separate rights.
        var viewer = await UserWithAsync(admin, "us.viewer", Perm.DashboardView, Perm.RadViewUltrasound);
        Denied(await viewer.GetAsync($"/Radiology/Attachment/{png.Id}"), "Download without permission");
        var fileViewer = await UserWithAsync(admin, "us.files", Perm.DashboardView, Perm.RadViewUltrasound, Perm.RadViewAttachments);
        Assert.Equal(HttpStatusCode.OK, (await fileViewer.GetAsync($"/Radiology/Attachment/{png.Id}")).StatusCode);
        Denied(await fileViewer.PostMultipartAsync($"/Radiology/Exam/{u.Id}", None, ("Upload", "x.png", TinyPng, "image/png"), $"/Radiology/Exam/{u.Id}?handler=Upload"), "Upload without permission");
        var xrayOnly = await UserWithAsync(admin, "xr.files", Perm.DashboardView, Perm.RadViewXRay, Perm.RadViewAttachments);
        Denied(await xrayOnly.GetAsync($"/Radiology/Attachment/{png.Id}"), "Other modality's file");

        // Removal hides the file (reason required) – the record and the file stay; the record cannot be rewritten.
        Assert.Contains("reason", await (await StepAsync(admin, u.Id, $"RemoveFile&attachmentId={png.Id}", ("Reason", ""))).Content.ReadAsStringAsync());
        await Ok(await StepAsync(admin, u.Id, $"RemoveFile&attachmentId={png.Id}", ("Reason", "Wrong patient image")), "Remove");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/Radiology/Attachment/{png.Id}")).StatusCode);
        await using (var db = Factory.NewDb())
        {
            var a = await db.RadiologyAttachments.SingleAsync(x => x.Id == png.Id);
            Assert.True(a.IsRemoved);
            Assert.Equal("Wrong patient image", a.RemoveReason);
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"RadiologyAttachments\" SET \"StoredPath\" = 'x' WHERE \"Id\" = {0}", a.Id));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"RadiologyAttachments\" WHERE \"Id\" = {0}", a.Id));
        }
        Assert.Single(await AuditAsync(AuditActions.RadAttachmentRemoved));

        // Backup / restore of the storage folder: archived next to the database backup and put back without overwriting.
        var root = Path.Combine(Path.GetTempPath(), "bnhms-rad-" + Guid.NewGuid().ToString("N"));
        var store = new RadiologyFileStore(Options.Create(new HmsOptions { RadiologyStorageDirectory = root }));
        await using (var input = new MemoryStream(TinyPng))
        {
            var saved = await store.SaveAsync(input, 1_000_000, DateTime.UtcNow, CancellationToken.None);
            Assert.NotNull(saved);
            var zip = Path.Combine(Path.GetTempPath(), "bnhms-" + Guid.NewGuid().ToString("N") + ".backup");
            Assert.Equal(1, store.ArchiveTo(RadiologyFileStore.ArchiveNameFor(zip)));
            var path = store.PathOf(saved!.Value.StoredPath)!;
            File.Delete(path);
            Assert.Equal(1, store.RestoreFrom(RadiologyFileStore.ArchiveNameFor(zip)));
            Assert.Equal(TinyPng, await File.ReadAllBytesAsync(path));
            Assert.Equal(0, store.RestoreFrom(RadiologyFileStore.ArchiveNameFor(zip))); // existing files are never overwritten
            Assert.Null(store.PathOf("../../etc/passwd"));
        }
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Concurrent_bookings_get_unique_order_numbers_and_tokens()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var cxr = await ServiceAsync(admin, "XR-C", "Chest X-Ray", "XRay", 300);
        var patients = new List<long>();
        for (var i = 0; i < 8; i++) patients.Add(await RegisterPatientAsync(admin, PatientFields($"Queue Patient {i}", $"Queue Father {i}")));
        var computers = new List<Browser>();
        for (var i = 0; i < 4; i++) computers.Add(await UserWithAsync(admin, $"rad.desk{i}", Perm.DashboardView, Perm.RadCreateBooking, Perm.RadViewXRay));
        var results = await Task.WhenAll(patients.Select((p, i) => PostBookAsync(computers[i % 4], p, new[] { cxr })));
        foreach (var r in results) await OrderIdAsync(r);
        await using var db = Factory.NewDb();
        var exams = await db.RadiologyExams.Include(e => e.Order).ToListAsync();
        Assert.Equal(8, exams.Count);
        Assert.Equal(Enumerable.Range(1, 8), exams.Select(e => e.TokenNumber).OrderBy(t => t));
        Assert.Equal(8, exams.Select(e => e.Order!.OrderNumber).Distinct().Count());
        Assert.Equal(8, await db.Charges.CountAsync(c => c.RadiologyOrderId != null));

        // Rescheduling to another day gives a new token on that day.
        var first = exams.OrderBy(e => e.TokenNumber).First();
        await Ok(await StepAsync(admin, first.Id, "Reschedule", ("NewDate", Today.AddDays(1).ToString("yyyy-MM-dd")), ("NewTime", "10:30")), "Reschedule");
        var moved = await db.RadiologyExams.AsNoTracking().SingleAsync(e => e.Id == first.Id);
        Assert.Equal((Today.AddDays(1), 1), (moved.ScheduledDate, moved.TokenNumber));
        Assert.Contains("past", await (await StepAsync(admin, first.Id, "Reschedule", ("NewDate", Today.AddDays(-1).ToString("yyyy-MM-dd")))).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Radiology_reports_exports_and_previous_phases_still_work()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var cxr = await ServiceAsync(admin, "XR-R", "Chest X-Ray", "XRay", 400);
        var pid = await RegisterPatientAsync(admin, PatientFields("Report Patient", "Report Father"));
        var order = await OrderIdAsync(await PostBookAsync(admin, pid, new[] { cxr }, extra: ("Input.ReferringDepartmentId", (await DepartmentIdAsync("OPD")).ToString())));
        Assert.Contains("OPD", await admin.GetHtmlAsync("/Radiology/Reports?report=departments&period=today"));
        foreach (var key in ReportsKeys)
        {
            var html = await admin.GetHtmlAsync($"/Radiology/Reports?report={key}&period=today");
            Assert.Contains($"data-rad-report=\"{key}\"", html);
        }
        var xlsx = await admin.GetAsync("/Radiology/Reports?handler=Export&format=xlsx&report=services&period=today");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
        var csv = await (await admin.GetAsync("/Radiology/Reports?handler=Export&format=csv&report=xray&period=today")).Content.ReadAsStringAsync();
        Assert.Contains("Report Patient", csv);
        Assert.Equal(2, (await AuditAsync(AuditActions.RadExported)).Count);

        // Every radiology page opens; earlier modules are unchanged.
        foreach (var url in new[] { "/Radiology", "/Radiology?view=XRay", "/Radiology/Book", $"/Radiology/Book?patientId={pid}", "/Radiology/Bookings", "/Radiology/Reporting",
                     "/Radiology/Verify", "/Radiology/History?q=Report", $"/Radiology/Order/{order}", "/Radiology/Services", "/Radiology/Services/Edit", "/Radiology/Templates/Edit",
                     "/Radiology/Settings", "/Radiology/Worklist/Ultrasound",
                     "/", "/Patients", "/Lab", "/Lab/Book", "/Pharmacy", "/Finance", "/Finance/Collect", "/Emergency", "/Appointments" })
            await admin.GetHtmlAsync(url);
        // Statistics follow the user's modality: an Ultrasound-only user cannot open X-Ray figures.
        var usStats = await UserWithAsync(admin, "us.stats", Perm.DashboardView, Perm.RadViewStatistics, Perm.RadViewUltrasound, Perm.RadDashboard);
        Denied(await usStats.GetAsync("/Radiology/Reports?report=xray"), "X-Ray statistics");
        Denied(await usStats.GetAsync("/Radiology/Reports?report=revenue"), "Revenue without financial permission");
        var dash = await usStats.GetHtmlAsync("/Radiology");
        Assert.DoesNotContain("data-kpi=\"xray\"", dash);
        Assert.DoesNotContain("data-kpi=\"revenue\"", dash);
    }

    private static readonly string[] ReportsKeys = { "summary", "xray", "ultrasound", "services", "departments", "doctors", "pending-exams", "pending-reports", "verified", "cancelled", "revenue" };
}
