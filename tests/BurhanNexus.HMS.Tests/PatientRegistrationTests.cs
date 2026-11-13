using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public class PatientRegistrationTests : Phase2TestBase
{
    [Fact]
    public async Task Registration_assigns_permanent_sequential_patient_ids_and_calculates_age()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();

        // Validation: address, city and age are required for a normal registration; optional fields stay optional.
        var bad = await PostRegisterAsync(admin, new Dictionary<string, string> { ["Patient.FullName"] = "Gul Bibi", ["Patient.Gender"] = "Female", ["Next"] = "profile" });
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        var badHtml = await bad.Content.ReadAsStringAsync();
        Assert.Contains("Address is required", badHtml);
        Assert.Contains("Enter the age or the date of birth", badHtml);
        Assert.DoesNotContain("CNIC must", badHtml);

        var p1 = await RegisterPatientAsync(admin, PatientFields("muhammad   ali"));
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddMonths(-3).AddDays(-2);
        var infant = PatientFields("Baby Of Gul Bibi", "Gul Bibi", "Female");
        infant.Remove("Patient.Age");
        infant["Patient.GuardianRelation"] = "DaughterOf";
        infant["Patient.DateOfBirth"] = dob.ToString("yyyy-MM-dd");
        infant["Patient.Cnic"] = "1730112345671";
        infant["Patient.MobileNumber"] = "+92 300 1234567";
        var p2 = await RegisterPatientAsync(admin, infant);
        var newborn = PatientFields("Newborn Male", "Gul Bibi");
        newborn["Patient.Age"] = "5";
        newborn["Patient.AgeUnit"] = "Days";
        var p3 = await RegisterPatientAsync(admin, newborn);

        var a = await PatientAsync(p1);
        var b = await PatientAsync(p2);
        var c = await PatientAsync(p3);
        Assert.Equal("BNH-000001", a.PatientNumber);
        Assert.Equal("BNH-000002", b.PatientNumber);
        Assert.Equal("BNH-000003", c.PatientNumber);
        Assert.Equal("Muhammad Ali", a.FullName); // cleaned & title-cased
        // Ages are calculated on the hospital's (Asia/Karachi) date, as at registration.
        Assert.Equal(35, AgeCalculator.Of(a, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)))!.Value.Value);
        Assert.Equal(AgeUnit.Months, AgeCalculator.Of(b, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)))!.Value.Unit);
        Assert.False(b.DateOfBirthEstimated);
        Assert.Equal("17301-1234567-1", b.Cnic);
        Assert.Equal("0300-1234567", b.MobileNumber);
        Assert.Equal(AgeUnit.Days, c.AgeUnitAtRegistration);
        Assert.True(c.DateOfBirthEstimated);
        Assert.Contains(a.Identifiers, i => i.Type == PatientIdentifierType.PatientNumber && i.Value == "BNH-000001");
        Assert.Equal(3, (await AuditAsync(AuditActions.PatientRegistered)).Count);

        var profile = await admin.GetHtmlAsync($"/Patients/Profile/{p2}");
        Assert.Contains("BNH-000002", profile);
        Assert.Contains("3 months", profile);
        Assert.Contains("D/O", profile);

        // Configurable format applies to new patients only.
        await using (var db = Factory.NewDb())
        {
            var s = await db.RegistrationSettings.SingleAsync();
            var save = await admin.PostFormAsync("/Settings/Registration", new[]
            {
                P("Input.PatientNumberPrefix", "RHC-"), P("Input.PatientNumberDigits", "5"), P("Input.OpdNumberPrefix", "OPD-"),
                P("Input.EmergencyNumberPrefix", "ER-"), P("Input.EncounterNumberIncludesYear", "true"), P("Input.EncounterNumberDigits", "6"),
                P("Input.OpdSlipPaper", "A5"), P("Input.EmergencySlipPaper", "A4"), P("RowVersion", s.RowVersion.ToString())
            });
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }
        var p4 = await RegisterPatientAsync(admin, PatientFields("Zahid Shah", "Noor Shah"));
        Assert.Equal("RHC-00004", (await PatientAsync(p4)).PatientNumber);
        Assert.Equal("BNH-000001", (await PatientAsync(p1)).PatientNumber);
    }

    [Fact]
    public async Task Possible_duplicates_are_shown_before_creating_a_new_patient()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var original = PatientFields("Muhammad Ali", "Akbar Khan");
        original["Patient.Cnic"] = "17301-1234567-1";
        original["Patient.MobileNumber"] = "0300-1112223";
        var firstId = await RegisterPatientAsync(admin, original, confirmDuplicate: false);

        // Same CNIC, different spelling → warning, nothing created.
        var same = PatientFields("Muhammad Aly", "Akbar");
        same["Patient.Cnic"] = "1730112345671";
        var warn = await PostRegisterAsync(admin, same);
        Assert.Equal(HttpStatusCode.OK, warn.StatusCode);
        var html = await warn.Content.ReadAsStringAsync();
        Assert.Contains("Possible existing patient found. Please verify before creating a new registration.", html);
        Assert.Contains("BNH-000001", html);
        Assert.Contains("Same CNIC", html);
        await using (var db = Factory.NewDb()) Assert.Equal(1, await db.Patients.CountAsync());

        // Same name + father's name (no CNIC) → warning as well; mobile match → warning.
        var byName = await PostRegisterAsync(admin, PatientFields("Muhammad  Ali", "Akbar Khan"));
        Assert.Contains("Possible existing patient found", await byName.Content.ReadAsStringAsync());
        var byMobile = PatientFields("Sana Ali", "Muhammad Ali", "Female");
        byMobile["Patient.MobileNumber"] = "03001112223";
        Assert.Contains("Same mobile number", await (await PostRegisterAsync(admin, byMobile)).Content.ReadAsStringAsync());

        // Different person with a different father → no warning.
        var other = await PostRegisterAsync(admin, PatientFields("Muhammad Ali", "Gul Zaman"));
        Assert.Equal(HttpStatusCode.Redirect, other.StatusCode);

        // Confirmed by staff → created, and the override is audited.
        var confirmed = await RegisterPatientAsync(admin, same, confirmDuplicate: true);
        Assert.NotEqual(firstId, confirmed);
        Assert.Single(await AuditAsync(AuditActions.PatientDuplicateWarningOverridden));

        var api = await (await admin.GetAsync("/api/patients/duplicates?name=Muhammad%20Ali&guardian=Akbar%20Khan")).Content.ReadAsStringAsync();
        Assert.Contains("BNH-000001", api);
    }

    [Fact]
    public async Task Search_by_id_name_mobile_cnic_father_and_visit_number_with_filters()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var f1 = PatientFields("Shahid Afridi", "Sahibzada Khan");
        f1["Patient.MobileNumber"] = "0333-9876543";
        f1["Patient.Cnic"] = "42101-7654321-3";
        var p1 = await RegisterPatientAsync(admin, f1);
        var p2 = await RegisterPatientAsync(admin, PatientFields("Ayesha Gul", "Imran Gul", "Female"));
        var visit = await RegisterOpdAsync(admin, p1);
        var visitNo = (await EncounterAsync(visit)).EncounterNumber;

        var exact = await admin.GetAsync("/Patients?q=bnh-000001");
        Assert.Equal($"/Patients/Profile/{p1}", exact.Headers.Location?.OriginalString);
        Assert.Contains("Shahid Afridi", await admin.GetHtmlAsync("/Patients?q=afridi"));
        Assert.Contains("Shahid Afridi", await admin.GetHtmlAsync("/Patients?q=9876543"));
        Assert.Contains("Shahid Afridi", await admin.GetHtmlAsync("/Patients?q=42101-7654321-3"));
        Assert.Contains("Shahid Afridi", await admin.GetHtmlAsync("/Patients?q=Sahibzada"));
        Assert.Contains("Shahid Afridi", await admin.GetHtmlAsync($"/Patients?q={visitNo}"));
        Assert.Contains("Ayesha Gul", await admin.GetHtmlAsync("/Patients?q=2"));

        var female = await admin.GetHtmlAsync("/Patients?gender=Female");
        Assert.Contains("Ayesha Gul", female);
        Assert.DoesNotContain("Shahid Afridi", female);
        var opdOnly = await admin.GetHtmlAsync("/Patients?visits=opd");
        Assert.Contains("Shahid Afridi", opdOnly);
        Assert.DoesNotContain("Ayesha Gul", opdOnly);

        // Archive → hidden from default search, shown in archived filter, cannot get new visits.
        var arch = await admin.PostFormAsync($"/Patients/Profile/{p2}", new[] { P("archive", "true"), P("Reason", "Registered by mistake") }, $"/Patients/Profile/{p2}?handler=Archive");
        Assert.Equal(HttpStatusCode.Redirect, arch.StatusCode);
        Assert.DoesNotContain("Ayesha Gul", await admin.GetHtmlAsync("/Patients"));
        Assert.Contains("Ayesha Gul", await admin.GetHtmlAsync("/Patients?status=archived"));
        var blocked = await admin.PostFormAsync($"/Opd/Register?patientId={p2}", Array.Empty<KeyValuePair<string, string>>());
        Assert.Contains("archived", await blocked.Content.ReadAsStringAsync());
        Assert.Single(await AuditAsync(AuditActions.PatientArchived));

        var csv = await admin.GetAsync("/Patients?status=all&handler=Export");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("BNH-000001", text);
        Assert.Contains("BNH-000002", text);
        Assert.Single(await AuditAsync(AuditActions.PatientsExported));
    }

    [Fact]
    public async Task Edit_keeps_patient_id_and_audits_changes_and_duplicates_can_be_merged()
    {
        await SetupAsync();
        var admin = await LoginAdminAsync();
        var keep = await RegisterPatientAsync(admin, PatientFields("Noor Muhammad", "Gul Muhammad"));
        var dup = await RegisterPatientAsync(admin, PatientFields("Noor Mohammad", "Gul Mohammad"));
        var dupVisit = await RegisterOpdAsync(admin, dup);

        var current = await PatientAsync(keep);
        var edit = PatientFields("Noor Muhammad Khan", "Gul Muhammad");
        edit["RowVersion"] = current.RowVersion.ToString();
        edit["Patient.MobileNumber"] = "03451234567";
        var r = await admin.PostFormAsync($"/Patients/Edit/{keep}", edit.Select(kv => P(kv.Key, kv.Value)));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var after = await PatientAsync(keep);
        Assert.Equal("Noor Muhammad Khan", after.FullName);
        Assert.Equal(current.PatientNumber, after.PatientNumber);
        var audit = Assert.Single(await AuditAsync(AuditActions.PatientUpdated));
        Assert.Contains("Noor Muhammad", audit.OldValues);
        Assert.Contains("Noor Muhammad Khan", audit.NewValues);

        var dupNumber = (await PatientAsync(dup)).PatientNumber;
        var noConfirm = await admin.PostFormAsync($"/Patients/Merge/{dup}?target={after.PatientNumber}",
            new[] { P("TargetId", keep.ToString()), P("Reason", "Same person registered twice"), P("Confirmation", "no") });
        Assert.Equal(HttpStatusCode.Redirect, noConfirm.StatusCode);
        Assert.Equal(PatientStatus.Active, (await PatientAsync(dup)).Status);

        var merge = await admin.PostFormAsync($"/Patients/Merge/{dup}?target={after.PatientNumber}",
            new[] { P("TargetId", keep.ToString()), P("TargetNumber", after.PatientNumber), P("Reason", "Same person registered twice"), P("Confirmation", "MERGE") });
        Assert.Equal($"/Patients/Profile/{keep}", merge.Headers.Location?.OriginalString);
        var merged = await PatientAsync(dup);
        Assert.Equal(PatientStatus.Merged, merged.Status);
        Assert.Equal(keep, merged.MergedIntoPatientId);
        var movedVisit = await EncounterAsync(dupVisit);
        Assert.Equal(keep, movedVisit.PatientId);
        Assert.Equal(dup, movedVisit.OriginalPatientId);
        Assert.Contains((await PatientAsync(keep)).Identifiers, i => i.Type == PatientIdentifierType.MergedPatientNumber && i.Value == dupNumber);
        Assert.Contains("Noor Muhammad Khan", await admin.GetHtmlAsync($"/Patients?q={dupNumber}&status=all"));
        Assert.Single(await AuditAsync(AuditActions.PatientMerged));
    }

    [Fact]
    public async Task Many_computers_registering_at_the_same_time_never_get_duplicate_numbers()
    {
        await SetupAsync("Government");
        var computers = new List<Browser>();
        for (var i = 0; i < 4; i++) computers.Add(await LoginAdminAsync());

        var tasks = Enumerable.Range(0, 32).Select(i =>
        {
            var f = PatientFields($"Concurrent Patient {i:D2} Test{(char)('A' + i % 26)}", $"Father {i:D2} Name");
            return RegisterPatientAsync(computers[i % computers.Count], f);
        });
        var ids = await Task.WhenAll(tasks);
        await using (var db = Factory.NewDb())
        {
            var numbers = await db.Patients.Where(p => ids.Contains(p.Id)).Select(p => p.PatientNumber).ToListAsync();
            Assert.Equal(32, numbers.Distinct().Count());
        }

        var patient = ids[0];
        var visits = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => RegisterOpdAsync(computers[i % computers.Count], patient)));
        await using var db2 = Factory.NewDb();
        var encs = await db2.PatientEncounters.Where(e => visits.Contains(e.Id)).ToListAsync();
        Assert.Equal(20, encs.Select(e => e.EncounterNumber).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 20), encs.Select(e => e.TokenNumber!.Value).OrderBy(t => t));
        Assert.All(encs, e => Assert.Equal(patient, e.PatientId));
    }
}
