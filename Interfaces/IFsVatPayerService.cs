using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IFsVatPayerService
{
    Task<ServiceResult<FsVatPayerResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default);
}
