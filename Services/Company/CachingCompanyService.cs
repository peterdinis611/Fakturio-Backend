using Fakturio.Api.Caching;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Company;

public sealed class CachingCompanyService(
    CompanyService inner,
    HybridCache cache,
    IOptions<CacheOptions> options) : ICompanyService
{
    public Task<CompanySettingsResponse> GetAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetAsync(ct);

        return cache.GetOrCreateAsync(
            CacheKeys.CompanySettings,
            async cancel => await inner.GetAsync(cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.CompanyTtl },
            tags: [CacheTags.Company],
            cancellationToken: ct).AsTask();
    }

    public async Task<CompanySettingsResponse> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default)
    {
        // Inner service invalidates company (+ reports) after save.
        return await inner.UpdateAsync(request, ct);
    }
}
