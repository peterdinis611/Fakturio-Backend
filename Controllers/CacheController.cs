using Fakturio.Api.Caching;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/cache")]
public sealed class CacheController(ICacheInvalidator cache) : ApiControllerBase
{
    /// <summary>Clears report and company caches (useful after bulk imports / debugging).</summary>
    [HttpPost("invalidate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Invalidate(CancellationToken ct)
    {
        await cache.InvalidateAllAsync(ct);
        return NoContent();
    }
}
