using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ot;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Phase 8 – Operation Theatre: theatres, catalogue, booking and conflicts, emergency workflow, pre-op, consent, team, anesthesia,
/// safety checklist with exceptions, operation notes (draft / final / amendment), OT stock (major OT) with the Minor OT unchanged,
/// specimens, recovery, post-op linked to IPD, CSSD, billing through Phase 3, printing, reports, permissions, audit and DB protection.</summary>
public partial class OtTests : Phase2TestBase
{
    [GeneratedRegex(@"/Ot/Surgery/(\d+)")] private static partial Regex SurgeryRegex();
    [GeneratedRegex(@"/Ipd/Admission/(\d+)")] private static partial Regex AdmissionRegex();

    private DateTime LocalNow => Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5);
    private string Tomorrow(int hour, int minute = 0) => LocalNow.Date.AddDays(1).AddHours(hour).AddMinutes(minute).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    private DateOnly Today => DateOnly.FromDateTime(LocalNow);

    private static async Task Ok(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, $"{what} failed: " + Errors(await r.Content.ReadAsStringAsync()));

    private static async Task<string> Refused(HttpResponseMessage r, string what)
    {
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"{what} should be refused with a message but returned {(int)r.StatusCode} {r.Headers.Location}");
        return Errors(await r.Content.ReadAsStringAsync());
    }

    private static void Denied(HttpResponseMessage r, string what) =>
        Assert.True(r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied")),
            $"{what} should be denied but returned {(int)r.StatusCode} {r.Headers.Location}");

    private static long SurgeryId(HttpResponseMessage r)
    {
        var m = SurgeryRegex().Match(r.Headers.Location?.OriginalString ?? "");
        Assert.True(m.Success, "Unexpected redirect " + r.Headers.Location);
        return long.Parse(m.Groups[1].Value);
    }

    private async Task<int> DoctorAsync(Browser admin, string name)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Opd/Setup", new[] { P("DoctorForm.Name", name), P("DoctorForm.IsActive", "true") }, "/Opd/Setup?handler=Doctor")).StatusCode);
        await using var db = Factory.NewDb();
        return await db.Doctors.Where(d => d.Name == name).Select(d => d.Id).SingleAsync();
    }

    private async Task<int> TheatreAsync(Browser admin, string code, string name, int? stock = null, int? charge = null, bool available = true, string type = "General")
    {
        var f = new List<KeyValuePair<string, string>>
        {
            P("Theatre.Code", code), P("Theatre.Name", name), P("Theatre.Type", type), P("Theatre.CleaningMinutes", "30"), P("Theatre.IsActive", "true"),
            P("Theatre.IsAvailable", available ? "true" : "false"), P("Theatre.UnavailableReason", available ? "" : "Under maintenance"),
            P("Theatre.StockLocationId", stock?.ToString() ?? ""), P("Theatre.ChargeServiceItemId", charge?.ToString() ?? ""),
        };
        await Ok(await admin.PostFormAsync("/Ot/Theatres", f, "/Ot/Theatres?handler=Theatre"), "Theatre " + code);
        await using var db = Factory.NewDb();
        return await db.OtTheatres.Where(t => t.Code == code).Select(t => t.Id).SingleAsync();
    }

    private async Task<int> ProcedureAsync(string code)
    {
        await using var db = Factory.NewDb();
        return await db.OtProcedures.Where(p => p.Code == code).Select(p => p.Id).SingleAsync();
    }

    private static Task<HttpResponseMessage> BookAsync(Browser b, long patientId, int theatreId, string? startLocal, int procedureId, int? surgeon = null, int? anesthetist = null,
        string priority = "Elective", Guid? key = null, string? emergencyReason = null, int duration = 60, bool overrideConflict = false) =>
        b.PostFormAsync($"/Ot/Book?patientId={patientId}", new[]
        {
            P("Input.IdempotencyKey", (key ?? Guid.NewGuid()).ToString()), P("Input.ProcedureId", procedureId.ToString()), P("Input.TheatreId", theatreId.ToString()),
            P("Input.StartLocal", startLocal ?? ""), P("Input.DurationMinutes", duration.ToString()), P("Input.Priority", priority), P("Input.Diagnosis", "Acute appendicitis (surgeon's diagnosis)"),
            P("Input.SurgeonDoctorId", surgeon?.ToString() ?? ""), P("Input.AnesthetistDoctorId", anesthetist?.ToString() ?? ""), P("Input.EmergencyReason", emergencyReason ?? ""),
            P("Input.OverrideStaffConflict", overrideConflict ? "true" : "false"), P("Input.OverrideReason", overrideConflict ? "Life-threatening emergency" : ""),
        });

    private static Task<HttpResponseMessage> ActAsync(Browser b, long surgeryId, string handler, string tab, params (string, string)[] fields) =>
        b.PostFormAsync($"/Ot/Surgery/{surgeryId}?tab={tab}", fields.Select(f => P(f.Item1, f.Item2)), $"/Ot/Surgery/{surgeryId}?handler={handler}");

    private static Task<HttpResponseMessage> RecordAsync(Browser b, long surgeryId, RecordKind kind, string tab, params (string Key, string Value)[] values)
    {
        var f = new List<(string, string)> { ("Record.Kind", kind.ToString()) };
        foreach (var (k, v) in values) f.Add(k.StartsWith("Record.") ? (k, v) : ($"Record.Values[{k}]", v));
        return ActAsync(b, surgeryId, "Record", tab, f.ToArray());
    }

    private async Task<(string, string)[]> AllDoneAsync(ChecklistStage stage)
    {
        await using var db = Factory.NewDb();
        var ids = await db.OtChecklistItems.Where(i => i.Stage == stage && i.IsActive).Select(i => i.Id).ToListAsync();
        return ids.Select(id => ($"Answers[{id}]", "Done")).Append(("Stage", stage.ToString())).ToArray();
    }

    private async Task CompleteStageAsync(Browser b, long surgeryId, ChecklistStage stage) =>
        await Ok(await ActAsync(b, surgeryId, "CompleteStage", stage == ChecklistStage.PreOp ? "preop" : "safety", await AllDoneAsync(stage)), "Complete " + stage);

    private async Task<Surgery> SurgeryAsync(long id)
    {
        await using var db = Factory.NewDb();
        return await db.Surgeries.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    private async Task<int> ServiceAsync(Browser admin, string code, string name, decimal fee, string kind)
    {
        await Ok(await admin.PostFormAsync("/Finance/Services", new[]
        {
            P("Form.Code", code), P("Form.Name", name), P("Form.Kind", kind), P("Form.Fee", fee.ToString(CultureInfo.InvariantCulture)),
            P("Form.IsFree", fee == 0 ? "true" : "false"), P("Form.AllowPartialPayment", "true"), P("Form.IsActive", "true"),
        }), "Service " + code);
        await using var db = Factory.NewDb();
        return await db.ServiceItems.Where(s => s.Code == code).Select(s => s.Id).SingleAsync();
    }

    /// <summary>Emergency surgery started "now" – the shortest path to an operation in progress.</summary>
    private async Task<long> EmergencyAsync(Browser admin, long patientId, int theatre, int surgeon)
    {
        var r = await BookAsync(admin, patientId, theatre, null, await ProcedureAsync("GS-APP"), surgeon, priority: "Emergency", emergencyReason: "Perforation suspected");
        await Ok(r, "Emergency booking");
        return SurgeryId(r);
    }

    private async Task StartAsync(Browser admin, long sid)
    {
        await CompleteStageAsync(admin, sid, ChecklistStage.SignIn);
        await CompleteStageAsync(admin, sid, ChecklistStage.TimeOut);
        await Ok(await ActAsync(admin, sid, "Start", "intraop"), "Start surgery");
    }

    // =====================================================================================
    // 8A – theatres, catalogue, booking, conflicts, emergency, reschedule / postpone / cancel
    // =====================================================================================

    [Fact]
    public async Task Theatres_catalogue_booking_conflicts_emergency_and_schedule_changes()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        await using (var db = Factory.NewDb())
        {
            // Starter configuration is data: procedures, consent types, checklists and forms without any alert limits.
            Assert.True(await db.OtProcedures.CountAsync() >= 15);
            Assert.True(await db.OtConsentTypes.CountAsync() >= 3);
            foreach (var st in Enum.GetValues<ChecklistStage>()) Assert.True(await db.OtChecklistItems.CountAsync(i => i.Stage == st) >= 5, st + " checklist");
            Assert.False(await db.ClinicalFormFields.AnyAsync(f => f.Kind >= RecordKind.PreOpAssessment && f.Kind <= RecordKind.PostOpRecord && (f.AlertLow != null || f.AlertHigh != null)));
            Assert.Equal(0, await db.OtTheatres.CountAsync());
        }
        var ot1 = await TheatreAsync(admin, "OT1", "Main OT 1");
        var ot2 = await TheatreAsync(admin, "OT2", "Main OT 2", type: "Gynaecology");
        var ot3 = await TheatreAsync(admin, "OT3", "OT 3", available: false);
        Assert.Contains("Under maintenance", await admin.GetHtmlAsync("/Ot/Theatres"));
        var surgeon = await DoctorAsync(admin, "Dr. Surgeon Khan");
        var surgeon2 = await DoctorAsync(admin, "Dr. Second Surgeon");
        var anest = await DoctorAsync(admin, "Dr. Anesthetist Ali");
        var app = await ProcedureAsync("GS-APP");
        // The catalogue is editable (new procedure).
        await Ok(await admin.PostFormAsync("/Ot/Procedures", new[] { P("Input.Code", "GS-TEST"), P("Input.Name", "Test excision"), P("Input.DurationMinutes", "45"), P("Input.AnesthesiaTypes", "Local"), P("Input.IsActive", "true") }), "Procedure");
        Assert.Contains("Test excision", await admin.GetHtmlAsync("/Ot/Procedures?q=excision"));

        var pid = await RegisterPatientAsync(admin, PatientFields("Surgical Patient", "Father S"));
        var pid2 = await RegisterPatientAsync(admin, PatientFields("Second Patient", "Father T"));

        // Booking (a double click does not create a second surgery).
        var key = Guid.NewGuid();
        var r1 = await BookAsync(admin, pid, ot1, Tomorrow(9), app, surgeon, anest, key: key);
        await Ok(r1, "Booking");
        var s1 = SurgeryId(r1);
        Assert.Equal(s1, SurgeryId(await BookAsync(admin, pid, ot1, Tomorrow(9), app, surgeon, anest, key: key)));
        var g = await SurgeryAsync(s1);
        Assert.Matches(@"^SRG-\d{4}-\d{6}$", g.Number);
        Assert.Equal((SurgeryStatus.Scheduled, 30), (g.Status, g.CleaningMinutes));
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.Surgeries.CountAsync());
            Assert.Equal(2, await db.SurgeryTeamMembers.CountAsync(m => m.SurgeryId == s1));
        }
        Assert.Single(await AuditAsync(AuditActions.OtSurgeryDuplicateBlocked));

        // Overlap: same theatre within the surgery + cleaning interval is refused; after it, accepted.
        Assert.Contains("not free", await Refused(await BookAsync(admin, pid2, ot1, Tomorrow(10, 10), app, surgeon2), "Overlap with cleaning interval"));
        await Ok(await BookAsync(admin, pid2, ot1, Tomorrow(10, 45), app, surgeon2), "After cleaning interval");
        // Surgeon / anesthetist cannot be in two theatres at the same time.
        Assert.Contains("Team schedule conflict", await Refused(await BookAsync(admin, pid2, ot2, Tomorrow(9, 15), app, surgeon), "Surgeon conflict"));
        Assert.Contains("Team schedule conflict", await Refused(await BookAsync(admin, pid2, ot2, Tomorrow(9, 30), app, surgeon2, anest), "Anesthetist conflict"));
        Assert.Contains("unavailable", await Refused(await BookAsync(admin, pid2, ot3, Tomorrow(14), app, surgeon2), "Unavailable theatre"));
        Assert.Contains("surgeon", (await Refused(await BookAsync(admin, pid2, ot2, Tomorrow(15), app), "Without surgeon")).ToLowerInvariant());

        // Emergency workflow: a reason is required; it may start now, without a full schedule entry.
        Assert.Contains("reason", (await Refused(await BookAsync(admin, pid2, ot2, null, app, priority: "Emergency"), "Emergency without reason")).ToLowerInvariant());
        var em = await EmergencyAsync(admin, pid2, ot2, surgeon2);
        var eg = await SurgeryAsync(em);
        Assert.Equal((SurgeryPriority.Emergency, SurgeryStatus.Scheduled), (eg.Priority, eg.Status));
        Assert.True(Math.Abs((eg.PlannedStartUtc - Factory.Clock.GetUtcNow().UtcDateTime).TotalMinutes) < 5);
        Assert.Contains("Perforation suspected", await admin.GetHtmlAsync($"/Ot/Surgery/{em}"));
        Assert.Single(await AuditAsync(AuditActions.OtEmergencyBooked));

        // Reschedule needs a reason and keeps history; a conflict is checked again.
        Assert.Contains("reason", (await Refused(await ActAsync(admin, s1, "Reschedule", "overview", ("TheatreId", ot1.ToString()), ("StartLocal", Tomorrow(13)), ("Duration", "60")), "Reschedule without reason")).ToLowerInvariant());
        await Ok(await ActAsync(admin, s1, "Reschedule", "overview", ("TheatreId", ot1.ToString()), ("StartLocal", Tomorrow(13)), ("Duration", "60"), ("Reason", "Surgeon request")), "Reschedule");
        Assert.Equal(LocalNow.Date.AddDays(1).AddHours(13).AddHours(-5), (await SurgeryAsync(s1)).PlannedStartUtc);
        Assert.Contains("Surgeon request", await admin.GetHtmlAsync($"/Ot/Surgery/{s1}?tab=history"));

        // Ready requires the pre-op checklist; postpone and cancel require a reason.
        Assert.Contains("pre-operative checklist", await Refused(await ActAsync(admin, s1, "Status", "overview", ("NewStatus", "Ready")), "Ready before pre-op"));
        await Refused(await ActAsync(admin, s1, "Status", "overview", ("NewStatus", "Postponed")), "Postpone without reason");
        await Ok(await ActAsync(admin, s1, "Status", "overview", ("NewStatus", "Postponed"), ("Reason", "Patient unfit today")), "Postpone");
        Assert.Contains("rescheduling", await Refused(await ActAsync(admin, s1, "Status", "overview", ("NewStatus", "Scheduled")), "Postponed → scheduled directly"));
        await Ok(await ActAsync(admin, s1, "Reschedule", "overview", ("TheatreId", ot1.ToString()), ("StartLocal", Tomorrow(16)), ("Duration", "60"), ("Reason", "New date agreed")), "Reschedule postponed");
        Assert.Equal(SurgeryStatus.Scheduled, (await SurgeryAsync(s1)).Status);
        await Refused(await ActAsync(admin, s1, "Status", "overview", ("NewStatus", "Cancelled")), "Cancel without reason");
        await Ok(await ActAsync(admin, s1, "Status", "overview", ("NewStatus", "Cancelled"), ("Reason", "Patient refused surgery")), "Cancel");
        Assert.Equal(SurgeryStatus.Cancelled, (await SurgeryAsync(s1)).Status);
        // A cancelled surgery frees the slot and accepts no further changes.
        await Ok(await BookAsync(admin, pid2, ot1, Tomorrow(16), app, surgeon), "Slot reused after cancel");
        await Refused(await ActAsync(admin, s1, "Reschedule", "overview", ("TheatreId", ot1.ToString()), ("StartLocal", Tomorrow(18)), ("Duration", "60"), ("Reason", "x")), "Reschedule cancelled");

        // Dashboard, schedule and patient profile.
        var dash = await admin.GetHtmlAsync("/Ot");
        Assert.Contains("data-ot-kpis", dash);
        Assert.Contains("Perforation suspected", await admin.GetHtmlAsync($"/Ot/Surgery/{em}?tab=overview"));
        var sched = await admin.GetHtmlAsync($"/Ot/Schedule?date={LocalNow.Date.AddDays(1):yyyy-MM-dd}");
        Assert.Contains("Second Patient", sched);
        Assert.Contains("data-profile-ot", await admin.GetHtmlAsync($"/Patients/Profile/{pid}"));
        Assert.True((await AuditAsync(AuditActions.OtSurgeryBooked)).Count >= 3);
        Assert.Single(await AuditAsync(AuditActions.OtSurgeryCancelled));
    }

    // =====================================================================================
    // 8B / 8C – pre-op, investigations, consent + upload, team, anesthesia, safety checklist,
    //           intra-op notes (draft / final / amend), end → recovery, post-op linked to IPD
    // =====================================================================================

    [Fact]
    public async Task Preop_consent_team_checklist_exception_notes_recovery_and_postop_linked_to_ipd()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var ot = await TheatreAsync(admin, "OT1", "Main OT 1");
        var surgeon = await DoctorAsync(admin, "Dr. Surgeon Khan");
        var pid = await RegisterPatientAsync(admin, PatientFields("Clinical Patient", "Father C"));
        // The patient is admitted (IPD); the surgery links to the admission automatically.
        var adm = await admin.PostFormAsync($"/Ipd/Admit?patientId={pid}", new[]
        {
            P("PatientId", pid.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"),
            P("Input.AdmittingDoctorName", "Dr. Surgeon Khan"), P("Input.AdmissionDiagnosis", "Appendicitis"), P("Input.Allergies", "Penicillin"),
        });
        await Ok(adm, "IPD admission");
        var admissionId = long.Parse(AdmissionRegex().Match(adm.Headers.Location!.OriginalString).Groups[1].Value);

        var app = await ProcedureAsync("GS-APP");
        var r = await BookAsync(admin, pid, ot, Tomorrow(9), app, surgeon);
        await Ok(r, "Booking");
        var sid = SurgeryId(r);
        Assert.Equal(admissionId, (await SurgeryAsync(sid)).IpdAdmissionId);
        Assert.Contains("Penicillin", await admin.GetHtmlAsync($"/Ot/Surgery/{sid}")); // allergy visible on the surgery banner
        Assert.Contains("data-admission-surgeries", await admin.GetHtmlAsync($"/Ipd/Admission/{admissionId}"));

        // Pre-operative assessment (configurable form) and existing investigations – viewing creates no order.
        await Ok(await RecordAsync(admin, sid, RecordKind.PreOpAssessment, "preop", ("history", "Pain RIF 2 days"), ("diagnosis", "Acute appendicitis")), "Pre-op assessment");
        int labOrders;
        await using (var db = Factory.NewDb()) labOrders = await db.LabOrders.CountAsync();
        var preop = await admin.GetHtmlAsync($"/Ot/Surgery/{sid}?tab=preop");
        Assert.Contains("data-investigations", preop);
        Assert.Contains($"/Lab/Book?patientId={pid}", preop);
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(labOrders, await db.LabOrders.CountAsync());
            var rec = await db.ClinicalRecords.SingleAsync(x => x.Kind == RecordKind.PreOpAssessment);
            Assert.Equal((sid, admissionId), (rec.SurgeryId!.Value, rec.AdmissionId!.Value));
        }

        // Pre-op checklist: mandatory items pending → warning; N/A on a mandatory item needs a remark; complete → PreOp, then Ready.
        int first;
        await using (var db = Factory.NewDb()) first = await db.OtChecklistItems.Where(i => i.Stage == ChecklistStage.PreOp && i.IsActive).OrderBy(i => i.SortOrder).Select(i => i.Id).FirstAsync();
        Assert.Contains("pending", (await Refused(await ActAsync(admin, sid, "CompleteStage", "preop", ("Stage", "PreOp"), ($"Answers[{first}]", "Done")), "Pre-op with pending items")).ToLowerInvariant());
        await Refused(await ActAsync(admin, sid, "Checklist", "preop", ("Stage", "PreOp"), ($"Answers[{first}]", "NotApplicable")), "N/A without remark");
        await CompleteStageAsync(admin, sid, ChecklistStage.PreOp);
        Assert.Equal(SurgeryStatus.PreOp, (await SurgeryAsync(sid)).Status);
        await Ok(await ActAsync(admin, sid, "Status", "overview", ("NewStatus", "Ready")), "Ready");

        // Consent: configurable type, printable template, signed scan stored as a file (not in the database).
        int ct;
        await using (var db = Factory.NewDb()) ct = await db.OtConsentTypes.OrderBy(c => c.SortOrder).Select(c => c.Id).FirstAsync();
        await Refused(await ActAsync(admin, sid, "Consent", "consent", ("Consent.ConsentTypeId", ct.ToString()), ("Consent.GivenBy", "Clinical Patient")), "Consent without staff");
        await Ok(await ActAsync(admin, sid, "Consent", "consent", ("Consent.ConsentTypeId", ct.ToString()), ("Consent.GivenBy", "Clinical Patient"), ("Consent.Status", "Obtained"),
            ("Consent.ResponsibleStaff", "Dr. Surgeon Khan"), ("Consent.Witness", "Nurse Ayesha")), "Consent");
        long consentId;
        await using (var db = Factory.NewDb()) consentId = await db.SurgeryConsents.Where(c => c.SurgeryId == sid).Select(c => c.Id).SingleAsync();
        var pdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();
        Assert.Contains("PDF", await Refused(await admin.PostMultipartAsync($"/Ot/Surgery/{sid}?tab=consent", new[] { P("DocKind", "Consent"), P("ConsentId", consentId.ToString()) },
            ("Upload", "consent.exe", "MZ-not-a-scan"u8.ToArray(), "application/octet-stream"), $"/Ot/Surgery/{sid}?handler=Upload"), "Executable upload"));
        await Ok(await admin.PostMultipartAsync($"/Ot/Surgery/{sid}?tab=consent", new[] { P("DocKind", "Consent"), P("ConsentId", consentId.ToString()) },
            ("Upload", "consent.pdf", pdf, "application/pdf"), $"/Ot/Surgery/{sid}?handler=Upload"), "Consent scan");
        long docId;
        await using (var db = Factory.NewDb()) docId = await db.OtDocuments.Where(d => d.SurgeryId == sid).Select(d => d.Id).SingleAsync();
        var file = await admin.GetAsync($"/Ot/Document/{docId}");
        Assert.Equal("application/pdf", file.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Clinical Patient", await admin.GetHtmlAsync($"/Print/Ot/consent/{consentId}"));
        await Refused(await ActAsync(admin, sid, "ConsentStatus", "consent", ("ItemId", consentId.ToString()), ("NewConsentStatus", "Withdrawn")), "Consent change without reason");

        // Team: HMS users need some OT permission; removal keeps history.
        var plain = await CreateUserAsync(admin, "plain.user", "Usr#Pass2026");
        await Refused(await ActAsync(admin, sid, "Team", "overview", ("Team.Role", "ScrubNurse"), ("Team.UserId", plain.ToString())), "User without OT permission");
        var nurseId = await CreateUserAsync(admin, "scrub.nurse", "Usr#Pass2026");
        await SetPermissionsAsync(admin, nurseId, (Perm.OtEnterIntraOp, "grant"));
        await Ok(await ActAsync(admin, sid, "Team", "overview", ("Team.Role", "ScrubNurse"), ("Team.UserId", nurseId.ToString())), "Scrub nurse");
        var anest = await DoctorAsync(admin, "Dr. Anesthetist Ali");
        await Ok(await ActAsync(admin, sid, "Team", "overview", ("Team.Role", "Anesthetist"), ("Team.DoctorId", anest.ToString())), "Anesthetist");
        Assert.True((await AuditAsync(AuditActions.OtTeamChanged)).Count >= 2);

        // Anesthesia assessment + drugs; values exactly as entered (no thresholds).
        await Ok(await RecordAsync(admin, sid, RecordKind.AnesthesiaAssessment, "anesthesia", ("airway", "Mallampati II (anesthetist)"), ("asa", "II")), "Anesthesia assessment");
        await Ok(await ActAsync(admin, sid, "Drug", "anesthesia", ("Drug.Phase", "Anesthesia"), ("Drug.Name", "Propofol"), ("Drug.Dose", "120 mg"), ("Drug.Route", "IV")), "Anesthesia drug");

        // Safety checklist: Time Out cannot come before Sign In; start needs Sign In + Time Out (or an authorised exception).
        var surgeonUser = await UserWithAsync(admin, "ot.surgeon", Perm.OtDashboard, Perm.OtEnterIntraOp, Perm.OtCompleteChecklist);
        Assert.Contains("Authorize", await Refused(await ActAsync(surgeonUser, sid, "Start", "intraop", ("Exception", "true"), ("Reason", "Bleeding")), "Start with exception without authority"));
        await Refused(await ActAsync(admin, sid, "CompleteStage", "safety", await AllDoneAsync(ChecklistStage.TimeOut)), "Time Out before Sign In");
        await Refused(await ActAsync(admin, sid, "Start", "intraop"), "Start without checklist");
        await StartAsync(admin, sid);
        Assert.Equal(SurgeryStatus.InProgress, (await SurgeryAsync(sid)).Status);
        await Refused(await ActAsync(admin, sid, "Team", "overview", ("Team.Role", "OtTechnician"), ("Team.DoctorId", surgeon.ToString())), "Team change after start");
        await Ok(await RecordAsync(admin, sid, RecordKind.IntraOpMonitoring, "anesthesia", ("pulse", "88"), ("bp_sys", "118")), "Monitoring");

        // Operation note: draft → final (DB-immutable) → amendment with reason (new version, history kept).
        Assert.Contains("finalize", (await Refused(await ActAsync(admin, sid, "Finalize", "intraop"), "Finalize without draft")).ToLowerInvariant());
        await Ok(await ActAsync(admin, sid, "Note", "intraop", ("Note.ProcedurePerformed", "Open appendicectomy"), ("Note.OperativeFindings", "Inflamed appendix"), ("Note.BloodLossMl", "50")), "Draft");
        await Refused(await ActAsync(admin, sid, "Finalize", "intraop"), "Finalize incomplete note");
        await Ok(await ActAsync(admin, sid, "Note", "intraop", ("Note.ProcedurePerformed", "Open appendicectomy"), ("Note.PostOpDiagnosis", "Acute appendicitis"), ("Note.SurgeonName", "Dr. Surgeon Khan"),
            ("Note.PostOpInstructions", "NPO 6 hours, then sips (as per surgeon)")), "Complete draft");
        await Ok(await ActAsync(admin, sid, "Finalize", "intraop"), "Finalize");
        Assert.Contains("Amend", await Refused(await ActAsync(admin, sid, "Note", "intraop", ("Note.ProcedurePerformed", "changed")), "Edit final note"));
        await Refused(await ActAsync(admin, sid, "Amend", "intraop", ("Note.ProcedurePerformed", "Open appendicectomy + drain")), "Amend without reason");
        await Ok(await ActAsync(admin, sid, "Amend", "intraop", ("Note.ProcedurePerformed", "Open appendicectomy + drain"), ("Note.PostOpDiagnosis", "Acute appendicitis"),
            ("Note.SurgeonName", "Dr. Surgeon Khan"), ("Reason", "Drain placement omitted")), "Amend");
        await Ok(await ActAsync(admin, sid, "Finalize", "intraop"), "Finalize amendment");
        await using (var db = Factory.NewDb())
        {
            var notes = await db.OperativeNotes.Where(n => n.SurgeryId == sid).OrderBy(n => n.Version).ToListAsync();
            Assert.Equal(new[] { (1, NoteStatus.Final), (2, NoteStatus.Final) }, notes.Select(n => (n.Version, n.Status)).ToArray());
            Assert.Equal("Open appendicectomy", notes[0].ProcedurePerformed);
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"OperativeNotes\" SET \"Technique\" = 'tampered' WHERE \"Status\" = 'Final'"));
        }
        Assert.Single(await AuditAsync(AuditActions.OtNoteAmended));

        // End requires Sign Out → recovery episode.
        Assert.Contains("Sign Out", await Refused(await ActAsync(admin, sid, "End", "intraop"), "End without Sign Out"));
        await CompleteStageAsync(admin, sid, ChecklistStage.SignOut);
        await Ok(await ActAsync(admin, sid, "End", "intraop"), "End");
        Assert.Equal(SurgeryStatus.Completed, (await SurgeryAsync(sid)).Status);
        Assert.Contains("Clinical Patient", await admin.GetHtmlAsync("/Ot/Recovery"));

        // Recovery: observations as entered; the transfer decision is documented by a clinician.
        await Ok(await ActAsync(admin, sid, "Recovery", "recovery", ("Recovery.Status", "InRecovery"), ("Recovery.Nurse", "Nurse PACU")), "In recovery");
        await Ok(await RecordAsync(admin, sid, RecordKind.PacuObservation, "recovery", ("pulse", "92"), ("pain", "4")), "PACU observation");
        var miss = await Refused(await ActAsync(admin, sid, "Recovery", "recovery", ("Recovery.Status", "TransferredWard"), ("Recovery.Destination", "Surgical ward")), "Transfer without decision");
        Assert.Contains("clinician", miss);
        await Ok(await ActAsync(admin, sid, "Recovery", "recovery", ("Recovery.Status", "TransferredWard"), ("Recovery.Destination", "Surgical ward"), ("Recovery.HandoverStaff", "Nurse Ward"),
            ("Recovery.DecisionBy", "Dr. Anesthetist Ali"), ("Recovery.HandoverNotes", "Stable, drain in situ")), "Transfer");
        Assert.Single(await AuditAsync(AuditActions.OtRecoveryTransferred));

        // Post-op instructions written by hand are entered with their source and linked to the IPD admission.
        await Ok(await RecordAsync(admin, sid, RecordKind.PostOpRecord, "postop", ("source", "Handwritten by doctor"), ("instructions", "Inj. as per chart, mobilise next day"),
            ("Record.ResponsibleName", "Dr. Surgeon Khan")), "Post-op record");
        await using (var db = Factory.NewDb())
        {
            var po = await db.ClinicalRecords.SingleAsync(x => x.Kind == RecordKind.PostOpRecord);
            Assert.Equal((sid, admissionId, "Dr. Surgeon Khan"), (po.SurgeryId!.Value, po.AdmissionId!.Value, po.ResponsibleName));
            Assert.Contains("Handwritten by doctor", po.Values);
        }
        Assert.Contains("NPO 6 hours", await admin.GetHtmlAsync($"/Print/Ot/postop/{sid}"));

        // History and audit trail.
        var hist = await admin.GetHtmlAsync($"/Ot/Surgery/{sid}?tab=history");
        foreach (var e in new[] { "Booked", "Drain placement omitted", "Surgical ward" }) Assert.Contains(e, hist);
        Assert.Single(await AuditAsync(AuditActions.OtSurgeryStarted));
        Assert.Single(await AuditAsync(AuditActions.OtSurgeryEnded));
        Assert.True((await AuditAsync(AuditActions.OtChecklistCompleted)).Count >= 4);
    }

    [Fact]
    public async Task Emergency_exception_is_authorised_documented_and_visible()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var ot = await TheatreAsync(admin, "OT1", "Main OT 1");
        var surgeon = await DoctorAsync(admin, "Dr. Surgeon Khan");
        var pid = await RegisterPatientAsync(admin, PatientFields("Emergency Patient", "Father E"));
        var sid = await EmergencyAsync(admin, pid, ot, surgeon);
        Assert.Contains("reason", (await Refused(await ActAsync(admin, sid, "Start", "intraop", ("Exception", "true")), "Exception without reason")).ToLowerInvariant());
        await Ok(await ActAsync(admin, sid, "Start", "intraop", ("Exception", "true"), ("Reason", "Massive haemorrhage – no time for checklist")), "Start by exception");
        Assert.Equal(SurgeryStatus.InProgress, (await SurgeryAsync(sid)).Status);
        Assert.Single(await AuditAsync(AuditActions.OtSafetyException));
        Assert.Contains("Massive haemorrhage", await admin.GetHtmlAsync($"/Ot/Surgery/{sid}?tab=history"));
        // A second surgery cannot start in the same theatre while one is in progress.
        var pid2 = await RegisterPatientAsync(admin, PatientFields("Other Patient", "Father O"));
        var surgeon2 = await DoctorAsync(admin, "Dr. Other");
        var later = await BookAsync(admin, pid2, ot, Tomorrow(9), await ProcedureAsync("GS-HERN"), surgeon2);
        await Ok(later, "Next booking in the same theatre");
        Assert.Contains("in progress", await Refused(await ActAsync(admin, SurgeryId(later), "Start", "intraop", ("Exception", "true"), ("Reason", "x")), "Second surgery in the same theatre"));
    }

    // =====================================================================================
    // 8D – OT stock (major OT patient-wise consumption) while the Minor OT stays unchanged
    // =====================================================================================

    [Fact]
    public async Task Major_ot_consumption_deducts_ot_stock_on_save_returns_and_minor_ot_unchanged()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        // A pharmacy department store for the major OT, filled by a normal pharmacy transfer.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Settings", new[] { P("location.Code", "MJOT"), P("location.Name", "Major OT store"), P("location.IsActive", "true"), P("location.SortOrder", "25") },
            "/Pharmacy/Settings?handler=Location")).StatusCode);
        int main, mjot, mot, form;
        await using (var db = Factory.NewDb())
        {
            main = await db.StockLocations.Where(l => l.Code == "MAIN").Select(l => l.Id).SingleAsync();
            mjot = await db.StockLocations.Where(l => l.Code == "MJOT").Select(l => l.Id).SingleAsync();
            mot = await db.StockLocations.Where(l => l.Code == "MOT").Select(l => l.Id).SingleAsync();
            form = await db.PharmacyLookups.Where(l => l.Kind == PharmacyLookupKind.DosageForm && l.Name == "Tablet").Select(l => l.Id).SingleAsync();
        }
        var med = await admin.PostFormAsync("/Pharmacy/Medicines/Edit", new[]
        {
            P("Input.Name", "Surgical Gauze"), P("Input.GenericName", "Gauze"), P("Input.Strength", "10x10"), P("Input.DosageFormId", form.ToString()), P("Input.Unit", "Piece"),
            P("Input.UnitsPerPack", "1"), P("Input.MinimumStock", "0"), P("Input.IsActive", "true"), P("Input.AllowDiscount", "false"), P("Input.MaxDiscountPercent", "0"),
        });
        Assert.Equal(HttpStatusCode.Redirect, med.StatusCode);
        int gauze;
        await using (var db = Factory.NewDb()) gauze = await db.Medicines.Where(m => m.Name == "Surgical Gauze").Select(m => m.Id).SingleAsync();
        var expiry = Today.AddMonths(10).ToString("yyyy-MM");
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Pharmacy/Receive", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.SourceName", "Store"), P("Lines[0].MedicineId", gauze.ToString()), P("Lines[0].BatchNumber", "G1"),
            P("Lines[0].Expiry", expiry), P("Lines[0].Quantity", "100"),
        })).StatusCode);
        async Task TransferAsync(int to, int qty) => Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Pharmacy/Transfer?from={main}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.FromLocationId", main.ToString()), P("Input.ToLocationId", to.ToString()), P("Input.Reference", "OT"),
            P("Input.Lines[0].MedicineId", gauze.ToString()), P("Input.Lines[0].Quantity", qty.ToString()), P("Input.Lines[0].Instructions", "-"),
        }, "/Pharmacy/Transfer")).StatusCode);
        await TransferAsync(mjot, 20);
        await TransferAsync(mot, 10); // Minor OT: the existing workflow – a transfer, nothing more.
        async Task<int> StockAsync(int loc) { await using var db = Factory.NewDb(); return await db.StockBalances.Where(s => s.LocationId == loc && s.MedicineId == gauze).SumAsync(s => (int?)s.Quantity) ?? 0; }
        Assert.Equal((70, 20, 10), (await StockAsync(main), await StockAsync(mjot), await StockAsync(mot)));

        var ot = await TheatreAsync(admin, "OT1", "Main OT 1", stock: mjot);
        var surgeon = await DoctorAsync(admin, "Dr. Surgeon Khan");
        var pid = await RegisterPatientAsync(admin, PatientFields("Stock Patient", "Father St"));
        var sid = await EmergencyAsync(admin, pid, ot, surgeon);
        Assert.Contains("Surgical Gauze", await admin.GetHtmlAsync($"/Ot/Surgery/{sid}?tab=stock"));
        // Nothing is deducted until the entry is saved; then exactly once, from the OT store only.
        await Refused(await ActAsync(admin, sid, "Consume", "stock", ("MedicineId", gauze.ToString()), ("Quantity", "1")), "Consumption before the surgery is prepared");
        await StartAsync(admin, sid);
        Assert.Equal(20, await StockAsync(mjot));
        await Ok(await ActAsync(admin, sid, "Consume", "stock", ("MedicineId", gauze.ToString()), ("Quantity", "6")), "Consumption");
        Assert.Equal((70, 14, 10), (await StockAsync(main), await StockAsync(mjot), await StockAsync(mot)));
        await Refused(await ActAsync(admin, sid, "Consume", "stock", ("MedicineId", gauze.ToString()), ("Quantity", "15")), "More than OT stock");
        long use;
        await using (var db = Factory.NewDb())
        {
            use = await db.OtConsumptions.Where(c => c.SurgeryId == sid && c.Kind == OtConsumptionKind.Use).Select(c => c.Id).SingleAsync();
            Assert.Equal(1, await db.InventoryTransactions.CountAsync(t => t.Type == InventoryTxnType.OtConsumption && t.LocationId == mjot && t.Quantity == -6));
        }
        await Refused(await ActAsync(admin, sid, "Return", "stock", ("ItemId", use.ToString()), ("Quantity", "2")), "Return without reason");
        await Ok(await ActAsync(admin, sid, "Return", "stock", ("ItemId", use.ToString()), ("Quantity", "2"), ("Reason", "Unopened packs")), "Return");
        Assert.Equal(16, await StockAsync(mjot));
        await Refused(await ActAsync(admin, sid, "Return", "stock", ("ItemId", use.ToString()), ("Quantity", "5"), ("Reason", "too many")), "Return more than used");
        await using (var db = Factory.NewDb())
        {
            // The Minor OT has no patient-wise entries; its stock moved only by the transfer.
            Assert.False(await db.OtConsumptions.AnyAsync(c => c.LocationId == mot));
            Assert.False(await db.InventoryTransactions.AnyAsync(t => t.LocationId == mot && t.Type != InventoryTxnType.TransferIn));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"OtConsumptions\""));
        }
        Assert.Single(await AuditAsync(AuditActions.OtConsumptionRecorded));
        Assert.Single(await AuditAsync(AuditActions.OtConsumptionReturned));
        Assert.Contains("Surgical Gauze", await admin.GetHtmlAsync("/Ot/Reports?report=consumption&period=today"));
    }

    // =====================================================================================
    // 8E – CSSD (failed cycles are never released) and specimens linked to the laboratory
    // =====================================================================================

    [Fact]
    public async Task Cssd_failed_cycle_never_released_issue_return_and_specimen_dispatch()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var ot = await TheatreAsync(admin, "OT1", "Main OT 1");
        await Ok(await admin.PostFormAsync("/Ot/Cssd", new[] { P("Code", "INS-1"), P("Name", "Artery forceps"), P("Active", "true") }, "/Ot/Cssd?handler=Instrument"), "Instrument");
        int ins;
        await using (var db = Factory.NewDb()) ins = await db.CssdInstruments.Select(i => i.Id).SingleAsync();
        await Ok(await admin.PostFormAsync("/Ot/Cssd", new[] { P("Code", "SET-LAP"), P("Name", "Laparotomy set"), P("TheatreId", ot.ToString()), P($"Qty[{ins}]", "6"), P("Active", "true") }, "/Ot/Cssd?handler=Set"), "Set");
        int set;
        await using (var db = Factory.NewDb()) set = await db.CssdSets.Select(s => s.Id).SingleAsync();
        async Task<long> BatchAsync()
        {
            await Ok(await admin.PostFormAsync("/Ot/Cssd", new[] { P("Batch.Method", "Steam autoclave"), P("Batch.CycleNumber", "C-1"), P("Batch.SetIds", set.ToString()) }, "/Ot/Cssd?handler=Batch"), "Batch");
            await using var db = Factory.NewDb();
            return await db.CssdBatches.OrderByDescending(b => b.Id).Select(b => b.Id).FirstAsync();
        }
        async Task<CssdSetStatus> SetStatusAsync() { await using var db = Factory.NewDb(); return await db.CssdSets.Where(s => s.Id == set).Select(s => s.Status).SingleAsync(); }

        var b1 = await BatchAsync();
        Assert.Equal(CssdSetStatus.InProcess, await SetStatusAsync());
        await Refused(await admin.PostFormAsync("/Ot/Cssd", new[] { P("ItemId", b1.ToString()), P("Passed", "false") }, "/Ot/Cssd?handler=Result"), "Failure without reason");
        await Ok(await admin.PostFormAsync("/Ot/Cssd", new[] { P("ItemId", b1.ToString()), P("Passed", "false"), P("Reason", "Indicator did not change") }, "/Ot/Cssd?handler=Result"), "Failed cycle");
        Assert.Equal(CssdSetStatus.NotSterile, await SetStatusAsync());
        await Refused(await admin.PostFormAsync("/Ot/Cssd", new[] { P("ItemId", b1.ToString()) }, "/Ot/Cssd?handler=Release"), "Release failed batch");
        await using (var db = Factory.NewDb())
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"CssdBatches\" SET \"Status\" = 'Released' WHERE \"Status\" = 'Failed'"));
        await Refused(await admin.PostFormAsync("/Ot/Cssd", new[] { P("ItemId", set.ToString()), P("IssuedTo", "OT1") }, "/Ot/Cssd?handler=Issue"), "Issue non-sterile set");

        var b2 = await BatchAsync();
        await Ok(await admin.PostFormAsync("/Ot/Cssd", new[] { P("ItemId", b2.ToString()), P("Passed", "true"), P("Expiry", Today.AddDays(14).ToString("yyyy-MM-dd")) }, "/Ot/Cssd?handler=Result"), "Passed cycle");
        Assert.Equal(CssdSetStatus.InProcess, await SetStatusAsync()); // passed but not yet released
        await Ok(await admin.PostFormAsync("/Ot/Cssd", new[] { P("ItemId", b2.ToString()) }, "/Ot/Cssd?handler=Release"), "Release");
        Assert.Equal(CssdSetStatus.Sterile, await SetStatusAsync());

        // Issue the sterile set to a surgery, then return it (back to not sterile).
        var surgeon = await DoctorAsync(admin, "Dr. Surgeon Khan");
        var pid = await RegisterPatientAsync(admin, PatientFields("Cssd Patient", "Father Cs"));
        var sid = await EmergencyAsync(admin, pid, ot, surgeon);
        await Ok(await ActAsync(admin, sid, "CssdIssue", "overview", ("ItemId", set.ToString()), ("By", "Scrub nurse")), "Issue to surgery");
        Assert.Equal(CssdSetStatus.Issued, await SetStatusAsync());
        Assert.Contains("SET-LAP", await admin.GetHtmlAsync($"/Ot/Surgery/{sid}"));
        long issue;
        await using (var db = Factory.NewDb()) issue = await db.CssdIssues.Where(i => i.SurgeryId == sid).Select(i => i.Id).SingleAsync();
        await Ok(await admin.PostFormAsync("/Ot/Cssd", new[] { P("ItemId", issue.ToString()), P("Reason", "Used") }, "/Ot/Cssd?handler=Return"), "Return set");
        Assert.Equal(CssdSetStatus.NotSterile, await SetStatusAsync());
        Assert.Contains("CSB-", await admin.GetHtmlAsync("/Ot/Reports?report=cssd&period=today"));

        // Specimens: recorded during surgery, dispatched, received (receiver required), labels printable.
        await StartAsync(admin, sid);
        await Ok(await ActAsync(admin, sid, "Specimen", "intraop", ("Specimen.Name", "Appendix"), ("Specimen.Type", "Tissue"), ("Specimen.Destination", "Histopathology")), "Specimen");
        long spec;
        await using (var db = Factory.NewDb()) spec = await db.SurgerySpecimens.Select(s => s.Id).SingleAsync();
        await Ok(await ActAsync(admin, sid, "SpecimenStatus", "intraop", ("ItemId", spec.ToString()), ("NewSpecimenStatus", "Dispatched")), "Dispatch");
        await Refused(await ActAsync(admin, sid, "SpecimenStatus", "intraop", ("ItemId", spec.ToString()), ("NewSpecimenStatus", "Received")), "Received without name");
        await Ok(await ActAsync(admin, sid, "SpecimenStatus", "intraop", ("ItemId", spec.ToString()), ("NewSpecimenStatus", "Received"), ("By", "Lab tech Imran")), "Received");
        var labels = await admin.GetHtmlAsync($"/Print/Ot/specimen/{sid}");
        Assert.Contains("data-specimen-label", labels);
        Assert.Contains("Lab tech Imran", labels);
    }

    // =====================================================================================
    // 8F – billing through Phase 3 (private) and government mode
    // =====================================================================================

    [Fact]
    public async Task Ot_charges_post_once_through_phase3_discount_rules_and_cancellation_keeps_charges()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var theatreFee = await ServiceAsync(admin, "OT-THEATRE", "Theatre charges", 5000, "Procedure");
        var surgeonFee = await ServiceAsync(admin, "OT-SURGEON", "Surgeon fee", 15000, "Procedure");
        var dressing = await ServiceAsync(admin, "OT-DRESS", "Special dressing", 800, "Procedure");
        var ot = await TheatreAsync(admin, "OT1", "Main OT 1", charge: theatreFee);
        uint rv;
        await using (var db = Factory.NewDb()) rv = (await db.OtSettings.SingleAsync()).RowVersion;
        await Ok(await admin.PostFormAsync("/Ot/Setup", new[]
        {
            P("RowVersion", rv.ToString()), P("Input.SurgeonFeeItemId", surgeonFee.ToString()), P("Input.DefaultCleaningMinutes", "30"), P("Input.PrintHeader", "true"),
            P("Input.LetterheadTopMarginMm", "45"), P("Input.MaxUploadMb", "10"),
        }), "OT settings");
        Assert.Contains("Private hospital mode", await admin.GetHtmlAsync("/Ot/Setup"));
        var surgeon = await DoctorAsync(admin, "Dr. Surgeon Khan");
        var pid = await RegisterPatientAsync(admin, PatientFields("Billing Patient", "Father B"));
        var r = await BookAsync(admin, pid, ot, Tomorrow(9), await ProcedureAsync("GS-HERN"), surgeon);
        await Ok(r, "Booking");
        var sid = SurgeryId(r);

        var billing = await admin.GetHtmlAsync($"/Ot/Surgery/{sid}?tab=billing");
        Assert.Contains("data-pending-charges", billing);
        await Ok(await ActAsync(admin, sid, "PostCharges", "billing", ("Kinds", "Theatre"), ("Kinds", "SurgeonFee")), "Post charges");
        Assert.Contains("Nothing to post", await Refused(await ActAsync(admin, sid, "PostCharges", "billing", ("Kinds", "Theatre"), ("Kinds", "SurgeonFee")), "Second post"));
        Assert.Contains("reason", (await Refused(await ActAsync(admin, sid, "Charge", "billing", ("ServiceItemId", dressing.ToString()), ("Quantity", "1"), ("Discount", "100")), "Discount without reason")).ToLowerInvariant());
        await Ok(await ActAsync(admin, sid, "Charge", "billing", ("ServiceItemId", dressing.ToString()), ("Quantity", "2"), ("Discount", "100"), ("Reason", "Staff relative")), "Extra charge");
        await using (var db = Factory.NewDb())
        {
            var charges = await db.Charges.Where(c => c.SurgeryId == sid).OrderBy(c => c.Id).ToListAsync();
            Assert.Equal(new[] { 5000m, 15000m, 1500m }, charges.Select(c => c.NetAmount).ToArray());
            Assert.All(charges, c => Assert.Equal(pid, c.PatientId));
            Assert.Equal(3, await db.SurgeryCharges.CountAsync(x => x.SurgeryId == sid));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"SurgeryCharges\""));
        }
        var collect = await admin.GetHtmlAsync($"/Finance/Collect?patientId={pid}&surgeryId={sid}");
        Assert.Contains($"name=\"SurgeryId\" value=\"{sid}\"", collect);
        Assert.Contains("21,500.00", await admin.GetHtmlAsync($"/Print/Ot/bill/{sid}"));
        // Cancelling the surgery does not delete charges; no new charges afterwards.
        await Ok(await ActAsync(admin, sid, "Status", "overview", ("NewStatus", "Cancelled"), ("Reason", "Patient referred")), "Cancel");
        await using (var db = Factory.NewDb()) Assert.Equal(3, await db.Charges.CountAsync(c => c.SurgeryId == sid));
        Assert.Contains("cancelled", (await Refused(await ActAsync(admin, sid, "Charge", "billing", ("ServiceItemId", dressing.ToString()), ("Quantity", "1")), "Charge after cancel")).ToLowerInvariant());
        Assert.True((await AuditAsync(AuditActions.OtChargePosted)).Count >= 2);
        // Without "View OT billing" the bill and revenue report are hidden / refused.
        var nurse = await UserWithAsync(admin, "ot.nurse", Perm.OtDashboard, Perm.OtViewStatistics, Perm.OtPrintDocuments);
        Denied(await nurse.GetAsync($"/Print/Ot/bill/{sid}"), "Bill without permission");
        Denied(await nurse.GetAsync("/Ot/Reports?report=revenue"), "Revenue report without permission");
        Assert.DoesNotContain("data-ot-bill", await nurse.GetHtmlAsync($"/Ot/Surgery/{sid}?tab=billing"));
    }

    [Fact]
    public async Task Government_mode_charges_only_configured_services()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        Assert.Contains("Government hospital mode", await admin.GetHtmlAsync("/Ot/Setup"));
        var theatreFee = await ServiceAsync(admin, "OT-GOV", "OT registration fee", 200, "Procedure");
        var ot = await TheatreAsync(admin, "OT1", "Main OT 1", charge: theatreFee);
        var surgeon = await DoctorAsync(admin, "Dr. Govt Surgeon");
        var pid = await RegisterPatientAsync(admin, PatientFields("Govt Patient", "Father G"));
        var sid = await EmergencyAsync(admin, pid, ot, surgeon);
        await Ok(await ActAsync(admin, sid, "PostCharges", "billing", ("Kinds", "Theatre")), "Post charges");
        await using var db = Factory.NewDb();
        var c = await db.Charges.SingleAsync(x => x.SurgeryId == sid);
        Assert.Equal((200m, 200m), (c.Fee, c.NetAmount)); // the configured fee – no retail / profit calculation
    }

    // =====================================================================================
    // 8G – printing, reports, permissions, audit, database protection
    // =====================================================================================

    [Fact]
    public async Task Fourteen_print_forms_fifteen_reports_permissions_audit_and_database_protection()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var ot = await TheatreAsync(admin, "OT1", "Main OT 1");
        var surgeon = await DoctorAsync(admin, "Dr. Surgeon Khan");
        var pid = await RegisterPatientAsync(admin, PatientFields("Print Patient", "Father P"));
        var sid = await EmergencyAsync(admin, pid, ot, surgeon);
        int ct;
        await using (var db = Factory.NewDb()) ct = await db.OtConsentTypes.Select(c => c.Id).FirstAsync();
        await Ok(await ActAsync(admin, sid, "Consent", "consent", ("Consent.ConsentTypeId", ct.ToString()), ("Consent.GivenBy", "Print Patient"), ("Consent.ResponsibleStaff", "Dr. Surgeon Khan")), "Consent");
        long consent;
        await using (var db = Factory.NewDb()) consent = await db.SurgeryConsents.Select(c => c.Id).SingleAsync();
        await StartAsync(admin, sid);
        await Ok(await RecordAsync(admin, sid, RecordKind.IntraOpMonitoring, "anesthesia", ("pulse", "80")), "Monitoring");

        int surgeries, charges, records;
        await using (var db = Factory.NewDb()) (surgeries, charges, records) = (await db.Surgeries.CountAsync(), await db.Charges.CountAsync(), await db.ClinicalRecords.CountAsync());
        foreach (var form in FormKeys)
        {
            var id = form == "consent" ? consent : form == "schedule" ? 0 : sid;
            var html = await admin.GetHtmlAsync($"/Print/Ot/{form}/{id}?date={Today:yyyy-MM-dd}");
            Assert.Contains("data-form-page", html);
            if (form != "schedule") Assert.Contains("Print Patient", html);
        }
        Assert.Equal(14, BurhanNexus.HMS.Web.Pages.Print.Ot.FormModel.Forms.Count);
        Assert.Contains("data-chart=\"pulse\"", await admin.GetHtmlAsync($"/Print/Ot/anesthesia-record/{sid}"));
        await using (var db = Factory.NewDb())
            Assert.Equal((surgeries, charges, records), (await db.Surgeries.CountAsync(), await db.Charges.CountAsync(), await db.ClinicalRecords.CountAsync()));
        Assert.True((await AuditAsync(AuditActions.OtFormPrinted)).Count >= 14);

        // Reports with filters, print and export.
        Assert.Equal(15, OtQueryService.Reports.Length);
        foreach (var (key, _, _) in OtQueryService.Reports)
            Assert.Contains("data-ot-report-filter", await admin.GetHtmlAsync($"/Ot/Reports?report={key}&period=month&theatreId={ot}"));
        Assert.Contains("Print Patient", await admin.GetHtmlAsync($"/Ot/Reports?report=daily&from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}"));
        var x = await admin.GetAsync($"/Ot/Reports?handler=Export&format=xlsx&report=daily&from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", x.Content.Headers.ContentType?.MediaType);
        Assert.Single(await AuditAsync(AuditActions.OtReportExported));

        // Backend permissions – hidden buttons are not enough.
        var viewer = await UserWithAsync(admin, "ot.viewer", Perm.OtDashboard);
        Assert.Contains("data-ot-kpis", await viewer.GetHtmlAsync("/Ot"));
        Denied(await viewer.PostFormAsync("/Ot", new[] { P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.TheatreId", ot.ToString()) }, $"/Ot/Book?patientId={pid}"), "Booking without permission");
        Denied(await viewer.GetAsync("/Ot/Theatres"), "Theatres without permission");
        Denied(await viewer.GetAsync("/Ot/Cssd"), "CSSD without permission");
        Denied(await viewer.PostFormAsync($"/Ot/Surgery/{sid}", new[] { P("Note.ProcedurePerformed", "x") }, $"/Ot/Surgery/{sid}?handler=Note"), "Note without permission");
        Denied(await viewer.PostFormAsync($"/Ot/Surgery/{sid}", new[] { P("NewStatus", "Cancelled"), P("Reason", "x") }, $"/Ot/Surgery/{sid}?handler=Status"), "Cancel without permission");
        Denied(await viewer.GetAsync($"/Print/Ot/opnote/{sid}"), "Print without permission");
        Denied(await viewer.GetAsync("/Ot/Reports"), "Reports without permission");
        Assert.DoesNotContain("data-test=\"start-surgery\"", await viewer.GetHtmlAsync($"/Ot/Surgery/{sid}?tab=intraop"));
        // The admin navigation shows the OT category; other users only see what they may open.
        Assert.Contains("data-adm-cat=\"ot\"", await admin.GetHtmlAsync("/"));
        var nav = await viewer.GetHtmlAsync("/");
        Assert.Contains("/Ot/Schedule", nav);
        Assert.DoesNotContain("/Ot/Cssd", nav);

        // Database protection: history rows are insert-only, master records cannot be deleted.
        await using (var db = Factory.NewDb())
        {
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"Surgeries\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"SurgeryEvents\" SET \"Note\" = 'changed'"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"SurgeryChecklistEntries\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"SurgeryChecklistStages\" SET \"IsException\" = true"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"Surgeries\" SET \"PatientId\" = \"PatientId\" + 1"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"OtTheatres\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"OtProcedures\""));
        }
    }

    private static readonly string[] FormKeys = BurhanNexus.HMS.Web.Pages.Print.Ot.FormModel.Forms.Keys.ToArray();
}
