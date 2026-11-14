using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Address;

/// <summary>CZ address — RÚIAN first, Nominatim fallback (soft-fail).</summary>
public sealed class CzAddressService(
    RuianAddressService ruian,
    NominatimAddressService nominatim) : ICzAddressService
{
    public async Task<ServiceResult<AddressNormalizeResponse>> LookupAsync(
        string query,
        CancellationToken ct = default)
    {
        var ruianResult = await ruian.TryLookupAsync(query, ct);
        if (ruianResult is { Success: true, Data: not null })
            return ruianResult;

        return await nominatim.NormalizeAsync(query, "CZ", ct, sourceLabel: "nominatim-cz");
    }
}