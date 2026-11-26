using BurhanNexus.HMS.Infrastructure;
using BurhanNexus.HMS.Infrastructure.Backup;
using BurhanNexus.HMS.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace BurhanNexus.HMS.Web.Services;

/// <summary>
/// Update 15: daily automatic backup. Every 30 minutes the server checks whether today's complete backup exists; if not, it creates
/// one (every backup holds ALL data, from the first record to today) and then deletes automatic backups older than 30 days.
/// A computer that is switched off at night simply makes the day's backup soon after it is started.
/// </summary>
public sealed class AutoBackupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<HmsOptions> _options;
    private readonly ILogger<AutoBackupWorker> _logger;
    public AutoBackupWorker(IServiceScopeFactory scopes, IOptions<HmsOptions> options, ILogger<AutoBackupWorker> logger)
    { _scopes = scopes; _options = options; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.AutoBackup) return;
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<ISetupService>().GetStateAsync(stoppingToken) == SetupState.Completed)
                {
                    var r = await scope.ServiceProvider.GetRequiredService<IBackupService>().RunDailyAsync(DateTime.Now, stoppingToken);
                    if (!r.Succeeded) _logger.LogWarning("Automatic daily backup failed: {Error}", r.ErrorText);
                    else if (r.Value is { } b) _logger.LogInformation("Automatic daily backup created: {File}", b.FileName);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "Automatic daily backup failed"); }
            try { await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
