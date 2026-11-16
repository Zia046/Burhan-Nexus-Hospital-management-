using BurhanNexus.HMS.Core.Domain;

namespace BurhanNexus.HMS.Infrastructure.Laboratory;

/// <summary>
/// Update 9 – the standard laboratory catalogue: disciplines, commonly performed investigations, their parameters, units and
/// reference intervals.
///
/// The reference intervals are the commonly published adult (and where marked, paediatric / pregnancy) intervals in conventional
/// units, as printed in standard clinical-laboratory references (e.g. Tietz Clinical Guide to Laboratory Tests, WHO and the
/// manufacturers' package inserts). They are a STARTING POINT: many intervals depend on the analyser, the method and the population,
/// so every test is created with "review required". The laboratory administrator confirms or edits each range, sets the price, and
/// can deactivate what the laboratory does not perform. Existing tests (matched by code) are never changed, and a range change never
/// alters a finalised report (results keep the range that applied when they were booked).
/// </summary>
public static class LabStandardCatalogue
{
    public sealed record Param(string Name, string? Unit, LabResultType Type, int Decimals, LabReferenceRange[] Ranges, string? Options = null, bool Optional = false);
    public sealed record Test(string Code, string Name, string Category, string Sample, Param[] Parameters, string? Method = null, string? Instructions = null, string? Interpretation = null, int? Hours = null);

    /// <summary>Disciplines (existing names are kept – a synonym maps to the category that already exists).</summary>
    public static readonly (string Name, string[] Synonyms)[] Categories =
    {
        ("Haematology", new[] { "Hematology" }),
        ("Coagulation / Hemostasis", new[] { "Coagulation" }),
        ("Biochemistry", new[] { "Clinical Chemistry", "Clinical Chemistry / Biochemistry" }),
        ("Electrolytes", Array.Empty<string>()),
        ("Arterial Blood Gases (ABG)", new[] { "ABG" }),
        ("Special Chemistry", Array.Empty<string>()),
        ("Vitamins & Trace Elements", Array.Empty<string>()),
        ("Endocrinology", Array.Empty<string>()),
        ("Hormonal Assays", new[] { "Hormones" }),
        ("Cardiac Markers", Array.Empty<string>()),
        ("Tumor Markers", new[] { "Tumour Markers" }),
        ("Serology", Array.Empty<string>()),
        ("Immunology", Array.Empty<string>()),
        ("Autoimmune Testing", new[] { "Autoimmunity" }),
        ("Allergy Testing", new[] { "Allergy" }),
        ("Molecular Biology / PCR", new[] { "Molecular Biology", "PCR" }),
        ("Microbiology", Array.Empty<string>()),
        ("Parasitology", Array.Empty<string>()),
        ("Clinical Pathology", Array.Empty<string>()),
        ("Urine Analysis", new[] { "Urinalysis" }),
        ("Stool Examination", Array.Empty<string>()),
        ("Body Fluids", Array.Empty<string>()),
        ("Andrology / Semen Analysis", new[] { "Semen Analysis" }),
        ("Blood Bank / Transfusion Medicine", new[] { "Blood Banking / Transfusion Medicine", "Blood Bank" }),
        ("Histopathology", Array.Empty<string>()),
        ("Cytology", new[] { "Cytopathology" }),
        ("Toxicology / Drug Monitoring", new[] { "Toxicology" }),
    };

    /// <summary>Sample types used by the catalogue (existing codes are reused).</summary>
    public static readonly (string Code, string Name, string? Container)[] SampleTypes =
    {
        ("BLD", "Blood (EDTA)", "Purple top (EDTA)"), ("SER", "Serum", "Red / yellow top"), ("PLS", "Plasma (fluoride)", "Grey top"),
        ("URN", "Urine", "Sterile urine container"), ("STL", "Stool", "Stool container"), ("OTH", "Other", null),
        ("CIT", "Plasma (citrate)", "Light blue top (sodium citrate)"), ("ABG", "Arterial blood (heparinised)", "Heparinised syringe, on ice"),
        ("BC", "Blood culture", "Blood culture bottle"), ("SWB", "Swab", "Sterile swab in transport medium"), ("FLD", "Body fluid", "Sterile container"),
        ("TIS", "Tissue / biopsy", "Container with 10 % formalin"), ("SPT", "Sputum", "Sterile sputum container"), ("SMN", "Semen", "Sterile wide-mouth container"),
        ("CSF", "Cerebrospinal fluid", "Sterile tubes"), ("U24", "24-hour urine", "24-hour urine container"),
    };

    // ---------------------------------------------------------------- choice sets (dropdowns at result entry)
    public const string PosNeg = "Negative|Positive";
    public const string Reactive = "Non-Reactive|Reactive";
    public const string Detected = "Not Detected|Detected";
    public const string PresentAbsent = "Absent|Present";
    public const string NormalAbnormal = "Normal|Abnormal";
    public const string Seen = "Not seen|Seen";
    public const string Grade = "Nil|Trace|+|++|+++|++++";
    public const string BloodGroups = "A|B|AB|O";
    public const string Rh = "Positive|Negative";
    public const string Compatible = "Compatible|Incompatible";

    private const Gender M = Gender.Male, F = Gender.Female;
    private const int Y = 365;

    private static LabReferenceRange R(decimal? low, decimal? high, Gender? g = null, int? minDays = null, int? maxDays = null, bool pregnancy = false, string? text = null) =>
        new() { Low = low, High = high, Gender = g, MinAgeDays = minDays, MaxAgeDays = maxDays, IsPregnancy = pregnancy, NormalText = text };
    private static LabReferenceRange Adult(decimal? low, decimal? high, Gender? g = null) => R(low, high, g, 18 * Y);
    private static LabReferenceRange Child(decimal? low, decimal? high, int fromDays, int toDays, Gender? g = null) => R(low, high, g, fromDays, toDays);
    private static LabReferenceRange Preg(decimal? low, decimal? high, string? text = null) => R(low, high, F, pregnancy: true, text: text);
    private static LabReferenceRange Txt(string text, Gender? g = null) => new() { NormalText = text, Gender = g };

    private static Param N(string name, string? unit, int dec, params LabReferenceRange[] r) => new(name, unit, LabResultType.Numeric, dec, r);
    private static Param NO(string name, string? unit, int dec, params LabReferenceRange[] r) => new(name, unit, LabResultType.Numeric, dec, r, Optional: true);
    private static Param O(string name, string options, string? normal) => new(name, null, LabResultType.Options, 0, normal is null ? Array.Empty<LabReferenceRange>() : new[] { Txt(normal) }, options);
    private static Param OO(string name, string options, string? normal) => new(name, null, LabResultType.Options, 0, normal is null ? Array.Empty<LabReferenceRange>() : new[] { Txt(normal) }, options, true);
    private static Param T(string name, string? normal = null) => new(name, null, LabResultType.Text, 0, normal is null ? Array.Empty<LabReferenceRange>() : new[] { Txt(normal) });
    private static Param TO(string name, string? normal = null) => new(name, null, LabResultType.Text, 0, normal is null ? Array.Empty<LabReferenceRange>() : new[] { Txt(normal) }, null, true);
    private static Param Culture(string name = "Culture & sensitivity") => new(name, null, LabResultType.Culture, 0, new[] { Txt("No growth") });
    private static Param Q(string name, string options, string normal) => O(name, options, normal);

