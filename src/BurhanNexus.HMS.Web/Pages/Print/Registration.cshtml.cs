using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Infrastructure.Printing;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Print;

/// <summary>
/// Update 14: compact thermal (58 / 80 mm) registration receipt of an OPD or emergency visit – printed straight after registration
/// instead of an A4 page. The A5 / A4 doctor's slip is still one click away (toolbar). Printing only reads the stored records.
/// </summary>
[HasPermission(Perm.OpdPrint, Perm.EmergencyPrint)]
public class RegistrationModel : HmsPageModel
{
    private readonly IEncounterService _encounters;
    private readonly DisplayFormatter _fmt;
    private readonly IPrinterProfileService _printers;
    private readonly IRequestContext _ctx;
    private readonly HmsDbContext _db;
    public RegistrationModel(IEncounterService encounters, DisplayFormatter fmt, IPrinterProfileService printers, IRequestContext ctx, HmsDbContext db)
    { _encounters = encounters; _fmt = fmt; _printers = printers; _ctx = ctx; _db = db; }

    [BindProperty(SupportsGet = true)] public int? Width { get; set; }
    [BindProperty(SupportsGet = true)] public bool AutoPrint { get; set; }
    public PatientEncounter Encounter { get; private set; } = null!;
    public Patient Patient => Encounter.Patient!;
    public HospitalProfile Hospital { get; private set; } = null!;
    public PrinterProfile Printer { get; private set; } = null!;
    public IReadOnlyList<Charge> Charges { get; private set; } = Array.Empty<Charge>();
    public IReadOnlyList<Receipt> Receipts { get; private set; } = Array.Empty<Receipt>();
    public string AgeText { get; private set; } = "—";
    public bool IsEmergency => Encounter.Type == EncounterType.Emergency;
    public string SlipUrl => IsEmergency ? $"/Print/Emergency/{Encounter.Id}" : $"/Print/Opd/{Encounter.Id}";
    public string BackUrl => IsEmergency ? $"/Emergency/Visit/{Encounter.Id}" : $"/Opd/Visit/{Encounter.Id}";

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var enc = await _encounters.GetAsync(id);
        if (enc is null || enc.Type is not (EncounterType.Opd or EncounterType.Emergency)) return NotFound();
        if (await DenyUnlessAsync(enc.Type == EncounterType.Emergency ? Perm.EmergencyPrint : Perm.OpdPrint) is { } deny) return deny;
        Encounter = enc;
        Hospital = (await _fmt.ProfileAsync())!;
        Printer = await _printers.ForWorkstationAsync(_ctx.Workstation ?? "");
        Width = Width is 58 or 80 ? Width : Printer.PaperWidthMm is 58 ? 58 : 80;
        AgeText = AgeCalculator.Of(enc.Patient!, DateOnly.FromDateTime(_fmt.ToLocal(enc.EncounterUtc)))?.ToString() ?? "—";
        Charges = await _db.Charges.AsNoTracking().Where(c => c.EncounterId == id && c.Status != ChargeStatus.Cancelled).OrderBy(c => c.Id).ToListAsync();
        var ids = Charges.Select(c => c.Id).ToList();
        Receipts = ids.Count == 0 ? Array.Empty<Receipt>() : await _db.Receipts.AsNoTracking().Include(r => r.PaymentMethod)
            .Where(r => r.Status == ReceiptStatus.Valid && _db.ReceiptLines.Any(l => l.ReceiptId == r.Id && ids.Contains(l.ChargeId)))
            .OrderBy(r => r.Id).ToListAsync();
        return Page();
    }

    public string Guardian => Patient.GuardianName is null ? "—" : $"{SlipModelBase.RelationLabel(Patient.GuardianRelation)} {Patient.GuardianName}".Trim();
    public string GenderText => Patient.Gender switch { Gender.Male => "Male", Gender.Female => "Female", Gender.Other => "Other", _ => "Unknown" };
}
