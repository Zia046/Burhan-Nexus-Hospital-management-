using System.Text;
using BurhanNexus.HMS.Core.Abstractions;
using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Infrastructure.Services;
using BurhanNexus.HMS.Web.Security;
using BurhanNexus.HMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Patients;

[HasPermission(Perm.PatientsSearch)]
public class IndexModel : HmsPageModel
{
    private readonly IPatientService _patients;
    private readonly IAuditService _audit;
    private readonly DisplayFormatter _fmt;

    public IndexModel(IPatientService patients, IAuditService audit, DisplayFormatter fmt)
    {
        _patients = patients;
        _audit = audit;
        _fmt = fmt;
    }

    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "from")] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "gender")] public Gender? GenderFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "visits")] public string? Visits { get; set; }
    [BindProperty(SupportsGet = true, Name = "status")] public string? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

    public PagedResult<PatientListItem> Result { get; private set; } = new(Array.Empty<PatientListItem>(), 0, 1, 25);

    private PatientSearchQuery Query(int page, int size) => new()
    {
        Q = Q, RegisteredFrom = From, RegisteredTo = To, Gender = GenderFilter, VisitType = Visits, Status = Status, Page = page, PageSize = size
    };

    public async Task<IActionResult> OnGetAsync()
    {
        ViewData["Nav"] = "patients";
        Result = await _patients.SearchAsync(Query(PageNumber, 25));
        // Exact Patient ID / visit number → open the profile directly (fast for reception).
        if (Result.Total == 1 && !string.IsNullOrWhiteSpace(Q) && PageNumber == 1 && Q.Trim().Contains('-')
            && await CanAsync(Perm.PatientsView) && Result.Items[0].PatientNumber.Equals(Q.Trim(), StringComparison.OrdinalIgnoreCase))
            return Redirect($"/Patients/Profile/{Result.Items[0].Id}");
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        if (await DenyUnlessAsync(Perm.PatientsExport) is { } deny) return deny;
        await _fmt.EnsureLoadedAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Patient ID,Name,Relation,Father/Husband,Gender,Age,Mobile,CNIC,City/Village,Registered,Status,OPD visits,Emergency visits,Last visit");
        var page = 1;
        int exported = 0;
        while (true)
        {
            var batch = await _patients.SearchAsync(Query(page, 200));
            foreach (var p in batch.Items)
            {
                sb.AppendJoin(',', new[]
                {
                    p.PatientNumber, p.FullName, Print.SlipModelBase.RelationLabel(p.GuardianRelation), p.GuardianName, p.Gender.ToString(), p.AgeText,
                    p.MobileNumber, p.Cnic, p.CityOrVillage, _fmt.DateTimeText(p.RegisteredUtc), p.Status.ToString(),
                    p.OpdVisits.ToString(), p.EmergencyVisits.ToString(), p.LastVisitUtc is null ? "" : _fmt.DateTimeText(p.LastVisitUtc)
                }.Select(Csv));
                sb.AppendLine();
            }
            exported += batch.Items.Count;
            if (page >= batch.TotalPages || page >= 250) break;
            page++;
        }
        await _audit.LogAsync(AuditActions.PatientsExported, "Patient", null, $"{exported} patient record(s) exported to CSV",
            newValues: new { Q, From, To, Gender = GenderFilter?.ToString(), Visits, Status });
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"patients-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
