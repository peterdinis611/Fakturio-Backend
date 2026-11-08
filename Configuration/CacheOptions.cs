namespace Fakturio.Api.Configuration;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Enable/disable application caching.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true and <see cref="RedisConnection"/> (or ConnectionStrings:Redis) is set,
    /// HybridCache uses Redis as distributed L2. Otherwise L1 memory only.
    /// </summary>
    public bool UseRedis { get; set; } = true;

    /// <summary>
    /// Optional Redis connection (overrides ConnectionStrings:Redis when set).
    /// Example: localhost:6379
    /// </summary>
    public string? RedisConnection { get; set; }

    /// <summary>Key prefix for Redis entries (multi-tenant / multi-env isolation).</summary>
    public string RedisInstanceName { get; set; } = "fakturio:";

    /// <summary>Default TTL for report endpoints.</summary>
    public int ReportsSeconds { get; set; } = 60;

    /// <summary>TTL for company settings.</summary>
    public int CompanySeconds { get; set; } = 300;

    public TimeSpan ReportsTtl => TimeSpan.FromSeconds(Math.Clamp(ReportsSeconds, 5, 3600));
    public TimeSpan CompanyTtl => TimeSpan.FromSeconds(Math.Clamp(CompanySeconds, 5, 3600));
}
