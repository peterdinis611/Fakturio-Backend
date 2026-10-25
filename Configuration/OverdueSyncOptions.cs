namespace Fakturio.Api.Configuration;

public sealed class OverdueSyncOptions
{
    public const string SectionName = "OverdueSync";

    /// <summary>Enable/disable the background overdue sync job.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>UTC hour (0–23) when the daily sync runs.</summary>
    public int RunAtHourUtc { get; set; } = 6;

    /// <summary>Run once shortly after app start, then on the daily schedule.</summary>
    public bool RunOnStartup { get; set; } = true;
}
