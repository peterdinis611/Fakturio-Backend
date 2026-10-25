using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Invoices;

/// <summary>
/// Periodically flips Issued → Overdue for past-due unpaid invoices.
/// </summary>
public sealed class OverdueSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<OverdueSyncOptions> optionsMonitor,
    ILogger<OverdueSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Overdue sync background service started.");

        var options = optionsMonitor.CurrentValue;
        if (options.RunOnStartup && options.Enabled)
        {
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            options = optionsMonitor.CurrentValue;
            if (!options.Enabled)
            {
                logger.LogDebug("Overdue sync is disabled; checking again in 1 hour.");
                await DelaySafe(TimeSpan.FromHours(1), stoppingToken);
                continue;
            }

            var delay = DelayUntilNextRun(options.RunAtHourUtc);
            logger.LogInformation(
                "Next overdue sync scheduled in {Delay} (at {Hour}:00 UTC).",
                delay,
                Math.Clamp(options.RunAtHourUtc, 0, 23));

            if (!await DelaySafe(delay, stoppingToken))
                return;

            if (!optionsMonitor.CurrentValue.Enabled)
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

    private static TimeSpan DelayUntilNextRun(int runAtHourUtc)
    {
        var hour = Math.Clamp(runAtHourUtc, 0, 23);
        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, hour, 0, 0, DateTimeKind.Utc);
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }

    private static async Task<bool> DelaySafe(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
