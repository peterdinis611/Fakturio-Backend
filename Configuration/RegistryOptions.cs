namespace Fakturio.Api.Configuration;

public sealed class RegistryOptions
{
    public const string SectionName = "Registry";

    public string AresBaseUrl { get; set; } =
        "https://ares.gov.cz/ekonomicke-subjekty-v-be/rest/";

    public string RpoBaseUrl { get; set; } =
        "https://api.statistics.sk/rpo/v1/";

    public int TimeoutSeconds { get; set; } = 15;
}
