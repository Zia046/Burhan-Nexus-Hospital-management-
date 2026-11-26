using System.Net;
using System.Text.RegularExpressions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Backup;
using BurhanNexus.HMS.Infrastructure.Laboratory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>
/// Update 15 – Government OPD slip straight after registration (no receipt), CBC default 14 parameters + configuration, ECG in radiology
/// with payment at booking and one receipt, daily automatic backup with 30-day retention. (Part of Update10Tests to reuse its helpers.)
/// </summary>
public partial class Update10Tests
{
    private static readonly string[] CbcDefault =
    {
        "Total WBC", "Total RBC", "Hemoglobin (HGB)", "PCV (HCT)", "MCV", "MCH", "MCHC", "DLC", "Neutrophils", "Lymphocytes", "Monocytes", "Eosinophils", "Basophils", "Platelets Count",
    };

    [Fact]
    public async Task Private_emergency_keeps_the_thermal_receipt_and_government_opd_makes_no_charge()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Private Emergency", "Father Private"));
        var er = await PostEmergencyAsync(admin, pid, ("Visit.BriefComplaint", "Fall"));
        Assert.Equal(HttpStatusCode.Redirect, er.StatusCode);
        Assert.StartsWith("/Print/Registration/", er.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Government_opd_registration_prints_the_slip_and_records_no_payment()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        var pid = await RegisterPatientAsync(admin, PatientFields("Government Slip", "Father Gov"));
        var r = await admin.PostFormAsync("/Patients/Register", new[] { P("Visit.ChiefComplaint", "Cough") }, $"/Opd/Register?patientId={pid}");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var location = r.Headers.Location!.OriginalString;
        Assert.StartsWith("/Print/Opd/", location);
        var slip = await admin.GetHtmlAsync(location);
        Assert.Contains("data-autoprint=\"1\"", slip);
        Assert.Contains("paper-a", slip); // A4 / A5 from the registration settings
        await using var db = Factory.NewDb();
        Assert.Equal(0, await db.Charges.CountAsync());
        Assert.Equal(0, await db.Receipts.CountAsync());
    }

    [Fact]
    public async Task Government_opd_fee_from_fees_and_services_goes_to_the_registering_users_cash_account()
    {
        await SetupAsync("Government");
        var admin = await LoginAdminAsync();
        await using (var db = Factory.NewDb())
        {
            var item = await db.ServiceItems.SingleAsync(x => x.Code == "OPD-REG");
            item.IsActive = true;
            db.ServiceFees.Add(new ServiceFee { ServiceItemId = item.Id, Fee = 20, EffectiveFrom = new DateOnly(2000, 1, 1), CreatedUtc = DateTime.UtcNow, CreatedByUsername = "test" });
            await db.SaveChangesAsync();
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Fee Slip", "Father Fee"));
        var r = await admin.PostFormAsync("/Patients/Register", new[] { P("Visit.ChiefComplaint", "Fever") }, $"/Opd/Register?patientId={pid}");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "OPD: " + Errors(await r.Content.ReadAsStringAsync()));
        Assert.StartsWith("/Print/Opd/", r.Headers.Location!.OriginalString);                 // still the OPD slip, no receipt page
        await using (var db = Factory.NewDb())
        {
            var charge = await db.Charges.SingleAsync();
            Assert.Equal((20m, 20m, ChargeStatus.Paid), (charge.Fee, charge.PaidAmount, charge.Status));
            Assert.NotNull(charge.EncounterId);
            var receipt = await db.Receipts.SingleAsync();
            Assert.Equal(20m, receipt.AmountPaid);
            Assert.Equal(await db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync(), receipt.CashierUserId); // in the admin's (registering user's) account
        }
    }

    [Fact]
    public async Task Cbc_has_the_14_default_parameters_and_can_be_configured_without_changing_old_reports()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        int cbcId;
        await using (var db = Factory.NewDb())
        {
            var cbc = await db.LabTests.Include(t => t.Parameters).SingleAsync(t => t.Code == "CBC-FULL");
            cbcId = cbc.Id;
            Assert.Equal(CbcDefault, cbc.Parameters.Where(p => p.IsActive).OrderBy(p => p.SortOrder).Select(p => p.Name));
            Assert.Contains(cbc.Parameters, p => !p.IsActive && p.Name == "RDW-CV");          // kept, only inactive
            Assert.Contains(cbc.Parameters, p => !p.IsActive && p.Name == "MPV");
            Assert.Equal(LabResultType.Heading, cbc.Parameters.Single(p => p.Name == "DLC").ResultType);
            var hb = cbc.Parameters.Single(p => p.Name == "Hemoglobin (HGB)");
            Assert.Equal("g/dL", hb.Unit);                                                      // units / ranges untouched
            Assert.Contains(await db.LabReferenceRanges.Where(x => x.ParameterId == hb.Id).ToListAsync(), x => x.Low == 13.0m);
        }
        await SetFeeAsync(admin, "LT-CBC-FULL", 800);
        var pid = await RegisterPatientAsync(admin, PatientFields("Cbc Patient", "Father Cbc"));
        async Task<long> BookCbcAsync()
        {
            var b = await admin.PostFormAsync($"/Lab/Book?patientId={pid}", new[]
            {
                P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("PatientId", pid.ToString()), P("TestIds", cbcId.ToString()), P("Input.ReferralType", "None"),
            });
            Assert.True(b.StatusCode == HttpStatusCode.Redirect, "CBC booking: " + Errors(await b.Content.ReadAsStringAsync()));
            return long.Parse(LabOrderRegex().Match(b.Headers.Location!.OriginalString).Groups[1].Value);
        }
        var first = await BookCbcAsync();
        var entry = await admin.GetHtmlAsync($"/Lab/Entry/{first}");
        Assert.Contains("data-heading=\"DLC\"", entry);
        Assert.DoesNotContain("data-param=\"RDW-CV\"", entry);
        Assert.Contains("data-param=\"Platelets Count\"", entry);

