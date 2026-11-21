using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IOpenDataSearchService
{
    Task<ServiceResult<OpenDataSearchResponse>> SearchAsync(
        string q,
        string? country = "SK",
        CancellationToken ct = default);
}
