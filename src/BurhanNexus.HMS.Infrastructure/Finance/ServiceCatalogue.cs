using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Finance;

/// <summary>
/// Update 10 – the standard Fees &amp; Services catalogue: ten categories and their common services. It is added once
/// (FinanceSettings.ServiceCatalogueVersion); services are ACTIVE with fee 0 until the administrator sets the fee. Existing
/// services are never changed except that a service without a category is placed in the category of its type.
/// </summary>
public static class ServiceCatalogue
{
    public const string Opd = "OPD", Lab = "LAB", Ipd = "IPD", Rad = "RAD", Er = "ER", Ot = "OT", Proc = "PROC", Ward = "WARD", Nicu = "NICU", Other = "OTHER", Pharm = "PHARM";

    public static readonly (string Code, string Name, string Description)[] Categories =
    {
        (Opd, "OPD & Consultation", "Registration, consultation and follow-up fees"),
        (Lab, "Laboratory", "Laboratory investigations, profiles and packages"),
        (Ipd, "Admission & IPD", "Admission fee, room / ward and inpatient services"),
        (Rad, "Radiology & Imaging", "X-Ray, ultrasound, Doppler, CT, MRI, ECG, echo"),
        (Er, "Emergency", "Emergency registration, consultation and procedures"),
        (Ot, "Operation Theatre", "OT, surgery, surgeon, anaesthesia and recovery charges"),
        (Proc, "Minor Procedures", "Dressing, suturing, catheterisation and other minor procedures"),
        (Ward, "Ward & Patient Care", "Daily bed, nursing, doctor visit, oxygen and monitoring charges"),
        (Nicu, "NICU Equipment & Services", "Incubator, warmer, phototherapy, ventilator, CPAP and other neonatal equipment"),
        (Other, "Other Services", "Certificates, reports, ambulance and other hospital services"),
        (Pharm, "Pharmacy", "Medicine charges (priced per medicine in the pharmacy)"),
    };

    private sealed record S(string Code, string Name, string Category, ServiceKind Kind, ChargeUnit Unit);