        // Configuration page: deactivate Basophils, activate MPV after the platelets, rename MCV.
        var page = await admin.GetHtmlAsync("/Lab/Cbc");
        Assert.Contains("data-cbc-row=\"Total WBC\"", page);
        List<LabTestParameter> ps;
        await using (var db = Factory.NewDb()) ps = await db.LabTestParameters.Where(p => p.LabTestId == cbcId).OrderBy(p => p.SortOrder).ToListAsync();
        var fields = new List<KeyValuePair<string, string>>();
        var pos = 0;
        foreach (var (p, i) in ps.Select((p, i) => (p, i)))
        {
            var active = p.Name == "MPV" || p.IsActive && p.Name != "Basophils";
            var position = p.Name == "MPV" ? 15 : ++pos;
            fields.Add(P($"Rows[{i}].Id", p.Id.ToString()));
            fields.Add(P($"Rows[{i}].Name", p.Name == "MCV" ? "MCV (fL)" : p.Name));
            fields.Add(P($"Rows[{i}].Position", position.ToString()));
            if (active) fields.Add(P($"Rows[{i}].IsActive", "true"));
            fields.Add(P($"Rows[{i}].IsActive", "false"));
        }
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Lab/Cbc", fields)).StatusCode);
        var second = await BookCbcAsync();
        await using (var db = Factory.NewDb())
        {
            var newNames = await db.LabResultValues.Where(v => v.OrderItem!.LabOrderId == second).OrderBy(v => v.SortOrder).Select(v => v.Name).ToListAsync();
            Assert.Contains("MCV (fL)", newNames);
            Assert.DoesNotContain("Basophils", newNames);
            Assert.Equal("MPV", newNames.Last());
            var oldNames = await db.LabResultValues.Where(v => v.OrderItem!.LabOrderId == first).OrderBy(v => v.SortOrder).Select(v => v.Name).ToListAsync();
            Assert.Equal(CbcDefault, oldNames);                                                // the earlier booking keeps its snapshot
        }

