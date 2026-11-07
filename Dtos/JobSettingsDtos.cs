namespace Fakturio.Api.Dtos;

public record OverdueJobSettingsDto(
    bool Enabled,
    int RunAtHourUtc,
    int RunAtMinuteUtc,
    bool RunOnStartup,
    int StartupDelaySeconds,
    int DisabledRecheckHours
);

public record ReminderJobSettingsDto(
    bool Enabled,
    int RunAtHourUtc,
    int RunAtMinuteUtc,
    bool RunOnStartup,
    int StartupDelaySeconds,
    int DisabledRecheckHours,
    int WithinDays,
    bool OnlyOverdue,
    int MinDaysBetweenReminders,
    bool MarkAsSent
);

public record RecurringJobSettingsDto(
    bool Enabled,
    int IntervalHours,
    int StartupDelaySeconds,
    int DisabledRecheckHours
);

public record JobRunStatusDto(
    DateTimeOffset? LastRunAtUtc,
    string? LastMessage
);

public record JobSettingsResponse(
    OverdueJobSettingsDto OverdueSync,
    ReminderJobSettingsDto ReminderJob,
    RecurringJobSettingsDto RecurringInvoices,
    JobRunStatusDto OverdueSyncRun,
    JobRunStatusDto ReminderJobRun,
    JobRunStatusDto RecurringInvoicesRun
);

public record UpdateJobSettingsRequest(
    OverdueJobSettingsDto OverdueSync,
    ReminderJobSettingsDto ReminderJob,
    RecurringJobSettingsDto RecurringInvoices
);
