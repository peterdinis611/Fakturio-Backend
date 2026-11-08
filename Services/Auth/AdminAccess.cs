using System.Security.Claims;
using Fakturio.Api.Configuration;
using Fakturio.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace Fakturio.Api.Services.Auth;

public sealed class AdminAccess(IOptions<AdminOptions> options) : IAdminAccess
{
    public const string RoleName = "Admin";

    private readonly HashSet<string> _emails = new(
        (options.Value.Emails ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant()),
        StringComparer.Ordinal);

    public bool IsAdminEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || _emails.Count == 0)
            return false;

        return _emails.Contains(email.Trim().ToLowerInvariant());
    }

    public bool IsAdmin(ClaimsPrincipal? user)
    {
        if (user is null)
            return false;

        if (user.IsInRole(RoleName)
            || user.HasClaim(ClaimTypes.Role, RoleName)
            || user.HasClaim("role", RoleName))
        {
            return true;
        }

        var email = user.FindFirstValue(ClaimTypes.Email)
                    ?? user.FindFirstValue(ClaimTypes.Upn)
                    ?? user.FindFirstValue("email");
        return IsAdminEmail(email);
    }
}