    private static IEnumerable<S> Services()
    {
        S A(string c, string n, ChargeUnit u = ChargeUnit.PerVisit) => new(c, n, Opd, ServiceKind.Other, u);
        yield return A("SVC-OPD-SPEC", "Specialist consultation");
        yield return A("SVC-OPD-FUP", "Follow-up consultation");
        yield return A("SVC-OPD-OTH", "Other consultation charges");
        S I(string c, string n, ChargeUnit u) => new(c, n, Ipd, ServiceKind.Inpatient, u);
        yield return I("SVC-IPD-ADM", "Admission fee", ChargeUnit.PerAdmission);
        yield return I("SVC-IPD-GEN", "General ward charges", ChargeUnit.PerDay);
        yield return I("SVC-IPD-PVT", "Private room charges", ChargeUnit.PerDay);
        yield return I("SVC-IPD-SEMI", "Semi-private room charges", ChargeUnit.PerDay);
        yield return I("SVC-IPD-ICU", "ICU charges", ChargeUnit.PerDay);
        yield return I("SVC-IPD-NICU", "NICU charges", ChargeUnit.PerDay);
        yield return I("SVC-IPD-LAB", "Labour room charges", ChargeUnit.PerDay);
        yield return I("SVC-IPD-NURS", "Nursing charges", ChargeUnit.PerDay);
        yield return I("SVC-IPD-DRV", "Doctor visit charges", ChargeUnit.PerVisit);
        yield return I("SVC-IPD-OTH", "Other inpatient services", ChargeUnit.PerItem);
        S R(string c, string n, ServiceKind k = ServiceKind.Other) => new(c, n, Rad, k, ChargeUnit.PerTest);
        yield return R("SVC-RAD-OBS", "Obstetric ultrasound", ServiceKind.Ultrasound);
        yield return R("SVC-RAD-DOP", "Doppler ultrasound", ServiceKind.Ultrasound);
        yield return R("SVC-RAD-CT", "CT scan");
        yield return R("SVC-RAD-MRI", "MRI");
        yield return R("SVC-RAD-ECG", "ECG");
        yield return R("SVC-RAD-ECHO", "Echocardiography");
        yield return R("SVC-RAD-OTH", "Other diagnostic services");
        S EE(string c, string n, ChargeUnit u = ChargeUnit.PerProcedure) => new(c, n, Er, ServiceKind.Procedure, u);
        yield return EE("SVC-ER-CONS", "Emergency consultation", ChargeUnit.PerVisit);
        yield return EE("SVC-ER-NEB", "Nebulization (emergency)", ChargeUnit.PerSession);
        yield return EE("SVC-ER-O2", "Oxygen (emergency)", ChargeUnit.PerHour);
        yield return EE("SVC-ER-INJ", "Injection administration (emergency)");
        yield return EE("SVC-ER-DRS", "Dressing (emergency)");
        yield return EE("SVC-ER-SUT", "Suturing (emergency)");
        yield return EE("SVC-ER-PROC", "Emergency procedure");
        yield return EE("SVC-ER-OTH", "Other emergency services", ChargeUnit.PerItem);
        S O(string c, string n, ChargeUnit u = ChargeUnit.PerProcedure) => new(c, n, Ot, ServiceKind.Procedure, u);
        yield return O("SVC-OT-CHG", "OT charges");
        yield return O("SVC-OT-MAJ", "Major surgery charges");
        yield return O("SVC-OT-MIN", "Minor surgery charges");
        yield return O("SVC-OT-SURG", "Surgeon fee");
        yield return O("SVC-OT-ASST", "Assistant surgeon fee");
        yield return O("SVC-OT-ANES", "Anaesthesia fee");
        yield return O("SVC-OT-PACU", "Recovery / PACU charges", ChargeUnit.PerHour);
        yield return O("SVC-OT-CONS", "OT consumables and other services", ChargeUnit.PerItem);
        S P(string c, string n, ChargeUnit u = ChargeUnit.PerProcedure) => new(c, n, Proc, ServiceKind.Procedure, u);
        yield return P("SVC-PR-DRS", "Wound dressing");
        yield return P("SVC-PR-SUT", "Suturing");
        yield return P("SVC-PR-CATH", "Catheterization");
        yield return P("SVC-PR-IVC", "IV cannulation");
        yield return P("SVC-PR-NGT", "NG tube insertion");
        yield return P("SVC-PR-NEB", "Nebulization", ChargeUnit.PerSession);
        yield return P("SVC-PR-ENEMA", "Enema");
        yield return P("SVC-PR-INJ", "Injection administration");
        yield return P("SVC-PR-BT", "Blood transfusion administration");
        yield return P("SVC-PR-LP", "Lumbar puncture");
        yield return P("SVC-PR-OTH", "Other minor procedures");
        S W(string c, string n, ChargeUnit u) => new(c, n, Ward, ServiceKind.Inpatient, u);
        yield return W("SVC-WD-BED", "Daily bed charges", ChargeUnit.PerDay);
        yield return W("SVC-WD-NURS", "Nursing charges (ward)", ChargeUnit.PerDay);
        yield return W("SVC-WD-DRV", "Doctor visit (ward)", ChargeUnit.PerVisit);
        yield return W("SVC-WD-O2", "Oxygen charges", ChargeUnit.PerHour);
        yield return W("SVC-WD-MON", "Monitoring charges", ChargeUnit.PerDay);
        yield return W("SVC-WD-DRS", "Dressing charges (ward)", ChargeUnit.PerProcedure);
        yield return W("SVC-WD-OTH", "Other ward services", ChargeUnit.PerItem);
        foreach (var (code, name, unit) in NicuEquipmentServices) yield return new S(code, name, Nicu, ServiceKind.Inpatient, unit);
        S X(string c, string n, ChargeUnit u = ChargeUnit.PerItem) => new(c, n, Other, ServiceKind.Other, u);
        yield return X("SVC-MS-MCERT", "Medical certificate");
        yield return X("SVC-MS-MREP", "Medical report");
        yield return X("SVC-MS-AMB", "Ambulance", ChargeUnit.PerVisit);
        yield return X("SVC-MS-MREC", "Medical record / documentation charges");
        yield return X("SVC-MS-SNUR", "Special nursing services", ChargeUnit.PerDay);
        yield return X("SVC-MS-OTH", "Other hospital service");
    }

    /// <summary>NICU equipment and services (also used by the NICU equipment catalogue).</summary>
    public static readonly (string Code, string Name, ChargeUnit Unit)[] NicuEquipmentServices =
    {
        ("SVC-NICU-INC", "Neonatal incubator", ChargeUnit.PerDay),
        ("SVC-NICU-RW", "Radiant warmer / Baby warmer", ChargeUnit.PerDay),
        ("SVC-NICU-PT1", "Single phototherapy", ChargeUnit.PerDay),
        ("SVC-NICU-PT2", "Double phototherapy", ChargeUnit.PerDay),
        ("SVC-NICU-VENT", "Neonatal ventilator", ChargeUnit.PerDay),
        ("SVC-NICU-CPAP", "CPAP / Bubble CPAP", ChargeUnit.PerDay),
        ("SVC-NICU-BIPAP", "BiPAP", ChargeUnit.PerDay),
        ("SVC-NICU-HFNC", "High-flow nasal cannula (HFNC)", ChargeUnit.PerDay),
        ("SVC-NICU-CMON", "Cardiac monitor", ChargeUnit.PerDay),
        ("SVC-NICU-SPO2", "Pulse oximeter", ChargeUnit.PerDay),
        ("SVC-NICU-INF", "Infusion pump", ChargeUnit.PerDay),
        ("SVC-NICU-SYR", "Syringe pump", ChargeUnit.PerDay),
        ("SVC-NICU-O2", "Oxygen therapy (NICU)", ChargeUnit.PerHour),
        ("SVC-NICU-SUC", "Suction", ChargeUnit.PerSession),
        ("SVC-NICU-OTH", "Other neonatal equipment", ChargeUnit.PerDay),
    };

