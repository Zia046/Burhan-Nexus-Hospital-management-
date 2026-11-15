using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Persistence;
using BurhanNexus.HMS.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Web.Pages.Ot;

/// <summary>Recovery room / PACU board: patients awaiting or in recovery, and today's transfers.</summary>
[HasPermission(Perm.OtManageRecovery, Perm.OtDashboard)]
public class RecoveryModel : OtPageModel
{
    public IReadOnlyList<RecoveryEpisode> Open { get; private set; } = Array.Empty<RecoveryEpisode>();
    public IReadOnlyList<RecoveryEpisode> Transferred { get; private set; } = Array.Empty<RecoveryEpisode>();

    public async Task OnGetAsync()
    {
        Nav("ot-recovery", "recovery");
        var db = HttpContext.RequestServices.GetRequiredService<HmsDbContext>();
        var open = new[] { RecoveryStatus.Awaiting, RecoveryStatus.InRecovery, RecoveryStatus.UnderObservation, RecoveryStatus.ReadyForTransfer };
        Open = await db.RecoveryEpisodes.AsNoTracking().Include(r => r.Surgery!).ThenInclude(s => s.Patient).Include(r => r.Surgery!).ThenInclude(s => s.Theatre)
            .Where(r => open.Contains(r.Status)).OrderBy(r => r.CreatedUtc).ToListAsync();
        var since = await Clock.StartOfDayUtcAsync(await Clock.TodayAsync());
        Transferred = await db.RecoveryEpisodes.AsNoTracking().Include(r => r.Surgery!).ThenInclude(s => s.Patient)
            .Where(r => !open.Contains(r.Status) && r.TransferUtc >= since).OrderByDescending(r => r.TransferUtc).ToListAsync();
    }
}
