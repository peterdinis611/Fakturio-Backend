using Fakturio.Api.Caching;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Reports;

/// <summary>
/// Decorator that caches successful report reads via HybridCache.
/// Validation failures are not cached.
/// </summary>
public sealed class CachingReportService(
    ReportService inner,
    HybridCache cache,
    IOptions<CacheOptions> options,
    ITenantContext tenant) : IReportService
{
    public async Task<ServiceResult<ReportOverviewResponse>> GetOverviewAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetOverviewAsync(from, to, ct);

        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail<ReportOverviewResponse>(rangeError);

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var data = await cache.GetOrCreateAsync(
            CacheKeys.ReportOverview(await tenant.GetCompanyIdAsync(ct), from, to, asOf),
            async cancel =>
            {
                var result = await inner.GetOverviewAsync(from, to, cancel);
                return result.Data!;
            },
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct);

        return ServiceResult<ReportOverviewResponse>.Ok(data);
    }

    public async Task<ServiceResult<MonthlyRevenueResponse>> GetMonthlyRevenueAsync(
        int? year,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetMonthlyRevenueAsync(year, ct);

        var selectedYear = year ?? DateTime.UtcNow.Year;
        if (ServiceGuards.ValidateYear(selectedYear) is { } yearError)
            return Fail<MonthlyRevenueResponse>(yearError);

        var data = await cache.GetOrCreateAsync(
            CacheKeys.ReportMonthly(await tenant.GetCompanyIdAsync(ct), selectedYear),
            async cancel =>
            {
                var result = await inner.GetMonthlyRevenueAsync(selectedYear, cancel);
                return result.Data!;
            },
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct);

        return ServiceResult<MonthlyRevenueResponse>.Ok(data);
    }

    public async Task<ServiceResult<AgingReportResponse>> GetAgingAsync(
        int largestLimit = 10,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetAgingAsync(largestLimit, ct);

        var result = await inner.GetAgingAsync(largestLimit, ct);
        if (!result.Success)
            return result;

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var data = await cache.GetOrCreateAsync(
            CacheKeys.ReportAging(await tenant.GetCompanyIdAsync(ct), asOf, largestLimit),
            async cancel =>
            {
                var innerResult = await inner.GetAgingAsync(largestLimit, cancel);
                return innerResult.Data!;
            },
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct);

        return ServiceResult<AgingReportResponse>.Ok(data);
    }

    public async Task<ServiceResult<TopCustomersReportResponse>> GetTopCustomersAsync(
        DateOnly? from,
        DateOnly? to,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetTopCustomersAsync(from, to, limit, ct);

        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail<TopCustomersReportResponse>(rangeError);

        var data = await cache.GetOrCreateAsync(
            CacheKeys.ReportTopCustomers(await tenant.GetCompanyIdAsync(ct), from, to, limit),
            async cancel =>
            {
                var result = await inner.GetTopCustomersAsync(from, to, limit, cancel);
                return result.Data!;
            },
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct);

        return ServiceResult<TopCustomersReportResponse>.Ok(data);
    }

    public async Task<ServiceResult<VatReportResponse>> GetVatSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetVatSummaryAsync(from, to, ct);

        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail<VatReportResponse>(rangeError);

        var data = await cache.GetOrCreateAsync(
            CacheKeys.ReportVat(await tenant.GetCompanyIdAsync(ct), from, to),
            async cancel =>
            {
                var result = await inner.GetVatSummaryAsync(from, to, cancel);
                return result.Data!;
            },
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct);

        return ServiceResult<VatReportResponse>.Ok(data);
    }

    private static ServiceResult<T> Fail<T>(ServiceResult error) =>
        ServiceResult<T>.Fail(error.Error!, error.StatusCode, error.ErrorCode);
}