    /// <summary>Category of a service of the given type (used for services created before update 10 and by other modules).</summary>
    public static string CategoryOf(ServiceKind kind) => kind switch
    {
        ServiceKind.OpdRegistration or ServiceKind.Consultation => Opd,
        ServiceKind.EmergencyRegistration => Er,
        ServiceKind.Laboratory => Lab,
        ServiceKind.XRay or ServiceKind.Ultrasound => Rad,
        ServiceKind.Procedure => Proc,
        ServiceKind.Pharmacy => Pharm,
        ServiceKind.Inpatient => Ipd,
        _ => Other,
    };

    public static async Task SeedAsync(HmsDbContext db, DateTime now, CancellationToken ct = default)
    {
        var settings = await db.FinanceSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return;
        var cats = await db.ServiceCategories.ToListAsync(ct);
        if (settings.ServiceCatalogueVersion < 1)
        {
            var sort = cats.Count == 0 ? 0 : cats.Max(c => c.SortOrder);
            foreach (var (code, name, desc) in Categories)
                if (cats.All(c => c.Code != code && !string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    var c = new ServiceCategory { Code = code, Name = name, Description = desc, SortOrder = sort += 10, CreatedUtc = now, UpdatedUtc = now };
                    db.ServiceCategories.Add(c);
                    cats.Add(c);
                }
            await db.SaveChangesAsync(ct);
            var codes = (await db.ServiceItems.Select(s => s.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var names = (await db.ServiceItems.Select(s => s.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var s in Services())
            {
                if (codes.Contains(s.Code) || names.Contains(s.Name)) continue;
                var cat = cats.FirstOrDefault(c => c.Code == s.Category);
                var item = new ServiceItem
                {
                    Code = s.Code, Name = s.Name, Category = EncounterType.ServiceOnly, Kind = s.Kind, ServiceCategoryId = cat?.Id, ChargeUnit = s.Unit,
                    DefaultFee = 0, IsActive = true, CreatedUtc = now, UpdatedUtc = now, Description = "Standard catalogue – set the fee"
                };
                db.ServiceItems.Add(item);
                db.ServiceFees.Add(new ServiceFee { ServiceItem = item, Fee = 0, EffectiveFrom = DateOnly.MinValue, CreatedUtc = now, CreatedByUsername = "SYSTEM", Note = "Standard catalogue – set the fee" });
                codes.Add(s.Code);
            }
            settings.ServiceCatalogueVersion = 1;
            await db.SaveChangesAsync(ct);
        }
        // Services of other modules (laboratory tests, radiology, pharmacy, registration …) without a category: place them by type.
        var loose = await db.ServiceItems.Where(s => s.ServiceCategoryId == null).ToListAsync(ct);
        if (loose.Count == 0) return;
        var byCode = cats.Where(c => c.Id > 0).ToDictionary(c => c.Code, c => c.Id);
        foreach (var s in loose)
        {
            if (byCode.TryGetValue(CategoryOf(s.Kind), out var id)) s.ServiceCategoryId = id;
            if (s.ChargeUnit == ChargeUnit.Fixed)
                s.ChargeUnit = s.Kind switch
                {
                    ServiceKind.Laboratory or ServiceKind.XRay or ServiceKind.Ultrasound => ChargeUnit.PerTest,
                    ServiceKind.Consultation or ServiceKind.OpdRegistration or ServiceKind.EmergencyRegistration => ChargeUnit.PerVisit,
                    ServiceKind.Procedure => ChargeUnit.PerProcedure,
                    ServiceKind.Pharmacy => ChargeUnit.PerItem,
                    _ => ChargeUnit.Fixed,
                };
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Category id for a service a module creates (null when the catalogue has no such category).</summary>
    public static Task<int?> CategoryIdAsync(HmsDbContext db, ServiceKind kind, CancellationToken ct = default)
    {
        var code = CategoryOf(kind);
        return db.ServiceCategories.Where(c => c.Code == code).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// The fee of a service on a date – the single place every module takes fees from. In Government mode the government rate is used
    /// when one is set.
    /// </summary>
    public static async Task<decimal> FeeOnAsync(HmsDbContext db, int serviceId, DateOnly date, CancellationToken ct = default)
    {
        var row = await db.ServiceFees.AsNoTracking().Where(f => f.ServiceItemId == serviceId && f.EffectiveFrom <= date)
            .OrderByDescending(f => f.EffectiveFrom).ThenByDescending(f => f.Id).Select(f => new { f.Fee, f.GovernmentFee }).FirstOrDefaultAsync(ct);
        if (row is null) return await db.ServiceItems.Where(s => s.Id == serviceId).Select(s => s.DefaultFee).FirstAsync(ct);
        if (row.GovernmentFee is decimal g && await IsGovernmentAsync(db, ct)) return g;
        return row.Fee;
    }

    public static async Task<bool> IsGovernmentAsync(HmsDbContext db, CancellationToken ct = default) =>
        await db.HospitalConfigurations.AsNoTracking().Select(c => (HospitalType?)c.HospitalType).FirstOrDefaultAsync(ct) == HospitalType.Government;
}
