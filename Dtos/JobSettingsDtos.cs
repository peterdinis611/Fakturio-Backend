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

public record JobSettingsResponse(
    OverdueJobSettingsDto OverdueSync,
    ReminderJobSettingsDto ReminderJob
);

public record UpdateJobSettingsRequest(
    OverdueJobSettingsDto OverdueSync,
    ReminderJobSettingsDto ReminderJob
);
