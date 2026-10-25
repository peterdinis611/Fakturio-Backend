using Fakturio.Api.Caching;
using Fakturio.Api.Configuration;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Reports;

/// <summary>
/// Decorator that caches expensive report reads via HybridCache.
/// </summary>
public sealed class CachingReportService(
    ReportService inner,
    HybridCache cache,
    IOptions<CacheOptions> options) : IReportService
{
    public Task<ReportOverviewResponse> GetOverviewAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetOverviewAsync(from, to, ct);

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        return cache.GetOrCreateAsync(
            CacheKeys.ReportOverview(from, to, asOf),
            async cancel => await inner.GetOverviewAsync(from, to, cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct).AsTask();
    }

    public Task<MonthlyRevenueResponse> GetMonthlyRevenueAsync(
        int? year,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetMonthlyRevenueAsync(year, ct);

        var selectedYear = year ?? DateTime.UtcNow.Year;
        return cache.GetOrCreateAsync(
            CacheKeys.ReportMonthly(selectedYear),
            async cancel => await inner.GetMonthlyRevenueAsync(selectedYear, cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct).AsTask();
    }

    public Task<AgingReportResponse> GetAgingAsync(
        int largestLimit = 10,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetAgingAsync(largestLimit, ct);

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        return cache.GetOrCreateAsync(
            CacheKeys.ReportAging(asOf, largestLimit),
            async cancel => await inner.GetAgingAsync(largestLimit, cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct).AsTask();
    }

    public Task<TopCustomersReportResponse> GetTopCustomersAsync(
        DateOnly? from,
        DateOnly? to,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetTopCustomersAsync(from, to, limit, ct);

        return cache.GetOrCreateAsync(
            CacheKeys.ReportTopCustomers(from, to, limit),
            async cancel => await inner.GetTopCustomersAsync(from, to, limit, cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct).AsTask();
    }

    public Task<VatReportResponse> GetVatSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return inner.GetVatSummaryAsync(from, to, ct);

        return cache.GetOrCreateAsync(
            CacheKeys.ReportVat(from, to),
            async cancel => await inner.GetVatSummaryAsync(from, to, cancel),
            new HybridCacheEntryOptions { Expiration = options.Value.ReportsTtl },
            tags: [CacheTags.Reports],
            cancellationToken: ct).AsTask();
    }
}
