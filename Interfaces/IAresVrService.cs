using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IAresVrService
{
    Task<ServiceResult<AresVrSummaryResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}
