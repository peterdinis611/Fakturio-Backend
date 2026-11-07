namespace Fakturio.Api.Configuration;

public sealed class FxOptions
{
    public const string SectionName = "Fx";

    public string FrankfurterBaseUrl { get; set; } = "https://api.frankfurter.dev/v1/";

    public int TimeoutSeconds { get; set; } = 10;

    public int CacheSeconds { get; set; } = 3600;
}
