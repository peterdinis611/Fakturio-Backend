namespace Fakturio.Api.Configuration;

public sealed class ForwardedHeadersConfig
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// When true, clear KnownProxies/Networks so any peer can set X-Forwarded-*.
    /// Only enable behind a trusted edge that strips client-supplied XFF.
    /// </summary>
    public bool TrustAllProxies { get; set; }

    /// <summary>Explicit proxy IPs allowed to set X-Forwarded-For / Proto.</summary>
    public string[] KnownProxies { get; set; } = ["127.0.0.1", "::1"];
}
