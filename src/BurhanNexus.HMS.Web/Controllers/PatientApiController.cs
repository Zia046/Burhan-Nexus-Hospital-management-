using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Patients;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientApiController : ControllerBase
{
    private readonly IPatientService _patients;
    public PatientApiController(IPatientService patients) => _patients = patients;

    /// <summary>Live duplicate check while registering (name, father's/husband's name, CNIC, mobile).</summary>
    [HttpGet("duplicates")]
    [HasPermission(Perm.PatientsAdd)]
    public async Task<IActionResult> Duplicates([FromQuery] string? name, [FromQuery] string? guardian, [FromQuery] string? cnic,
        [FromQuery] string? mobile, CancellationToken ct)
    {
        var list = await _patients.FindDuplicatesAsync(new PatientInput
        {
            FullName = Trim(name, 150), GuardianName = Trim(guardian, 150), Cnic = Trim(cnic, 20), MobileNumber = Trim(mobile, 20)
        }, null, ct);
        return Ok(list.Select(d => new
        {
            d.Id, d.PatientNumber, d.FullName, d.GuardianName, Gender = d.Gender.ToString(), d.AgeText, d.MobileNumber, d.Cnic,
            d.CityOrVillage, Status = d.Status.ToString(), d.Reasons
        }));
    }

    [HttpGet("search")]
    [HasPermission(Perm.PatientsSearch)]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var r = await _patients.SearchAsync(new PatientSearchQuery { Q = Trim(q, 100), Page = page, PageSize = 25 }, ct);
        return Ok(new
        {
            r.Total, r.Page,
            items = r.Items.Select(p => new { p.Id, p.PatientNumber, p.FullName, p.GuardianName, Gender = p.Gender.ToString(), p.AgeText, p.MobileNumber, Status = p.Status.ToString() })
        });
    }

    [HttpGet("{id:long}")]
    [HasPermission(Perm.PatientsView)]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var p = await _patients.GetAsync(id, ct);
        if (p is null) return NotFound();
        return Ok(new { p.Id, p.PatientNumber, p.FullName, p.GuardianName, Gender = p.Gender.ToString(), p.MobileNumber, p.Cnic, Status = p.Status.ToString() });
    }

    private static string? Trim(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;
}
