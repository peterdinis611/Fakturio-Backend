using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ILogoStorageService
{
    Task<ServiceResult<string>> SaveAsync(IFormFile file, CancellationToken ct = default);

    Task DeleteIfLocalAsync(string? logoUrl, CancellationToken ct = default);

    /// <summary>Read local logo bytes for PDF embedding (PNG/JPEG/WebP). SVG returns null.</summary>
    Task<byte[]?> TryReadImageBytesAsync(string? logoUrl, CancellationToken ct = default);
}
