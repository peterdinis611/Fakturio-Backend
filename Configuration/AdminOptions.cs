namespace Fakturio.Api.Configuration;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>Emails allowed to access /api/admin and see platform overview.</summary>
    public string[] Emails { get; set; } = [];
}
