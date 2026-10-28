using System.Security.Claims;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Authorize]
[Route("api/users")]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await users.ListAsync(page, pageSize, search, ct));

    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        var clerkId = GetClerkUserId();
        if (clerkId is null)
            return Unauthorized();

        return OkOrNotFound(
            await users.GetByClerkIdAsync(clerkId, ct),
            "User not found.",
            ErrorCodes.NotFound);
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<UserResponse>> Sync(
        [FromBody] SyncUserRequest request,
        CancellationToken ct)
    {
        var clerkId = GetClerkUserId();
        if (clerkId is null)
            return Unauthorized();

        return (await users.SyncAsync(clerkId, request, ct)).ToActionResult(this);
    }

    private string? GetClerkUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }
}
