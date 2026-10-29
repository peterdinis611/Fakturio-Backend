using System.Security.Claims;
using Serilog;
using Serilog.Events;

namespace Fakturio.Api.Extensions;

public static class SerilogExtensions
{
    public static IHostBuilder AddFakturioSerilog(this IHostBuilder host)
    {
        return host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Fakturio.Api"));
    }

    public static IApplicationBuilder UseFakturioRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms TraceId={TraceId} UserId={UserId}";
            options.GetLevel = (httpContext, _, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                    return LogEventLevel.Error;

                if (httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest)
                    return LogEventLevel.Warning;

                if (HttpMethods.IsGet(httpContext.Request.Method)
                    && httpContext.Request.Path.StartsWithSegments("/api/health"))
                    return LogEventLevel.Debug;

                return LogEventLevel.Information;
            };
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirstValue("sub");
                diagnosticContext.Set("UserId", string.IsNullOrEmpty(userId) ? "-" : userId);
            };
        });
    }
}
