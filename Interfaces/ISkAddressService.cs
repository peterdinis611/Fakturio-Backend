using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ISkAddressService
{
    Task<ServiceResult<AddressNormalizeResponse>> LookupAsync(
        string query,
        CancellationToken ct = default);
}