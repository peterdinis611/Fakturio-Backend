namespace Fakturio.Api.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;

    public int PublicPermitLimit { get; set; } = 60;
    public int PublicWindowSeconds { get; set; } = 60;

    public int IntegrationsPermitLimit { get; set; } = 30;
    public int IntegrationsWindowSeconds { get; set; } = 60;
}
