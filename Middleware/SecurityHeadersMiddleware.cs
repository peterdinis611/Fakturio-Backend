namespace Fakturio.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            Apply(context.Response.Headers, IsHttps(context.Request));
            return Task.CompletedTask;
        });

        await next(context);
    }

    public static void Apply(IHeaderDictionary headers, bool isHttps)
    {
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        headers["Cache-Control"] = "no-store";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        if (isHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    public static bool IsHttps(HttpRequest request) =>
        request.IsHttps
        || string.Equals(
            request.Headers["X-Forwarded-Proto"].ToString(),
            "https",
            StringComparison.OrdinalIgnoreCase);
}
