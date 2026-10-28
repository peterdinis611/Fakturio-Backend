using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IServiceCatalogService
{
    Task<PagedResponse<ServiceCatalogItemResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool? activeOnly,
        CancellationToken ct = default);

    Task<ServiceCatalogItemResponse?> GetAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<ServiceCatalogItemResponse>> CreateAsync(
        CreateServiceCatalogItemRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ServiceCatalogItemResponse>> UpdateAsync(
        Guid id,
        UpdateServiceCatalogItemRequest request,
        CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
