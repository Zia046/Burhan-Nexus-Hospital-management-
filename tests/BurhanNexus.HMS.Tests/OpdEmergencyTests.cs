using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public class OpdEmergencyTests : Phase2TestBase
{
    [Fact]
    public async Task Government_opd_repeat_visits_keep_patient_id_and_print_prescription_slip()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Fazal Rabi", "Sher Ali"));
        var v1 = await RegisterOpdAsync(admin, pid, ("Visit.ChiefComplaint", "Fever 3 days"));
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var v2 = await RegisterOpdAsync(admin, pid);

        var e1 = await EncounterAsync(v1);
        var e2 = await EncounterAsync(v2);
        var year = e1.EncounterDate.Year;
        Assert.Equal($"OPD-{year}-000001", e1.EncounterNumber);
        Assert.Equal($"OPD-{year}-000002", e2.EncounterNumber);
        Assert.Equal(pid, e1.PatientId);
        Assert.Equal(pid, e2.PatientId);
        Assert.Equal(1, e1.TokenNumber);
        Assert.Equal(2, e2.TokenNumber);
        Assert.False(e1.Opd!.IsRepeatVisit);
        Assert.True(e2.Opd!.IsRepeatVisit);
        Assert.Equal(HospitalType.Government, e1.HospitalTypeAtVisit);
        Assert.NotNull(e1.ServiceItemId); // fee reference prepared, no payment recorded
        Assert.Equal("BNH-000001", (await PatientAsync(pid)).PatientNumber);

        var slip = await admin.GetHtmlAsync($"/Print/Opd/{v1}");
        foreach (var expected in new[] { "OPD SLIP", "Burhan Test Hospital", "/branding/logo", "Main Road", "BNH-000001", e1.EncounterNumber, "Fazal Rabi",
                     "S/O Sher Ali", "35 years", "Male", "Token", "Complaints", "Clinical findings", "Diagnosis", "Lab investigations",
                     "X-Ray / Ultrasound", "Advice / Follow-up", "Doctor&#x27;s signature", "Fever 3 days", "Save as PDF", "paper-a5" })
            Assert.Contains(expected, slip);
        Assert.Contains("paper-a4", await admin.GetHtmlAsync($"/Print/Opd/{v1}?paper=A4"));
        Assert.Equal(2, (await EncounterAsync(v1)).PrintedDocuments.Count);
        Assert.Contains("copy 2", await admin.GetHtmlAsync($"/Opd/Visit/{v1}"));

        var history = await admin.GetHtmlAsync($"/Patients/Profile/{pid}");
        Assert.Contains(e1.EncounterNumber, history);
        Assert.Contains(e2.EncounterNumber, history);
        var list = await admin.GetHtmlAsync("/Opd");
        Assert.Contains(e2.EncounterNumber, list);
        Assert.Equal(2, (await AuditAsync(AuditActions.OpdVisitRegistered)).Count);
        Assert.NotEmpty(await AuditAsync(AuditActions.DocumentPrinted));
        await using var db = Factory.NewDb();
        Assert.Equal(1, await db.Patients.CountAsync()); // no new patient per visit
    }

    [Fact]
    public async Task Private_hospital_records_visits_against_doctors_with_visit_document()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var addDoc = await admin.PostFormAsync("/Opd/Setup", new[]
        {
            P("DoctorForm.Name", "Dr. Sana Khan"), P("DoctorForm.Qualification", "MBBS, FCPS"), P("DoctorForm.Specialty", "Gynaecology"),
            P("DoctorForm.IsActive", "true")
        }, "/Opd/Setup?handler=Doctor");
        Assert.Equal(HttpStatusCode.Redirect, addDoc.StatusCode);
        await admin.PostFormAsync("/Opd/Setup", new[] { P("DoctorForm.Name", "Dr. Imran Ali"), P("DoctorForm.IsActive", "true") }, "/Opd/Setup?handler=Doctor");
        int sana, imran;
        await using (var db = Factory.NewDb())
        {
            sana = await db.Doctors.Where(d => d.Name == "Dr. Sana Khan").Select(d => d.Id).SingleAsync();
            imran = await db.Doctors.Where(d => d.Name == "Dr. Imran Ali").Select(d => d.Id).SingleAsync();
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Rukhsana Bibi", "Javed Iqbal", "Female"));

        var noDoctor = await admin.PostFormAsync($"/Opd/Register?patientId={pid}", Array.Empty<KeyValuePair<string, string>>());
        Assert.Contains("Please select the doctor", await noDoctor.Content.ReadAsStringAsync());

        var v1 = await RegisterOpdAsync(admin, pid, ("Visit.DoctorId", sana.ToString()));
        var v2 = await RegisterOpdAsync(admin, pid, ("Visit.DoctorId", imran.ToString()));
        var doc = await admin.GetHtmlAsync($"/Print/Opd/{v1}");
        Assert.Contains("VISIT RECORD", doc);
        Assert.DoesNotContain("OPD SLIP", doc);
        Assert.Contains("Dr. Sana Khan", doc);
        Assert.Contains("MBBS, FCPS", doc);
        Assert.Equal(PrintedDocumentType.PrivateVisitDocument, (await EncounterAsync(v1)).PrintedDocuments.Single().DocumentType);

        var sanaList = await admin.GetHtmlAsync($"/Opd?doctor={sana}");
        Assert.Contains((await EncounterAsync(v1)).EncounterNumber, sanaList);
        Assert.DoesNotContain((await EncounterAsync(v2)).EncounterNumber, sanaList);
        await using var db2 = Factory.NewDb();
        Assert.Equal(HospitalType.Private, (await db2.PatientEncounters.FindAsync(v1))!.HospitalTypeAtVisit);
    }

    [Fact]
    public async Task Emergency_registration_with_minimum_information_and_existing_patient()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        int category;
        await using (var db = Factory.NewDb()) category = await db.EmergencyCategories.Where(c => c.Name == "Road Traffic Accident").Select(c => c.Id).SingleAsync();

        // Unconscious, unidentified patient – nothing else known.
        var r1 = await PostEmergencyAsync(admin, null, ("Patient.IsUnidentified", "true"), ("Patient.Gender", "Male"),
            ("Visit.CategoryId", category.ToString()), ("Visit.BroughtBy", "Rescue 1122"), ("Visit.IsMedicoLegal", "true"));
        Assert.True(r1.StatusCode == HttpStatusCode.Redirect, Errors(await r1.Content.ReadAsStringAsync()));
        var e1 = await EncounterAsync(EncounterId(r1));
        var unknown = await PatientAsync(e1.PatientId);
        Assert.True(unknown.IsUnidentified);
        Assert.Equal("Unidentified Male", unknown.FullName);
        Assert.Equal($"ER-{e1.EncounterDate.Year}-000001", e1.EncounterNumber);
        Assert.Equal(EmergencyStatus.Registered, e1.Emergency!.Status);
        Assert.Single(e1.Emergency.StatusHistory);

        // Existing patient keeps the same Patient ID.
        var pid = await RegisterPatientAsync(admin, PatientFields("Wali Khan", "Bacha Khan"));
        var r2 = await PostEmergencyAsync(admin, pid, ("Visit.BriefComplaint", "Chest pain"), ("Visit.AttendantName", "Asfandyar"), ("Visit.AttendantContact", "0312-3456789"));
        var e2 = await EncounterAsync(EncounterId(r2));
        Assert.Equal(pid, e2.PatientId);
        Assert.Equal($"ER-{e2.EncounterDate.Year}-000002", e2.EncounterNumber);

        // Minimal new patient: name + gender only.
        var r3 = await PostEmergencyAsync(admin, null, ("Patient.FullName", "Bakht Zada"), ("Patient.Gender", "Male"));
        Assert.True(r3.StatusCode == HttpStatusCode.Redirect, Errors(await r3.Content.ReadAsStringAsync()));

        var slip = await admin.GetHtmlAsync($"/Print/Emergency/{e1.Id}");
        foreach (var expected in new[] { "EMERGENCY", e1.EncounterNumber, unknown.PatientNumber, "Unidentified Male", "Road Traffic Accident", "Rescue 1122", "MLC",
                     "Initial assessment", "Diagnosis", "Treatment / Medicines", "Injections", "IV infusion", "Dressing / Stitches",
                     "Referral instructions", "Discharge instructions", "paper-a4", "Burhan Test Hospital" })
            Assert.Contains(expected, slip);
        var slip2 = await admin.GetHtmlAsync($"/Print/Emergency/{e2.Id}");
        Assert.Contains("Chest pain", slip2);
        Assert.Contains("Asfandyar", slip2);

        // Complete the unidentified patient's details later.
        var edit = new Dictionary<string, string>
        {
            ["Patient.FullName"] = "Saleem Khan", ["Patient.Gender"] = "Male", ["Patient.Age"] = "40", ["Patient.AgeUnit"] = "Years",
            ["RowVersion"] = unknown.RowVersion.ToString()
        };
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Patients/Edit/{unknown.Id}", edit.Select(kv => P(kv.Key, kv.Value)))).StatusCode);
        var known = await PatientAsync(unknown.Id);
        Assert.False(known.IsUnidentified);
        Assert.Equal(unknown.PatientNumber, known.PatientNumber);
        Assert.Equal(3, (await AuditAsync(AuditActions.EmergencyRegistered)).Count);
        Assert.Contains(e1.EncounterNumber, await admin.GetHtmlAsync("/Emergency"));
    }

    [Fact]
    public async Task Emergency_status_changes_keep_history_and_respect_restricted_permissions()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Hamid Gul", "Zarin Gul"));
        var r = await PostEmergencyAsync(admin, pid);
        var id = EncounterId(r);
        var url = $"/Emergency/Visit/{id}";

        async Task<string> Change(Browser b, string status, string? reason = null)
        {
            var fields = new List<KeyValuePair<string, string>> { P("NewStatus", status) };
            if (reason is not null) fields.Add(P("Reason", reason));
            await b.PostFormAsync(url, fields, url + "?handler=Status");
            return await b.GetHtmlAsync(url);
        }

        Assert.Contains("Status changed to Under Treatment", await Change(admin, "UnderTreatment"));
        Assert.Contains("Please give a reason", await Change(admin, "Referred"));
        Assert.Contains("Status changed to Referred", await Change(admin, "Referred", "Referred to LRH Peshawar"));

        // Nurse with basic status rights cannot close.
        var nurse = await UserWithAsync(admin, "er.nurse", Perm.DashboardView, Perm.EmergencyView, Perm.EmergencyStatus);
        Assert.Contains("Status changed to Discharged", await Change(nurse, "Discharged"));
        Assert.Contains("not allowed", await Change(nurse, "Closed", "Done"));
        Assert.DoesNotContain("value=\"Closed\"", await nurse.GetHtmlAsync(url));

        // Viewer without status rights cannot post at all.
        var viewer = await UserWithAsync(admin, "er.viewer", Perm.DashboardView, Perm.EmergencyView);
        Assert.DoesNotContain("Update status", await viewer.GetHtmlAsync(url));
        var token = await viewer.TokenAsync(url);
        var forged = await viewer.Http.PostAsync(url + "?handler=Status", new FormUrlEncodedContent(new[] { P("NewStatus", "Closed"), P("Reason", "x x x"), P("__RequestVerificationToken", token) }));
        Assert.Contains("/Account/AccessDenied", forged.Headers.Location!.ToString());

        Assert.Contains("Status changed to Closed", await Change(admin, "Closed", "Treatment complete"));
        var enc = await EncounterAsync(id);
        Assert.Equal(EmergencyStatus.Closed, enc.Emergency!.Status);
        var history = enc.Emergency.StatusHistory.OrderBy(h => h.Id).ToList();
        Assert.Equal(new[] { EmergencyStatus.Registered, EmergencyStatus.UnderTreatment, EmergencyStatus.Referred, EmergencyStatus.Discharged, EmergencyStatus.Closed },
            history.Select(h => h.ToStatus));
        Assert.Equal("er.nurse", history[3].ChangedByUsername);
        Assert.Equal(EmergencyStatus.Referred, history[3].FromStatus);
        Assert.Equal("Referred to LRH Peshawar", history[2].Reason);
        Assert.Equal(4, (await AuditAsync(AuditActions.EmergencyStatusChanged)).Count);
    }

    [Fact]
    public async Task Patient_module_permissions_are_enforced_in_ui_and_server()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Karim Dad", "Rahim Dad"));
        var reception = await UserWithAsync(admin, "reception2", Perm.DashboardView, Perm.PatientsAdd, Perm.PatientsSearch, Perm.OpdAdd, Perm.OpdPrint);

        var dash = await reception.GetHtmlAsync("/");
        Assert.Contains("href=\"/Patients/Register\"", dash);
        Assert.Contains("href=\"/Patients\"", dash);
        Assert.Contains("href=\"/Opd/Register\"", dash);
        Assert.DoesNotContain("href=\"/Emergency", dash);
        Assert.DoesNotContain("Emergency Management", dash);

        var search = await reception.GetHtmlAsync("/Patients?q=Karim");
        Assert.Contains("Karim Dad", search);
        Assert.DoesNotContain($"/Patients/Profile/{pid}", search); // no View Profile permission → no link
        Assert.DoesNotContain("handler=Export", search);

        foreach (var url in new[] { $"/Patients/Profile/{pid}", $"/Patients/Edit/{pid}", $"/Patients/Merge/{pid}", "/Emergency", "/Emergency/Register", "/Opd", "/Opd/Setup", "/Settings/Registration", "/Patients?handler=Export" })
        {
            var r = await reception.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("/Account/AccessDenied"),
                $"{url} should be denied, got {(int)r.StatusCode} {r.Headers.Location}");
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/patients/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/patients/search?q=Karim")).StatusCode);

        // Allowed workflow: OPD registration and slip printing.
        var visit = await RegisterOpdAsync(reception, pid);
        Assert.Contains("OPD SLIP", await reception.GetHtmlAsync($"/Print/Opd/{visit}"));

        // Forged edit is blocked and nothing changes.
        var p = await PatientAsync(pid);
        var token = await reception.TokenAsync("/Patients/Register");
        var forged = await reception.Http.PostAsync($"/Patients/Edit/{pid}", new FormUrlEncodedContent(new[]
        {
            P("Patient.FullName", "Hacked"), P("Patient.Gender", "Male"), P("RowVersion", p.RowVersion.ToString()), P("__RequestVerificationToken", token)
        }));
        Assert.Contains("/Account/AccessDenied", forged.Headers.Location!.ToString());
        Assert.Equal("Karim Dad", (await PatientAsync(pid)).FullName);

        // Without search permission the search API is blocked too.
        var registerOnly = await UserWithAsync(admin, "register.only", Perm.DashboardView, Perm.PatientsAdd);
        Assert.Equal(HttpStatusCode.Forbidden, (await registerOnly.GetAsync("/api/patients/search?q=Karim")).StatusCode);
        Assert.DoesNotContain("href=\"/Patients\"", await registerOnly.GetHtmlAsync("/"));
    }

    [Fact]
    public async Task Patient_data_survives_backup_and_restore_and_numbering_continues_safely()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var p1 = await RegisterPatientAsync(admin, PatientFields("Before Backup", "Father One"));
        await RegisterOpdAsync(admin, p1);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Backup", new[] { P("Notes", "phase2") }, "/Backup?handler=Create")).StatusCode);
        int backupId;
        await using (var db = Factory.NewDb()) backupId = await db.BackupHistory.Select(b => b.Id).SingleAsync();
        await RegisterPatientAsync(admin, PatientFields("After Backup", "Father Two"));

        var restore = await admin.PostFormAsync($"/Backup/Restore/{backupId}", new[] { P("Password", AdminPassword), P("Understood", "true"), P("Confirmation", "RESTORE") });
        Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
        admin = await LoginAdminAsync();
        await using (var db = Factory.NewDb())
        {
            Assert.True(await db.Patients.AnyAsync(p => p.FullName == "Before Backup"));
            Assert.False(await db.Patients.AnyAsync(p => p.FullName == "After Backup"));
            Assert.Equal(1, await db.PatientEncounters.CountAsync());
        }
        var p3 = await RegisterPatientAsync(admin, PatientFields("After Restore", "Father Three"));
        var v3 = await RegisterOpdAsync(admin, p3);
        await using var db2 = Factory.NewDb();
        Assert.Equal(2, await db2.Patients.Select(p => p.PatientNumber).Distinct().CountAsync());
        Assert.Equal(2, await db2.PatientEncounters.Select(e => e.EncounterNumber).Distinct().CountAsync());
        Assert.NotNull(await db2.PatientEncounters.FindAsync(v3));
    }
}
