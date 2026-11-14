using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Address;

public sealed class SkAddressService(NominatimAddressService nominatim) : ISkAddressService
{
    public Task<ServiceResult<AddressNormalizeResponse>> LookupAsync(
        string query,
        CancellationToken ct = default)
        => nominatim.NormalizeAsync(query, "SK", ct, sourceLabel: "nominatim-sk");
}