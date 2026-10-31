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
    IOptions<CacheOptions> options,
    ITenantContext tenant) : ICompanyService
{
    public async Task<CompanySettingsResponse> GetAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetAsync(ct);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        return await cache.GetOrCreateAsync(
            CacheKeys.CompanySettings(companyId),
            async cancel => await inner.GetAsync(cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.CompanyTtl },
            tags: [CacheTags.Company],
            cancellationToken: ct);
    }

    public async Task<CompanySummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetSummaryAsync(ct);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        return await cache.GetOrCreateAsync(
            CacheKeys.CompanySummary(companyId),
            async cancel => await inner.GetSummaryAsync(cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.CompanyTtl },
            tags: [CacheTags.Company],
            cancellationToken: ct);
    }

    public async Task<CompanyCompletenessResponse> GetStatusAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetStatusAsync(ct);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        return await cache.GetOrCreateAsync(
            CacheKeys.CompanyStatus(companyId),
            async cancel => await inner.GetStatusAsync(cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.CompanyTtl },
            tags: [CacheTags.Company],
            cancellationToken: ct);
    }

    public async Task<CompanyBankResponse> GetBankAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetBankAsync(ct);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        return await cache.GetOrCreateAsync(
            CacheKeys.CompanyBank(companyId),
            async cancel => await inner.GetBankAsync(cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.CompanyTtl },
            tags: [CacheTags.Company],
            cancellationToken: ct);
    }

    public Task<ServiceResult<CompanySettingsResponse>> UpdateAsync(
        UpdateCompanySettingsRequest request,
        CancellationToken ct = default)
        => inner.UpdateAsync(request, ct);

    public Task<ServiceResult<CompanySettingsResponse>> UpdateDefaultsAsync(
        UpdateCompanyDefaultsRequest request,
        CancellationToken ct = default)
        => inner.UpdateDefaultsAsync(request, ct);

    public Task<ServiceResult<CompanySettingsResponse>> UpdateBankAsync(
        UpdateCompanyBankRequest request,
        CancellationToken ct = default)
        => inner.UpdateBankAsync(request, ct);

    public Task<ServiceResult<CompanySettingsResponse>> UpdateBrandingAsync(
        UpdateCompanyBrandingRequest request,
        CancellationToken ct = default)
        => inner.UpdateBrandingAsync(request, ct);

    public Task<ServiceResult<CompanySettingsResponse>> UpdateContactAsync(
        UpdateCompanyContactRequest request,
        CancellationToken ct = default)
        => inner.UpdateContactAsync(request, ct);

    public Task<ServiceResult<CompanySettingsResponse>> UploadLogoAsync(
        IFormFile file,
        CancellationToken ct = default)
        => inner.UploadLogoAsync(file, ct);

    public Task<ServiceResult<CompanySettingsResponse>> DeleteLogoAsync(
        CancellationToken ct = default)
        => inner.DeleteLogoAsync(ct);
}