    public static IEnumerable<Test> Tests()
    {
        // =============================================================== Haematology
        yield return new("CBC-FULL", "Complete Blood Count with Differential (CBC)", "Haematology", "BLD", new[]
        {
            N("Haemoglobin", "g/dL", 1, Adult(13.0m, 17.0m, M), Adult(12.0m, 15.5m, F), Preg(11.0m, null), Child(14.0m, 22.0m, 0, 7), Child(10.0m, 15.0m, 7, 180),
                Child(10.5m, 13.5m, 180, 2 * Y), Child(11.5m, 15.5m, 2 * Y, 12 * Y), Child(12.0m, 16.0m, 12 * Y, 18 * Y)),
            N("Total RBC count", "×10⁶/µL", 2, Adult(4.5m, 5.9m, M), Adult(4.0m, 5.2m, F), Child(3.9m, 5.3m, 6 * 30, 12 * Y)),
            N("Haematocrit (HCT / PCV)", "%", 1, Adult(40m, 52m, M), Adult(36m, 46m, F), Child(33m, 45m, 6 * 30, 12 * Y)),
            N("MCV", "fL", 1, Adult(80m, 100m), Child(70m, 90m, 6 * 30, 12 * Y)),
            N("MCH", "pg", 1, Adult(27m, 33m), Child(23m, 31m, 6 * 30, 12 * Y)),
            N("MCHC", "g/dL", 1, R(32m, 36m)),
            N("RDW-CV", "%", 1, R(11.5m, 14.5m)),
            N("Total WBC count", "×10³/µL", 1, Adult(4.0m, 11.0m), Child(9.0m, 30.0m, 0, 7), Child(6.0m, 17.5m, 7, 2 * Y), Child(5.0m, 15.5m, 2 * Y, 12 * Y), Child(4.5m, 13.5m, 12 * Y, 18 * Y)),
            N("Neutrophils", "%", 0, Adult(40m, 75m), Child(20m, 60m, 30, 6 * Y)),
            N("Lymphocytes", "%", 0, Adult(20m, 45m), Child(30m, 70m, 30, 6 * Y)),
            N("Monocytes", "%", 0, R(2m, 10m)),
            N("Eosinophils", "%", 0, R(1m, 6m)),
            N("Basophils", "%", 0, R(0m, 1m)),
            NO("Absolute neutrophil count", "×10³/µL", 2, Adult(2.0m, 7.5m)),
            NO("Absolute lymphocyte count", "×10³/µL", 2, Adult(1.0m, 4.0m)),
            N("Platelet count", "×10³/µL", 0, R(150m, 400m)),
            NO("MPV", "fL", 1, R(7.5m, 11.5m)),
        }, "Automated haematology analyser");
        yield return new("ESR", "Erythrocyte Sedimentation Rate (ESR)", "Haematology", "BLD", new[]
        {
            N("ESR (1st hour)", "mm/hr", 0, Adult(0m, 15m, M), Adult(0m, 20m, F), R(0m, 10m, null, 0, 18 * Y)),
        }, "Westergren");
        yield return new("PBF", "Peripheral Blood Film", "Haematology", "BLD", new[]
        {
            T("RBC morphology", "Normocytic normochromic"), T("WBC morphology", "Normal"), T("Platelets on film", "Adequate"),
            O("Malaria parasites", Seen, "Not seen"), O("Abnormal / immature cells", Seen, "Not seen"), TO("Impression"),
        }, "Leishman stain – reviewed by the pathologist");
        yield return new("RETIC", "Reticulocyte Count", "Haematology", "BLD", new[] { N("Reticulocytes", "%", 1, Adult(0.5m, 2.5m), Child(2.0m, 6.0m, 0, 30)) });
        yield return new("AEC", "Absolute Eosinophil Count", "Haematology", "BLD", new[] { N("Absolute eosinophil count", "cells/µL", 0, R(40m, 440m)) });
        yield return new("HB-ELP", "Haemoglobin Electrophoresis / HPLC", "Haematology", "BLD", new[]
        {
            N("HbA", "%", 1, Adult(95.0m, 98.0m)), N("HbA2", "%", 1, Adult(1.5m, 3.5m)), N("HbF", "%", 1, Adult(0m, 2.0m)), TO("Abnormal haemoglobin variant", "Not detected"), T("Interpretation"),
        }, "HPLC / electrophoresis", null, "HbA2 > 3.5 % suggests beta-thalassaemia trait (interpret with indices and iron status).");
        yield return new("SICKLE", "Sickling Test", "Haematology", "BLD", new[] { Q("Sickling", PosNeg, "Negative") });
        yield return new("G6PD", "G6PD Screening", "Haematology", "BLD", new[] { Q("G6PD activity", "Normal|Deficient|Intermediate", "Normal") });
        yield return new("IRON", "Iron Profile", "Haematology", "SER", new[]
        {
            N("Serum iron", "µg/dL", 0, Adult(65m, 175m, M), Adult(50m, 170m, F)), N("TIBC", "µg/dL", 0, R(250m, 450m)),
            N("Transferrin saturation", "%", 0, R(20m, 50m)), N("Serum ferritin", "ng/mL", 1, Adult(24m, 336m, M), Adult(11m, 307m, F)),
        }, null, "Fasting morning sample preferred.");
        yield return new("FERR", "Serum Ferritin", "Haematology", "SER", new[] { N("Ferritin", "ng/mL", 1, Adult(24m, 336m, M), Adult(11m, 307m, F)) });

        // =============================================================== Coagulation
        yield return new("PT-INR", "Prothrombin Time (PT / INR)", "Coagulation / Hemostasis", "CIT", new[]
        {
            N("PT (patient)", "seconds", 1, R(11.0m, 13.5m)), NO("PT (control)", "seconds", 1), N("INR", null, 2, R(0.8m, 1.2m)),
        }, "Clotting (citrated plasma)", "Fill the citrate tube exactly to the mark.", "Therapeutic INR on warfarin is usually 2.0–3.0 (as decided by the treating doctor).");
        yield return new("APTT", "Activated Partial Thromboplastin Time (APTT)", "Coagulation / Hemostasis", "CIT", new[]
        {
            N("APTT (patient)", "seconds", 1, R(25m, 35m)), NO("APTT (control)", "seconds", 1),
        }, "Clotting (citrated plasma)");
        yield return new("FIBRIN", "Fibrinogen", "Coagulation / Hemostasis", "CIT", new[] { N("Fibrinogen", "mg/dL", 0, R(200m, 400m)) });
        yield return new("DDIMER", "D-Dimer", "Coagulation / Hemostasis", "CIT", new[] { N("D-Dimer", "µg/mL FEU", 2, R(null, 0.50m)) }, null, null,
            "Age-adjusted cut-off over 50 years: age × 0.01 µg/mL FEU (per local protocol).");
        yield return new("BT-CT", "Bleeding Time & Clotting Time", "Coagulation / Hemostasis", "OTH", new[]
        {
            N("Bleeding time", "minutes", 1, R(2m, 7m)), N("Clotting time", "minutes", 1, R(5m, 11m)),
        }, "Ivy / capillary method");

        // =============================================================== Biochemistry
        yield return new("FBS", "Fasting Blood Sugar (FBS)", "Biochemistry", "PLS", new[] { N("Glucose (fasting)", "mg/dL", 0, R(70m, 99m), Preg(null, 92m)) }, "Glucose oxidase", "8–10 hours fasting.",
            "100–125 mg/dL: impaired fasting glucose; ≥ 126 mg/dL on two occasions: consistent with diabetes (ADA).");
        yield return new("RBS", "Random Blood Sugar (RBS)", "Biochemistry", "PLS", new[] { N("Glucose (random)", "mg/dL", 0, R(70m, 140m)) }, "Glucose oxidase", null,
            "≥ 200 mg/dL with symptoms is consistent with diabetes (ADA).");
        yield return new("PPBS", "Blood Sugar 2 Hours After Meal", "Biochemistry", "PLS", new[] { N("Glucose (2 h post-prandial)", "mg/dL", 0, R(null, 140m)) });
        yield return new("OGTT", "Oral Glucose Tolerance Test (75 g)", "Biochemistry", "PLS", new[]
        {
            N("Glucose fasting", "mg/dL", 0, R(70m, 99m), Preg(null, 92m)), NO("Glucose 1 hour", "mg/dL", 0, Preg(null, 180m)), N("Glucose 2 hours", "mg/dL", 0, R(null, 140m), Preg(null, 153m)),
        }, null, "Fasting sample, then 75 g glucose; samples at 1 and 2 hours.", "Pregnancy limits shown are the IADPSG / WHO 2013 GDM criteria.");
        yield return new("HBA1C-H", "Glycated Haemoglobin (HbA1c)", "Biochemistry", "BLD", new[] { N("HbA1c", "%", 1, R(4.0m, 5.6m)), NO("Estimated average glucose", "mg/dL", 0) },
            "HPLC / immunoassay", null, "5.7–6.4 %: increased risk (pre-diabetes); ≥ 6.5 %: consistent with diabetes (ADA).");
        yield return new("LFT-FULL", "Liver Function Tests (LFT)", "Biochemistry", "SER", new[]
        {
            N("Bilirubin total", "mg/dL", 2, R(0.1m, 1.2m)), N("Bilirubin direct", "mg/dL", 2, R(0m, 0.3m)), NO("Bilirubin indirect", "mg/dL", 2, R(0.1m, 1.0m)),
            N("ALT (SGPT)", "U/L", 0, Adult(7m, 56m)), N("AST (SGOT)", "U/L", 0, Adult(10m, 40m)),
            N("Alkaline phosphatase (ALP)", "U/L", 0, Adult(44m, 147m), Child(100m, 390m, 30, 12 * Y), Child(100m, 450m, 12 * Y, 18 * Y)),
            N("Gamma GT (GGT)", "U/L", 0, Adult(8m, 61m, M), Adult(5m, 36m, F)),
            N("Total protein", "g/dL", 1, R(6.0m, 8.3m)), N("Albumin", "g/dL", 1, R(3.5m, 5.0m)), NO("Globulin", "g/dL", 1, R(2.0m, 3.5m)), NO("A/G ratio", null, 2, R(1.0m, 2.2m)),
        });
        yield return new("BILI", "Serum Bilirubin (Total & Direct)", "Biochemistry", "SER", new[]
        {
            N("Bilirubin total", "mg/dL", 2, R(0.1m, 1.2m)), N("Bilirubin direct", "mg/dL", 2, R(0m, 0.3m)), NO("Bilirubin indirect", "mg/dL", 2, R(0.1m, 1.0m)),
        }, null, "Protect the sample from light.", "Neonatal bilirubin is interpreted on the hour-specific nomogram by the treating doctor.");
        yield return new("ALT", "ALT (SGPT)", "Biochemistry", "SER", new[] { N("ALT (SGPT)", "U/L", 0, Adult(7m, 56m)) });
        yield return new("AST", "AST (SGOT)", "Biochemistry", "SER", new[] { N("AST (SGOT)", "U/L", 0, Adult(10m, 40m)) });
        yield return new("ALP", "Alkaline Phosphatase (ALP)", "Biochemistry", "SER", new[] { N("ALP", "U/L", 0, Adult(44m, 147m), Child(100m, 390m, 30, 12 * Y), Child(100m, 450m, 12 * Y, 18 * Y)) });
        yield return new("GGT", "Gamma GT (GGT)", "Biochemistry", "SER", new[] { N("GGT", "U/L", 0, Adult(8m, 61m, M), Adult(5m, 36m, F)) });
        yield return new("TPA", "Total Protein & Albumin", "Biochemistry", "SER", new[]
        {
            N("Total protein", "g/dL", 1, R(6.0m, 8.3m)), N("Albumin", "g/dL", 1, R(3.5m, 5.0m)), NO("Globulin", "g/dL", 1, R(2.0m, 3.5m)), NO("A/G ratio", null, 2, R(1.0m, 2.2m)),
        });
        yield return new("RFT-FULL", "Renal Function Tests (RFT)", "Biochemistry", "SER", new[]
        {
            N("Blood urea", "mg/dL", 0, Adult(15m, 45m), Child(10m, 40m, 30, 18 * Y)), NO("BUN", "mg/dL", 0, Adult(7m, 20m)),
            N("Serum creatinine", "mg/dL", 2, Adult(0.7m, 1.3m, M), Adult(0.6m, 1.1m, F), Child(0.2m, 0.4m, 30, 3 * Y), Child(0.3m, 0.7m, 3 * Y, 12 * Y), Child(0.5m, 1.0m, 12 * Y, 18 * Y)),
            N("Uric acid", "mg/dL", 1, Adult(3.4m, 7.0m, M), Adult(2.4m, 6.0m, F)),
            NO("Sodium", "mmol/L", 0, R(135m, 145m)), NO("Potassium", "mmol/L", 1, R(3.5m, 5.1m)), NO("Chloride", "mmol/L", 0, R(98m, 107m)),
            NO("eGFR", "mL/min/1.73 m²", 0, Adult(90m, null)),
        });
        yield return new("UREA", "Blood Urea", "Biochemistry", "SER", new[] { N("Blood urea", "mg/dL", 0, Adult(15m, 45m), Child(10m, 40m, 30, 18 * Y)) });
        yield return new("CREAT", "Serum Creatinine", "Biochemistry", "SER", new[]
        {
            N("Creatinine", "mg/dL", 2, Adult(0.7m, 1.3m, M), Adult(0.6m, 1.1m, F), Child(0.2m, 0.4m, 30, 3 * Y), Child(0.3m, 0.7m, 3 * Y, 12 * Y), Child(0.5m, 1.0m, 12 * Y, 18 * Y)),
        });
        yield return new("URIC", "Serum Uric Acid", "Biochemistry", "SER", new[] { N("Uric acid", "mg/dL", 1, Adult(3.4m, 7.0m, M), Adult(2.4m, 6.0m, F)) });
        yield return new("LIPID-FULL", "Lipid Profile", "Biochemistry", "SER", new[]
        {
            N("Total cholesterol", "mg/dL", 0, R(null, 200m)), N("Triglycerides", "mg/dL", 0, R(null, 150m)),
            N("HDL cholesterol", "mg/dL", 0, R(40m, null, M), R(50m, null, F)), N("LDL cholesterol", "mg/dL", 0, R(null, 100m)),
            NO("VLDL cholesterol", "mg/dL", 0, R(5m, 40m)), NO("Total cholesterol / HDL ratio", null, 1, R(null, 5.0m)),
        }, null, "10–12 hours fasting.", "Targets depend on the patient's cardiovascular risk (treating doctor).");
        yield return new("AMYL", "Serum Amylase", "Biochemistry", "SER", new[] { N("Amylase", "U/L", 0, R(30m, 110m)) });
        yield return new("LIPASE", "Serum Lipase", "Biochemistry", "SER", new[] { N("Lipase", "U/L", 0, R(13m, 60m)) }, null, null, "Method dependent – confirm with your analyser.");
        yield return new("LDH", "Lactate Dehydrogenase (LDH)", "Biochemistry", "SER", new[] { N("LDH", "U/L", 0, Adult(140m, 280m)) });
        yield return new("CK", "Creatine Kinase (CK / CPK)", "Biochemistry", "SER", new[] { N("CK total", "U/L", 0, Adult(39m, 308m, M), Adult(26m, 192m, F)) });
        yield return new("CA", "Serum Calcium", "Biochemistry", "SER", new[] { N("Calcium (total)", "mg/dL", 1, R(8.5m, 10.5m), Child(7.6m, 10.4m, 0, 30)) });
        yield return new("ICA", "Ionised Calcium", "Biochemistry", "SER", new[] { N("Ionised calcium", "mg/dL", 2, R(4.6m, 5.3m)) });
        yield return new("MG", "Serum Magnesium", "Biochemistry", "SER", new[] { N("Magnesium", "mg/dL", 1, R(1.7m, 2.2m)) });
        yield return new("PHOS", "Serum Phosphorus", "Biochemistry", "SER", new[] { N("Phosphorus (inorganic)", "mg/dL", 1, Adult(2.5m, 4.5m), Child(4.0m, 7.0m, 30, 12 * Y)) });
        yield return new("BONE", "Bone Profile (Ca, PO4, ALP)", "Biochemistry", "SER", new[]
        {
            N("Calcium (total)", "mg/dL", 1, R(8.5m, 10.5m)), N("Phosphorus (inorganic)", "mg/dL", 1, Adult(2.5m, 4.5m), Child(4.0m, 7.0m, 30, 12 * Y)),
            N("Alkaline phosphatase (ALP)", "U/L", 0, Adult(44m, 147m), Child(100m, 390m, 30, 12 * Y)),
        });
        yield return new("CRP-Q", "C-Reactive Protein (CRP, quantitative)", "Biochemistry", "SER", new[] { N("CRP", "mg/L", 1, R(null, 6m)) });
        yield return new("HSCRP", "High-Sensitivity CRP (hs-CRP)", "Biochemistry", "SER", new[] { N("hs-CRP", "mg/L", 2, R(null, 1.0m)) }, null, null,
            "Cardiovascular risk: < 1 low, 1–3 average, > 3 mg/L high.");
        yield return new("PCT", "Procalcitonin", "Biochemistry", "SER", new[] { N("Procalcitonin", "ng/mL", 2, R(null, 0.5m)) });
        yield return new("AMMON", "Plasma Ammonia", "Special Chemistry", "BLD", new[] { N("Ammonia", "µmol/L", 0, Adult(11m, 51m)) }, null, "Transport on ice; analyse within 30 minutes.");
        yield return new("LACT", "Plasma Lactate", "Special Chemistry", "PLS", new[] { N("Lactate", "mmol/L", 1, R(0.5m, 2.2m)) });
        yield return new("OSMO", "Serum Osmolality", "Special Chemistry", "SER", new[] { N("Osmolality", "mOsm/kg", 0, R(275m, 295m)) });
        yield return new("CERUL", "Ceruloplasmin", "Special Chemistry", "SER", new[] { N("Ceruloplasmin", "mg/dL", 0, Adult(20m, 35m)) });
        yield return new("HCY", "Homocysteine", "Special Chemistry", "SER", new[] { N("Homocysteine", "µmol/L", 1, Adult(5m, 15m)) });
        yield return new("HAPTO", "Haptoglobin", "Special Chemistry", "SER", new[] { N("Haptoglobin", "mg/dL", 0, R(30m, 200m)) });
        yield return new("SPE", "Serum Protein Electrophoresis", "Special Chemistry", "SER", new[]
        {
            N("Albumin", "g/dL", 2, R(3.5m, 5.0m)), N("Alpha-1 globulin", "g/dL", 2, R(0.1m, 0.3m)), N("Alpha-2 globulin", "g/dL", 2, R(0.6m, 1.0m)),
            N("Beta globulin", "g/dL", 2, R(0.7m, 1.2m)), N("Gamma globulin", "g/dL", 2, R(0.7m, 1.6m)), O("M-band (paraprotein)", Detected, "Not Detected"), TO("Interpretation"),
        });

        // =============================================================== Electrolytes & ABG
        yield return new("ELEC", "Serum Electrolytes", "Electrolytes", "SER", new[]
        {
            N("Sodium (Na⁺)", "mmol/L", 0, R(135m, 145m)), N("Potassium (K⁺)", "mmol/L", 1, R(3.5m, 5.1m)), N("Chloride (Cl⁻)", "mmol/L", 0, R(98m, 107m)),
            NO("Bicarbonate (HCO₃⁻)", "mmol/L", 0, R(22m, 29m)), NO("Anion gap", "mmol/L", 0, R(8m, 16m)),
        }, "Ion-selective electrode", "Avoid haemolysis (falsely high potassium).");
        yield return new("ABG", "Arterial Blood Gases (ABG)", "Arterial Blood Gases (ABG)", "ABG", new[]
        {
            N("pH", null, 2, R(7.35m, 7.45m)), N("pCO₂", "mmHg", 0, R(35m, 45m)), N("pO₂", "mmHg", 0, R(80m, 100m)), N("HCO₃⁻ (actual)", "mmol/L", 1, R(22m, 26m)),
            N("Base excess", "mmol/L", 1, R(-2m, 2m)), N("SaO₂", "%", 0, R(95m, 100m)), NO("Lactate", "mmol/L", 1, R(0.5m, 2.0m)), NO("FiO₂ at sampling", "%", 0), TO("Interpretation"),
        }, "Blood gas analyser", "Arterial sample in a heparinised syringe, no air bubbles, analyse within 15 minutes (on ice).");

        // =============================================================== Vitamins & trace elements
        yield return new("VITD", "Vitamin D (25-OH)", "Vitamins & Trace Elements", "SER", new[] { N("25-hydroxy vitamin D", "ng/mL", 1, R(30m, 100m)) }, null, null,
            "< 20 ng/mL deficiency; 20–29 insufficiency; 30–100 sufficiency (Endocrine Society).");
        yield return new("B12", "Vitamin B12", "Vitamins & Trace Elements", "SER", new[] { N("Vitamin B12", "pg/mL", 0, R(200m, 900m)) });
        yield return new("FOLATE", "Serum Folate", "Vitamins & Trace Elements", "SER", new[] { N("Folate", "ng/mL", 1, R(3.0m, 17.0m)) });
        yield return new("ZINC", "Serum Zinc", "Vitamins & Trace Elements", "SER", new[] { N("Zinc", "µg/dL", 0, R(60m, 120m)) });
        yield return new("COPPER", "Serum Copper", "Vitamins & Trace Elements", "SER", new[] { N("Copper", "µg/dL", 0, Adult(70m, 140m)) });

        // =============================================================== Endocrinology / hormones
        yield return new("TFT-FULL", "Thyroid Function Tests (TFT)", "Endocrinology", "SER", new[]
        {
            N("TSH", "µIU/mL", 2, R(0.4m, 4.0m), Preg(0.1m, 2.5m)), N("Free T4", "ng/dL", 2, R(0.8m, 1.8m)), N("Free T3", "pg/mL", 2, R(2.3m, 4.2m)),
        }, "Immunoassay (CLIA / ECLIA)", null, "Pregnancy TSH shown is the commonly used first-trimester range; laboratories should use trimester-specific ranges.");
        yield return new("TSH", "TSH", "Endocrinology", "SER", new[] { N("TSH", "µIU/mL", 2, R(0.4m, 4.0m), Preg(0.1m, 2.5m)) });
        yield return new("T3T4", "Total T3 & T4", "Endocrinology", "SER", new[] { N("Total T3", "ng/dL", 0, R(80m, 200m)), N("Total T4", "µg/dL", 1, R(5.0m, 12.0m)) });
        yield return new("ATPO", "Anti-TPO Antibodies", "Autoimmune Testing", "SER", new[] { N("Anti-TPO", "IU/mL", 1, R(null, 35m)) });
        yield return new("PRL", "Prolactin", "Hormonal Assays", "SER", new[] { N("Prolactin", "ng/mL", 1, Adult(2m, 18m, M), Adult(2m, 29m, F), Preg(10m, 209m)) }, null,
            "Morning sample, at rest (stress raises prolactin).");
        yield return new("LH", "Luteinising Hormone (LH)", "Hormonal Assays", "SER", new[]
        {
            N("LH", "mIU/mL", 1, Adult(1.5m, 9.3m, M), new LabReferenceRange { Gender = F, MinAgeDays = 18 * Y, NormalText = "Follicular 1.9–12.5; mid-cycle 8.7–76.3; luteal 0.5–16.9; post-menopause 15.9–54.0" }),
        }, null, "Note the day of the menstrual cycle.");
        yield return new("FSH", "Follicle Stimulating Hormone (FSH)", "Hormonal Assays", "SER", new[]
        {
            N("FSH", "mIU/mL", 1, Adult(1.4m, 18.1m, M), new LabReferenceRange { Gender = F, MinAgeDays = 18 * Y, NormalText = "Follicular 2.5–10.2; mid-cycle 3.4–33.4; luteal 1.5–9.1; post-menopause 23–116" }),
        }, null, "Note the day of the menstrual cycle.");
        yield return new("LHFSH", "LH, FSH & Prolactin (Hormone Profile)", "Hormonal Assays", "SER", new[]
        {
            N("LH", "mIU/mL", 1, Adult(1.5m, 9.3m, M), new LabReferenceRange { Gender = F, MinAgeDays = 18 * Y, NormalText = "Follicular 1.9–12.5; mid-cycle 8.7–76.3; luteal 0.5–16.9" }),
            N("FSH", "mIU/mL", 1, Adult(1.4m, 18.1m, M), new LabReferenceRange { Gender = F, MinAgeDays = 18 * Y, NormalText = "Follicular 2.5–10.2; mid-cycle 3.4–33.4; luteal 1.5–9.1" }),
            N("Prolactin", "ng/mL", 1, Adult(2m, 18m, M), Adult(2m, 29m, F)),
        });
        yield return new("TESTO", "Testosterone (Total)", "Hormonal Assays", "SER", new[] { N("Testosterone", "ng/dL", 0, Adult(270m, 1070m, M), Adult(15m, 70m, F)) }, null, "Morning sample (7–10 am).");
        yield return new("E2", "Estradiol (E2)", "Hormonal Assays", "SER", new[]
        {
            N("Estradiol", "pg/mL", 0, Adult(10m, 40m, M), new LabReferenceRange { Gender = F, MinAgeDays = 18 * Y, NormalText = "Follicular 30–120; ovulatory 130–370; luteal 70–250; post-menopause < 30" }),
        });
        yield return new("PROG", "Progesterone", "Hormonal Assays", "SER", new[]
        {
            N("Progesterone", "ng/mL", 2, Adult(0.1m, 0.3m, M), new LabReferenceRange { Gender = F, MinAgeDays = 18 * Y, NormalText = "Follicular < 1; luteal 5–20; post-menopause < 1" }),
        });
        yield return new("BHCG", "Beta hCG (Quantitative)", "Hormonal Assays", "SER", new[] { N("Beta hCG", "mIU/mL", 1, R(null, 5m)) }, null, null,
            "Non-pregnant < 5 mIU/mL; pregnancy values depend on the gestational age.");
        yield return new("CORT", "Serum Cortisol", "Hormonal Assays", "SER", new[] { N("Cortisol (8 am)", "µg/dL", 1, R(6m, 23m)) }, null, "Morning sample (8 am) unless stated.");
        yield return new("INSUL", "Fasting Insulin", "Hormonal Assays", "SER", new[] { N("Insulin (fasting)", "µIU/mL", 1, R(2.6m, 24.9m)) });
        yield return new("PTH", "Parathyroid Hormone (Intact PTH)", "Hormonal Assays", "SER", new[] { N("PTH (intact)", "pg/mL", 1, R(15m, 65m)) }, null, "EDTA plasma or serum, on ice.");
        yield return new("AMH", "Anti-Müllerian Hormone (AMH)", "Hormonal Assays", "SER", new[] { N("AMH", "ng/mL", 2, Adult(1.0m, 3.5m, F)) }, null, null, "Age dependent – interpret with the patient's age.");
        yield return new("GH", "Growth Hormone (GH)", "Hormonal Assays", "SER", new[] { N("Growth hormone", "ng/mL", 2, Adult(0.0m, 3.0m, M), Adult(0.0m, 8.0m, F)) });

        // =============================================================== Cardiac markers
        yield return new("TROP-I", "Troponin I", "Cardiac Markers", "SER", new[] { N("Troponin I", "ng/mL", 3, R(null, 0.04m)) }, "Immunoassay", null,
            "The 99th-percentile cut-off is assay specific – use the analyser's value.");
        yield return new("TROP-T", "Troponin T (hs)", "Cardiac Markers", "SER", new[] { N("hs-Troponin T", "ng/L", 0, R(null, 14m)) });
        yield return new("TROP-Q", "Troponin I (Rapid, qualitative)", "Cardiac Markers", "SER", new[] { Q("Troponin I", PosNeg, "Negative") }, "Immunochromatography");
        yield return new("CKMB", "CK-MB", "Cardiac Markers", "SER", new[] { N("CK-MB", "U/L", 0, R(null, 25m)) });
        yield return new("MYO", "Myoglobin", "Cardiac Markers", "SER", new[] { N("Myoglobin", "ng/mL", 0, Adult(25m, 72m)) });
        yield return new("NTBNP", "NT-proBNP", "Cardiac Markers", "SER", new[] { N("NT-proBNP", "pg/mL", 0, R(null, 125m)) }, null, null,
            "Age-related cut-offs apply (e.g. < 450 pg/mL over 75 years).");
        yield return new("BNP", "BNP", "Cardiac Markers", "PLS", new[] { N("BNP", "pg/mL", 0, R(null, 100m)) });

        // =============================================================== Tumor markers
        yield return new("PSA", "PSA (Total)", "Tumor Markers", "SER", new[] { N("PSA total", "ng/mL", 2, Adult(null, 4.0m, M)) });
        yield return new("FPSA", "PSA (Free & Total)", "Tumor Markers", "SER", new[]
        {
            N("PSA total", "ng/mL", 2, Adult(null, 4.0m, M)), N("PSA free", "ng/mL", 2), NO("Free / total PSA ratio", "%", 0, R(25m, null)),
        });
        yield return new("CEA", "CEA", "Tumor Markers", "SER", new[] { N("CEA", "ng/mL", 1, R(null, 5.0m)) }, null, null, "Smokers may have values up to ~5–10 ng/mL.");
        yield return new("AFP", "Alpha-Fetoprotein (AFP)", "Tumor Markers", "SER", new[] { N("AFP", "ng/mL", 1, R(null, 10m)) });
        yield return new("CA125", "CA-125", "Tumor Markers", "SER", new[] { N("CA-125", "U/mL", 1, R(null, 35m)) });
        yield return new("CA199", "CA 19-9", "Tumor Markers", "SER", new[] { N("CA 19-9", "U/mL", 1, R(null, 37m)) });
        yield return new("CA153", "CA 15-3", "Tumor Markers", "SER", new[] { N("CA 15-3", "U/mL", 1, R(null, 30m)) });

        // =============================================================== Serology
        yield return new("HBSAG", "HBsAg (Hepatitis B surface antigen)", "Serology", "SER", new[] { Q("HBsAg", Reactive, "Non-Reactive") }, "ICT / CLIA");
        yield return new("AHCV", "Anti-HCV (Hepatitis C antibodies)", "Serology", "SER", new[] { Q("Anti-HCV", Reactive, "Non-Reactive") }, "ICT / CLIA");
        yield return new("HIV", "HIV 1 & 2 Antibodies (Screening)", "Serology", "SER", new[] { Q("HIV 1 & 2", Reactive, "Non-Reactive") }, "ICT / CLIA", null,
            "A reactive screening result must be confirmed by the national testing algorithm.");
        yield return new("HEPPAN", "Hepatitis Screening (HBsAg, Anti-HCV)", "Serology", "SER", new[] { Q("HBsAg", Reactive, "Non-Reactive"), Q("Anti-HCV", Reactive, "Non-Reactive") });
        yield return new("HBEAG", "HBeAg", "Serology", "SER", new[] { Q("HBeAg", Reactive, "Non-Reactive") });
        yield return new("AHBS", "Anti-HBs (antibody titre)", "Serology", "SER", new[] { N("Anti-HBs", "mIU/mL", 1, R(10m, null)) }, null, null, "≥ 10 mIU/mL is considered protective.");
        yield return new("AHBC", "Anti-HBc (Total)", "Serology", "SER", new[] { Q("Anti-HBc", Reactive, "Non-Reactive") });
        yield return new("HAVIGM", "Hepatitis A IgM", "Serology", "SER", new[] { Q("Anti-HAV IgM", Reactive, "Non-Reactive") });
        yield return new("HEVIGM", "Hepatitis E IgM", "Serology", "SER", new[] { Q("Anti-HEV IgM", Reactive, "Non-Reactive") });
        yield return new("VDRL", "VDRL / RPR", "Serology", "SER", new[] { Q("VDRL", Reactive, "Non-Reactive"), TO("Titre") });
        yield return new("TPHA", "TPHA", "Serology", "SER", new[] { Q("TPHA", Reactive, "Non-Reactive") });
        yield return new("TYPHI", "Typhidot (S. Typhi IgM / IgG)", "Serology", "SER", new[] { Q("IgM", PosNeg, "Negative"), Q("IgG", PosNeg, "Negative") }, "Immunochromatography");
        yield return new("WIDAL", "Widal Test", "Serology", "SER", new[]
        {
            T("S. Typhi O", "< 1:80"), T("S. Typhi H", "< 1:80"), TO("S. Paratyphi AH", "< 1:80"), TO("S. Paratyphi BH", "< 1:80"),
        }, "Tube / slide agglutination", null, "A single titre has limited value; a four-fold rise is significant.");
        yield return new("DENGUE", "Dengue NS1, IgM & IgG", "Serology", "SER", new[] { Q("NS1 antigen", PosNeg, "Negative"), Q("IgM", PosNeg, "Negative"), Q("IgG", PosNeg, "Negative") });
        yield return new("DNS1", "Dengue NS1 Antigen", "Serology", "SER", new[] { Q("NS1 antigen", PosNeg, "Negative") });
        yield return new("HPYAB", "H. pylori Antibodies", "Serology", "SER", new[] { Q("H. pylori antibodies", PosNeg, "Negative") });
        yield return new("BRUC", "Brucella Antibodies", "Serology", "SER", new[] { Q("Brucella", PosNeg, "Negative"), TO("Titre") });
        yield return new("CRP-L", "CRP (Latex, qualitative)", "Serology", "SER", new[] { Q("CRP", PosNeg, "Negative"), TO("Titre") });
        yield return new("RA", "Rheumatoid Factor (RA Factor)", "Serology", "SER", new[] { N("Rheumatoid factor", "IU/mL", 1, R(null, 14m)) });
        yield return new("ASO", "ASO Titre", "Serology", "SER", new[] { N("ASO", "IU/mL", 0, Adult(null, 200m), Child(null, 250m, 2 * Y, 18 * Y)) });
        yield return new("TORCH", "TORCH Profile (IgG & IgM)", "Serology", "SER", new[]
        {
            Q("Toxoplasma IgG", PosNeg, "Negative"), Q("Toxoplasma IgM", PosNeg, "Negative"), Q("Rubella IgG", PosNeg, "Negative"), Q("Rubella IgM", PosNeg, "Negative"),
            Q("CMV IgG", PosNeg, "Negative"), Q("CMV IgM", PosNeg, "Negative"), Q("HSV IgG", PosNeg, "Negative"), Q("HSV IgM", PosNeg, "Negative"),
        });
        yield return new("COVAG", "COVID-19 Rapid Antigen", "Serology", "SWB", new[] { Q("SARS-CoV-2 antigen", PosNeg, "Negative") });
        yield return new("MALICT", "Malaria Antigen (ICT)", "Parasitology", "BLD", new[] { Q("P. falciparum antigen", PosNeg, "Negative"), Q("P. vivax / pan antigen", PosNeg, "Negative") });
        yield return new("UPT", "Pregnancy Test (Urine)", "Clinical Pathology", "URN", new[] { Q("Urine hCG", PosNeg, "Negative") }, "Immunochromatography", "Early-morning urine preferred.");
        yield return new("SPT-HCG", "Pregnancy Test (Serum, qualitative)", "Serology", "SER", new[] { Q("Serum hCG", PosNeg, "Negative") });

        // =============================================================== Immunology & autoimmune
        yield return new("ANA", "Antinuclear Antibodies (ANA)", "Autoimmune Testing", "SER", new[] { Q("ANA", PosNeg, "Negative"), TO("Titre / pattern") }, "IIF / ELISA");
        yield return new("DSDNA", "Anti-dsDNA", "Autoimmune Testing", "SER", new[] { N("Anti-dsDNA", "IU/mL", 0, R(null, 30m)) });
        yield return new("ACCP", "Anti-CCP", "Autoimmune Testing", "SER", new[] { N("Anti-CCP", "U/mL", 1, R(null, 20m)) });
        yield return new("ANCA", "ANCA (c-ANCA / p-ANCA)", "Autoimmune Testing", "SER", new[] { Q("c-ANCA (PR3)", PosNeg, "Negative"), Q("p-ANCA (MPO)", PosNeg, "Negative") });
        yield return new("TTG", "Anti-tTG IgA (Coeliac)", "Autoimmune Testing", "SER", new[] { N("Anti-tTG IgA", "U/mL", 1, R(null, 20m)) });
        yield return new("C3C4", "Complement C3 & C4", "Immunology", "SER", new[] { N("C3", "mg/dL", 0, R(90m, 180m)), N("C4", "mg/dL", 0, R(10m, 40m)) });
        yield return new("IGS", "Immunoglobulins (IgG, IgA, IgM)", "Immunology", "SER", new[]
        {
            N("IgG", "mg/dL", 0, Adult(700m, 1600m)), N("IgA", "mg/dL", 0, Adult(70m, 400m)), N("IgM", "mg/dL", 0, Adult(40m, 230m)),
        });
        yield return new("IGE", "Total IgE", "Allergy Testing", "SER", new[] { N("Total IgE", "IU/mL", 0, Adult(null, 100m)) }, null, null, "Children's ranges vary with age.");
        yield return new("SIGE", "Specific IgE Allergy Panel", "Allergy Testing", "SER", new[] { T("Allergens tested"), T("Results (class / kU/L)", "Class 0 (< 0.35 kU/L)"), TO("Interpretation") });

        // =============================================================== Molecular / PCR
        yield return new("HCVPCR", "HCV RNA PCR (Quantitative)", "Molecular Biology / PCR", "PLS", new[] { Q("HCV RNA", Detected, "Not Detected"), NO("Viral load", "IU/mL", 0) }, "Real-time PCR");
        yield return new("HCVQL", "HCV RNA PCR (Qualitative)", "Molecular Biology / PCR", "PLS", new[] { Q("HCV RNA", Detected, "Not Detected") }, "Real-time PCR");
        yield return new("HBVPCR", "HBV DNA PCR (Quantitative)", "Molecular Biology / PCR", "PLS", new[] { Q("HBV DNA", Detected, "Not Detected"), NO("Viral load", "IU/mL", 0) }, "Real-time PCR");
        yield return new("HIVPCR", "HIV-1 RNA PCR (Viral Load)", "Molecular Biology / PCR", "PLS", new[] { Q("HIV-1 RNA", Detected, "Not Detected"), NO("Viral load", "copies/mL", 0) }, "Real-time PCR");
        yield return new("MTBPCR", "MTB / Rifampicin Resistance (GeneXpert)", "Molecular Biology / PCR", "SPT", new[]
        {
            Q("M. tuberculosis", Detected, "Not Detected"), OO("Rifampicin resistance", "Not Detected|Detected|Indeterminate", "Not Detected"),
        }, "Cartridge-based NAAT");
        yield return new("COVPCR", "SARS-CoV-2 RT-PCR", "Molecular Biology / PCR", "SWB", new[] { Q("SARS-CoV-2 RNA", Detected, "Not Detected"), NO("Ct value", null, 1) }, "RT-PCR");
        yield return new("HPV", "HPV DNA (High-risk)", "Molecular Biology / PCR", "SWB", new[] { Q("High-risk HPV DNA", Detected, "Not Detected"), TO("Genotypes") });

        // =============================================================== Microbiology (culture: organism + sensitivity table)
        yield return new("BLDCS", "Blood Culture & Sensitivity", "Microbiology", "BC", new[] { Culture() }, "Automated blood culture", "Collect before antibiotics; two sets from separate sites.", null, 120);
        yield return new("URNCS", "Urine Culture & Sensitivity", "Microbiology", "URN", new[] { Culture(), NO("Colony count", "CFU/mL", 0) }, null, "Clean-catch midstream urine in a sterile container.", "≥ 10⁵ CFU/mL of a single organism is usually significant.", 48);
        yield return new("PUSCS", "Pus / Wound Culture & Sensitivity", "Microbiology", "SWB", new[] { TO("Gram stain"), Culture() }, null, null, null, 72);
        yield return new("SPTCS", "Sputum Culture & Sensitivity", "Microbiology", "SPT", new[] { TO("Gram stain"), Culture() }, null, "Early-morning deep-cough sample.", null, 72);
        yield return new("STLCS", "Stool Culture & Sensitivity", "Microbiology", "STL", new[] { Culture() }, null, null, null, 72);
        yield return new("HVSCS", "High Vaginal Swab (HVS) Culture & Sensitivity", "Microbiology", "SWB", new[] { TO("Gram stain / wet mount"), Culture() });
        yield return new("THRCS", "Throat Swab Culture & Sensitivity", "Microbiology", "SWB", new[] { Culture() });
        yield return new("CSFCS", "CSF Culture & Sensitivity", "Microbiology", "CSF", new[] { TO("Gram stain"), Culture() });
        yield return new("FLDCS", "Body Fluid Culture & Sensitivity", "Microbiology", "FLD", new[] { TO("Gram stain"), Culture() });
        yield return new("GRAM", "Gram Stain", "Microbiology", "OTH", new[] { T("Gram stain findings"), TO("Pus cells") });
        yield return new("AFB", "AFB Smear (Ziehl-Neelsen)", "Microbiology", "SPT", new[] { Q("Acid-fast bacilli", "Not seen|Scanty|1+|2+|3+", "Not seen") }, "Ziehl-Neelsen stain", "Two early-morning sputum samples.");
        yield return new("KOH", "KOH Mount (Fungal)", "Microbiology", "OTH", new[] { Q("Fungal elements", Seen, "Not seen") });
        yield return new("FUNGCS", "Fungal Culture", "Microbiology", "OTH", new[] { T("Fungal culture", "No fungal growth") }, null, null, null, 21 * 24);

        // =============================================================== Parasitology
        yield return new("MP", "Malaria Parasite (Smear)", "Parasitology", "BLD", new[]
        {
            Q("Malaria parasites", "Not seen|P. vivax seen|P. falciparum seen|Mixed infection", "Not seen"), TO("Stage / parasite density"),
        }, "Thick & thin film (Giemsa)");
        yield return new("MF", "Microfilaria (Smear)", "Parasitology", "BLD", new[] { Q("Microfilaria", Seen, "Not seen") }, null, "Night sample (10 pm – 2 am).");
        yield return new("STOVA", "Stool for Ova & Cysts", "Parasitology", "STL", new[] { Q("Ova", Seen, "Not seen"), Q("Cysts", Seen, "Not seen"), TO("Parasite identified") });

        // =============================================================== Urine analysis
        yield return new("UCE", "Urine Complete Examination (Urine R/E)", "Urine Analysis", "URN", new[]
        {
            O("Colour", "Pale yellow|Yellow|Dark yellow|Amber|Red|Brown|Other", "Pale yellow"), O("Appearance", "Clear|Slightly turbid|Turbid", "Clear"),
            N("pH", null, 1, R(4.5m, 8.0m)), N("Specific gravity", null, 3, R(1.005m, 1.030m)),
            O("Protein", Grade, "Nil"), O("Glucose", Grade, "Nil"), O("Ketones", "Negative|Trace|+|++|+++", "Negative"), O("Blood", "Negative|Trace|+|++|+++", "Negative"),
            O("Bilirubin", PosNeg, "Negative"), O("Urobilinogen", "Normal|Increased", "Normal"), O("Nitrite", PosNeg, "Negative"), O("Leukocyte esterase", "Negative|Trace|+|++|+++", "Negative"),
            T("Pus cells", "0–5 /HPF"), T("Red blood cells", "0–2 /HPF"), T("Epithelial cells", "Few"), OO("Casts", "Nil|Hyaline|Granular|RBC casts|WBC casts", "Nil"),
            OO("Crystals", "Nil|Calcium oxalate|Uric acid|Triple phosphate|Amorphous", "Nil"), OO("Bacteria", "Nil|Few|Moderate|Plenty", "Nil"), OO("Yeast", "Nil|Present", "Nil"),
        }, "Dipstick + microscopy", "Early-morning midstream sample.");
        yield return new("UMALB", "Urine Microalbumin (ACR)", "Urine Analysis", "URN", new[] { N("Albumin / creatinine ratio", "mg/g", 1, R(null, 30m)) }, null, null,
            "30–300 mg/g: moderately increased (microalbuminuria); > 300 mg/g: severely increased.");
        yield return new("U24P", "24-Hour Urine Protein", "Urine Analysis", "U24", new[] { NO("Urine volume", "mL/24 h", 0, R(800m, 2000m)), N("Protein", "mg/24 h", 0, R(null, 150m)) }, null,
            "Discard the first morning urine, collect all urine for 24 hours including the next first-morning sample.");
        yield return new("UPCR", "Urine Protein / Creatinine Ratio", "Urine Analysis", "URN", new[] { N("Protein / creatinine ratio", "mg/mg", 2, R(null, 0.2m)) });
        yield return new("UKET", "Urine Ketones", "Urine Analysis", "URN", new[] { Q("Ketones", "Negative|Trace|+|++|+++", "Negative") });
        yield return new("USUG", "Urine Sugar", "Urine Analysis", "URN", new[] { Q("Glucose", Grade, "Nil") });

        // =============================================================== Stool
        yield return new("SCE", "Stool Complete Examination (Stool R/E)", "Stool Examination", "STL", new[]
        {
            O("Colour", "Brown|Yellow|Green|Black|Clay|Red", "Brown"), O("Consistency", "Formed|Semi-formed|Loose|Watery", "Formed"), O("Mucus", PresentAbsent, "Absent"),
            O("Blood (visible)", PresentAbsent, "Absent"), O("Occult blood", PosNeg, "Negative"), T("Pus cells", "Nil"), T("Red blood cells", "Nil"),
            O("Ova", Seen, "Not seen"), O("Cysts", Seen, "Not seen"), OO("Trophozoites", Seen, "Not seen"), OO("Undigested food / fat globules", "Nil|Present", "Nil"),
        }, "Macroscopy + microscopy");
        yield return new("SOB", "Stool Occult Blood", "Stool Examination", "STL", new[] { Q("Occult blood", PosNeg, "Negative") });
        yield return new("SHPAG", "Stool H. pylori Antigen", "Stool Examination", "STL", new[] { Q("H. pylori antigen", PosNeg, "Negative") });
        yield return new("SRED", "Stool Reducing Substances", "Stool Examination", "STL", new[] { Q("Reducing substances", PosNeg, "Negative") });

        // =============================================================== Body fluids & semen
        yield return new("CSFRE", "CSF Analysis (Routine)", "Body Fluids", "CSF", new[]
        {
            O("Appearance", "Clear|Turbid|Blood-stained|Xanthochromic", "Clear"), N("Protein", "mg/dL", 0, Adult(15m, 45m)), N("Glucose", "mg/dL", 0, R(40m, 70m)),
            N("Total cells", "/µL", 0, Adult(0m, 5m)), TO("Differential count"),
        }, null, "Send to the laboratory immediately; paired blood glucose recommended.");
        yield return new("FLDRE", "Pleural / Ascitic / Synovial Fluid Analysis", "Body Fluids", "FLD", new[]
        {
            T("Fluid type"), T("Appearance"), NO("Protein", "g/dL", 1), NO("Glucose", "mg/dL", 0), NO("LDH", "U/L", 0), NO("Total cells", "/µL", 0), TO("Differential count"), TO("Interpretation"),
        });
        yield return new("SEMEN", "Semen Analysis", "Andrology / Semen Analysis", "SMN", new[]
        {
            NO("Abstinence", "days", 0, R(2m, 7m)), N("Volume", "mL", 1, R(1.4m, null)), NO("pH", null, 1, R(7.2m, null)), O("Liquefaction", "Complete|Incomplete", "Complete"),
            N("Sperm concentration", "million/mL", 1, R(16m, null)), NO("Total sperm count", "million/ejaculate", 0, R(39m, null)), N("Total motility", "%", 0, R(42m, null)),
            N("Progressive motility", "%", 0, R(30m, null)), N("Normal morphology", "%", 0, R(4m, null)), NO("Vitality", "%", 0, R(54m, null)), TO("Pus cells"),
        }, "WHO laboratory manual (6th ed., 2021) lower reference limits", "2–7 days abstinence; deliver within 1 hour at body temperature.");

        // =============================================================== Blood bank
        yield return new("BG", "Blood Group & Rh Type", "Blood Bank / Transfusion Medicine", "BLD", new[] { O("ABO blood group", BloodGroups, null), O("Rh (D)", Rh, null) }, "Forward & reverse grouping");
        yield return new("XMATCH", "Cross Match", "Blood Bank / Transfusion Medicine", "BLD", new[]
        {
            O("Patient blood group", "A Positive|A Negative|B Positive|B Negative|AB Positive|AB Negative|O Positive|O Negative", null),
            T("Donor / bag number"), O("Donor blood group", "A Positive|A Negative|B Positive|B Negative|AB Positive|AB Negative|O Positive|O Negative", null),
            O("Cross-match result", Compatible, "Compatible"),
        });
        yield return new("DCT", "Direct Coombs Test (DAT)", "Blood Bank / Transfusion Medicine", "BLD", new[] { Q("Direct antiglobulin test", PosNeg, "Negative") });
        yield return new("ICT", "Indirect Coombs Test (IAT)", "Blood Bank / Transfusion Medicine", "SER", new[] { Q("Indirect antiglobulin test", PosNeg, "Negative"), TO("Titre") });
        yield return new("ABSCR", "Antibody Screening", "Blood Bank / Transfusion Medicine", "SER", new[] { Q("Irregular antibodies", Detected, "Not Detected") });

        // =============================================================== Histopathology & cytology (narrative reports)
        yield return new("BIOPSY", "Histopathology – Biopsy", "Histopathology", "TIS", new[]
        {
            T("Specimen"), T("Clinical details"), T("Gross description"), T("Microscopic description"), T("Diagnosis"), TO("Comment"),
        }, "Formalin fixed, paraffin embedded (H&E)", "Whole specimen in 10 % formalin (10× its volume).", null, 7 * 24);
        yield return new("PAP", "Pap Smear (Cervical Cytology)", "Cytology", "SWB", new[]
        {
            T("Specimen adequacy", "Satisfactory for evaluation"), O("Interpretation", "Negative for intraepithelial lesion or malignancy (NILM)|ASC-US|ASC-H|LSIL|HSIL|Squamous cell carcinoma|AGC|Other", "Negative for intraepithelial lesion or malignancy (NILM)"),
            TO("Organisms / other findings"), TO("Recommendation"),
        }, "Bethesda System");
        yield return new("FNAC", "FNAC (Fine-Needle Aspiration Cytology)", "Cytology", "OTH", new[] { T("Site"), T("Aspirate"), T("Microscopy"), T("Diagnosis") });
        yield return new("FLCYT", "Fluid Cytology", "Cytology", "FLD", new[] { T("Fluid"), T("Microscopy"), O("Malignant cells", Seen, "Not seen"), TO("Diagnosis") });

        // =============================================================== Toxicology & therapeutic drug monitoring
        yield return new("UDS", "Urine Drug Screen", "Toxicology / Drug Monitoring", "URN", new[]
        {
            Q("Opiates", PosNeg, "Negative"), Q("Cannabinoids (THC)", PosNeg, "Negative"), Q("Benzodiazepines", PosNeg, "Negative"), Q("Amphetamines", PosNeg, "Negative"),
            Q("Cocaine", PosNeg, "Negative"), OO("Barbiturates", PosNeg, "Negative"), OO("Methamphetamine", PosNeg, "Negative"),
        }, "Immunoassay screening", null, "A positive screen should be confirmed by a confirmatory method before clinical or legal use.");
        yield return new("DIGOX", "Serum Digoxin", "Toxicology / Drug Monitoring", "SER", new[] { N("Digoxin", "ng/mL", 2, R(0.8m, 2.0m)) }, null, "At least 6 hours after the last dose.", "Therapeutic range (some protocols target 0.5–0.9 ng/mL in heart failure).");
        yield return new("LITH", "Serum Lithium", "Toxicology / Drug Monitoring", "SER", new[] { N("Lithium", "mmol/L", 2, R(0.6m, 1.2m)) }, null, "12 hours after the last dose.", "Therapeutic range.");
        yield return new("VALP", "Valproic Acid", "Toxicology / Drug Monitoring", "SER", new[] { N("Valproic acid", "µg/mL", 0, R(50m, 100m)) }, null, "Trough sample (before the next dose).", "Therapeutic range.");
        yield return new("PHENY", "Phenytoin", "Toxicology / Drug Monitoring", "SER", new[] { N("Phenytoin", "µg/mL", 1, R(10m, 20m)) }, null, "Trough sample.", "Therapeutic range.");
        yield return new("CARBA", "Carbamazepine", "Toxicology / Drug Monitoring", "SER", new[] { N("Carbamazepine", "µg/mL", 1, R(4m, 12m)) }, null, "Trough sample.", "Therapeutic range.");
        yield return new("PARAC", "Serum Paracetamol", "Toxicology / Drug Monitoring", "SER", new[] { N("Paracetamol", "µg/mL", 0), TO("Hours since ingestion") }, null, null,
            "Interpret on the treatment nomogram according to the time since ingestion (treating doctor).");
    }
}
