using Fakturio.Api.Caching;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
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

    public Task<CompanySummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetSummaryAsync(ct);

        return cache.GetOrCreateAsync(
            CacheKeys.CompanySummary,
            async cancel => await inner.GetSummaryAsync(cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.CompanyTtl },
            tags: [CacheTags.Company],
            cancellationToken: ct).AsTask();
    }

    public Task<CompanyCompletenessResponse> GetStatusAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetStatusAsync(ct);

        return cache.GetOrCreateAsync(
            CacheKeys.CompanyStatus,
            async cancel => await inner.GetStatusAsync(cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.CompanyTtl },
            tags: [CacheTags.Company],
            cancellationToken: ct).AsTask();
    }

    public Task<ServiceResult<CompanySettingsResponse>> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default)
        => inner.UpdateAsync(request, ct);

    public Task<ServiceResult<CompanySettingsResponse>> UpdateDefaultsAsync(
        UpdateCompanyDefaultsRequest request,
        CancellationToken ct = default)
        => inner.UpdateDefaultsAsync(request, ct);
}
