using System.Security.Claims;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Audit;

public sealed class CurrentActorAccessor(
    IHttpContextAccessor httpContextAccessor,
    AppDbContext db) : ICurrentActorAccessor
{
    public static readonly AuditActor System = new(
        null,
        "system",
        "Systém",
        null);

    public async Task<AuditActor> GetAsync(CancellationToken ct = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return System;

        var rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(rawId, out var userId))
            return System;

        var appUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (appUser is null)
        {
            var email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue("email");
            return new AuditActor(userId, userId.ToString(), email ?? userId.ToString(), email);
        }

        var display = string.Join(
            ' ',
            new[] { appUser.FirstName, appUser.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            .Trim();

        if (string.IsNullOrWhiteSpace(display))
            display = appUser.Email;

        return new AuditActor(
            appUser.Id,
            appUser.Id.ToString(),
            display,
            appUser.Email);
    }
}
