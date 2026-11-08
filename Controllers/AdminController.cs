using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Authorize(Policy = "Admin")]
[Route("api/admin")]
public sealed class AdminController(IAdminService admin) : ApiControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(AdminOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminOverviewResponse>> Overview(CancellationToken ct)
        => (await admin.GetOverviewAsync(ct)).ToActionResult(this);

    [HttpGet("users")]
    [ProducesResponseType(typeof(PagedResponse<AdminUserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminUserSummaryDto>>> Users(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => (await admin.ListUsersAsync(page, pageSize, search, ct)).ToActionResult(this);
}
