namespace Fakturio.Api.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "fakturio";
    public string Audience { get; set; } = "fakturio";
    /// <summary>HMAC secret — min 32 characters.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Access-token lifetime. Prefer short values in production (e.g. 60).</summary>
    public int ExpireMinutes { get; set; } = 60;
    /// <summary>SignalR hub-token lifetime (minutes). Short-lived by design.</summary>
    public int HubExpireMinutes { get; set; } = 5;
}
