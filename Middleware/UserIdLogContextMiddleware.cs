using System.Security.Claims;
using Serilog.Context;

namespace Fakturio.Api.Middleware;

/// <summary>
/// Pushes authenticated UserId into Serilog LogContext for the remainder of the request.
/// Must run after <c>UseAuthentication</c>.
/// </summary>
public sealed class UserIdLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty("UserId", userId))
            await next(context);
    }
}
