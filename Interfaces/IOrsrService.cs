using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IOrsrService
{
    Task<ServiceResult<OrsrCompanySummaryResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}
