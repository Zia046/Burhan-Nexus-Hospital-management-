using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

[HasPermission(Perm.IpdViewPatients)]
public class AdmissionsModel : IpdPageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Scope { get; set; }
    [BindProperty(SupportsGet = true)] public int? WardId { get; set; }
    [BindProperty(SupportsGet = true)] public AdmissionKind? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public long? PatientId { get; set; }
    public IReadOnlyList<AdmissionRow> Rows { get; private set; } = Array.Empty<AdmissionRow>();
    public IReadOnlyList<IpdWard> Wards { get; private set; } = Array.Empty<IpdWard>();

    public async Task OnGetAsync()
    {
        Nav("ipd-patients", "patients");
        var wards = await WardsAsync();
        Scope ??= PatientId is null && string.IsNullOrWhiteSpace(Q) ? "active" : "all";
        Rows = await Query.AdmissionsAsync(Scope, Q, WardId, Kind, wards, patientId: PatientId);
        Wards = (await Setup.WardsAsync(activeOnly: true)).Where(w => wards is null || wards.Contains(w.Id)).ToList();
    }
}
