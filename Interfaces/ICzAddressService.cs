using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface ICzAddressService
{
    Task<ServiceResult<AddressNormalizeResponse>> LookupAsync(
        string query,
        CancellationToken ct = default);
}