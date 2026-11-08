using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IAdisregService
{
    Task<ServiceResult<AdisregStatusResponse>> LookupByDicAsync(
        string dic,
        CancellationToken ct = default);
}
