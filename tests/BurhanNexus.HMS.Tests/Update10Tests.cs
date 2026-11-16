using System.Globalization;
using System.Net;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static BurhanNexus.HMS.Tests.F;

namespace BurhanNexus.HMS.Tests;

/// <summary>Update 10 – central Fees &amp; Services, simplified laboratory, inpatient ledger, ward / NICU billing, discharge clearance.</summary>
public partial class Update10Tests : Phase2TestBase
{
    private async Task<int> CashAsync()
    {
        await using var db = Factory.NewDb();
        return await db.PaymentMethods.Where(m => m.Code == "CASH").Select(m => m.Id).SingleAsync();
    }

    private async Task<int> MainCounterAsync()
    {
        await using var db = Factory.NewDb();
        return await db.Counters.Where(c => c.Code == "MAIN").Select(c => c.Id).SingleAsync();
    }

    /// <summary>Collects one service for a patient at the cashier and returns the charge.</summary>
    private async Task<Charge> CollectServiceAsync(Browser b, long patientId, int serviceId)
    {
        var before = await LastChargeIdAsync();
        var r = await b.PostFormAsync($"/Finance/Collect?patientId={patientId}", new[]
        {
            P("Input.IdempotencyKey", Guid.NewGuid().ToString()), P("Input.CounterId", (await MainCounterAsync()).ToString()), P("Input.PaymentMethodId", (await CashAsync()).ToString()),
            P("PatientId", patientId.ToString()), P("Lines[0].ServiceItemId", serviceId.ToString()),
        }, "/Finance/Collect");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, "Collect: " + Errors(await r.Content.ReadAsStringAsync()));
        await using var db = Factory.NewDb();
        return await db.Charges.AsNoTracking().Where(c => c.Id > before && c.ServiceItemId == serviceId).OrderByDescending(c => c.Id).FirstAsync();
    }

    private async Task<long> LastChargeIdAsync()
    {
        await using var db = Factory.NewDb();
        return await db.Charges.MaxAsync(c => (long?)c.Id) ?? 0;
    }

    private static KeyValuePair<string, string>[] ServiceForm(string code, string name, int? category, string unit, decimal fee, decimal? gov, bool active = true) => new[]
    {
        P("Form.Code", code), P("Form.Name", name), P("Form.Kind", "Procedure"), P("Form.ServiceCategoryId", category?.ToString() ?? ""), P("Form.ChargeUnit", unit),
        P("Form.Applicability", "Both"), P("Form.Fee", fee.ToString(CultureInfo.InvariantCulture)), P("Form.GovernmentFee", gov?.ToString(CultureInfo.InvariantCulture) ?? ""),
        P("Form.IsFree", "false"), P("Form.IsActive", active ? "true" : "false"), P("Form.AllowPartialPayment", "false"),
    };

    // =========================================================================== 1A – Fees & Services

    [Fact]
    public async Task Central_fee_catalogue_categories_fee_history_government_rate_and_permissions()
    {
        await SetupAsync("Private");
        var admin = await LoginAdminAsync();

        // Standard categories and services are present (fee 0 until the administrator sets it).
        await using (var db = Factory.NewDb())
        {
            var codes = await db.ServiceCategories.Select(c => c.Code).ToListAsync();
            foreach (var c in new[] { "OPD", "LAB", "IPD", "RAD", "ER", "OT", "PROC", "WARD", "NICU", "OTHER" }) Assert.Contains(c, codes);
            var inc = await db.ServiceItems.Include(s => s.ServiceCategory).SingleAsync(s => s.Code == "SVC-NICU-INC");
            Assert.Equal(("NICU", ChargeUnit.PerDay, 0m, true), (inc.ServiceCategory!.Code, inc.ChargeUnit, inc.DefaultFee, inc.IsActive));
            // Services of other modules are placed in their category (laboratory tests → Laboratory).
            Assert.True(await db.ServiceItems.Where(s => s.Kind == ServiceKind.Laboratory).AllAsync(s => s.ServiceCategory!.Code == "LAB"));
        }
        var list = await admin.GetHtmlAsync("/Fees?q=incubator");
        Assert.Contains("SVC-NICU-INC", list);
        Assert.DoesNotContain("SVC-PR-SUT", list);

        // 35. New category.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Fees/Categories", new[] { P("Form.Code", "physio"), P("Form.Name", "Physiotherapy"), P("Form.IsActive", "true") })).StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Fees/Categories", new[] { P("Form.Code", "PHYSIO2"), P("Form.Name", "physiotherapy"), P("Form.IsActive", "true") })).StatusCode);
        int cat;
        await using (var db = Factory.NewDb()) cat = await db.ServiceCategories.Where(c => c.Code == "PHYSIO").Select(c => c.Id).SingleAsync();

        // 36. New service with fee 500 (government rate 300), charged per session.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync("/Fees/Service", ServiceForm("PHY-1", "Physiotherapy session", cat, "PerSession", 500, 300))).StatusCode);
        int svc;
        await using (var db = Factory.NewDb())
        {
            var s = await db.ServiceItems.SingleAsync(x => x.Code == "PHY-1");
            svc = s.Id;
            Assert.Equal((cat, ChargeUnit.PerSession), (s.ServiceCategoryId!.Value, s.ChargeUnit));
        }
        var pid = await RegisterPatientAsync(admin, PatientFields("Fee Catalogue Patient", "Father Fee"));
        var first = await CollectServiceAsync(admin, pid, svc);
        Assert.Equal(500m, first.Fee);

        // 37 / 38. Change the fee: new transactions use 700, the earlier bill keeps 500.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Fees/Service?id={svc}", ServiceForm("PHY-1", "Physiotherapy session", cat, "PerSession", 700, 300)
            .Append(P("Form.FeeNote", "New rates 2026")).ToArray())).StatusCode);
        var second = await CollectServiceAsync(admin, pid, svc);
        Assert.Equal(700m, second.Fee);
        await using (var db = Factory.NewDb())
        {
            Assert.Equal(500m, (await db.Charges.SingleAsync(c => c.Id == first.Id)).Fee);
            Assert.Equal(2, await db.ServiceFees.CountAsync(f => f.ServiceItemId == svc));
        }
        Assert.Contains("New rates 2026", await admin.GetHtmlAsync($"/Fees/Service?id={svc}"));

        // 40. Every department reads the fee from the catalogue; in Government mode the government rate is used.
        using (var scope = Factory.Services.CreateScope())
        {
            var setup = scope.ServiceProvider.GetRequiredService<IFinanceSetupService>();
            var today = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().UtcDateTime.AddHours(5));
            Assert.Equal(700m, await setup.FeeOnAsync(svc, today));
            await using (var db = Factory.NewDb())
            {
                var cfg = await db.HospitalConfigurations.SingleAsync();
                cfg.HospitalType = HospitalType.Government;
                await db.SaveChangesAsync();
            }
            Assert.Equal(300m, await setup.FeeOnAsync(svc, today));
            await using (var db = Factory.NewDb())
            {
                var cfg = await db.HospitalConfigurations.SingleAsync();
                cfg.HospitalType = HospitalType.Private;
                await db.SaveChangesAsync();
            }
        }

        // 39. Deactivate: the service disappears from the active list, its bills stay.
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostFormAsync($"/Fees/Service?id={svc}", ServiceForm("PHY-1", "Physiotherapy session", cat, "PerSession", 700, 300, active: false))).StatusCode);
        Assert.DoesNotContain("PHY-1", await admin.GetHtmlAsync("/Fees"));
        Assert.Contains("PHY-1", await admin.GetHtmlAsync("/Fees?status=inactive"));
        Assert.Contains("data-has-history", await admin.GetHtmlAsync($"/Fees/Service?id={svc}"));
        await using (var db = Factory.NewDb())
        {
            Assert.False((await db.ServiceItems.SingleAsync(s => s.Id == svc)).IsActive);
            Assert.Equal(2, await db.Charges.CountAsync(c => c.ServiceItemId == svc));
        }

        // Permissions are enforced on the server: view-only cannot save; details-only cannot change the fee.
        var viewer = await UserWithAsync(admin, "feeviewer", Perm.DashboardView, Perm.SvcViewFees);
        Assert.Contains("SVC-NICU-INC", await viewer.GetHtmlAsync("/Fees"));
        var denied = await viewer.PostFormAsync("/Fees", ServiceForm("PHY-1", "Hacked", cat, "PerSession", 1, null), $"/Fees/Service?id={svc}");
        Assert.True(denied.StatusCode is HttpStatusCode.Forbidden || denied.Headers.Location?.ToString().Contains("AccessDenied") == true, $"viewer save: {(int)denied.StatusCode}");
        Assert.NotEqual(HttpStatusCode.OK, (await viewer.Http.GetAsync("/Fees/Categories")).StatusCode);
        var editor = await UserWithAsync(admin, "feeeditor", Perm.DashboardView, Perm.SvcManageServices);
        var noFee = await editor.PostFormAsync($"/Fees/Service?id={svc}", ServiceForm("PHY-1", "Physiotherapy (renamed)", cat, "PerSession", 1, null, active: true));
        Assert.Equal(HttpStatusCode.Redirect, noFee.StatusCode); // details saved, fee fields ignored
        await using (var db = Factory.NewDb())
        {
            var s = await db.ServiceItems.SingleAsync(x => x.Id == svc);
            Assert.Equal(("Physiotherapy (renamed)", 700m), (s.Name, s.DefaultFee));
        }
        Assert.Single(await AuditAsync(AuditActions.ServiceCategorySaved));
        Assert.Contains(await AuditAsync(AuditActions.ServiceFeeChanged), a => a.Description!.Contains("500.00 to 700.00"));
    }
}
