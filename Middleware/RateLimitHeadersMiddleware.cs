using Fakturio.Api.Configuration;
using Fakturio.Api.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Middleware;

/// <summary>
/// Best-effort <c>X-RateLimit-Limit</c> on allowed responses for rate-limited endpoints.
/// Remaining is not exposed by ASP.NET sliding-window leases; 429 responses set Remaining=0 in OnRejected.
/// </summary>
public sealed class RateLimitHeadersMiddleware(
    RequestDelegate next,
    IOptions<RateLimitingOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!options.Value.Enabled)
                return Task.CompletedTask;

            if (context.Response.StatusCode is < 200 or >= 400)
                return Task.CompletedTask;

            if (context.Response.Headers.ContainsKey("X-RateLimit-Limit"))
                return Task.CompletedTask;

            if (context.GetEndpoint()?.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null)
                return Task.CompletedTask;

            var policy = RateLimitingExtensions.ResolvePolicyName(context);
            if (policy is null)
                return Task.CompletedTask;

            if (!RateLimitingExtensions.TryGetPermitLimit(options.Value, policy, out var limit))
                return Task.CompletedTask;

            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            return Task.CompletedTask;
        });

        await next(context);
    }
}
