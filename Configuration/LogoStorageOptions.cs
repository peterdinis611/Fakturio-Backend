namespace Fakturio.Api.Configuration;

public sealed class LogoStorageOptions
{
    public const string SectionName = "LogoStorage";

    /// <summary>Root folder relative to content root (or absolute).</summary>
    public string RootPath { get; set; } = "uploads";

    public long MaxBytes { get; set; } = 2 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp",
        "image/svg+xml",
    ];
}
