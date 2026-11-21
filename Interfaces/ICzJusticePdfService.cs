using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ICzJusticePdfService
{
    Task<ServiceResult<CzJusticePdfResponse>> LookupByIcoAsync(string ico, CancellationToken ct = default);
}
