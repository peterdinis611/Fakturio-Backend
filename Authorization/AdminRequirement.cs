using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Fakturio.Api.Authorization;

public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminAuthorizationHandler(IAdminAccess adminAccess)
    : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        if (adminAccess.IsAdmin(context.User))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
