using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using Microsoft.EntityFrameworkCore;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Phase 7 – IPD, wards &amp; beds, Gynae / ANC, Labour Room, delivery (twins), newborns, NICU, nursing, MAR,
/// IPD billing (Private / Government), printed forms, ward-level access, audit and database protection.</summary>
public partial class IpdTests : Phase2TestBase
{
    [GeneratedRegex(@"/Ipd/Admission/(\d+)")] private static partial Regex AdmissionRegex();
    [GeneratedRegex(@"/Ipd/Pregnancy/(\d+)")] private static partial Regex PregnancyRegex();
    [GeneratedRegex(@"/Ipd/Delivery/(\d+)")] private static partial Regex DeliveryRegex();

    private DateTime LocalNow => Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5);
    private string LocalText(double minutesAgo) => LocalNow.AddMinutes(-minutesAgo).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

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

    private static long Id(Regex rx, HttpResponseMessage r)
    {
        var m = rx.Match(r.Headers.Location?.OriginalString ?? "");
        Assert.True(m.Success, "Unexpected redirect " + r.Headers.Location);
        return long.Parse(m.Groups[1].Value);
    }

    private async Task<int> BedAsync(Browser admin, string wardCode, string code, string type = "Standard")
    {
        int room;
        await using (var db = Factory.NewDb()) room = await db.IpdRooms.Where(r => r.Ward!.Code == wardCode).Select(r => r.Id).FirstAsync();
        await Ok(await admin.PostFormAsync("/Ipd/Wards", new[] { P("Bed.RoomId", room.ToString()), P("Bed.Code", code), P("Bed.Type", type), P("Bed.IsActive", "true") }, "/Ipd/Wards?handler=Bed"), "Bed " + code);
        await using var db2 = Factory.NewDb();
        return await db2.IpdBeds.Where(b => b.Code == code).Select(b => b.Id).SingleAsync();
    }

    private async Task<int> ServiceAsync(Browser admin, string code, string name, decimal fee, string kind)
    {
        var r = await admin.PostFormAsync("/Finance/Services", new[]
        {
            P("Form.Code", code), P("Form.Name", name), P("Form.Kind", kind), P("Form.Fee", fee.ToString(CultureInfo.InvariantCulture)),
            P("Form.IsFree", fee == 0 ? "true" : "false"), P("Form.AllowPartialPayment", "true"), P("Form.IsActive", "true"),
        });
        await Ok(r, "Service " + code);
        await using var db = Factory.NewDb();
        return await db.ServiceItems.Where(s => s.Code == code).Select(s => s.Id).SingleAsync();
    }

    private async Task WardChargeAsync(Browser admin, string wardCode, int serviceItemId)
    {
        IpdWard w;
        await using (var db = Factory.NewDb()) w = await db.IpdWards.AsNoTracking().SingleAsync(x => x.Code == wardCode);
        await Ok(await admin.PostFormAsync($"/Ipd/Wards?ward={w.Id}", new[]
        {
            P("Ward.Code", w.Code), P("Ward.Name", w.Name), P("Ward.Type", w.Type.ToString()), P("Ward.DepartmentId", w.DepartmentId?.ToString() ?? ""),
            P("Ward.Capacity", "10"), P("Ward.BedChargeServiceItemId", serviceItemId.ToString()), P("Ward.IsActive", "true"),
        }, $"/Ipd/Wards?ward={w.Id}&handler=Ward"), "Ward bed charge");
    }

    private static Task<HttpResponseMessage> AdmitAsync(Browser b, long patientId, int? bedId, Guid key, string source = "Direct", long? encounterId = null, string kind = "General") =>
        b.PostFormAsync($"/Ipd/Admit?patientId={patientId}", new[]
        {
            P("PatientId", patientId.ToString()), P("Input.IdempotencyKey", key.ToString()), P("Input.Kind", kind), P("Input.Source", source),
            P("Input.SourceEncounterId", encounterId?.ToString() ?? ""), P("Input.BedId", bedId?.ToString() ?? ""), P("Input.AdmittingDoctorName", "Dr. Handwritten Order"),
            P("Input.AdmissionDiagnosis", "Fever under evaluation"), P("Input.Allergies", "Penicillin"), P("Input.AttendantName", "Brother"), P("Input.AttendantPhone", "03001234567"),
        });

    private static Task<HttpResponseMessage> ActAsync(Browser b, long admissionId, string handler, params (string, string)[] fields) =>
        b.PostFormAsync($"/Ipd/Admission/{admissionId}", fields.Select(f => P(f.Item1, f.Item2)), $"/Ipd/Admission/{admissionId}?handler={handler}");

    private static Task<HttpResponseMessage> RecordAsync(Browser b, long admissionId, RecordKind kind, params (string Key, string Value)[] values)
    {
        var f = new List<(string, string)> { ("Record.Kind", kind.ToString()) };
        foreach (var (k, v) in values) f.Add(k.StartsWith("Record.") ? (k, v) : ($"Record.Values[{k}]", v));
        return ActAsync(b, admissionId, "Record", f.ToArray());
    }

    private async Task<IpdAdmission> AdmissionAsync(long id)
    {
        await using var db = Factory.NewDb();
        return await db.IpdAdmissions.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    private async Task<BedStatus> BedStatusAsync(int id)
    {
        await using var db = Factory.NewDb();
        return await db.IpdBeds.Where(b => b.Id == id).Select(b => b.Status).SingleAsync();
    }

    private async Task<Browser> StaffAsync(Browser admin, string username, string[] departments, params string[] perms)
    {
        var deptIds = new List<int>();
        foreach (var d in departments) deptIds.Add(await DepartmentIdAsync(d));
        var id = await CreateUserAsync(admin, username, "Usr#Pass2026", false, deptIds.ToArray());
        await SetPermissionsAsync(admin, id, perms.Select(p => (p, "grant")).ToArray());
        var b = Factory.NewBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginAsync(b, username, "Usr#Pass2026")).StatusCode);
        return b;
    }

    private static readonly (string, string)[] Discharge =
    {
        ("Discharge.Type", "Routine"), ("Discharge.FinalDiagnosis", "Enteric fever (as documented by the doctor)"), ("Discharge.ConditionAtDischarge", "Stable"),
        ("Discharge.TreatmentSummary", "Treated as per the consultant's orders."), ("Discharge.FollowUp", "OPD after one week"), ("Discharge.DischargeDoctor", "Dr. Handwritten Order"),
    };

    // =====================================================================================

    [Fact]
    public async Task Admission_bed_allocation_transfer_records_billing_discharge_and_forms_private()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        await using (var db = Factory.NewDb())
        {
            // Seeded configuration – data, not code: wards with rooms (beds added by the hospital) and default forms without any alert limits.
            Assert.True(await db.IpdWards.CountAsync() >= 7);
            Assert.True(await db.ClinicalFormFields.CountAsync() > 50);
            Assert.False(await db.ClinicalFormFields.AnyAsync(f => f.AlertLow != null || f.AlertHigh != null));
            Assert.Equal(0, await db.IpdBeds.CountAsync());
        }
        var bedFee = await ServiceAsync(admin, "IPD-BED-GEN", "General ward bed (per night)", 1000, "Inpatient");
        var admFee = await ServiceAsync(admin, "IPD-ADM", "Admission charges", 500, "Inpatient");
        var proc = await ServiceAsync(admin, "IPD-DRESS", "Dressing", 300, "Procedure");
        await WardChargeAsync(admin, "MGW", bedFee);
        uint rv;
        await using (var db = Factory.NewDb()) rv = (await db.IpdSettings.SingleAsync()).RowVersion;
        await Ok(await admin.PostFormAsync("/Ipd/Settings", new[]
        {
            P("RowVersion", rv.ToString()), P("Input.AdmissionServiceItemId", admFee.ToString()), P("Input.AllowDirectAdmission", "true"), P("Input.PrintHeader", "true"),
            P("Input.LetterheadTopMarginMm", "45"), P("Input.RequireMarConfirmations", "true"), P("Input.RequireBillClearanceForDischarge", "false"),
        }), "IPD settings");
        var b1 = await BedAsync(admin, "MGW", "MGW-01");
        var b2 = await BedAsync(admin, "MGW", "MGW-02");
        Assert.Contains("MGW-01", await admin.GetHtmlAsync("/Ipd/Beds"));

        // Admission from an OPD visit with a bed; a double click does not create a second admission.
        var pid = await RegisterPatientAsync(admin, PatientFields("Inpatient One", "Father One"));
        var visit = await RegisterOpdAsync(admin, pid);
        var key = Guid.NewGuid();
        var first = await AdmitAsync(admin, pid, b1, key, "Opd", visit);
        await Ok(first, "Admission");
        var adm = Id(AdmissionRegex(), first);
        Assert.Equal(adm, Id(AdmissionRegex(), await AdmitAsync(admin, pid, b1, key, "Opd", visit)));
        var a = await AdmissionAsync(adm);
        Assert.Matches(@"^IPD-\d{4}-\d{6}$", a.AdmissionNumber);
        Assert.Equal((AdmissionStatus.Admitted, b1, visit), (a.Status, a.CurrentBedId!.Value, a.SourceEncounterId!.Value));
        Assert.Equal(BedStatus.Occupied, await BedStatusAsync(b1));
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.IpdAdmissions.CountAsync());
            var c = await db.Charges.SingleAsync(x => x.IpdAdmissionId == adm);
            Assert.Equal((500m, ServiceKind.Inpatient), (c.NetAmount, c.ServiceKind));
        }

        // A second admission of the same patient (new form) is refused, and an occupied bed cannot be given to another patient.
        Assert.Contains("already admitted", await Refused(await AdmitAsync(admin, pid, b2, Guid.NewGuid()), "Second admission"));
        var pid2 = await RegisterPatientAsync(admin, PatientFields("Inpatient Two", "Father Two"));
        await Refused(await AdmitAsync(admin, pid2, b1, Guid.NewGuid()), "Occupied bed");
        await using (var db = Factory.NewDb()) Assert.Equal(1, await db.IpdAdmissions.CountAsync());

        // The chart opens with the allergy visible; documentation is stored, empty fields stay "not recorded".
        var chart = await admin.GetHtmlAsync($"/Ipd/Admission/{adm}");
        Assert.Contains("Penicillin", chart);
        Assert.Contains("data-allergy", chart);
        await Ok(await RecordAsync(admin, adm, RecordKind.Vitals, ("temp", "38.4"), ("pulse", "104")), "Vitals");
        await Ok(await RecordAsync(admin, adm, RecordKind.ProgressNote, ("condition", "Febrile, alert"), ("plan", "As per consultant")), "Progress note");
        Assert.Contains("enter a number", await Refused(await RecordAsync(admin, adm, RecordKind.Vitals, ("temp", "high")), "Text in a number field"));
        long vitalsId;
        await using (var db = Factory.NewDb())
        {
            var v = await db.ClinicalRecords.SingleAsync(r => r.AdmissionId == adm && r.Kind == RecordKind.Vitals);
            vitalsId = v.Id;
            Assert.Contains("38.4", v.Values);
            Assert.DoesNotContain("spo2", v.Values); // not entered = not stored (never assumed normal)
        }
        Assert.Equal(AdmissionStatus.UnderTreatment, (await AdmissionAsync(adm)).Status);
        // Correction keeps the original and records the reason.
        await Refused(await RecordAsync(admin, adm, RecordKind.Vitals, ("Record.CorrectsRecordId", vitalsId.ToString()), ("temp", "37.4")), "Correction without a reason");
        await Ok(await RecordAsync(admin, adm, RecordKind.Vitals, ("Record.CorrectsRecordId", vitalsId.ToString()), ("Record.CorrectionReason", "Wrong patient thermometer"), ("temp", "37.4")), "Correction");
        await using (var db = Factory.NewDb())
        {
            var all = await db.ClinicalRecords.Where(r => r.AdmissionId == adm && r.Kind == RecordKind.Vitals).OrderBy(r => r.Id).ToListAsync();
            Assert.Equal(2, all.Count);
            Assert.Contains("38.4", all[0].Values);
            Assert.Equal(vitalsId, all[1].CorrectsRecordId);
        }
        Assert.Single(await AuditAsync(AuditActions.IpdRecordCorrected));

        // Transfer to another bed: history kept, old bed needs cleaning, new bed occupied.
        Assert.Contains("reason", (await Refused(await ActAsync(admin, adm, "Transfer", ("BedId", b2.ToString())), "Transfer without reason")).ToLowerInvariant());
        await Ok(await ActAsync(admin, adm, "Transfer", ("BedId", b2.ToString()), ("Reason", "Closer to the nursing station"), ("Receiving", "Nurse Ayesha")), "Transfer");
        Assert.Equal((BedStatus.CleaningRequired, BedStatus.Occupied), (await BedStatusAsync(b1), await BedStatusAsync(b2)));
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.IpdTransfers.CountAsync(t => t.AdmissionId == adm));
            Assert.Equal(2, await db.IpdBedAllocations.CountAsync(x => x.AdmissionId == adm));
        }
        Assert.Contains("Closer to the nursing station", await admin.GetHtmlAsync($"/Ipd/Admission/{adm}?tab=transfer"));

        // Billing through Phase 3 charges; investigation / medicine services are not added by hand.
        await Ok(await ActAsync(admin, adm, "Charge", ("ServiceItemId", proc.ToString()), ("Quantity", "2"), ("Discount", "0")), "Dressing charge");
        await using (var db = Factory.NewDb())
        {
            var dressing = await db.Charges.SingleAsync(c => c.IpdAdmissionId == adm && c.ServiceItemId == proc);
            Assert.Equal(600m, dressing.NetAmount);
        }
        var bill = await admin.GetHtmlAsync($"/Ipd/Admission/{adm}?tab=billing");
        Assert.Contains("data-ipd-bill", bill);
        Assert.Contains("ipdAdmissionId=" + adm, bill);

        // Discharge: required details first, then the bed is released and at least one night is billed.
        Assert.Contains("discharge type", (await Refused(await ActAsync(admin, adm, "Discharge"), "Discharge without details")).ToLowerInvariant());
        await Ok(await ActAsync(admin, adm, "RequestDischarge", Discharge), "Request discharge");
        Assert.Equal(AdmissionStatus.DischargeRequested, (await AdmissionAsync(adm)).Status);
        await Ok(await ActAsync(admin, adm, "Discharge", Discharge), "Discharge");
        a = await AdmissionAsync(adm);
        Assert.Equal(AdmissionStatus.Discharged, a.Status);
        Assert.Null(a.CurrentBedId);
        Assert.Equal(BedStatus.CleaningRequired, await BedStatusAsync(b2));
        int charges;
        await using (var db = Factory.NewDb())
        {
            Assert.True(await db.IpdBedCharges.CountAsync(c => c.AdmissionId == adm) >= 1);
            Assert.True(await db.Charges.AnyAsync(c => c.IpdAdmissionId == adm && c.ServiceItemId == bedFee && c.NetAmount == 1000m));
            charges = await db.Charges.CountAsync();
        }
        // A closed admission accepts no new entries.
        await Refused(await RecordAsync(admin, adm, RecordKind.ProgressNote, ("condition", "late note")), "Note after discharge");

        // Printed forms: discharge summary and final bill; reprinting creates nothing.
        var summary = await admin.GetHtmlAsync($"/Print/Ipd/discharge/{adm}");
        Assert.Contains("Enteric fever (as documented by the doctor)", summary);
        Assert.Contains("data-form-header", summary);
        Assert.Contains("Penicillin", summary);
        var finalBill = await admin.GetHtmlAsync($"/Print/Ipd/bill/{adm}");
        Assert.Contains("Dressing", finalBill);
        Assert.Contains("data-balance", finalBill);
        foreach (var form in new[] { "admission", "progress", "vitals", "nursing", "mar", "daily-bill", "investigations" })
            Assert.Contains("data-form-page", await admin.GetHtmlAsync($"/Print/Ipd/{form}/{adm}"));
        Assert.Contains("data-chart=\"temp\"", await admin.GetHtmlAsync($"/Print/Ipd/vitals/{adm}"));
        await admin.GetHtmlAsync($"/Print/Ipd/discharge/{adm}");
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(charges, await db.Charges.CountAsync());
            Assert.Equal(1, await db.IpdAdmissions.CountAsync());
            Assert.Equal(3, await db.ClinicalRecords.CountAsync(r => r.AdmissionId == adm));
        }
        Assert.True((await AuditAsync(AuditActions.IpdFormPrinted)).Count >= 10);
        Assert.Single(await AuditAsync(AuditActions.IpdDischarged));
        Assert.Single(await AuditAsync(AuditActions.IpdTransferred));

        // Letterhead mode: no header, blank space instead.
        await using (var db = Factory.NewDb()) rv = (await db.IpdSettings.SingleAsync()).RowVersion;
        await Ok(await admin.PostFormAsync("/Ipd/Settings", new[]
        {
            P("RowVersion", rv.ToString()), P("Input.AdmissionServiceItemId", admFee.ToString()), P("Input.AllowDirectAdmission", "true"), P("Input.PrintHeader", "false"),
            P("Input.LetterheadTopMarginMm", "50"), P("Input.RequireMarConfirmations", "true"),
        }), "Letterhead");
        var lh = await admin.GetHtmlAsync($"/Print/Ipd/discharge/{adm}");
        Assert.Contains("data-letterhead-space", lh);
        Assert.DoesNotContain("data-form-header", lh);

        // Dashboard and patient profile show the IPD information.
        Assert.Contains("Inpatient One", await admin.GetHtmlAsync("/Ipd/Admissions?scope=discharged"));
        Assert.Contains("data-profile-ipd", await admin.GetHtmlAsync($"/Patients/Profile/{pid}"));
        Assert.Contains("data-ipd-nav", await admin.GetHtmlAsync("/Ipd"));
    }

    [Fact]
    public async Task Pregnancy_anc_labour_twin_delivery_newborns_nicu_and_mother_baby_rules()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var lr = await BedAsync(admin, "LR", "LR-01", "Labour");
        var cot = await BedAsync(admin, "NICU", "NICU-01", "Incubator");
        var cot2 = await BedAsync(admin, "NICU", "NICU-02", "Cot");
        var pnw = await BedAsync(admin, "PNW", "PNW-01");

        var mother = await RegisterPatientAsync(admin, PatientFields("Mother Fatima", "Husband Ali", "Female"));
        var male = await RegisterPatientAsync(admin, PatientFields("Male Patient", "Father X"));
        // Pregnancy: female only, one ongoing record, EDD from LMP when not entered.
        var lmp = DateOnly.FromDateTime(LocalNow).AddDays(-270);
        Assert.Contains("female", (await Refused(await admin.PostFormAsync($"/Ipd/Gynae/Patient/{male}", new[] { P("Preg.Lmp", lmp.ToString("yyyy-MM-dd")) }, $"/Ipd/Gynae/Patient/{male}?handler=Pregnancy"), "Male pregnancy")).ToLowerInvariant());
        var reg = await admin.PostFormAsync($"/Ipd/Gynae/Patient/{mother}", new[] { P("Preg.Lmp", lmp.ToString("yyyy-MM-dd")), P("Preg.Gravida", "2"), P("Preg.Para", "1") }, $"/Ipd/Gynae/Patient/{mother}?handler=Pregnancy");
        await Ok(reg, "Pregnancy");
        var preg = Id(PregnancyRegex(), reg);
        await using (var db = Factory.NewDb()) Assert.Equal(lmp.AddDays(280), (await db.Pregnancies.SingleAsync(p => p.Id == preg)).Edd);
        await Refused(await admin.PostFormAsync($"/Ipd/Gynae/Patient/{mother}", new[] { P("Preg.Lmp", lmp.ToString("yyyy-MM-dd")) }, $"/Ipd/Gynae/Patient/{mother}?handler=Pregnancy"), "Second ongoing pregnancy");

        // ANC visits are separate entries (never overwritten); high-risk needs a reason.
        foreach (var bp in new[] { "110", "118" })
            await Ok(await admin.PostFormAsync($"/Ipd/Pregnancy/{preg}", new[] { P("Record.Values[bp_sys]", bp), P("Record.Values[fhr]", "142"), P("Record.Notes", "Routine ANC") }, $"/Ipd/Pregnancy/{preg}?handler=Record"), "ANC " + bp);
        await Refused(await admin.PostFormAsync($"/Ipd/Pregnancy/{preg}", new[] { P("Risk.IsHighRisk", "true") }, $"/Ipd/Pregnancy/{preg}?handler=Risk"), "High risk without reason");
        await Ok(await admin.PostFormAsync($"/Ipd/Pregnancy/{preg}", new[] { P("Risk.IsHighRisk", "true"), P("Risk.Reason", "Previous caesarean (doctor's assessment)") }, $"/Ipd/Pregnancy/{preg}?handler=Risk"), "High risk");
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(2, await db.ClinicalRecords.CountAsync(r => r.PregnancyId == preg && r.Kind == RecordKind.AncVisit));
            Assert.True((await db.Pregnancies.SingleAsync(p => p.Id == preg)).IsHighRisk);
        }
        var timeline = await admin.GetHtmlAsync($"/Ipd/Pregnancy/{preg}");
        Assert.Contains("data-timeline", timeline);
        Assert.Contains("High-risk pregnancy", timeline);
        Assert.Contains("data-pregnancy", await admin.GetHtmlAsync("/Ipd/Gynae?risk=true"));
        Assert.Contains("ANC", await admin.GetHtmlAsync($"/Print/Ipd/anc/{preg}"));

        // Labour Room admission (needs a Labour Room bed), observations and status.
        var labourPage = $"/Ipd/Labour/Admit?patientId={mother}";
        var la = await admin.PostFormAsync(labourPage, new[]
        {
            P("PatientId", mother.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Source", "Maternity"), P("Input.BedId", lr.ToString()),
            P("Labour.PregnancyId", preg.ToString()), P("Labour.MembraneStatus", "Intact"), P("Input.AdmittingDoctorName", "Dr. Gynae"),
        });
        await Ok(la, "Labour admission");
        var madm = Id(AdmissionRegex(), la);
        Assert.Equal(AdmissionKind.Labour, (await AdmissionAsync(madm)).Kind);
        Assert.Contains("Move to Labour Room", await Refused(await admin.PostFormAsync(labourPage, new[]
        {
            P("PatientId", mother.ToString()), P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.Source", "Maternity"), P("Input.BedId", lr.ToString()),
        }), "Second labour admission"));
        await Ok(await RecordAsync(admin, madm, RecordKind.LabourObservation, ("cervix", "4"), ("fhr", "140"), ("contractions", "3")), "Partograph 1");
        await Ok(await RecordAsync(admin, madm, RecordKind.LabourObservation, ("cervix", "7"), ("fhr", "138"), ("Record.RecordedLocal", LocalText(0))), "Partograph 2");
        await Ok(await ActAsync(admin, madm, "LabourStatus", ("LabourStatusValue", "ActiveLabour")), "Active labour");
        Assert.Contains("Delivery completed", await Refused(await ActAsync(admin, madm, "LabourStatus", ("LabourStatusValue", "DeliveryCompleted")), "Manual delivery completed"));
        var partograph = await admin.GetHtmlAsync($"/Print/Ipd/partograph/{madm}");
        Assert.Contains("data-chart=\"cervix\"", partograph);
        Assert.DoesNotContain("c-limit", partograph); // no built-in alert / action lines
        Assert.Contains("data-labour-row", await admin.GetHtmlAsync("/Ipd/Labour"));

        // Twins: baby 1 with the mother, baby 2 to the NICU – each gets a separate Patient ID linked to the mother. Double submit → one delivery.
        var key = Guid.NewGuid();
        (string, string)[] delivery =
        {
            ("Input.IdempotencyKey", key.ToString()), ("Input.DeliveredLocal", LocalText(1)), ("Input.Type", "Nvd"), ("Input.DeliveringDoctor", "Dr. Gynae"),
            ("Input.Babies[0].Sex", "Female"), ("Input.Babies[0].Condition", "LiveBirth"), ("Input.Babies[0].BirthWeightGrams", "2600"), ("Input.Babies[0].Apgar1", "8"), ("Input.Babies[0].Apgar5", "9"),
            ("Input.Babies[0].Placement", "WithMother"),
            ("Input.Babies[1].Sex", "Male"), ("Input.Babies[1].Condition", "LiveBirth"), ("Input.Babies[1].BirthWeightGrams", "1800"), ("Input.Babies[1].Apgar1", "6"),
            ("Input.Babies[1].Placement", "Nicu"), ("Input.Babies[1].NicuBedId", cot.ToString()), ("Input.Babies[1].NicuReason", "Low birth weight (as assessed)"),
        };
        var deliveryPage = $"/Ipd/Delivery/New/{madm}?babies=2";
        Assert.Contains("NICU", await Refused(await admin.PostFormAsync(deliveryPage, delivery.Select(f => f.Item1 == "Input.Babies[1].NicuBedId" ? P(f.Item1, "") : P(f.Item1, f.Item2))), "NICU baby without a cot"));
        var dr = await admin.PostFormAsync(deliveryPage, delivery.Select(f => P(f.Item1, f.Item2)));
        await Ok(dr, "Delivery");
        var did = Id(DeliveryRegex(), dr);
        Assert.Equal(did, Id(DeliveryRegex(), await admin.PostFormAsync(deliveryPage, delivery.Select(f => P(f.Item1, f.Item2)))));
        long baby1, baby2, b1Adm, b2Adm;
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.DeliveryRecords.CountAsync());
            var babies = await db.NewbornRecords.AsNoTracking().Where(n => n.DeliveryId == did).OrderBy(n => n.BirthOrder).ToListAsync();
            Assert.Equal(2, babies.Count);
            Assert.All(babies, n => Assert.Equal(mother, n.MotherPatientId));
            baby1 = babies[0].NewbornPatientId!.Value; baby2 = babies[1].NewbornPatientId!.Value;
            Assert.NotEqual(baby1, baby2);
            Assert.DoesNotContain(mother, new[] { baby1, baby2 });
            var p1 = await db.Patients.SingleAsync(p => p.Id == baby1);
            Assert.Contains("Mother Fatima", p1.FullName);
            Assert.Equal(Gender.Female, p1.Gender);
            Assert.Equal(Gender.Male, (await db.Patients.SingleAsync(p => p.Id == baby2)).Gender);
            var a1 = await db.IpdAdmissions.SingleAsync(x => x.PatientId == baby1);
            var a2 = await db.IpdAdmissions.SingleAsync(x => x.PatientId == baby2);
            (b1Adm, b2Adm) = (a1.Id, a2.Id);
            Assert.Equal((AdmissionKind.Newborn, madm, lr), (a1.Kind, a1.MotherAdmissionId!.Value, a1.CurrentBedId!.Value)); // rooming-in on the mother's bed
            Assert.Equal((AdmissionKind.Nicu, madm, cot), (a2.Kind, a2.MotherAdmissionId!.Value, a2.CurrentBedId!.Value));
            Assert.True(await db.IpdBedAllocations.AnyAsync(x => x.AdmissionId == a1.Id && x.IsShared && x.ToUtc == null));
            Assert.Equal(LabourStatus.DeliveryCompleted, (await db.LabourRecords.SingleAsync(l => l.AdmissionId == madm)).Status);
            Assert.Equal(PregnancyStatus.Delivered, (await db.Pregnancies.SingleAsync(p => p.Id == preg)).Status);
            Assert.NotNull(await db.NicuDetails.SingleAsync(n => n.AdmissionId == a2.Id));
        }
        Assert.Equal(BedStatus.Occupied, await BedStatusAsync(cot));
        var deliveryPrint = await admin.GetHtmlAsync($"/Print/Ipd/delivery/{did}");
        Assert.Contains("data-newborn-table", deliveryPrint);
        await using (var db = Factory.NewDb())
            foreach (var bid in new[] { baby1, baby2 }) Assert.Contains((await db.Patients.SingleAsync(p => p.Id == bid)).PatientNumber, deliveryPrint);
        Assert.Contains("data-newborns", await admin.GetHtmlAsync($"/Ipd/Delivery/{did}"));
        Assert.Contains("data-babies", await admin.GetHtmlAsync($"/Ipd/Admission/{madm}"));

        // NICU: observations on the baby's own record, chart, then back to the mother (cot released for cleaning).
        await Ok(await RecordAsync(admin, b2Adm, RecordKind.NicuObservation, ("temp", "36.6"), ("hr", "150"), ("spo2", "95")), "NICU observation");
        await Ok(await RecordAsync(admin, b2Adm, RecordKind.NicuFeeding, ("Record.Notes", "Expressed breast milk 10 ml")), "Feeding note");
        Assert.Contains("data-nicu-list", await admin.GetHtmlAsync("/Ipd/Nicu"));
        Assert.Contains("data-chart=\"hr\"", await admin.GetHtmlAsync($"/Print/Ipd/nicu-chart/{b2Adm}"));
        await Ok(await ActAsync(admin, b2Adm, "ReturnToMother", ("Reason", "Stable, weight gain")), "Return to mother");
        Assert.Equal(BedStatus.CleaningRequired, await BedStatusAsync(cot));
        Assert.Equal(lr, (await AdmissionAsync(b2Adm)).CurrentBedId);

        // Mother cannot be discharged while a baby is rooming-in; babies are discharged first.
        Assert.Contains("newborn", (await Refused(await ActAsync(admin, madm, "Discharge", Discharge), "Mother before babies")).ToLowerInvariant());
        await Ok(await ActAsync(admin, b1Adm, "Discharge", Discharge), "Baby 1 discharge");
        await Ok(await ActAsync(admin, b2Adm, "Discharge", Discharge), "Baby 2 discharge");
        Assert.Equal(BedStatus.Occupied, await BedStatusAsync(lr)); // still the mother's bed
        await Ok(await ActAsync(admin, madm, "Discharge", Discharge), "Mother discharge");
        Assert.Equal(BedStatus.CleaningRequired, await BedStatusAsync(lr));
        await using (var db = Factory.NewDb()) Assert.Equal(LabourStatus.Discharged, (await db.LabourRecords.SingleAsync(l => l.AdmissionId == madm)).Status);
        Assert.Contains("data-form-page", await admin.GetHtmlAsync($"/Print/Ipd/nicu-discharge/{b2Adm}"));
        Assert.Single(await AuditAsync(AuditActions.IpdDeliveryRecorded));
        Assert.Equal(2, (await AuditAsync(AuditActions.IpdNewbornRegistered)).Count);
        _ = (cot2, pnw);
    }

    [Fact]
    public async Task Nursing_mar_handover_ward_access_permissions_concurrency_and_database_protection()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var bed = await BedAsync(admin, "MGW", "MGW-11");
        var bed2 = await BedAsync(admin, "MGW", "MGW-12");
        var gyn = await BedAsync(admin, "GYN", "GYN-01");
        int medicineId;
        await using (var db = Factory.NewDb())
        {
            var m = new Medicine { Code = "MED-T1", Name = "Paracetamol", Strength = "500 mg", Unit = "Tablet", IsActive = true, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow };
            db.Medicines.Add(m);
            await db.SaveChangesAsync();
            medicineId = m.Id;
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Ward Patient", "Ward Father"));
        var adm = Id(AdmissionRegex(), await AdmitAsync(admin, pid, bed, Guid.NewGuid()));

        // MAR: the order is entered exactly as written; doses are scheduled, nothing is given automatically.
        Assert.Contains("prescribing", (await Refused(await ActAsync(admin, adm, "Order", ("Order.MedicineId", medicineId.ToString()), ("Order.Dose", "1 tab"), ("Order.Route", "Oral"),
            ("Order.Frequency", "BD"), ("Order.Times", "08:00, 20:00"), ("Order.DurationDays", "2"), ("Order.Source", "HandwrittenPrescription")), "Order without prescriber")).ToLowerInvariant());
        await Ok(await ActAsync(admin, adm, "Order", ("Order.MedicineId", medicineId.ToString()), ("Order.Dose", "1 tab"), ("Order.Route", "Oral"), ("Order.Frequency", "BD"),
            ("Order.Times", "08:00, 20:00"), ("Order.DurationDays", "2"), ("Order.Prescriber", "Dr. Handwritten Order"), ("Order.Source", "HandwrittenPrescription"), ("Order.StockUnitsPerDose", "0")), "Order");
        List<MedicationDose> doses;
        await using (var db = Factory.NewDb()) doses = await db.MedicationDoses.AsNoTracking().Where(d => d.AdmissionId == adm).OrderBy(d => d.ScheduledUtc).ToListAsync();
        Assert.InRange(doses.Count, 3, 5);
        Assert.All(doses, d => Assert.Equal(DoseStatus.Scheduled, d.Status));
        var mar = await admin.GetHtmlAsync($"/Ipd/Admission/{adm}?tab=mar");
        Assert.Contains("Paracetamol", mar);
        Assert.Contains("Allergies: Penicillin", mar);
        var d0 = doses[0].Id; var d1 = doses[1].Id;
        Assert.Contains("Confirm", await Refused(await ActAsync(admin, adm, "Dose", ("ItemId", d0.ToString()), ("Dose.Status", "Administered"), ("Dose.DoseGiven", "1 tab"), ("Dose.RouteGiven", "Oral")), "Dose without confirmations"));
        await Ok(await ActAsync(admin, adm, "Dose", ("ItemId", d0.ToString()), ("Dose.Status", "Administered"), ("Dose.DoseGiven", "1 tab"), ("Dose.RouteGiven", "Oral"),
            ("Dose.ConfirmPatient", "true"), ("Dose.ConfirmMedicine", "true"), ("Dose.ConfirmDose", "true"), ("Dose.ConfirmRoute", "true"), ("Dose.ConfirmTime", "true")), "Dose given");
        Assert.Contains("already recorded", await Refused(await ActAsync(admin, adm, "Dose", ("ItemId", d0.ToString()), ("Dose.Status", "Missed"), ("Dose.Reason", "x")), "Dose twice"));
        Assert.Contains("reason", (await Refused(await ActAsync(admin, adm, "Dose", ("ItemId", d1.ToString()), ("Dose.Status", "Withheld")), "Withheld without reason")).ToLowerInvariant());
        await Ok(await ActAsync(admin, adm, "Dose", ("ItemId", d1.ToString()), ("Dose.Status", "Withheld"), ("Dose.Reason", "Patient vomiting – doctor informed")), "Withheld");
        long orderId;
        await using (var db = Factory.NewDb()) orderId = await db.MedicationOrders.Where(o => o.AdmissionId == adm).Select(o => o.Id).SingleAsync();
        await Ok(await ActAsync(admin, adm, "Stop", ("ItemId", orderId.ToString()), ("Reason", "Course changed by the doctor")), "Stop order");
        await using (var db = Factory.NewDb())
        {
            var after = await db.MedicationDoses.Where(d => d.AdmissionId == adm).OrderBy(d => d.ScheduledUtc).Select(d => d.Status).ToListAsync();
            Assert.Equal(DoseStatus.Administered, after[0]);
            Assert.Equal(DoseStatus.Withheld, after[1]);
            Assert.All(after.Skip(2), s => Assert.Equal(DoseStatus.Cancelled, s));
        }
        Assert.Contains("Withheld", await admin.GetHtmlAsync($"/Print/Ipd/mar/{adm}"));

        // Nursing staff only see the wards of their department; shift handover needs the incoming nurse.
        string[] nursePerms = { Perm.DashboardView, Perm.IpdViewPatients, Perm.IpdManageNursing, Perm.IpdNursingNotes, Perm.IpdHandover, Perm.IpdRecordMedication };
        var nurse1 = await StaffAsync(admin, "nurse.one", new[] { "IPD" }, nursePerms);
        var nurse2 = await StaffAsync(admin, "nurse.two", new[] { "IPD" }, nursePerms);
        var gynaeNurse = await StaffAsync(admin, "nurse.gyn", new[] { "Gynae" }, nursePerms);
        Assert.Contains("Ward Patient", await nurse1.GetHtmlAsync("/Ipd/Nursing"));
        Assert.DoesNotContain("Ward Patient", await gynaeNurse.GetHtmlAsync("/Ipd/Nursing"));
        Denied(await gynaeNurse.GetAsync($"/Ipd/Admission/{adm}"), "Other ward's patient");
        await Ok(await RecordAsync(nurse1, adm, RecordKind.NursingNote, ("condition", "Comfortable"), ("interventions", "Tepid sponging")), "Nursing note");
        await Ok(await ActAsync(nurse1, adm, "Handover", ("Handover.Shift", "Morning"), ("Handover.PatientCondition", "Stable, febrile at 10:00"), ("Handover.MedicationDue", "20:00 dose")), "Handover");
        long ho;
        await using (var db = Factory.NewDb()) ho = await db.ShiftHandovers.Where(h => h.AdmissionId == adm).Select(h => h.Id).SingleAsync();
        Assert.Contains("incoming nurse", await Refused(await ActAsync(nurse1, adm, "Acknowledge", ("ItemId", ho.ToString())), "Self acknowledgement"));
        await Ok(await ActAsync(nurse2, adm, "Acknowledge", ("ItemId", ho.ToString()), ("Note", "Received")), "Acknowledge");
        await using (var db = Factory.NewDb()) Assert.Equal("nurse.two", (await db.ShiftHandovers.SingleAsync(h => h.Id == ho)).AcknowledgedByUsername);
        Assert.Contains("Acknowledged by nurse.two", await admin.GetHtmlAsync($"/Print/Ipd/handover/{ho}"));

        // Permissions: nurses cannot discharge, enter orders, see finance or Gynae records, or configure wards.
        Denied(await ActAsync(nurse1, adm, "Discharge", Discharge), "Nurse discharge");
        Denied(await ActAsync(nurse1, adm, "Order", ("Order.MedicineId", medicineId.ToString())), "Nurse medication order");
        Denied(await nurse1.GetAsync("/Ipd/Gynae"), "Gynae without permission");
        Denied(await nurse1.GetAsync("/Ipd/Wards"), "Ward setup");
        Denied(await nurse1.GetAsync($"/Print/Ipd/bill/{adm}"), "Bill print without finance permission");
        Assert.DoesNotContain("tab=billing", await nurse1.GetHtmlAsync($"/Ipd/Admission/{adm}"));
        Denied(await RecordAsync(nurse1, adm, RecordKind.ProgressNote, ("condition", "x")), "Doctor's progress note by a nurse");

        // Concurrency: two simultaneous admissions to the same bed – exactly one wins.
        var pA = await RegisterPatientAsync(admin, PatientFields("Race Patient A", "Race Father A"));
        var pB = await RegisterPatientAsync(admin, PatientFields("Race Patient B", "Race Father B"));
        var clerk = await StaffAsync(admin, "clerk.ipd", new[] { "IPD" }, Perm.DashboardView, Perm.IpdRegisterAdmission, Perm.IpdAllocateBed, Perm.IpdViewPatients);
        var race = await Task.WhenAll(AdmitAsync(admin, pA, bed2, Guid.NewGuid()), AdmitAsync(clerk, pB, bed2, Guid.NewGuid()));
        Assert.Equal(1, race.Count(r => r.StatusCode == HttpStatusCode.Redirect));
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.IpdBedAllocations.CountAsync(x => x.BedId == bed2 && x.ToUtc == null));
            Assert.Equal(1, await db.IpdAdmissions.CountAsync(x => x.CurrentBedId == bed2));
        }

        // Bed status rules: an occupied bed cannot be set available by hand; maintenance needs a reason.
        await admin.PostFormAsync("/Ipd/Beds", new[] { P("NewStatus", "Available") }, $"/Ipd/Beds?handler=Status&bedId={bed}");
        Assert.Equal(BedStatus.Occupied, await BedStatusAsync(bed));
        await admin.PostFormAsync("/Ipd/Beds", new[] { P("NewStatus", "UnderMaintenance") }, $"/Ipd/Beds?handler=Status&bedId={gyn}");
        Assert.Equal(BedStatus.Available, await BedStatusAsync(gyn));
        await Ok(await admin.PostFormAsync("/Ipd/Beds", new[] { P("NewStatus", "UnderMaintenance"), P("Note", "Broken side rail") }, $"/Ipd/Beds?handler=Status&bedId={gyn}"), "Maintenance");
        Assert.Equal(BedStatus.UnderMaintenance, await BedStatusAsync(gyn));

        // Database protection: clinical history is insert-only and admissions cannot be deleted.
        await using (var db = Factory.NewDb())
        {
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"IpdAdmissions\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"ClinicalRecords\" SET \"Notes\" = 'changed'"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"MedicationDoses\""));
            await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"IpdTransfers\""));
        }
        Assert.NotEmpty(await AuditAsync(AuditActions.IpdDoseRecorded));
        Assert.Single(await AuditAsync(AuditActions.IpdHandoverAcknowledged));
    }

    [Fact]
    public async Task Government_mode_admissions_without_commercial_information()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var bed = await BedAsync(admin, "FGW", "FGW-01");
        var pid = await RegisterPatientAsync(admin, PatientFields("Govt Patient", "Govt Father", "Female"));
        var visit = await RegisterOpdAsync(admin, pid);
        var r = await AdmitAsync(admin, pid, bed, Guid.NewGuid(), "Opd", visit);
        await Ok(r, "Government admission");
        var adm = Id(AdmissionRegex(), r);
        var settings = await admin.GetHtmlAsync("/Ipd/Settings");
        Assert.Contains("Government mode", settings);
        foreach (var page in new[] { $"/Ipd/Admission/{adm}?tab=billing", $"/Print/Ipd/bill/{adm}", "/Ipd" })
        {
            var html = (await admin.GetHtmlAsync(page)).ToLowerInvariant();
            Assert.DoesNotContain("profit", html);
            Assert.DoesNotContain("purchase price", html);
            Assert.DoesNotContain("sale price", html);
        }
        Assert.Contains("data-form-page", await admin.GetHtmlAsync($"/Print/Ipd/admission/{adm}"));
        await Ok(await ActAsync(admin, adm, "Discharge", Discharge), "Discharge");
        Assert.Equal(AdmissionStatus.Discharged, (await AdmissionAsync(adm)).Status);
    }
}
