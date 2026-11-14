using System.Threading.RateLimiting;
using Fakturio.Api.Configuration;
using Microsoft.AspNetCore.RateLimiting;

namespace Fakturio.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string PublicPolicy = "public";
    public const string IntegrationsPolicy = "integrations";

    public static IServiceCollection AddFakturioRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));
        var opts = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                await response.WriteAsJsonAsync(
                    new
                    {
                        type = $"https://httpstatuses.com/{StatusCodes.Status429TooManyRequests}",
                        title = "Too Many Requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "Rate limit exceeded. Try again later.",
                        code = "rate_limited",
                        instance = context.HttpContext.Request.Path.Value,
                    },
                    cancellationToken);
            };

            if (!opts.Enabled)
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetNoLimiter("off"));
                options.AddPolicy(AuthPolicy, _ => RateLimitPartition.GetNoLimiter("off-auth"));
                options.AddPolicy(PublicPolicy, _ => RateLimitPartition.GetNoLimiter("off-public"));
                options.AddPolicy(IntegrationsPolicy, _ => RateLimitPartition.GetNoLimiter("off-integrations"));
                return;
            }

            options.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"auth:{GetClientKey(httpContext)}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, opts.AuthPermitLimit),
                        Window = TimeSpan.FromSeconds(Math.Max(1, opts.AuthWindowSeconds)),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(PublicPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"public:{GetClientKey(httpContext)}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, opts.PublicPermitLimit),
                        Window = TimeSpan.FromSeconds(Math.Max(1, opts.PublicWindowSeconds)),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(IntegrationsPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"integrations:{GetClientKey(httpContext)}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, opts.IntegrationsPermitLimit),
                        Window = TimeSpan.FromSeconds(Math.Max(1, opts.IntegrationsWindowSeconds)),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });

        return services;
    }

    private static string GetClientKey(HttpContext httpContext)
    {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
