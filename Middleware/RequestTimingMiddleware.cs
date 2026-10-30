using System.Diagnostics;

namespace Fakturio.Api.Middleware;

public sealed class RequestTimingMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Response-Time-Ms";

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();

        context.Response.OnStarting(() =>
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            context.Response.Headers[HeaderName] = elapsedMs.ToString("0.00");
            return Task.CompletedTask;
        });

        await next(context);
    }
}
