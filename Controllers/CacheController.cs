using Fakturio.Api.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Authorize(Policy = "Admin")]
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
