using Fakturio.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Controllers;

[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var utc = DateTime.UtcNow;
        bool databaseUp;
        try
        {
            databaseUp = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            databaseUp = false;
        }

        if (!databaseUp)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "degraded",
                service = "Fakturio.Api",
                database = "down",
                utc,
            });
        }

        return Ok(new
        {
            status = "ok",
            service = "Fakturio.Api",
            database = "up",
            utc,
        });
    }
}
