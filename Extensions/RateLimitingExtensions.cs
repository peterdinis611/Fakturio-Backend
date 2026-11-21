using System.Security.Claims;
using System.Threading.RateLimiting;
using Fakturio.Api.Configuration;
using Microsoft.AspNetCore.RateLimiting;

namespace Fakturio.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string ApiPolicy = "api";
    public const string PublicPolicy = "public";
    public const string IntegrationsPolicy = "integrations";
    public const string PasswordPolicy = "password";

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

                var policy = ResolvePolicyName(context.HttpContext);
                if (policy is not null && TryGetPermitLimit(opts, policy, out var limit))
                {
                    response.Headers["X-RateLimit-Limit"] = limit.ToString();
                    response.Headers["X-RateLimit-Remaining"] = "0";
                }

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
                options.AddPolicy(ApiPolicy, _ => RateLimitPartition.GetNoLimiter("off-api"));
                options.AddPolicy(PublicPolicy, _ => RateLimitPartition.GetNoLimiter("off-public"));
                options.AddPolicy(IntegrationsPolicy, _ => RateLimitPartition.GetNoLimiter("off-integrations"));
                options.AddPolicy(PasswordPolicy, _ => RateLimitPartition.GetNoLimiter("off-password"));
                return;
            }

            options.AddPolicy(AuthPolicy, httpContext =>
                SlidingPartition(
                    $"auth:{GetPartitionKey(httpContext)}",
                    Math.Max(1, opts.AuthPermitLimit),
                    Math.Max(1, opts.AuthWindowSeconds)));

            options.AddPolicy(ApiPolicy, httpContext =>
                SlidingPartition(
                    $"api:{GetPartitionKey(httpContext)}",
                    Math.Max(1, opts.ApiPermitLimit),
                    Math.Max(1, opts.ApiWindowSeconds)));

            options.AddPolicy(PublicPolicy, httpContext =>
                SlidingPartition(
                    $"public:{GetClientIp(httpContext)}",
                    Math.Max(1, opts.PublicPermitLimit),
                    Math.Max(1, opts.PublicWindowSeconds)));

            options.AddPolicy(IntegrationsPolicy, httpContext =>
                SlidingPartition(
                    $"integrations:{GetPartitionKey(httpContext)}",
                    Math.Max(1, opts.IntegrationsPermitLimit),
                    Math.Max(1, opts.IntegrationsWindowSeconds)));

            options.AddPolicy(PasswordPolicy, httpContext =>
                SlidingPartition(
                    $"password:{GetClientIp(httpContext)}",
                    Math.Max(1, opts.PasswordPermitLimit),
                    Math.Max(1, opts.PasswordWindowSeconds)));
        });

        return services;
    }

    private static RateLimitPartition<string> SlidingPartition(
        string partitionKey,
        int permitLimit,
        int windowSeconds)
    {
        var window = TimeSpan.FromSeconds(windowSeconds);
        var segments = Math.Clamp(windowSeconds / 10, 2, 12);

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                SegmentsPerWindow = segments,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    /// <summary>
    /// Authenticated traffic partitions by user id; anonymous by remote IP
    /// (after UseForwardedHeaders — never raw X-Forwarded-For).
    /// </summary>
    public static string GetPartitionKey(HttpContext httpContext)
    {
        var sub = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(sub))
            return $"user:{sub}";

        return $"ip:{GetClientIp(httpContext)}";
    }

    public static string GetClientIp(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static string? ResolvePolicyName(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var enable = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        return enable?.PolicyName;
    }

    public static bool TryGetPermitLimit(RateLimitingOptions opts, string policy, out int limit)
    {
        limit = policy switch
        {
            AuthPolicy => opts.AuthPermitLimit,
            ApiPolicy => opts.ApiPermitLimit,
            PublicPolicy => opts.PublicPermitLimit,
            IntegrationsPolicy => opts.IntegrationsPermitLimit,
            PasswordPolicy => opts.PasswordPermitLimit,
            _ => 0,
        };
        return limit > 0;
    }
}
