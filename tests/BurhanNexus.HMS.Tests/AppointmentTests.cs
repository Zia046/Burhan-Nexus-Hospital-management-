using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Private hospital: doctor-wise daily tokens, thermal appointment slips and printer integration.</summary>
public partial class AppointmentTests : Phase2TestBase
{
    [GeneratedRegex(@"/Print/Appointment/(\d+)")] private static partial Regex SlipRegex();
    [GeneratedRegex(@"data-field=""token"">(\d+)<")] private static partial Regex TokenRegex();

    // ---------------- helpers ----------------

    private async Task<int> AddDoctorAsync(Browser admin, string name, string? specialty = null, string? qualification = null, decimal? fee = null)
    {
        var fields = new List<KeyValuePair<string, string>> { P("DoctorForm.Name", name), P("DoctorForm.IsActive", "true") };
        if (specialty is not null) fields.Add(P("DoctorForm.Specialty", specialty));
        if (qualification is not null) fields.Add(P("DoctorForm.Qualification", qualification));
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Opd/Setup", fields, "/Opd/Setup?handler=Doctor")).StatusCode);
        int id;
        await using (var db = Factory.NewDb()) id = await db.Doctors.Where(d => d.Name == name).Select(d => d.Id).SingleAsync();
        if (fee is decimal f)
        {
            var r = await admin.PostFormAsync("/Opd/Setup", new[]
            {
                P("ServiceForm.Code", "CONS-" + id), P("ServiceForm.Name", "Consultation " + name), P("ServiceForm.Category", "Opd"),
                P("ServiceForm.DefaultFee", f.ToString(System.Globalization.CultureInfo.InvariantCulture)), P("ServiceForm.DoctorId", id.ToString()),
                P("ServiceForm.IsActive", "true")
            }, "/Opd/Setup?handler=Service");
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        }
        return id;
    }

    private static IEnumerable<KeyValuePair<string, string>> BookFields(int doctorId, DateOnly? date = null, string? complaint = null)
    {
        yield return P("Input.DoctorId", doctorId.ToString());
        if (date is { } d) yield return P("Input.AppointmentDate", d.ToString("yyyy-MM-dd"));
        if (complaint is not null) yield return P("Input.Complaint", complaint);
    }

    private static async Task<long> SlipIdAsync(HttpResponseMessage r)
    {
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Booking failed: " + Errors(await r.Content.ReadAsStringAsync()));
        var m = SlipRegex().Match(r.Headers.Location!.OriginalString);
        Assert.True(m.Success, "Unexpected redirect " + r.Headers.Location);
        return long.Parse(m.Groups[1].Value);
    }

    private async Task<long> BookAsync(Browser b, long patientId, int doctorId, DateOnly? date = null, string? complaint = null) =>
        await SlipIdAsync(await b.PostFormAsync("/Patients/Register", BookFields(doctorId, date, complaint), $"/Appointments/Book?patientId={patientId}"));

    private async Task<Appointment> ApptAsync(long id)
    {
        await using var db = Factory.NewDb();
        return await db.Appointments.Include(a => a.Encounter).ThenInclude(e => e!.PrintedDocuments).SingleAsync(a => a.Id == id);
    }

    private async Task<List<int>> TokensAsync(int doctorId, DateOnly date)
    {
        await using var db = Factory.NewDb();
        return await db.Appointments.Where(a => a.DoctorId == doctorId && a.AppointmentDate == date).OrderBy(a => a.Id).Select(a => a.TokenNumber).ToListAsync();
    }

