using System.Text.Json;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Jobs;

/// <summary>
/// Runtime job settings: defaults from appsettings, overrides persisted to
/// <c>data/job-settings.json</c> so the UI can change schedules without restart.
/// </summary>
public sealed class JobSettingsStore : IJobSettingsStore
{
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

    public JobSettingsStore(
        IHostEnvironment env,
        IOptions<OverdueSyncOptions> overdueDefaults,
        IOptions<ReminderJobOptions> reminderDefaults,
        ILogger<JobSettingsStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "job-settings.json");

        _overdue = Clone(overdueDefaults.Value);
        _reminder = Clone(reminderDefaults.Value);
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

    public JobSettingsResponse GetSnapshot()
    {
        lock (_gate)
        {
            return new JobSettingsResponse(
                ToDto(_overdue),
                ToDto(_reminder));
        }
    }

    public async Task<JobSettingsResponse> UpdateAsync(
        UpdateJobSettingsRequest request,
        CancellationToken ct = default)
    {
        var overdue = FromDto(request.OverdueSync);
        var reminder = FromDto(request.ReminderJob);
        Normalize(overdue);
        Normalize(reminder);

        lock (_gate)
        {
            _overdue = overdue;
            _reminder = reminder;
        }

        var snapshot = GetSnapshot();
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
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
            var parsed = JsonSerializer.Deserialize<JobSettingsResponse>(json, JsonOptions);
            if (parsed is null)
                return;

            lock (_gate)
            {
                _overdue = FromDto(parsed.OverdueSync);
                _reminder = FromDto(parsed.ReminderJob);
                Normalize(_overdue);
                Normalize(_reminder);
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
}
