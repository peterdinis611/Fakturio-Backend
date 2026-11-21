namespace Fakturio.Api.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;

    public int ApiPermitLimit { get; set; } = 120;
    public int ApiWindowSeconds { get; set; } = 60;

    public int PublicPermitLimit { get; set; } = 60;
    public int PublicWindowSeconds { get; set; } = 60;

    public int IntegrationsPermitLimit { get; set; } = 30;
    public int IntegrationsWindowSeconds { get; set; } = 60;

    /// <summary>HIBP password-check — tighter than general integrations.</summary>
    public int PasswordPermitLimit { get; set; } = 5;
    public int PasswordWindowSeconds { get; set; } = 60;
}