        // Restore default.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Lab/Cbc", Array.Empty<KeyValuePair<string, string>>(), "/Lab/Cbc?handler=Restore")).StatusCode);
        await using (var db = Factory.NewDb())
            Assert.Equal(CbcDefault, await db.LabTestParameters.Where(p => p.LabTestId == cbcId && p.IsActive).OrderBy(p => p.SortOrder).Select(p => p.Name).ToListAsync());
        Assert.Equal(CbcDefault.Length, CbcConfigService.Default.Length);
    }

    [Fact]
    public async Task Ecg_is_booked_in_radiology_with_its_fee_and_one_receipt()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();
        int ecg, item;
        await using (var db = Factory.NewDb())
        {
            var s = await db.RadiologyServices.Include(x => x.ServiceItem).SingleAsync(x => x.Modality == RadiologyModality.Ecg);
            (ecg, item) = (s.Id, s.ServiceItemId);
            Assert.Equal(ServiceKind.Ecg, s.ServiceItem!.Kind);
            Assert.Equal(1, await db.RadiologyServices.CountAsync(x => x.Modality == RadiologyModality.Ecg)); // one ECG service, also after restarts
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Ecg Patient", "Father Ecg"));
        // The fee is set (and the service enabled) in Fees & Services – never hard-coded.
        await using (var db = Factory.NewDb())
        {
            var si = await db.ServiceItems.SingleAsync(x => x.Id == item);
            si.IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.DoesNotContain("ECG (12-lead)", await admin.GetHtmlAsync($"/Radiology/Book?patientId={pid}")); // disabled → not offered
        await using (var db = Factory.NewDb())
        {
            var si = await db.ServiceItems.SingleAsync(x => x.Id == item);
            si.IsActive = true;
            db.ServiceFees.Add(new ServiceFee { ServiceItemId = item, Fee = 600, EffectiveFrom = DateOnly.MinValue, CreatedUtc = DateTime.UtcNow, CreatedByUsername = "test" });
            await db.SaveChangesAsync();
        }
        var book = await admin.GetHtmlAsync($"/Radiology/Book?patientId={pid}");
        Assert.Contains("ECG (12-lead)", book);
        Assert.Contains("600.00", book);
        Assert.Contains("data-rad-payment", book);
        var key = Guid.NewGuid().ToString();
        KeyValuePair<string, string>[] Form() => new[]
        {
            P("Input.IdempotencyKey", key), P("PatientId", pid.ToString()), P("VisitChoice", ""), P("Input.ReferralType", "External"),
            P("Input.ExternalDoctorName", "Dr. Advice"), P("Input.ClinicalIndication", "Chest pain"), P("Input.Priority", "Routine"), P("ServiceIds", ecg.ToString()),
            P("Input.CollectPayment", "true"), P("Input.PaymentMethodId", CashIdSync()),
        };
        var r = await admin.PostFormAsync($"/Radiology/Book?patientId={pid}", Form());
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "ECG booking: " + Errors(await r.Content.ReadAsStringAsync()));
        var location = r.Headers.Location!.OriginalString;
        var orderId = long.Parse(Regex.Match(location, @"/Radiology/Order/(\d+)").Groups[1].Value);
        Assert.Contains("receipt=", location);
        // A repeated click creates nothing new.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Radiology/Book?patientId={pid}", Form())).StatusCode);
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(1, await db.RadiologyOrders.CountAsync());
            var exam = await db.RadiologyExams.SingleAsync();
            Assert.Equal(RadiologyModality.Ecg, exam.Modality);
            var charge = await db.Charges.SingleAsync(c => c.RadiologyOrderId == orderId);
            Assert.Equal((600m, 600m, ServiceKind.Ecg), (charge.Fee, charge.PaidAmount, charge.ServiceKind));
            Assert.Equal(1, await db.Receipts.CountAsync());
        }
        var order = await admin.GetHtmlAsync(location);
        Assert.Contains("data-test=\"rad-receipt\"", order);
        Assert.Contains("E-1", order);                                                          // ECG token
        Assert.Contains("ECG (12-lead)", await admin.GetHtmlAsync("/Radiology/Bookings?modality=Ecg"));
        Assert.Contains("ECG (12-lead)", await admin.GetHtmlAsync("/Radiology/Worklist/Ecg"));
        long receiptId;
        await using (var db = Factory.NewDb()) receiptId = await db.Receipts.Select(x => x.Id).SingleAsync();
        var receipt = await admin.GetHtmlAsync($"/Print/Receipt/{receiptId}");
        Assert.Contains("Ecg Patient", receipt);
        Assert.Contains("ECG", receipt);
        Assert.Contains("600.00", receipt);
    }

    private string CashIdSync()
    {
        using var db = Factory.NewDb();
        return db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).Single().ToString();
    }

    [Fact]
    public async Task Daily_automatic_backup_runs_once_a_day_and_deletes_automatic_backups_older_than_30_days()
    {
        await SetupAsync("Private");
        using var scope = Factory.Services.CreateScope();
        var backups = scope.ServiceProvider.GetRequiredService<IBackupService>();
        // An automatic backup 40 days old and a manual one 40 days old.
        var oldFile = Path.Combine(backups.BackupDirectory, "BNHMS_old_auto.backup");
        var manualFile = Path.Combine(backups.BackupDirectory, "BNHMS_old_manual.backup");
        Directory.CreateDirectory(backups.BackupDirectory);
        await File.WriteAllTextAsync(oldFile, "old");
        await File.WriteAllTextAsync(manualFile, "manual");
        await using (var db = Factory.NewDb())
        {
            var t = DateTime.UtcNow.AddDays(-40);
            db.BackupHistory.Add(new BackupHistory { FileName = "BNHMS_old_auto.backup", Kind = BackupKind.Scheduled, Status = BackupStatus.Completed, StartedUtc = t, CompletedUtc = t, SizeBytes = 3 });
            db.BackupHistory.Add(new BackupHistory { FileName = "BNHMS_old_manual.backup", Kind = BackupKind.Manual, Status = BackupStatus.Completed, StartedUtc = t, CompletedUtc = t, SizeBytes = 6 });
            await db.SaveChangesAsync();
        }
        var first = await backups.RunDailyAsync(DateTime.Now);
        Assert.True(first.Succeeded, first.ErrorText);
        Assert.NotNull(first.Value);
        Assert.Equal(BackupKind.Scheduled, first.Value!.Kind);
        var second = await backups.RunDailyAsync(DateTime.Now);
        Assert.True(second.Succeeded);
        Assert.Null(second.Value);                                                              // once a day
        await using (var db = Factory.NewDb())
        {
            var all = await db.BackupHistory.ToListAsync();
            Assert.DoesNotContain(all, b => b.FileName == "BNHMS_old_auto.backup");             // older than 30 days: deleted
            Assert.Contains(all, b => b.FileName == "BNHMS_old_manual.backup");                 // manual backups are kept
            Assert.Single(all, b => b.Kind == BackupKind.Scheduled && b.Status == BackupStatus.Completed);
            Assert.True(all.Single(b => b.Kind == BackupKind.Scheduled).SizeBytes > 0);       // complete database dump
        }
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(manualFile));
    }
}
