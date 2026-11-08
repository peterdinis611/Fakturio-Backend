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

    /// <summary>Open Register of Slovak Firms — search + SK company detail.</summary>
    public string OrsfBaseUrl { get; set; } = "https://api.orsf.sk/v1/";

    /// <summary>EU VIES REST API for VAT ID checks.</summary>
    public string ViesBaseUrl { get; set; } =
        "https://ec.europa.eu/taxation_customs/vies/rest-api/";

    /// <summary>Official ORSR Open Data API.</summary>
    public string OrsrBaseUrl { get; set; } = "https://sluzby.orsr.sk/api/";

    /// <summary>openiban.com — IBAN → bank / BIC (no key).</summary>
    public string OpenIbanBaseUrl { get; set; } = "https://openiban.com/";

    /// <summary>OSM Nominatim — address normalize (rate-limited).</summary>
    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>MF ČR ADISREG — nespolehlivý plátce DPH + účty (SOAP).</summary>
    public string AdisregBaseUrl { get; set; } =
        "https://adisrws.mfcr.cz/dpr/axis2/services/rozhraniCRPDPH.rozhraniCRPDPHSOAP";

    /// <summary>ISIR CUZK WS — insolvenční rejstřík (SOAP).</summary>
    public string IsirBaseUrl { get; set; } =
        "https://isir.justice.cz:8443/isir_cuzk_ws/IsirWsCuzkService";

    /// <summary>Register účtovných závierok (RÚZ) — SK accounting units.</summary>
    public string RegisterUzBaseUrl { get; set; } =
        "https://www.registeruz.sk/cruz-public/api/";

    /// <summary>Prefer ORSF over RPO for SK IČO lookup (falls back to RPO).</summary>
    public bool PreferOrsf { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 15;
}
