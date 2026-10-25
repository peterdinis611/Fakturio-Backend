using Fakturio.Api.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Fakturio.Api.Infrastructure;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problem = ApiProblemDetails.From(
            StatusCodes.Status500InternalServerError,
            environment.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred.",
            "internal_error",
            httpContext.Request.Path);

        if (environment.IsDevelopment())
            problem.Extensions["exception"] = exception.GetType().Name;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
