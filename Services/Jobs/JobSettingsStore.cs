using System.Text.Json;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Jobs;

/// <summary>
/// Runtime job settings: defaults from appsettings, overrides persisted to
/// <c>data/job-settings.json</c> so the UI can change schedules without restart.
/// Last-run status stays in memory (survives until process restart).
/// </summary>
public sealed class JobSettingsStore : IJobSettingsStore
{
    public const string OverdueKey = "overdue";
    public const string ReminderKey = "reminder";
    public const string RecurringKey = "recurring";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly ILogger<JobSettingsStore> _logger;
    private OverdueSyncOptions _overdue;
    private ReminderJobOptions _reminder;
    private RecurringJobOptions _recurring;
    private JobRunStatusDto _overdueRun = new(null, null);
    private JobRunStatusDto _reminderRun = new(null, null);
    private JobRunStatusDto _recurringRun = new(null, null);

    public JobSettingsStore(
        IHostEnvironment env,
        IOptions<OverdueSyncOptions> overdueDefaults,
        IOptions<ReminderJobOptions> reminderDefaults,
        IOptions<RecurringJobOptions> recurringDefaults,
        ILogger<JobSettingsStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "job-settings.json");

        _overdue = Clone(overdueDefaults.Value);
        _reminder = Clone(reminderDefaults.Value);
        _recurring = Clone(recurringDefaults.Value);
        LoadFromDisk();
    }

    public OverdueSyncOptions GetOverdue()
    {
        lock (_gate) return Clone(_overdue);
    }

    public ReminderJobOptions GetReminder()
    {
        lock (_gate) return Clone(_reminder);
    }

    public RecurringJobOptions GetRecurring()
    {
        lock (_gate) return Clone(_recurring);
    }

    public JobSettingsResponse GetSnapshot()
    {
        lock (_gate)
        {
            return new JobSettingsResponse(
                ToDto(_overdue),
                ToDto(_reminder),
                ToDto(_recurring),
                _overdueRun,
                _reminderRun,
                _recurringRun);
        }
    }

    public void RecordRun(string jobKey, string message)
    {
        var status = new JobRunStatusDto(DateTimeOffset.UtcNow, message);
        lock (_gate)
        {
            switch (jobKey)
            {
                case OverdueKey:
                    _overdueRun = status;
                    break;
                case ReminderKey:
                    _reminderRun = status;
                    break;
                case RecurringKey:
                    _recurringRun = status;
                    break;
            }
        }
    }

    public async Task<JobSettingsResponse> UpdateAsync(
        UpdateJobSettingsRequest request,
        CancellationToken ct = default)
    {
        var overdue = FromDto(request.OverdueSync);
        var reminder = FromDto(request.ReminderJob);
        var recurring = FromDto(request.RecurringInvoices);
        Normalize(overdue);
        Normalize(reminder);
        Normalize(recurring);

        lock (_gate)
        {
            _overdue = overdue;
            _reminder = reminder;
            _recurring = recurring;
        }

        var snapshot = GetSnapshot();
        var persisted = new PersistedJobSettings(
            snapshot.OverdueSync,
            snapshot.ReminderJob,
            snapshot.RecurringInvoices);
        var json = JsonSerializer.Serialize(persisted, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        _logger.LogInformation("Job settings updated and saved to {Path}.", _filePath);
        return snapshot;
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath);
            var parsed = JsonSerializer.Deserialize<PersistedJobSettings>(json, JsonOptions);
            if (parsed is null)
                return;

            lock (_gate)
            {
                _overdue = FromDto(parsed.OverdueSync);
                _reminder = FromDto(parsed.ReminderJob);
                if (parsed.RecurringInvoices is not null)
                    _recurring = FromDto(parsed.RecurringInvoices);
                Normalize(_overdue);
                Normalize(_reminder);
                Normalize(_recurring);
            }

            _logger.LogInformation("Loaded job settings overrides from {Path}.", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load job settings from {Path}; using appsettings defaults.", _filePath);
        }
    }

    private static void Normalize(OverdueSyncOptions o)
    {
        o.RunAtHourUtc = Math.Clamp(o.RunAtHourUtc, 0, 23);
        o.RunAtMinuteUtc = Math.Clamp(o.RunAtMinuteUtc, 0, 59);
        o.StartupDelaySeconds = Math.Max(0, o.StartupDelaySeconds);
        o.DisabledRecheckHours = Math.Max(1, o.DisabledRecheckHours);
    }

    private static void Normalize(ReminderJobOptions o)
    {
        o.RunAtHourUtc = Math.Clamp(o.RunAtHourUtc, 0, 23);
        o.RunAtMinuteUtc = Math.Clamp(o.RunAtMinuteUtc, 0, 59);
        o.StartupDelaySeconds = Math.Max(0, o.StartupDelaySeconds);
        o.DisabledRecheckHours = Math.Max(1, o.DisabledRecheckHours);
        o.WithinDays = Math.Clamp(o.WithinDays, 0, 90);
        o.MinDaysBetweenReminders = Math.Max(0, o.MinDaysBetweenReminders);
    }

    private static void Normalize(RecurringJobOptions o)
    {
        o.IntervalHours = Math.Clamp(o.IntervalHours, 1, 24);
        o.StartupDelaySeconds = Math.Max(0, o.StartupDelaySeconds);
        o.DisabledRecheckHours = Math.Max(1, o.DisabledRecheckHours);
    }

    private static OverdueJobSettingsDto ToDto(OverdueSyncOptions o) => new(
        o.Enabled,
        o.RunAtHourUtc,
        o.RunAtMinuteUtc,
        o.RunOnStartup,
        o.StartupDelaySeconds,
        o.DisabledRecheckHours);

    private static ReminderJobSettingsDto ToDto(ReminderJobOptions o) => new(
        o.Enabled,
        o.RunAtHourUtc,
        o.RunAtMinuteUtc,
        o.RunOnStartup,
        o.StartupDelaySeconds,
        o.DisabledRecheckHours,
        o.WithinDays,
        o.OnlyOverdue,
        o.MinDaysBetweenReminders,
        o.MarkAsSent);

    private static RecurringJobSettingsDto ToDto(RecurringJobOptions o) => new(
        o.Enabled,
        o.IntervalHours,
        o.StartupDelaySeconds,
        o.DisabledRecheckHours);

    private static OverdueSyncOptions FromDto(OverdueJobSettingsDto d) => new()
    {
        Enabled = d.Enabled,
        RunAtHourUtc = d.RunAtHourUtc,
        RunAtMinuteUtc = d.RunAtMinuteUtc,
        RunOnStartup = d.RunOnStartup,
        StartupDelaySeconds = d.StartupDelaySeconds,
        DisabledRecheckHours = d.DisabledRecheckHours,
    };

    private static ReminderJobOptions FromDto(ReminderJobSettingsDto d) => new()
    {
        Enabled = d.Enabled,
        RunAtHourUtc = d.RunAtHourUtc,
        RunAtMinuteUtc = d.RunAtMinuteUtc,
        RunOnStartup = d.RunOnStartup,
        StartupDelaySeconds = d.StartupDelaySeconds,
        DisabledRecheckHours = d.DisabledRecheckHours,
        WithinDays = d.WithinDays,
        OnlyOverdue = d.OnlyOverdue,
        MinDaysBetweenReminders = d.MinDaysBetweenReminders,
        MarkAsSent = d.MarkAsSent,
    };

    private static RecurringJobOptions FromDto(RecurringJobSettingsDto d) => new()
    {
        Enabled = d.Enabled,
        IntervalHours = d.IntervalHours,
        StartupDelaySeconds = d.StartupDelaySeconds,
        DisabledRecheckHours = d.DisabledRecheckHours,
    };

    private static OverdueSyncOptions Clone(OverdueSyncOptions o) => new()
    {
        Enabled = o.Enabled,
        RunAtHourUtc = o.RunAtHourUtc,
        RunAtMinuteUtc = o.RunAtMinuteUtc,
        RunOnStartup = o.RunOnStartup,
        StartupDelaySeconds = o.StartupDelaySeconds,
        DisabledRecheckHours = o.DisabledRecheckHours,
    };

    private static ReminderJobOptions Clone(ReminderJobOptions o) => new()
    {
        Enabled = o.Enabled,
        RunAtHourUtc = o.RunAtHourUtc,
        RunAtMinuteUtc = o.RunAtMinuteUtc,
        RunOnStartup = o.RunOnStartup,
        StartupDelaySeconds = o.StartupDelaySeconds,
        DisabledRecheckHours = o.DisabledRecheckHours,
        WithinDays = o.WithinDays,
        OnlyOverdue = o.OnlyOverdue,
        MinDaysBetweenReminders = o.MinDaysBetweenReminders,
        MarkAsSent = o.MarkAsSent,
    };

    private static RecurringJobOptions Clone(RecurringJobOptions o) => new()
    {
        Enabled = o.Enabled,
        IntervalHours = o.IntervalHours,
        StartupDelaySeconds = o.StartupDelaySeconds,
        DisabledRecheckHours = o.DisabledRecheckHours,
    };

    private sealed record PersistedJobSettings(
        OverdueJobSettingsDto OverdueSync,
        ReminderJobSettingsDto ReminderJob,
        RecurringJobSettingsDto? RecurringInvoices);
}
