using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/services")]
public sealed class ServiceCatalogController(IServiceCatalogService services) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ServiceCatalogItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ServiceCatalogItemResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] bool? activeOnly = null,
        CancellationToken ct = default)
        => Ok(await services.ListAsync(page, pageSize, search, activeOnly, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceCatalogItemResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceCatalogItemResponse>> Get(Guid id, CancellationToken ct)
        => OkOrNotFound(
            await services.GetAsync(id, ct),
            "Service not found.",
            ErrorCodes.NotFound);

    [HttpPost]
    [ProducesResponseType(typeof(ServiceCatalogItemResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ServiceCatalogItemResponse>> Create(
        [FromBody] CreateServiceCatalogItemRequest request,
        CancellationToken ct)
    {
        var result = await services.CreateAsync(request, ct);
        return result.ToActionResult(this, nameof(Get), c => new { id = c.Id });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ServiceCatalogItemResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceCatalogItemResponse>> Update(
        Guid id,
        [FromBody] UpdateServiceCatalogItemRequest request,
        CancellationToken ct)
        => (await services.UpdateAsync(id, request, ct)).ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
        => (await services.DeleteAsync(id, ct)).ToActionResult(this);
}
