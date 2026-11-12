namespace Fakturio.Api.Configuration;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "AttachmentStorage";

    public string RootPath { get; set; } = "uploads";

    /// <summary>Max file size (default 8 MB).</summary>
    public long MaxBytes { get; set; } = 8 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp",
        "application/pdf",
        "image/heic",
        "image/heif",
    ];
}
