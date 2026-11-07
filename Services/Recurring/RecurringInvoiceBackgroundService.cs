using Fakturio.Api.Interfaces;
using Fakturio.Api.Jobs;
using Fakturio.Api.Services.Jobs;

namespace Fakturio.Api.Services.Recurring;

/// <summary>Hourly (configurable) job: materialize due recurring invoice templates into draft invoices.</summary>
public sealed class RecurringInvoiceBackgroundService(
    IServiceScopeFactory scopeFactory,
    IJobSettingsStore jobSettings,
    ILogger<RecurringInvoiceBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Recurring invoice job started.");

        var options = jobSettings.GetRecurring();
        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(0, options.StartupDelaySeconds)),
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            options = jobSettings.GetRecurring();
            if (!options.Enabled)
            {
                var recheck = Math.Max(1, options.DisabledRecheckHours);
                logger.LogDebug(
                    "Recurring invoice job is disabled; checking again in {Hours} hour(s).",
                    recheck);
                if (!await JobSchedule.DelaySafe(TimeSpan.FromHours(recheck), stoppingToken))
                    return;
                continue;
            }

            await RunAsync(stoppingToken);

            options = jobSettings.GetRecurring();
            var interval = TimeSpan.FromHours(Math.Clamp(options.IntervalHours, 1, 24));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recurring = scope.ServiceProvider.GetRequiredService<IRecurringInvoiceService>();
            var result = await recurring.ProcessDueAsync(ct);
            var message = $"generated={result.GeneratedCount}, skipped={result.SkippedCount}";
            jobSettings.RecordRun(JobSettingsStore.RecurringKey, message);

            if (result.GeneratedCount > 0 || result.SkippedCount > 0)
            {
                logger.LogInformation(
                    "Recurring invoices: generated {Generated}, skipped {Skipped}.",
                    result.GeneratedCount,
                    result.SkippedCount);
            }
            else
            {
                logger.LogDebug("Recurring invoices: nothing due.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            jobSettings.RecordRun(JobSettingsStore.RecurringKey, $"error: {ex.Message}");
            logger.LogError(ex, "Recurring invoice job failed.");
        }
    }
}
