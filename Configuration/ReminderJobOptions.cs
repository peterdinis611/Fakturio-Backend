namespace Fakturio.Api.Configuration;

public sealed class ReminderJobOptions
{
    public const string SectionName = "ReminderJob";

    /// <summary>Enable/disable the reminder background job.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>UTC hour (0–23) when the daily job runs.</summary>
    public int RunAtHourUtc { get; set; } = 7;

    /// <summary>UTC minute (0–59) within <see cref="RunAtHourUtc"/>.</summary>
    public int RunAtMinuteUtc { get; set; }

    /// <summary>Run once shortly after app start, then on the daily schedule.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>Delay before the optional startup run.</summary>
    public int StartupDelaySeconds { get; set; } = 20;

    /// <summary>How often to re-check when the job is disabled.</summary>
    public int DisabledRecheckHours { get; set; } = 1;

    /// <summary>Include invoices due within this many days (0–90).</summary>
    public int WithinDays { get; set; } = 7;

    /// <summary>If true, only overdue invoices are considered.</summary>
    public bool OnlyOverdue { get; set; }

    /// <summary>
    /// Skip invoices that already had a reminder within this many days
    /// (based on <c>LastReminderSentAt</c>). 0 = no cooldown.
    /// </summary>
    public int MinDaysBetweenReminders { get; set; } = 3;

    /// <summary>
    /// After a successful dispatch, update <c>LastReminderSentAt</c> / <c>ReminderCount</c>.
    /// Keep false until a real notifier (e.g. email) is wired — otherwise counters advance on log-only runs.
    /// </summary>
    public bool MarkAsSent { get; set; }
}
