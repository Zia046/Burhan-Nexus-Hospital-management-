using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ot;
using BurhanNexus.HMS.Web.Pages.Finance;
using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>OT dashboard – live figures from the surgery records with date, theatre and surgeon filters.</summary>
[HasPermission(Perm.OtDashboard)]
public class IndexModel : OtPageModel
{
    [BindProperty(SupportsGet = true)] public string? Period { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? TheatreId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SurgeonId { get; set; }
    public string PresetKey { get; private set; } = "today";
    public OtDashboard Data { get; private set; } = null!;
    public IReadOnlyList<OtTheatre> Theatres { get; private set; } = Array.Empty<OtTheatre>();
    public IReadOnlyList<(int Id, string Name)> Surgeons { get; private set; } = Array.Empty<(int, string)>();

    public async Task OnGetAsync()
    {
        Nav("ot-dashboard", "dashboard");
        var (from, to, key) = FinancePeriod.Resolve(Period, From, To, await Clock.TodayAsync());
        From = from; To = to; PresetKey = key;
        Data = await Query.DashboardAsync(new OtFilter { From = from, To = to, TheatreId = TheatreId, SurgeonId = SurgeonId });
        Theatres = await Setup.TheatresAsync(activeOnly: true);
        Surgeons = await Query.SurgeonsAsync();
    }

    public IReadOnlyList<SurgeryRow> Section(string key)
    {
        var now = DateTime.UtcNow;
        return key switch
        {
            "scheduled" => Data.Rows.Where(r => r.Priority != SurgeryPriority.Emergency && r.Status is SurgeryStatus.Requested or SurgeryStatus.Scheduled).ToList(),
            "emergency" => Data.Rows.Where(r => r.Priority == SurgeryPriority.Emergency && r.Status is not (SurgeryStatus.Completed or SurgeryStatus.Cancelled)).ToList(),
            "preop" => Data.Rows.Where(r => r.Status is SurgeryStatus.PreOp or SurgeryStatus.Ready).ToList(),
            "progress" => Data.Rows.Where(r => r.Status == SurgeryStatus.InProgress).ToList(),
            "completed" => Data.Rows.Where(r => r.Status == SurgeryStatus.Completed).ToList(),
            "recovery" => Data.Rows.Where(r => r.Recovery is RecoveryStatus.Awaiting or RecoveryStatus.InRecovery or RecoveryStatus.UnderObservation or RecoveryStatus.ReadyForTransfer).ToList(),
            _ => Data.Rows.Where(r => r.Status is SurgeryStatus.Cancelled or SurgeryStatus.Postponed).ToList(),
        };
    }
}
