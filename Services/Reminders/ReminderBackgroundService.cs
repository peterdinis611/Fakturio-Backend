using Fakturio.Api.Interfaces;
using Fakturio.Api.Jobs;

namespace Fakturio.Api.Services.Reminders;

/// <summary>
/// In-process daily reminder loop used when BullMQ is disabled (no Redis / local / tests).
/// </summary>
public sealed class ReminderBackgroundService(
    IReminderJobRunner runner,
    IJobSettingsStore jobSettings,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reminder job background service started (in-process scheduler).");

        var options = jobSettings.GetReminder();
        if (options.RunOnStartup && options.Enabled)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(0, options.StartupDelaySeconds)),
                    stoppingToken);
                await RunSafeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            options = jobSettings.GetReminder();
            if (!options.Enabled)
            {
                var recheck = Math.Max(1, options.DisabledRecheckHours);
                logger.LogDebug(
                    "Reminder job is disabled; checking again in {Hours} hour(s).",
                    recheck);
                if (!await JobSchedule.DelaySafe(TimeSpan.FromHours(recheck), stoppingToken))
                    return;
                continue;
            }

            var delay = JobSchedule.DelayUntilNextUtc(options.RunAtHourUtc, options.RunAtMinuteUtc);
            logger.LogInformation(
                "Next reminder job scheduled in {Delay} (at {Hour:D2}:{Minute:D2} UTC).",
                delay,
                Math.Clamp(options.RunAtHourUtc, 0, 23),
                Math.Clamp(options.RunAtMinuteUtc, 0, 59));

            if (!await JobSchedule.DelaySafe(delay, stoppingToken))
                return;

            if (!jobSettings.GetReminder().Enabled)
                continue;

            await RunSafeAsync(stoppingToken);
        }
    }

    private async Task RunSafeAsync(CancellationToken ct)
    {
        try
        {
            await runner.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception)
        {
            // Already logged + RecordRun inside the runner.
        }
    }
}
