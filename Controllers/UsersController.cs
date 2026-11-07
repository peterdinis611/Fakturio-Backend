using System.Security.Claims;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/users")]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Get(Guid id, CancellationToken ct)
    {
        if (ForbidUnlessSelf(id) is { } forbid)
            return forbid;

        return OkOrNotFound(
            await users.GetByIdAsync(id, ct),
            "User not found.",
            ErrorCodes.NotFound);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
    {
        if (ForbidUnlessSelf(id) is { } forbid)
            return forbid;

        return (await users.UpdateAsync(id, request, ct)).ToActionResult(this);
    }

    [HttpPost("{id:guid}/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ChangePassword(
        Guid id,
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        if (ForbidUnlessSelf(id) is { } forbid)
            return forbid;

        return (await users.ChangePasswordAsync(id, request, ct)).ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (ForbidUnlessSelf(id) is { } forbid)
            return forbid;

        return (await users.DeleteAsync(id, ct)).ToActionResult(this);
    }

    private ActionResult? ForbidUnlessSelf(Guid id)
    {
        var current = CurrentUserId();
        if (current is null)
            return Unauthorized();

        if (id != current.Value)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiProblemDetails.From(
                    StatusCodes.Status403Forbidden,
                    "Môžeš spravovať len vlastný účet.",
                    ErrorCodes.Forbidden,
                    HttpContext.Request.Path));
        }

        return null;
    }

    private Guid? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
