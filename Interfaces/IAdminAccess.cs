using System.Security.Claims;

namespace Fakturio.Api.Interfaces;

public interface IAdminAccess
{
    bool IsAdminEmail(string? email);

    bool IsAdmin(ClaimsPrincipal? user);
}
