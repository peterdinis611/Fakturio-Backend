namespace Fakturio.Api.Configuration;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Enable/disable application caching.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default TTL for report endpoints.</summary>
    public int ReportsSeconds { get; set; } = 60;

    /// <summary>TTL for company settings.</summary>
    public int CompanySeconds { get; set; } = 300;

    public TimeSpan ReportsTtl => TimeSpan.FromSeconds(Math.Clamp(ReportsSeconds, 5, 3600));
    public TimeSpan CompanyTtl => TimeSpan.FromSeconds(Math.Clamp(CompanySeconds, 5, 3600));
}
