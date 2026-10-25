using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Fakturio.Api.Configuration;

namespace Fakturio.Api.Caching;

public sealed class CacheInvalidator(
    HybridCache cache,
    IOptions<CacheOptions> options,
    ILogger<CacheInvalidator> logger) : ICacheInvalidator
{
    public Task InvalidateReportsAsync(CancellationToken ct = default)
        => InvalidateTagAsync(CacheTags.Reports, ct);

    public Task InvalidateCompanyAsync(CancellationToken ct = default)
        => InvalidateTagAsync(CacheTags.Company, ct);

    public async Task InvalidateAllAsync(CancellationToken ct = default)
    {
        await InvalidateReportsAsync(ct);
        await InvalidateCompanyAsync(ct);
    }

    private async Task InvalidateTagAsync(string tag, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return;

        await cache.RemoveByTagAsync(tag, ct);
        logger.LogDebug("Cache tag invalidated: {Tag}", tag);
    }
}
