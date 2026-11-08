using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IIsirService
{
    Task<ServiceResult<IsirStatusResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}
