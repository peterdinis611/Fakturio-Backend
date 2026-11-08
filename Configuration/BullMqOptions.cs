namespace Fakturio.Api.Configuration;

/// <summary>
/// BullMQ distributed queue (Redis). When enabled, reminders run via a Redis-backed
/// scheduler/worker instead of the in-process <c>ReminderBackgroundService</c> loop.
/// </summary>
public sealed class BullMqOptions
{
    public const string SectionName = "BullMQ";

    /// <summary>
    /// Master switch. Requires a Redis connection
    /// (<see cref="RedisConnection"/>, Cache:RedisConnection, or ConnectionStrings:Redis).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Optional Redis connection override for BullMQ.</summary>
    public string? RedisConnection { get; set; }

    /// <summary>Redis key prefix (BullMQ default is <c>bull</c>).</summary>
    public string Prefix { get; set; } = "fakturio";

    /// <summary>Queue name for the daily reminder dispatch job.</summary>
    public string ReminderQueueName { get; set; } = "reminders";

    /// <summary>Stable scheduler id used with <c>UpsertJobSchedulerAsync</c>.</summary>
    public string ReminderSchedulerId { get; set; } = "reminder-daily";

    /// <summary>How often to re-apply enable/cron settings from <c>IJobSettingsStore</c>.</summary>
    public int SettingsSyncSeconds { get; set; } = 60;
}
