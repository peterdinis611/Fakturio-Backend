using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/logs")]
public sealed class AuditLogsController(IAuditLogService logs) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AuditLogEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AuditLogEntryResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] AuditAction? action = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
        => Ok(await logs.ListAsync(
            new AuditLogListQuery(page, pageSize, action, entityId, search, from, to),
            ct));
}
