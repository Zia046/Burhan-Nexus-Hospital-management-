using System.Globalization;
using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 11 – admission date + time, Ward → Room → vacant bed, bed vacant at discharge, admitted-patient count and list.</summary>
public partial class IpdTests
{
    private static Task<HttpResponseMessage> AdmitAtAsync(Browser b, long patientId, int? bedId, string date, string time) =>
        b.PostFormAsync($"/Ipd/Admit?patientId={patientId}", new[]
        {
            P("PatientId", patientId.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Kind", "General"), P("Input.Source", "Direct"),
            P("Input.SourceEncounterId", ""), P("Input.BedId", bedId?.ToString() ?? ""), P("Input.AdmittingDoctorName", "Dr. Eleven"),
            P("Input.AdmissionDiagnosis", "Fever under evaluation"), P("Input.AttendantName", "Brother"), P("Input.AttendantPhone", "03001234567"),
            P("AdmitDate", date), P("AdmitTime", time),
        });

    [Fact]
    public async Task Update11_admission_time_ward_room_bed_selection_vacant_on_discharge_and_admitted_list()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var b1 = await BedAsync(admin, "MGW", "MGW-11");
        var b2 = await BedAsync(admin, "FGW", "FGW-11");
        var p1 = await RegisterPatientAsync(admin, PatientFields("Eleven Inpatient", "Father Eleven"));
        var p2 = await RegisterPatientAsync(admin, PatientFields("Eleven Second", "Father Second"));

        // Admit form: date and time side by side defaulting to the hospital time; Ward → Room → Bed; no bed-type filter.
        var form = await admin.GetHtmlAsync($"/Ipd/Admit?patientId={p1}");
        var today = LocalNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Contains($"name=\"AdmitDate\" value=\"{today}\"", form);
        Assert.Contains("name=\"AdmitTime\"", form);
        Assert.Contains("data-close-on-pick", form);
        Assert.Contains("data-bed-ward", form);
        Assert.Contains("data-bed-room", form);
        Assert.DoesNotContain("All bed types", form);
        Assert.DoesNotContain("data-bed-filter=\"type\"", form);

        // Validation: no future time, not more than 7 days back.
        string T(DateTime x) => x.ToString("HH:mm", CultureInfo.InvariantCulture);
        string D(DateTime x) => x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var future = LocalNow.AddHours(2);
        Assert.Contains("future", (await Refused(await AdmitAtAsync(admin, p1, b1, D(future), T(future)), "Future admission")).ToLowerInvariant());
        var old = LocalNow.AddDays(-10);
        Assert.Contains("7 days", await Refused(await AdmitAtAsync(admin, p1, b1, D(old), T(old)), "Old admission"));
        Assert.Contains("valid admission date", await Refused(await AdmitAtAsync(admin, p1, b1, "2026-13-45", "25:99"), "Bad date"));

        // The exact entered time is saved and audited with the user.
        var at = LocalNow.AddMinutes(-90);
        var r = await AdmitAtAsync(admin, p1, b1, D(at), T(at));
        await Ok(r, "Admission");
        var adm = Id(AdmissionRegex(), r);
        var a = await AdmissionAsync(adm);
        var expectedUtc = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, DateTimeKind.Utc).AddHours(-5);
        Assert.Equal(expectedUtc, DateTime.SpecifyKind(a.AdmittedUtc, DateTimeKind.Utc));
        Assert.Contains("time entered by admin", (await AuditAsync(AuditActions.IpdAdmitted)).Single().Description);
        Assert.Equal(BedStatus.Occupied, await BedStatusAsync(b1));

        // An occupied bed is not offered and cannot be assigned.
        Assert.DoesNotContain($"<option value=\"{b1}\" data-ward", await admin.GetHtmlAsync($"/Ipd/Admit?patientId={p2}"));
        Assert.Contains("bed", (await Refused(await AdmitAtAsync(admin, p2, b1, D(LocalNow), T(LocalNow)), "Occupied bed")).ToLowerInvariant());

        // Admitted count on the admin dashboard and the IPD dashboard opens the list (name, ID, admission no., ward, room, bed, doctor, status).
        Assert.Contains("data-kpi=\"admitted\">1<", await admin.GetHtmlAsync("/"));
        var ipd = await admin.GetHtmlAsync("/Ipd");
        Assert.Contains("href=\"/Ipd/Admissions?scope=active\"", ipd);
        var list = await admin.GetHtmlAsync("/Ipd/Admissions?scope=active");
        Assert.Contains("Eleven Inpatient", list);
        Assert.Contains(a.AdmissionNumber, list);
        Assert.Contains("MGW-11", list);
        Assert.Contains("Dr. Eleven", list);

        // Discharge releases the bed as vacant at once – shown in the available list again.
        await Ok(await ActAsync(admin, adm, "RequestDischarge", Discharge), "Request discharge");
        await Ok(await ActAsync(admin, adm, "Discharge", Discharge), "Discharge");
        Assert.Equal(BedStatus.Available, await BedStatusAsync(b1));
        await using (var db = Factory.NewDb())
            Assert.Equal(BedStatus.Available, (await db.IpdBedStatusChanges.Where(c => c.BedId == b1).OrderByDescending(c => c.Id).FirstAsync()).To);
        Assert.Contains($"<option value=\"{b1}\" data-ward", await admin.GetHtmlAsync($"/Ipd/Admit?patientId={p2}"));
        await Ok(await AdmitAtAsync(admin, p2, b1, D(LocalNow), T(LocalNow)), "Bed reused after discharge");
        Assert.Equal(BedStatus.Available, await BedStatusAsync(b2));
    }
}