    private async Task<DateOnly> TodayAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Infrastructure.Patients.HospitalClock>().TodayAsync();
    }

    private static async Task<HttpResponseMessage> AjaxAsync(Browser b, string pageUrl, string handlerUrl, params KeyValuePair<string, string>[] fields)
    {
        var token = await b.TokenAsync(pageUrl);
        return await b.Http.PostAsync(handlerUrl, new FormUrlEncodedContent(fields.Append(P("__RequestVerificationToken", token))));
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement;
    }

    /// <summary>"TOKEN NO." line followed (after bold/size commands only) by the token number on its own line.</summary>
    private static bool TokenLine(string escpos, int token) =>
        Regex.IsMatch(escpos, @"TOKEN NO\.\n(?:\x1B..|\x1D..){0,4}" + token + "\n", RegexOptions.Singleline);

    // ---------------- tests ----------------

    [Fact]
    public async Task Every_doctor_has_independent_daily_tokens_that_restart_next_day()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var gyn = await AddDoctorAsync(admin, "Dr. Ayesha Gynae", "Gynaecology", "MBBS, FCPS");
        var ortho = await AddDoctorAsync(admin, "Dr. Bilal Ortho", "Orthopaedics");
        var third = await AddDoctorAsync(admin, "Dr. Kamran Child", "Paediatrics");
        var p1 = await RegisterPatientAsync(admin, PatientFields("Zainab Bibi", "Iqbal Khan", "Female"));
        var p2 = await RegisterPatientAsync(admin, PatientFields("Hamid Ullah", "Rehmat Ullah"));
        var today = await TodayAsync();

        // Interleaved: 15 gynae and 20 ortho bookings.
        for (var i = 0; i < 20; i++)
        {
            if (i < 15) await BookAsync(admin, i % 2 == 0 ? p1 : p2, gyn);
            await BookAsync(admin, i % 2 == 0 ? p2 : p1, ortho);
        }
        Assert.Equal(Enumerable.Range(1, 15), await TokensAsync(gyn, today));
        Assert.Equal(Enumerable.Range(1, 20), await TokensAsync(ortho, today));

        // A third doctor starts at 1 too.
        var t1 = await BookAsync(admin, p1, third);
        Assert.Equal(1, (await ApptAsync(t1)).TokenNumber);
        Assert.Contains(">1<", await admin.GetHtmlAsync($"/Print/Appointment/{t1}"));

        // Booking ahead for tomorrow: independent token series for that date.
        var ahead = await BookAsync(admin, p2, gyn, today.AddDays(1));
        Assert.Equal(1, (await ApptAsync(ahead)).TokenNumber);

        // Next day: every doctor restarts at 1 (tomorrow's gynae already has token 1, so 2), yesterday unchanged.
        Factory.Clock.Advance(TimeSpan.FromDays(1));
        var tomorrow = await TodayAsync();
        Assert.Equal(today.AddDays(1), tomorrow);
        var o1 = await BookAsync(admin, p1, ortho);
        var g2 = await BookAsync(admin, p1, gyn);
        Assert.Equal(1, (await ApptAsync(o1)).TokenNumber);
        Assert.Equal(2, (await ApptAsync(g2)).TokenNumber);
        Assert.Equal(Enumerable.Range(1, 15), await TokensAsync(gyn, today));
        Assert.Equal(Enumerable.Range(1, 20), await TokensAsync(ortho, today));

        // Past dates cannot be booked.
        var past = await admin.PostFormAsync("/Patients/Register", BookFields(gyn, today), $"/Appointments/Book?patientId={p1}");
        Assert.Contains("past date", Errors(await past.Content.ReadAsStringAsync()));

        // Doctor-wise lists for the first day: each doctor separately with his own totals.
        var list = await admin.GetHtmlAsync($"/Appointments?date={today:yyyy-MM-dd}");
        Assert.Contains($"data-doctor-list=\"{gyn}\"", list);
        Assert.Contains($"data-doctor-list=\"{ortho}\"", list);
        Assert.Contains("15 patient(s)", list);
        Assert.Contains("20 patient(s)", list);
        var orthoOnly = await admin.GetHtmlAsync($"/Appointments?date={today:yyyy-MM-dd}&doctor={ortho}");
        Assert.DoesNotContain($"data-doctor-list=\"{gyn}\"", orthoOnly);

        // Audit: every booking is logged with its token.
        Assert.Equal(15 + 20 + 1 + 1 + 2, (await AuditAsync(AuditActions.AppointmentBooked)).Count);
        Assert.Contains(await AuditAsync(AuditActions.AppointmentBooked), a => a.Description!.Contains("token 20"));
    }

    [Fact]
    public async Task Simultaneous_bookings_never_issue_duplicate_tokens()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var docA = await AddDoctorAsync(admin, "Dr. Parallel A");
        var docB = await AddDoctorAsync(admin, "Dr. Parallel B");
        var pid = await RegisterPatientAsync(admin, PatientFields("Rush Hour", "Busy Father"));
        var second = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(second, AdminUser, AdminPassword, "Reception-2")).StatusCode);
        var tokenA = await admin.TokenAsync("/Patients/Register");
        var tokenB = await second.TokenAsync("/Patients/Register");

        var tasks = Enumerable.Range(0, 36).Select(i =>
        {
            var (b, t) = i % 2 == 0 ? (admin, tokenA) : (second, tokenB);
            var fields = BookFields(i % 3 == 0 ? docB : docA).Append(P("__RequestVerificationToken", t));
            return b.Http.PostAsync($"/Appointments/Book?patientId={pid}", new FormUrlEncodedContent(fields));
        }).ToList();
        var responses = await Task.WhenAll(tasks);
        foreach (var r in responses) await SlipIdAsync(r);

        var today = await TodayAsync();
        Assert.Equal(Enumerable.Range(1, 24), (await TokensAsync(docA, today)).Order());
        Assert.Equal(Enumerable.Range(1, 12), (await TokensAsync(docB, today)).Order());
    }

    [Fact]
    public async Task Cancelled_tokens_are_never_reused_and_edits_keep_the_token()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var doc = await AddDoctorAsync(admin, "Dr. Cancel Test", "Medicine");
        var pid = await RegisterPatientAsync(admin, PatientFields("Saima Noreen", "Abdul Ghani", "Female"));
        var a1 = await BookAsync(admin, pid, doc);
        var a2 = await BookAsync(admin, pid, doc);
        var a3 = await BookAsync(admin, pid, doc);

        var noReason = await admin.PostFormAsync($"/Appointments/Details/{a2}", Array.Empty<KeyValuePair<string, string>>(), $"/Appointments/Details/{a2}?handler=Cancel");
        Assert.Contains("reason", Errors(await noReason.Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        var cancel = await admin.PostFormAsync($"/Appointments/Details/{a2}", new[] { P("CancelReason", "Patient left") }, $"/Appointments/Details/{a2}?handler=Cancel");
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Equal(AppointmentStatus.Cancelled, (await ApptAsync(a2)).Status);

        var a4 = await BookAsync(admin, pid, doc);
        Assert.Equal(4, (await ApptAsync(a4)).TokenNumber);
        Assert.Equal(2, (await ApptAsync(a2)).TokenNumber);

        // Edit complaint: token, doctor and date stay.
        var before = await ApptAsync(a3);
        var edit = await admin.PostFormAsync($"/Appointments/Details/{a3}",
            new[] { P("Edit.Complaint", "Back pain"), P("RowVersion", before.RowVersion.ToString()) }, $"/Appointments/Details/{a3}?handler=Edit");
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        var after = await ApptAsync(a3);
        Assert.Equal("Back pain", after.Complaint);
        Assert.Equal(3, after.TokenNumber);
        Assert.Equal(before.DoctorId, after.DoctorId);

        // Cancelled appointment cannot be printed; history and audit show the cancellation.
        Assert.Contains("printing disabled", await admin.GetHtmlAsync($"/Print/Appointment/{a2}"));
        var rec = await AjaxAsync(admin, $"/Print/Appointment/{a1}", $"/Print/Appointment/{a2}?handler=Record", P("width", "80"));
        Assert.Equal(HttpStatusCode.BadRequest, rec.StatusCode);
        var report = await admin.GetHtmlAsync("/Appointments/Reports");
        Assert.Contains("Patient left", report);
        Assert.Single(await AuditAsync(AuditActions.AppointmentCancelled));
        Assert.Single(await AuditAsync(AuditActions.AppointmentUpdated));

        // Seen status.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Appointments/Details/{a1}", Array.Empty<KeyValuePair<string, string>>(), $"/Appointments/Details/{a1}?handler=Seen")).StatusCode);
        Assert.Equal(AppointmentStatus.Seen, (await ApptAsync(a1)).Status);
        var stats = await admin.GetHtmlAsync("/Appointments/Reports?period=month");
        Assert.Contains("Dr. Cancel Test", stats);
        Assert.Contains("Dr. Cancel Test", await admin.GetHtmlAsync("/Appointments/Reports?period=year"));
    }

    [Fact]
    public async Task Thermal_slip_shows_required_fields_on_58_and_80mm_and_escpos()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var doc = await AddDoctorAsync(admin, "Dr. Nadia Hussain", "Gynaecology & Obstetrics", "MBBS, FCPS (Gyn)", fee: 1500);
        var pid = await RegisterPatientAsync(admin, PatientFields("Farzana Bibi", "Nasir Khan", "Female"));
        for (var i = 0; i < 11; i++) await BookAsync(admin, pid, doc);
        var id = await BookAsync(admin, pid, doc);
        var appt = await ApptAsync(id);
        Assert.Equal(12, appt.TokenNumber);
        Assert.Equal(1500m, appt.ConsultationFee);
        var patient = await PatientAsync(pid);

        foreach (var width in new[] { 58, 80 })
        {
            var html = await admin.GetHtmlAsync($"/Print/Appointment/{id}?width={width}");
            Assert.Contains($"w{width}", html);
            Assert.Contains("Burhan Test Hospital", html);
            Assert.Contains("Dr. Nadia Hussain", html);
            Assert.Contains("MBBS, FCPS (Gyn)", html);
            Assert.Contains("Gynaecology &amp; Obstetrics", html);
            Assert.Equal("12", TokenRegex().Match(html).Groups[1].Value);
            Assert.Contains("Farzana Bibi", html);
            Assert.Contains(patient.PatientNumber, html);
            Assert.Contains("35Y / F", html);
            Assert.Contains("1,500.00", html);
            Assert.Contains("Please wait for your turn.", html);
        }
        // Preview alone is not a print.
        Assert.Empty((await ApptAsync(id)).Encounter!.PrintedDocuments);

        // ESC/POS for the Print Agent: token in large text, cut command, 58mm = 32 columns.
        var esc = await JsonAsync(await AjaxAsync(admin, $"/Print/Appointment/{id}", $"/Print/Appointment/{id}?handler=EscPos"));
        var bytes = Convert.FromBase64String(esc.GetProperty("data").GetString()!);
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("TOKEN NO.", text);
        Assert.True(TokenLine(text, 12), "token 12 not found as large line after TOKEN NO.");
        Assert.Contains("Dr. Nadia Hussain", text);
        Assert.Contains(patient.PatientNumber, text);
        Assert.Contains("Please wait for your turn.", text);
        Assert.Contains("\u001dV", text); // GS V – paper cut

        // Settings: fee hidden, time + contact shown.
        await admin.PostFormAsync("/Settings/Appointments", new[]
        {
            P("Input.AppointmentSlipMessage", "Apni bari ka intezar karein."), P("Input.AppointmentSlipContact", "Reception: 091-1234567"),
            P("Input.AppointmentMaxDaysAhead", "10"), P("Input.AppointmentTimeEnabled", "true"), P("Input.ShowFeeOnAppointmentSlip", "false"),
            P("RowVersion", (await RowVersionAsync()).ToString())
        });
        var html2 = await admin.GetHtmlAsync($"/Print/Appointment/{id}");
        Assert.DoesNotContain("1,500.00", html2);
        Assert.Contains("Apni bari ka intezar karein.", html2);
        Assert.Contains("Reception: 091-1234567", html2);
    }

    private async Task<uint> RowVersionAsync()
    {
        await using var db = Factory.NewDb();
        return (await db.RegistrationSettings.SingleAsync()).RowVersion;
    }

    [Fact]
    public async Task Reprint_keeps_the_same_token_and_needs_reprint_permission()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var doc = await AddDoctorAsync(admin, "Dr. Reprint", "ENT");
        var pid = await RegisterPatientAsync(admin, PatientFields("Gul Zaman", "Sher Zaman"));
        var clerk = await UserWithAsync(admin, "appt.clerk", Perm.DashboardView, Perm.AppointmentsView, Perm.AppointmentsAdd, Perm.AppointmentsPrint);
        await BookAsync(admin, pid, doc);
        var id = await BookAsync(clerk, pid, doc);
        Assert.Equal(2, (await ApptAsync(id)).TokenNumber);

        var first = await JsonAsync(await AjaxAsync(clerk, $"/Print/Appointment/{id}", $"/Print/Appointment/{id}?handler=Record", P("width", "58"), P("method", "browser")));
        Assert.Equal(1, first.GetProperty("copy").GetInt32());
        // The clerk has no Reprint permission: UI shows the message, server refuses.
        Assert.Contains("Reprint Appointment Slip", await clerk.GetHtmlAsync($"/Print/Appointment/{id}"));
        var denied = await AjaxAsync(clerk, $"/Print/Appointment/{id}", $"/Print/Appointment/{id}?handler=Record", P("width", "58"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var copy2 = await JsonAsync(await AjaxAsync(admin, $"/Print/Appointment/{id}", $"/Print/Appointment/{id}?handler=Record", P("width", "80"), P("method", "agent")));
        Assert.Equal(2, copy2.GetProperty("copy").GetInt32());
        var html = await admin.GetHtmlAsync($"/Print/Appointment/{id}");
        Assert.Equal("2", TokenRegex().Match(html).Groups[1].Value);
        Assert.Contains("(REPRINT)", html);

        var a = await ApptAsync(id);
        Assert.Equal(2, a.TokenNumber);
        Assert.Equal(new[] { 1, 2 }, a.Encounter!.PrintedDocuments.OrderBy(p => p.PrintNumber).Select(p => p.PrintNumber));
        Assert.Equal(new[] { "58mm", "80mm" }, a.Encounter.PrintedDocuments.OrderBy(p => p.PrintNumber).Select(p => p.PaperSize));
        Assert.Single(await AuditAsync(AuditActions.AppointmentSlipPrinted));
        Assert.Contains(await AuditAsync(AuditActions.AppointmentSlipReprinted), x => x.Description!.Contains("copy 2") && x.Description.Contains("token 2"));
        Assert.Contains("Reprint – copy 2", await admin.GetHtmlAsync($"/Appointments/Details/{id}"));
    }

    [Fact]
    public async Task Lan_printer_receives_escpos_and_printer_settings_are_validated()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync(); // workstation "Server-PC"
        var doc = await AddDoctorAsync(admin, "Dr. Network Printer", "Cardiology");
        var pid = await RegisterPatientAsync(admin, PatientFields("Tariq Mehmood", "Mehmood Khan"));
        var id = await BookAsync(admin, pid, doc);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var ms = new MemoryStream();
            await client.GetStream().CopyToAsync(ms);
            return ms.ToArray();
        });

        IEnumerable<KeyValuePair<string, string>> Profile(string ws, string mode, string host, int p, int width = 80, string agent = "http://127.0.0.1:9123") => new[]
        {
            P("Input.WorkstationName", ws), P("Input.Mode", mode), P("Input.PaperWidthMm", width.ToString()), P("Input.LanHost", host),
            P("Input.LanPort", p.ToString()), P("Input.AgentUrl", agent), P("Input.Alignment", "Center"), P("Input.FontSize", "Normal"),
            P("Input.TokenSize", "4"), P("Input.Copies", "1"), P("Input.AutoCut", "true"), P("Input.AutoCut", "false"),
            P("Input.BoldText", "true"), P("Input.BoldText", "false"), P("Input.ShowLogo", "false")
        };
        // Validation: internet IPs, remote agents and odd paper sizes are refused.
        Assert.Contains("local network", Errors(await (await admin.PostFormAsync("/Settings/Printers?new=true", Profile("Server-PC", "LanDirect", "8.8.8.8", 9100), "/Settings/Printers")).Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("this computer", Errors(await (await admin.PostFormAsync("/Settings/Printers?new=true", Profile("Server-PC", "Browser", "", 9100, 80, "http://evil.example.com:9123"), "/Settings/Printers")).Content.ReadAsStringAsync()));
        Assert.Contains("58 mm or 80 mm", Errors(await (await admin.PostFormAsync("/Settings/Printers?new=true", Profile("Server-PC", "Browser", "", 9100, 70), "/Settings/Printers")).Content.ReadAsStringAsync()));
        // A USB (Print Agent) profile for another computer and a LAN profile for this one.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Settings/Printers?new=true",
            Profile("Reception-1", "LocalAgent", "", 9100, 58).Append(P("Input.WindowsPrinterName", "XP-58")), "/Settings/Printers")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Settings/Printers?new=true", Profile("Server-PC", "LanDirect", "127.0.0.1", port), "/Settings/Printers")).StatusCode);
        Assert.Equal(2, (await AuditAsync(AuditActions.PrinterProfileSaved)).Count);

        var page = await admin.GetHtmlAsync($"/Print/Appointment/{id}");
        Assert.Contains("data-mode=\"LanDirect\"", page);
        var r = await JsonAsync(await AjaxAsync(admin, $"/Print/Appointment/{id}", $"/Print/Appointment/{id}?handler=Lan"));
        Assert.True(r.GetProperty("ok").GetBoolean());
        var bytes = await received.WaitAsync(TimeSpan.FromSeconds(10));
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Equal(new byte[] { 0x1B, 0x40 }, bytes[..2]); // ESC @ initialise
        Assert.Contains("Dr. Network Printer", text);
        Assert.Contains("TOKEN NO.", text);
        Assert.True(TokenLine(text, 1), "token 1 not found after TOKEN NO.");
        Assert.Contains("\u001dV", text);
        Assert.Single((await ApptAsync(id)).Encounter!.PrintedDocuments);

        // Printer switched off → clear error, audit entry, nothing recorded as printed.
        listener.Stop();
        var off = await AjaxAsync(admin, $"/Print/Appointment/{id}", $"/Print/Appointment/{id}?handler=Lan");
        Assert.Equal(HttpStatusCode.BadRequest, off.StatusCode);
        Assert.Contains("did not respond", await off.Content.ReadAsStringAsync());
        Assert.Single(await AuditAsync(AuditActions.AppointmentPrintFailed));
        Assert.Single((await ApptAsync(id)).Encounter!.PrintedDocuments);

        // Test page from printer settings uses the same composer.
        var profiles = await admin.GetHtmlAsync("/Settings/Printers");
        Assert.Contains("Reception-1", profiles);
        Assert.Contains("XP-58", profiles);
        int agentProfile;
        await using (var db = Factory.NewDb()) agentProfile = await db.PrinterProfiles.Where(x => x.WorkstationName == "Reception-1").Select(x => x.Id).SingleAsync();
        var test = await JsonAsync(await AjaxAsync(admin, $"/Print/PrinterTest?profile={agentProfile}", $"/Print/PrinterTest?profile={agentProfile}&handler=EscPos"));
        Assert.Equal("XP-58", test.GetProperty("printer").GetString());
        Assert.Contains("PRINTER TEST", Encoding.ASCII.GetString(Convert.FromBase64String(test.GetProperty("data").GetString()!)));
        await JsonAsync(await AjaxAsync(admin, $"/Print/PrinterTest?profile={agentProfile}", $"/Print/PrinterTest?profile={agentProfile}&handler=Record", P("method", "agent")));
        Assert.Single(await AuditAsync(AuditActions.PrinterTestPrint));

        // The Print Agent profile is used by a computer named "Reception-1".
        var rec = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(rec, AdminUser, AdminPassword, "Reception-1")).StatusCode);
        var recPage = await rec.GetHtmlAsync($"/Print/Appointment/{id}");
        Assert.Contains("data-mode=\"LocalAgent\"", recPage);
        Assert.Contains("w58", recPage);
    }

    [Fact]
    public async Task Government_hospital_workflow_is_unchanged()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Sher Afzal", "Afzal Khan"));
        Assert.Contains("OPD registration", await admin.GetHtmlAsync($"/Opd/Register?patientId={pid}"));
        var v = await RegisterOpdAsync(admin, pid);
        var slip = await admin.GetHtmlAsync($"/Print/Opd/{v}");
        Assert.Contains("OPD SLIP", slip);
        Assert.Equal(PrintedDocumentType.OpdSlip, (await EncounterAsync(v)).PrintedDocuments.Single().DocumentType);
        var er = await PostEmergencyAsync(admin, pid, ("Visit.BriefComplaint", "Chest pain"));
        Assert.Equal(HttpStatusCode.Redirect, er.StatusCode);

        var home = await admin.GetHtmlAsync("/");
        Assert.DoesNotContain("data-nav=\"appointment-book\"", home);
        Assert.DoesNotContain("Today's appointments by doctor", home);
        var doc = await AddDoctorAsync(admin, "Dr. Govt");
        var book = await admin.PostFormAsync("/Patients/Register", BookFields(doc), $"/Appointments/Book?patientId={pid}");
        Assert.Contains("private hospital", Errors(await book.Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        await using var db = Factory.NewDb();
        Assert.Empty(await db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task Private_hospital_links_lead_to_appointments_and_permissions_are_enforced()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var doc = await AddDoctorAsync(admin, "Dr. Links", "Dermatology", fee: 800);
        var pid = await RegisterPatientAsync(admin, PatientFields("Link Patient", "Link Father"));

        // OPD entry points forward to appointment booking; new patients come back to booking.
        Assert.Equal($"/Appointments/Book?patientId={pid}", (await admin.GetAsync($"/Opd/Register?patientId={pid}")).Headers.Location!.OriginalString);
        var fields = PatientFields("Second Link", "Other Father");
        fields["Next"] = "appointment";
        fields["ConfirmNotDuplicate"] = "true";
        var reg = await PostRegisterAsync(admin, fields);
        Assert.StartsWith("/Appointments/Book?patientId=", reg.Headers.Location!.OriginalString);
        var book = await admin.GetHtmlAsync($"/Appointments/Book?patientId={pid}");
        Assert.Contains("Dr. Links", book);
        Assert.Contains($"data-next-token=\"{doc}\">1<", book);
        var id = await BookAsync(admin, pid, doc);
        Assert.Contains($"data-next-token=\"{doc}\">2<", await admin.GetHtmlAsync($"/Appointments/Book?patientId={pid}"));

        // The OPD visit print link of an appointment opens the thermal token slip.
        var encounterId = (await ApptAsync(id)).EncounterId;
        Assert.Equal($"/Print/Appointment/{id}", (await admin.GetAsync($"/Print/Opd/{encounterId}")).Headers.Location!.OriginalString);
        var home = await admin.GetHtmlAsync("/");
        Assert.Contains("data-nav=\"appointment-book\"", home);
        Assert.Contains("Today's appointments by doctor", home);
        Assert.Contains((await ApptAsync(id)).Encounter!.EncounterNumber, await admin.GetHtmlAsync($"/Opd?doctor={doc}"));

        // View-only user: lists yes (no fees), booking/cancel/reports/printers no.
        var viewer = await UserWithAsync(admin, "appt.viewer", Perm.DashboardView, Perm.AppointmentsView);
        var list = await viewer.GetHtmlAsync("/Appointments");
        Assert.Contains("Dr. Links", list);
        Assert.DoesNotContain("800.00", list);
        Assert.DoesNotContain("data-test=\"appt-book\"", list);
        var vhome = await viewer.GetHtmlAsync("/");
        Assert.Contains("data-nav=\"appointments\"", vhome);
        Assert.DoesNotContain("data-nav=\"appointment-book\"", vhome);
        var details = await viewer.GetHtmlAsync($"/Appointments/Details/{id}");
        Assert.DoesNotContain("data-test=\"appt-cancel\"", details);
        Assert.DoesNotContain("data-test=\"appt-print\"", details);
        foreach (var url in new[] { "/Appointments/Book", "/Appointments/Reports", "/Settings/Printers", "/Settings/Appointments", $"/Print/Appointment/{id}" })
        {
            var r = await viewer.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied"), $"{url} → {(int)r.StatusCode} {r.Headers.Location}");
        }
        var forged = await viewer.PostFormAsync($"/Appointments/Details/{id}", new[] { P("CancelReason", "forged") }, $"/Appointments/Details/{id}?handler=Cancel");
        Assert.Contains("/Account/AccessDenied", forged.Headers.Location!.ToString());
        var forgedBook = await viewer.PostFormAsync($"/Appointments/Details/{id}", BookFields(doc), $"/Appointments/Book?patientId={pid}");
        Assert.Contains("/Account/AccessDenied", forgedBook.Headers.Location!.ToString());
        var forgedPrint = await AjaxAsync(viewer, $"/Appointments/Details/{id}", $"/Print/Appointment/{id}?handler=Record", P("width", "80"));
        Assert.True(forgedPrint.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);
        Assert.Equal(AppointmentStatus.Booked, (await ApptAsync(id)).Status);
        Assert.Empty((await ApptAsync(id)).Encounter!.PrintedDocuments);

        // No appointment permissions at all.
        var other = await UserWithAsync(admin, "no.appt", Perm.DashboardView, Perm.PatientsSearch);
        Assert.Contains("/Account/AccessDenied", (await other.GetAsync("/Appointments")).Headers.Location!.ToString());

        // Fee visible with financial access.
        var cashier = await UserWithAsync(admin, "appt.fees", Perm.DashboardView, Perm.AppointmentsView, Perm.AppointmentsFees);
        Assert.Contains("800.00", await cashier.GetHtmlAsync("/Appointments"));
    }
}
