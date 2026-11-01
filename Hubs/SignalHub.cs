using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fakturio.Api.Hubs;

/// <summary>
/// Real-time notification channel. Clients join a per-user group after connect.
/// </summary>
[Authorize]
public sealed class SignalHub : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId:N}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (Guid.TryParse(userId, out var id))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(id));

        await base.OnConnectedAsync();
    }
}
