namespace Fakturio.Api.Configuration;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    public bool Enabled { get; set; }

    public string SiteKey { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}