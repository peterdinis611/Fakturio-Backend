namespace Fakturio.Api.Configuration;

public sealed class OverdueSyncOptions
{
    public const string SectionName = "OverdueSync";

    /// <summary>Enable/disable the background overdue sync job.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>UTC hour (0–23) when the daily sync runs.</summary>
    public int RunAtHourUtc { get; set; } = 6;

    /// <summary>UTC minute (0–59) within <see cref="RunAtHourUtc"/>.</summary>
    public int RunAtMinuteUtc { get; set; }

    /// <summary>Run once shortly after app start, then on the daily schedule.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Delay before the optional startup run.</summary>
    public int StartupDelaySeconds { get; set; } = 15;

    /// <summary>How often to re-check when the job is disabled.</summary>
    public int DisabledRecheckHours { get; set; } = 1;
}
