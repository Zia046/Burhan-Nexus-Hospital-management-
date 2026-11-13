using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

public abstract partial class Phase2TestBase : HmsTestBase
{
    [GeneratedRegex(@"/Patients/Profile/(\d+)")] private static partial Regex ProfileRegex();
    [GeneratedRegex(@"/(?:Print/Opd|Opd/Visit|Print/Emergency|Emergency/Visit)/(\d+)")] private static partial Regex EncounterRegex();

    protected static Dictionary<string, string> PatientFields(string name, string guardian = "Akbar Khan", string gender = "Male") => new()
    {
        ["Patient.FullName"] = name, ["Patient.GuardianRelation"] = "SonOf", ["Patient.GuardianName"] = guardian,
        ["Patient.Gender"] = gender, ["Patient.Age"] = "35", ["Patient.AgeUnit"] = "Years",
        ["Patient.Address"] = "Mohallah Sher Khan", ["Patient.CityOrVillage"] = "Charsadda", ["Next"] = "profile"
    };

    protected async Task<HttpResponseMessage> PostRegisterAsync(Browser b, Dictionary<string, string> fields) =>
        await b.PostFormAsync("/Patients/Register", fields.Select(kv => P(kv.Key, kv.Value)));

    protected async Task<long> RegisterPatientAsync(Browser b, Dictionary<string, string> fields, bool confirmDuplicate = true)
    {
        var f = new Dictionary<string, string>(fields);
        if (confirmDuplicate) f["ConfirmNotDuplicate"] = "true";
        var r = await PostRegisterAsync(b, f);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Registration failed: " + Errors(await r.Content.ReadAsStringAsync()));
        return long.Parse(ProfileRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);
    }

    protected async Task<long> RegisterOpdAsync(Browser b, long patientId, params (string Key, string Value)[] extra)
    {
        // Token taken from another page: in private hospitals GET /Opd/Register forwards to appointment booking.
        var url = $"/Opd/Register?patientId={patientId}";
        var r = await b.PostFormAsync("/Patients/Register", extra.Select(e => P(e.Key, e.Value)), url);
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "OPD registration failed: " + Errors(await r.Content.ReadAsStringAsync()));
        return long.Parse(EncounterRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);
    }

    protected async Task<HttpResponseMessage> PostEmergencyAsync(Browser b, long? patientId, params (string Key, string Value)[] fields)
    {
        var url = "/Emergency/Register" + (patientId is null ? "" : $"?patientId={patientId}");
        return await b.PostFormAsync(url, fields.Select(e => P(e.Key, e.Value)));
    }

    protected static long EncounterId(HttpResponseMessage r) =>
        long.Parse(EncounterRegex().Match(r.Headers.Location!.OriginalString).Groups[1].Value);

    protected async Task<Patient> PatientAsync(long id)
    {
        await using var db = Factory.NewDb();
        return await db.Patients.Include(p => p.Identifiers).SingleAsync(p => p.Id == id);
    }

    protected async Task<PatientEncounter> EncounterAsync(long id)
    {
        await using var db = Factory.NewDb();
        return await db.PatientEncounters.Include(e => e.Opd).Include(e => e.Emergency).ThenInclude(v => v!.StatusHistory)
            .Include(e => e.PrintedDocuments).Include(e => e.Doctor).SingleAsync(e => e.Id == id);
    }

    /// <summary>A second computer logged in as a new user with exactly the given permissions.</summary>
    protected async Task<Browser> UserWithAsync(Browser admin, string username, params string[] permissions)
    {
        var id = await CreateUserAsync(admin, username, "Usr#Pass2026");
        await SetPermissionsAsync(admin, id, permissions.Select(p => (p, "grant")).ToArray());
        var b = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(b, username, "Usr#Pass2026")).StatusCode);
        return b;
    }
}
