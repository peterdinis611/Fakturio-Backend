using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IAddressNormalizeService
{
    Task<ServiceResult<AddressNormalizeResponse>> NormalizeAsync(
        string query,
        string? countryCode = "SK",
        CancellationToken ct = default);
}
