using Fakturio.Api.Common;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Company;

public sealed class LogoStorageService(
    IWebHostEnvironment env,
    IOptions<LogoStorageOptions> options,
    ILogger<LogoStorageService> logger) : ILogoStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".svg" };

    public async Task<ServiceResult<string>> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        var opts = options.Value;

        if (file.Length <= 0)
            return ServiceResult<string>.BadRequest("Empty file.", ErrorCodes.BadRequest);

        if (file.Length > opts.MaxBytes)
            return ServiceResult<string>.BadRequest(
                $"Logo must be at most {opts.MaxBytes / (1024 * 1024)} MB.",
                ErrorCodes.FileTooLarge);

        var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!opts.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return ServiceResult<string>.BadRequest(
                "Allowed formats: PNG, JPEG, WebP, SVG.",
                ErrorCodes.InvalidFileType);

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            ext = ExtensionForContentType(contentType);

        if (ext is null)
            return ServiceResult<string>.BadRequest(
                "Allowed formats: PNG, JPEG, WebP, SVG.",
                ErrorCodes.InvalidFileType);

        var logosDir = GetLogosDirectory();
        Directory.CreateDirectory(logosDir);

        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absolutePath = Path.Combine(logosDir, fileName);

        await using (var stream = File.Create(absolutePath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var publicPath = $"/uploads/logos/{fileName}";
        logger.LogInformation("Saved company logo to {Path}", publicPath);
        return ServiceResult<string>.Ok(publicPath);
    }

    public Task DeleteIfLocalAsync(string? logoUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return Task.CompletedTask;

        if (!TryResolveLocalPath(logoUrl, out var absolutePath))
            return Task.CompletedTask;

        try
        {
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete logo file {Path}", absolutePath);
        }

        return Task.CompletedTask;
    }

    private string GetLogosDirectory()
    {
        var root = options.Value.RootPath;
        var basePath = Path.IsPathRooted(root)
            ? root
            : Path.Combine(env.ContentRootPath, root);
        return Path.Combine(basePath, "logos");
    }

    private bool TryResolveLocalPath(string logoUrl, out string absolutePath)
    {
        absolutePath = string.Empty;
        var path = logoUrl.Trim();

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            path = uri.AbsolutePath;

        if (!path.StartsWith("/uploads/logos/", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        absolutePath = Path.Combine(GetLogosDirectory(), fileName);
        return true;
    }

    private static string? ExtensionForContentType(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/webp" => ".webp",
        "image/svg+xml" => ".svg",
        _ => null,
    };
}
