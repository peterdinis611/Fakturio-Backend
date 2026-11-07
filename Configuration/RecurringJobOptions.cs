namespace Fakturio.Api.Configuration;

public sealed class RecurringJobOptions
{
    public const string SectionName = "RecurringInvoices";

    public bool Enabled { get; set; } = true;

    /// <summary>How often to scan due templates (1–24 hours).</summary>
    public int IntervalHours { get; set; } = 1;

    public int StartupDelaySeconds { get; set; } = 45;

    /// <summary>How often to re-check when the job is disabled.</summary>
    public int DisabledRecheckHours { get; set; } = 1;
}
