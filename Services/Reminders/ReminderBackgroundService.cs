using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Jobs;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Reminders;

/// <summary>
/// Daily job: find invoices due soon / overdue, notify via <see cref="IReminderNotifier"/>,
/// optionally mark as sent.
/// </summary>
public sealed class ReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ReminderJobOptions> optionsMonitor,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reminder job background service started.");

        var options = optionsMonitor.CurrentValue;
        if (options.RunOnStartup && options.Enabled)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(0, options.StartupDelaySeconds)),
                    stoppingToken);
                await RunAsync(stoppingToken);
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

            if (!optionsMonitor.CurrentValue.Enabled)
                continue;

            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            var options = optionsMonitor.CurrentValue;
            await using var scope = scopeFactory.CreateAsyncScope();
            var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var notifier = scope.ServiceProvider.GetRequiredService<IReminderNotifier>();

            var withinDays = Math.Clamp(options.WithinDays, 0, 90);
            var candidates = await invoices.GetRemindersAsync(
                withinDays,
                options.OnlyOverdue,
                ct);

            var cooldown = Math.Max(0, options.MinDaysBetweenReminders);
            var cutoff = DateTime.UtcNow.AddDays(-cooldown);
            var due = candidates
                .Where(r =>
                    cooldown == 0
                    || r.LastReminderSentAt is null
                    || r.LastReminderSentAt < cutoff)
                .ToList();

            if (due.Count == 0)
            {
                logger.LogDebug(
                    "Reminder job: no candidates (scanned {Scanned}, withinDays={WithinDays}, onlyOverdue={OnlyOverdue}).",
                    candidates.Count,
                    withinDays,
                    options.OnlyOverdue);
                return;
            }

            logger.LogInformation(
                "Reminder job: dispatching {Count} of {Scanned} candidate(s) (MarkAsSent={MarkAsSent}).",
                due.Count,
                candidates.Count,
                options.MarkAsSent);

            var sent = 0;
            var failed = 0;

            foreach (var item in due)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await notifier.NotifyAsync(item, ct);

                    if (options.MarkAsSent)
                    {
                        var mark = await invoices.MarkReminderSentAsync(item.Id, ct);
                        if (!mark.Success)
                        {
                            failed++;
                            logger.LogWarning(
                                "Reminder notified for {Number} but MarkAsSent failed: {Error}",
                                item.Number,
                                mark.Error);
                            continue;
                        }
                    }

                    sent++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "Reminder dispatch failed for {Number}.", item.Number);
                }
            }

            logger.LogInformation(
                "Reminder job finished: sent={Sent}, failed={Failed}.",
                sent,
                failed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reminder job failed.");
        }
    }
}
