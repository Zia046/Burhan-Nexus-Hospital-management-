using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>Common data for printable slips: encounter, patient, branding, paper size, print counter.</summary>
public abstract class SlipModelBase : HmsPageModel
{
    protected readonly IEncounterService Encounters;
    protected readonly IRegistrationSetupService Setup;
    protected readonly DisplayFormatter Formatter;

    protected SlipModelBase(IEncounterService encounters, IRegistrationSetupService setup, DisplayFormatter formatter)
    {
        Encounters = encounters;
        Setup = setup;
        Formatter = formatter;
    }

    public PatientEncounter Encounter { get; private set; } = null!;
    public Patient Patient => Encounter.Patient!;
    public HospitalProfile Hospital { get; private set; } = null!;
    public string Paper { get; private set; } = "A5";
    public bool AutoPrint { get; private set; }
    public int CopyNumber { get; private set; }
    public string AgeText { get; private set; } = "—";
    public string BackUrl { get; private set; } = "/";

    protected async Task<IActionResult> LoadAsync(long id, EncounterType type, string? paper, bool autoprint, Func<RegistrationSettings, string> defaultPaper,
        Func<PatientEncounter, PrintedDocumentType> docType)
    {
        var enc = await Encounters.GetAsync(id);
        if (enc is null || enc.Type != type) return NotFound();
        Encounter = enc;
        Hospital = (await Formatter.ProfileAsync())!;
        var settings = await Setup.GetSettingsAsync();
        Paper = paper?.ToUpperInvariant() is "A4" or "A5" ? paper.ToUpperInvariant() : defaultPaper(settings);
        AutoPrint = autoprint;
        var visitDate = DateOnly.FromDateTime(Formatter.ToLocal(enc.EncounterUtc));
        AgeText = AgeCalculator.Of(enc.Patient!, visitDate)?.ToString() ?? "—";
        BackUrl = type == EncounterType.Opd ? $"/Opd/Visit/{id}" : $"/Emergency/Visit/{id}";
        CopyNumber = await Encounters.RecordPrintAsync(id, docType(enc), Paper);
        return Page();
    }

    public string Guardian =>
        Patient.GuardianName is null ? "—" : $"{RelationLabel(Patient.GuardianRelation)} {Patient.GuardianName}";

    public static string RelationLabel(GuardianRelation? r) => r switch
    {
        GuardianRelation.SonOf => "S/O",
        GuardianRelation.DaughterOf => "D/O",
        GuardianRelation.WifeOf => "W/O",
        _ => ""
    };

    public string GenderText => Patient.Gender switch { Gender.Male => "Male", Gender.Female => "Female", Gender.Other => "Other", _ => "Unknown" };
}
