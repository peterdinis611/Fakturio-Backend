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
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return System;

        var clerkId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(clerkId))
            return System;

        var appUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.ClerkUserId == clerkId, ct);

        if (appUser is null)
        {
            var email = user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue("email");
            var name = user.FindFirstValue("name")
                ?? email
                ?? clerkId;
            return new AuditActor(null, clerkId, name, email);
        }

        var display = string.Join(
            ' ',
            new[] { appUser.FirstName, appUser.LastName }
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            .Trim();

        if (string.IsNullOrWhiteSpace(display))
            display = appUser.Email ?? appUser.ClerkUserId;

        return new AuditActor(
            appUser.Id,
            appUser.ClerkUserId,
            display,
            appUser.Email);
    }
}
