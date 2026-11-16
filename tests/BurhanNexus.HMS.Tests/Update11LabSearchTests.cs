using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 11 – lab test selection without categories (frequent tests + search, no duplicates) and the routine patient search window.</summary>
public partial class Update11LabSearchTests : Phase2TestBase
{
    [GeneratedRegex("data-frequent=\"1\"")] private static partial Regex FrequentRegex();

    private DateTime LocalNow => Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5);

    /// <summary>Moves the clock to the given local time of today (+days) and signs in again.</summary>
    private async Task<Browser> AtAsync(int days, int hour, int minute)
    {
        var target = LocalNow.Date.AddDays(days).AddHours(hour).AddMinutes(minute);
        Factory.Clock.Advance(target - LocalNow);
        return await LoginAdminAsync();
    }

    [Fact]
    public async Task Update11_lab_booking_shows_frequent_tests_and_search_without_categories_and_refuses_duplicates()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Lab Eleven Patient", "Father Lab"));
        var book = await admin.GetHtmlAsync($"/Lab/Book?patientId={pid}");
        Assert.True(FrequentRegex().Matches(book).Count >= 6, "at least six frequently used tests below the search bar");
        Assert.Contains("data-test-filter", book);
        Assert.Contains("data-frequent-title", book);
        Assert.Contains("data-test-name=\"", book);
        Assert.DoesNotContain("data-test-category", book);
        int fbs, cbc;
        await using (var db = Factory.NewDb())
        {
            fbs = await db.LabTests.Where(t => t.Code == "FBS").Select(t => t.Id).SingleAsync();
            cbc = await db.LabTests.Where(t => t.Code == "CBC-FULL").Select(t => t.Id).SingleAsync();
            var total = await db.LabTests.CountAsync(t => t.IsActive);
            // Every active test is in the page once (reachable through the search), never twice.
            Assert.Equal(total, Regex.Matches(book, "class=\"test-choice").Count);
        }
        var dup = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("PatientId", pid.ToString()), P("TestIds", fbs.ToString()), P("TestIds", fbs.ToString()),
            P("TestIds", cbc.ToString()), P("Input.ReferralType", "None"),
        });
        Assert.NotEqual(HttpStatusCode.Redirect, dup.StatusCode);
        Assert.Contains("twice", Errors(await dup.Content.ReadAsStringAsync()));
        await using (var db = Factory.NewDb()) Assert.Equal(0, await db.LabOrders.CountAsync());
    }

    [Fact]
    public async Task Update11_routine_boxes_list_only_todays_patients_with_the_night_window_and_admitted_patients()
    {
        await SetupAsync("Private");
        var admin = await AtAsync(0, 22, 30);
        var night = await RegisterPatientAsync(admin, PatientFields("Nightowl Eleven", "Father Night"));
        var kept = await RegisterPatientAsync(admin, PatientFields("Wardkept Eleven", "Father Ward"));
        var adm = await admin.PostFormAsync($"/Ipd/Admit?patientId={kept}", new[]
        {
            P("PatientId", kept.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"),
            P("Input.SourceEncounterId", ""), P("Input.BedId", ""), P("Input.AdmittingDoctorName", "Dr. Night"), P("Input.AdmissionDiagnosis", "Observation"),
        });
        Assert.True(adm.StatusCode == HttpStatusCode.Redirect, "Admit: " + Errors(await adm.Content.ReadAsStringAsync()));
        Assert.Contains("Nightowl Eleven", await admin.GetHtmlAsync("/Lab/Book?q=Eleven"));

        // 01:30 next day: the 22:30 patient is still suggested (one continuous night window across midnight).
        admin = await AtAsync(1, 1, 30);
        Assert.Contains("Nightowl Eleven", await admin.GetHtmlAsync("/Lab/Book?q=Eleven"));
        Assert.Contains("Nightowl Eleven", await admin.GetHtmlAsync("/Radiology/Book?q=Eleven"));

        // 02:30: the window is over – hidden from the routine boxes, still found on the Patients page; the admitted patient stays.
        admin = await AtAsync(0, 2, 30);
        var lab = await admin.GetHtmlAsync("/Lab/Book?q=Eleven");
        Assert.DoesNotContain("Nightowl Eleven", lab);
        Assert.Contains("Wardkept Eleven", lab);
        Assert.DoesNotContain("Nightowl Eleven", await admin.GetHtmlAsync("/Pharmacy/Issue?q=Eleven"));
        Assert.Contains("Nightowl Eleven", await admin.GetHtmlAsync("/Patients?q=Nightowl"));

        // A patient registered in the afternoon is not carried into the next morning.
        admin = await AtAsync(0, 15, 0);
        await RegisterPatientAsync(admin, PatientFields("Afternoon Eleven", "Father Noon"));
        Assert.Contains("Afternoon Eleven", await admin.GetHtmlAsync("/Lab/Book?q=Eleven"));
        admin = await AtAsync(1, 0, 30);
        Assert.DoesNotContain("Afternoon Eleven", await admin.GetHtmlAsync("/Lab/Book?q=Eleven"));
        await using var db = Factory.NewDb();
        Assert.Equal(3, await db.Patients.CountAsync(p => p.FullName.EndsWith("Eleven"))); // visibility only – nothing changed
        Assert.True(night > 0);
    }
}
