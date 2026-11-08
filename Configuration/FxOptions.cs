namespace Fakturio.Api.Configuration;

public sealed class FxOptions
{
    public const string SectionName = "Fx";

    public string FrankfurterBaseUrl { get; set; } = "https://api.frankfurter.dev/v1/";

    /// <summary>ČNB denní kurz TXT (CZ).</summary>
    public string CnbBaseUrl { get; set; } =
        "https://www.cnb.cz/cs/financni-trhy/devizovy-trh/kurzy-devizoveho-trhu/kurzy-devizoveho-trhu/";

    /// <summary>NBS / ECB daily FX list (CSV) — foreign currency per 1 EUR.</summary>
    public string NbsBaseUrl { get; set; } = "https://nbs.sk/export/sk/exchange-rate/";

    public int TimeoutSeconds { get; set; } = 10;

    public int CacheSeconds { get; set; } = 3600;
}
