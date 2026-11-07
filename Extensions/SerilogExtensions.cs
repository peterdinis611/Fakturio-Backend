using System.Security.Claims;
using Fakturio.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Controllers;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace Fakturio.Api.Extensions;

public static class SerilogExtensions
{
    public const string DevelopmentOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {RequestMethod} {RequestPath} cid={CorrelationId}{NewLine}{Exception}";

    public static IHostBuilder AddFakturioSerilog(this IHostBuilder host)
    {
        return host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Fakturio.Api")
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName();

            if (context.HostingEnvironment.IsDevelopment())
            {
                configuration.WriteTo.Console(
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate: DevelopmentOutputTemplate);
            }
            else
            {
                var formatter = new CompactJsonFormatter();
                var logsDir = Path.Combine(context.HostingEnvironment.ContentRootPath, "logs");
                Directory.CreateDirectory(logsDir);

                configuration
                    .WriteTo.Console(formatter)
                    .WriteTo.File(
                        formatter,
                        path: Path.Combine(logsDir, "fakturio-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14,
                        shared: true);
            }
        });
    }

    public static IApplicationBuilder UseFakturioRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath}{QueryString} → {StatusCode} in {Elapsed:0.0} ms [{Endpoint}]";
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
                var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
                    && value is string id
                        ? id
                        : httpContext.TraceIdentifier;
                diagnosticContext.Set("CorrelationId", correlationId);

                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirstValue("sub");
                diagnosticContext.Set("UserId", string.IsNullOrEmpty(userId) ? "-" : userId);

                diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? "/");
                diagnosticContext.Set(
                    "QueryString",
                    httpContext.Request.QueryString.HasValue
                        ? httpContext.Request.QueryString.Value!
                        : string.Empty);
                diagnosticContext.Set("Endpoint", ResolveEndpointName(httpContext));
            };
        });
    }

    private static string ResolveEndpointName(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint is null)
            return "-";

        var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is not null)
            return $"{action.ControllerName}.{action.ActionName}";

        return endpoint.DisplayName ?? "-";
    }
}
