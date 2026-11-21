using Fakturio.Api.Configuration;
using Fakturio.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Fakturio.Api.Controllers;

/// <summary>
/// Single operational health probe: database, Redis (when enabled), and job runner mode.
/// Also reachable via redirect from <c>/health</c>.
/// </summary>
[AllowAnonymous]
[DisableRateLimiting]
[Route("api/health")]
public sealed class HealthController(
    AppDbContext db,
    IConfiguration configuration,
    IOptions<BullMqOptions> bullMqOptions) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var utc = DateTime.UtcNow;
        var database = await CheckDatabaseAsync(ct);
        var redis = await CheckRedisAsync(ct);
        var jobs = DescribeJobs();

        var degraded = database != "up" || redis is "down";
        var payload = new
        {
            status = degraded ? "degraded" : "ok",
            service = "Fakturio.Api",
            database,
            redis,
            jobs,
            utc,
        };

        return degraded
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, payload)
            : Ok(payload);
    }

    private async Task<string> CheckDatabaseAsync(CancellationToken ct)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct) ? "up" : "down";
        }
        catch
        {
            return "down";
        }
    }

    private async Task<string> CheckRedisAsync(CancellationToken ct)
    {
        var useRedis = configuration.GetValue("Cache:UseRedis", true);
        if (!useRedis)
            return "disabled";

        var connection =
            configuration["Cache:RedisConnection"]
            ?? configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(connection))
            return "unconfigured";

        try
        {
            await using var mux = await ConnectionMultiplexer.ConnectAsync(connection);
            _ = await mux.GetDatabase().PingAsync();
            return "up";
        }
        catch
        {
            return "down";
        }
    }

    private object DescribeJobs()
    {
        var bullEnabled = bullMqOptions.Value.Enabled;
        return new
        {
            reminders = bullEnabled ? "bullmq" : "in-process",
            recurring = bullEnabled ? "bullmq" : "in-process",
            overdueSync = "in-process",
        };
    }
}
