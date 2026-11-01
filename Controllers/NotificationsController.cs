using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationListResponse>> List(
        [FromQuery] int take = 30,
        CancellationToken ct = default)
        => Ok(await notifications.ListAsync(take, ct));

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> UnreadCount(CancellationToken ct)
        => Ok(new { count = await notifications.UnreadCountAsync(ct) });

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MarkRead(Guid id, CancellationToken ct)
        => (await notifications.MarkReadAsync(id, ct)).ToActionResult(this);

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> MarkAllRead(CancellationToken ct)
        => (await notifications.MarkAllReadAsync(ct)).ToActionResult(this);

    [HttpGet("preferences")]
    [ProducesResponseType(typeof(NotificationPreferencesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesResponse>> GetPreferences(CancellationToken ct)
        => Ok(await notifications.GetPreferencesAsync(ct));

    [HttpPut("preferences")]
    [ProducesResponseType(typeof(NotificationPreferencesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferencesResponse>> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken ct)
        => Ok(await notifications.UpdatePreferencesAsync(request, ct));

    [HttpPost("test")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> SendTest(CancellationToken ct)
        => Ok(await notifications.PublishTestAsync(ct));
}
