using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ILogoStorageService
{
    Task<ServiceResult<string>> SaveAsync(IFormFile file, CancellationToken ct = default);

    Task DeleteIfLocalAsync(string? logoUrl, CancellationToken ct = default);
}
