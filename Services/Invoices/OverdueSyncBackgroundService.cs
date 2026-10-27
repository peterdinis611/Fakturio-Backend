using Fakturio.Api.Interfaces;
using Fakturio.Api.Jobs;

namespace Fakturio.Api.Services.Invoices;

/// <summary>
/// Periodically flips Issued → Overdue for past-due unpaid invoices.
/// </summary>
public sealed class OverdueSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IJobSettingsStore jobSettings,
    ILogger<OverdueSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Overdue sync background service started.");

        var options = jobSettings.GetOverdue();
        if (options.RunOnStartup && options.Enabled)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(0, options.StartupDelaySeconds)),
                    stoppingToken);
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            options = jobSettings.GetOverdue();
            if (!options.Enabled)
            {
                var recheck = Math.Max(1, options.DisabledRecheckHours);
                logger.LogDebug(
                    "Overdue sync is disabled; checking again in {Hours} hour(s).",
                    recheck);
                if (!await JobSchedule.DelaySafe(TimeSpan.FromHours(recheck), stoppingToken))
                    return;
                continue;
            }

            var delay = JobSchedule.DelayUntilNextUtc(options.RunAtHourUtc, options.RunAtMinuteUtc);
            logger.LogInformation(
                "Next overdue sync scheduled in {Delay} (at {Hour:D2}:{Minute:D2} UTC).",
                delay,
                Math.Clamp(options.RunAtHourUtc, 0, 23),
                Math.Clamp(options.RunAtMinuteUtc, 0, 59));

            if (!await JobSchedule.DelaySafe(delay, stoppingToken))
                return;

            if (!jobSettings.GetOverdue().Enabled)
                continue;

            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var result = await invoices.SyncOverdueAsync(ct);

            if (result.UpdatedCount > 0)
                logger.LogInformation("Overdue sync updated {Count} invoice(s).", result.UpdatedCount);
            else
                logger.LogDebug("Overdue sync finished; no invoices updated.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Overdue sync failed.");
        }
    }
}
