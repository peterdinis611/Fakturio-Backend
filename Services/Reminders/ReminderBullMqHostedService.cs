using BullMQ;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Fakturio.Api.Services.Reminders;

/// <summary>
/// Hosts a BullMQ worker for reminder dispatch and keeps the daily cron scheduler
/// in sync with <see cref="IJobSettingsStore"/>.
/// </summary>
public sealed class ReminderBullMqHostedService(
    IConnectionMultiplexer redis,
    IOptions<BullMqOptions> bullMqOptions,
    IReminderJobRunner runner,
    IJobSettingsStore jobSettings,
    ILogger<ReminderBullMqHostedService> logger) : BackgroundService
{
    public const string DispatchJobName = "dispatch";

    private string? _lastScheduleKey;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = bullMqOptions.Value;
        var connection = new ConnectionOptions { Multiplexer = redis };
        var queueOpts = new QueueOptions
        {
            Connection = connection,
            Prefix = opts.Prefix,
        };
        var workerOpts = new WorkerOptions
        {
            Connection = connection,
            Prefix = opts.Prefix,
            Concurrency = 1,
            Autorun = false,
        };

        logger.LogInformation(
            "Reminder BullMQ worker starting (queue={Queue}, prefix={Prefix}).",
            opts.ReminderQueueName,
            opts.Prefix);

        await using var queue = new Queue(opts.ReminderQueueName, queueOpts);
        await using var worker = new Worker(opts.ReminderQueueName, ProcessAsync, workerOpts);

        worker.Failed += (job, error) =>
            logger.LogError(
                error,
                "Reminder BullMQ job {JobId} ({JobName}) failed.",
                job?.Id,
                job?.Name);

        worker.Completed += (job, _) =>
            logger.LogDebug(
                "Reminder BullMQ job {JobId} ({JobName}) completed.",
                job.Id,
                job.Name);

        try
        {
            await SyncSchedulerAsync(queue, opts, stoppingToken);
            await EnqueueStartupIfNeededAsync(queue, stoppingToken);

            var workerTask = worker.RunAsync();
            var syncTask = SyncLoopAsync(queue, opts, stoppingToken);

            var finished = await Task.WhenAny(workerTask, syncTask);
            if (finished == workerTask && workerTask.IsFaulted)
                logger.LogError(workerTask.Exception, "Reminder BullMQ worker stopped with error.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reminder BullMQ hosted service failed.");
        }
        finally
        {
            logger.LogInformation("Reminder BullMQ worker stopped.");
        }
    }

    private async Task<object?> ProcessAsync(Job job, CancellationToken ct)
    {
        if (!string.Equals(job.Name, DispatchJobName, StringComparison.Ordinal))
        {
            logger.LogWarning("Ignoring unknown reminder job name {JobName}.", job.Name);
            return "ignored";
        }

        if (!jobSettings.GetReminder().Enabled)
        {
            logger.LogInformation("Reminder job skipped (disabled in settings).");
            return "disabled";
        }

        await runner.RunAsync(ct);
        return "ok";
    }

    private async Task SyncLoopAsync(Queue queue, BullMqOptions opts, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(opts.SettingsSyncSeconds, 15, 3600));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct);
                await SyncSchedulerAsync(queue, opts, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync reminder BullMQ scheduler; will retry.");
            }
        }
    }

    private async Task SyncSchedulerAsync(Queue queue, BullMqOptions opts, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var reminder = jobSettings.GetReminder();
        var schedulerId = opts.ReminderSchedulerId;

        if (!reminder.Enabled)
        {
            if (_lastScheduleKey is not null)
            {
                await queue.RemoveJobSchedulerAsync(schedulerId);
                _lastScheduleKey = null;
                logger.LogInformation("Removed reminder BullMQ scheduler {SchedulerId}.", schedulerId);
            }

            return;
        }

        var hour = Math.Clamp(reminder.RunAtHourUtc, 0, 23);
        var minute = Math.Clamp(reminder.RunAtMinuteUtc, 0, 59);
        var scheduleKey = $"{hour:D2}:{minute:D2}";

        if (string.Equals(_lastScheduleKey, scheduleKey, StringComparison.Ordinal))
            return;

        // 5-field cron: minute hour day-of-month month day-of-week
        var pattern = $"{minute} {hour} * * *";
        await queue.UpsertJobSchedulerAsync(
            schedulerId,
            new RepeatOptions
            {
                Pattern = pattern,
                Tz = "UTC",
            },
            new JobSchedulerTemplate
            {
                Name = DispatchJobName,
                Data = new { source = "scheduler" },
                Opts = new JobsOptions
                {
                    Attempts = 3,
                    Backoff = new BackoffOptions { Type = "exponential", Delay = 5_000 },
                    RemoveOnComplete = 100,
                    RemoveOnFail = 200,
                },
            });

        _lastScheduleKey = scheduleKey;
        logger.LogInformation(
            "Upserted reminder BullMQ scheduler {SchedulerId} cron='{Pattern}' tz=UTC.",
            schedulerId,
            pattern);
    }

    private async Task EnqueueStartupIfNeededAsync(Queue queue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var reminder = jobSettings.GetReminder();
        if (!reminder.Enabled || !reminder.RunOnStartup)
            return;

        var delayMs = Math.Max(0, reminder.StartupDelaySeconds) * 1000L;
        var jobId = $"reminder-startup-{DateTime.UtcNow:yyyyMMdd}";

        await queue.AddAsync(
            DispatchJobName,
            new { source = "startup" },
            new JobsOptions
            {
                Delay = delayMs,
                JobId = jobId,
                Attempts = 3,
                Backoff = new BackoffOptions { Type = "exponential", Delay = 5_000 },
                RemoveOnComplete = 50,
                RemoveOnFail = 100,
            });

        logger.LogInformation(
            "Enqueued reminder startup job {JobId} (delay={DelayMs}ms).",
            jobId,
            delayMs);
    }
}
