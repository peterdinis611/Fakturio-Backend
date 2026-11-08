using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ILeiLookupService
{
    Task<ServiceResult<LeiLookupResponse>> LookupByIcoAsync(
        string ico,
        string? jurisdiction = null,
        CancellationToken ct = default);

    Task<ServiceResult<LeiLookupResponse>> LookupByLeiAsync(
        string lei,
        CancellationToken ct = default);
}
