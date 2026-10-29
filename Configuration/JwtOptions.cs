namespace Fakturio.Api.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "fakturio";
    public string Audience { get; set; } = "fakturio";
    /// <summary>HMAC secret — min 32 characters.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpireMinutes { get; set; } = 60 * 24 * 7; // 7 days
}
