using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IPeppolDirectoryService
{
    Task<ServiceResult<PeppolLookupResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}