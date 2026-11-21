using Fakturio.Api.Configuration;
using Fakturio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Caching;

public sealed class CacheInvalidator(
    HybridCache cache,
    AppDbContext db,
    IOptions<CacheOptions> options,
    ILogger<CacheInvalidator> logger) : ICacheInvalidator
{
    public Task InvalidateReportsAsync(Guid companyId, CancellationToken ct = default)
        => InvalidateTagAsync(CacheTags.Reports(companyId), ct);

    public Task InvalidateCompanyAsync(Guid companyId, CancellationToken ct = default)
        => InvalidateTagAsync(CacheTags.Company(companyId), ct);

    public async Task InvalidateAllAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return;

        // Legacy global tags (pre per-company rollout).
        await InvalidateTagAsync(CacheTags.ReportsGlobal, ct);
        await InvalidateTagAsync(CacheTags.CompanyGlobal, ct);

        var companyIds = await db.CompanySettings
            .AsNoTracking()
            .Select(c => c.Id)
            .ToListAsync(ct);

        foreach (var id in companyIds)
        {
            await InvalidateReportsAsync(id, ct);
            await InvalidateCompanyAsync(id, ct);
        }
    }

    private async Task InvalidateTagAsync(string tag, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return;

        await cache.RemoveByTagAsync(tag, ct);
        logger.LogDebug("Cache tag invalidated: {Tag}", tag);
    }
}
