namespace Fakturio.Api.Configuration;

public sealed class RegistryOptions
{
    public const string SectionName = "Registry";

    public string AresBaseUrl { get; set; } =
        "https://ares.gov.cz/ekonomicke-subjekty-v-be/rest/";

    public string RpoBaseUrl { get; set; } =
        "https://api.statistics.sk/rpo/v1/";

    /// <summary>
    /// Aggregates RPO + Finančná správa tax IDs (DIČ / IČ DPH) for Slovak entities.
    /// </summary>
    public string SubjektBaseUrl { get; set; } =
        "https://api.subjekt.sk/v1/";

    public int TimeoutSeconds { get; set; } = 15;
}
