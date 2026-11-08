using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Jobs;

namespace Fakturio.Api.Services.Reminders;

/// <summary>
/// Shared reminder dispatch logic used by the in-process background loop and BullMQ worker.
/// </summary>
public sealed class ReminderJobRunner(
    IServiceScopeFactory scopeFactory,
    IJobSettingsStore jobSettings,
    ILogger<ReminderJobRunner> logger) : IReminderJobRunner
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            var options = jobSettings.GetReminder();
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
                jobSettings.RecordRun(
                    JobSettingsStore.ReminderKey,
                    $"scanned={candidates.Count}, sent=0");
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

            jobSettings.RecordRun(
                JobSettingsStore.ReminderKey,
                $"scanned={candidates.Count}, sent={sent}, failed={failed}");
            logger.LogInformation(
                "Reminder job finished: sent={Sent}, failed={Failed}.",
                sent,
                failed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            jobSettings.RecordRun(JobSettingsStore.ReminderKey, $"error: {ex.Message}");
            logger.LogError(ex, "Reminder job failed.");
            throw;
        }
    }
}
