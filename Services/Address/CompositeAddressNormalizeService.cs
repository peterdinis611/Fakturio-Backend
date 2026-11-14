using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Address;

public sealed class CompositeAddressNormalizeService(
    RuianAddressService ruian,
    NominatimAddressService nominatim,
    SkAddressService skAddress) : IAddressNormalizeService
{
    public async Task<ServiceResult<AddressNormalizeResponse>> NormalizeAsync(
        string query,
        string? countryCode = "SK",
        CancellationToken ct = default)
    {
        var cc = (countryCode ?? "SK").Trim().ToUpperInvariant();

        if (cc == "CZ")
        {
            var ruianResult = await ruian.TryLookupAsync(query, ct);
            if (ruianResult is { Success: true, Data: not null })
                return ruianResult;

            return await nominatim.NormalizeAsync(query, "CZ", ct, sourceLabel: "nominatim-cz");
        }

        if (cc == "SK")
            return await skAddress.LookupAsync(query, ct);

        var nominatimCc = cc.ToLowerInvariant();
        return await nominatim.NormalizeAsync(query, nominatimCc, ct);
    }
}