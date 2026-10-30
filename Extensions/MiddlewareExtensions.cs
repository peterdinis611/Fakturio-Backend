using Fakturio.Api.Middleware;

namespace Fakturio.Api.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseFakturioMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestTimingMiddleware>();
        return app;
    }
}
