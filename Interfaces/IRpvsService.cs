using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IRpvsService
{
    Task<ServiceResult<RpvsStatusResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}
