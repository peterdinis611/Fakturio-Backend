using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/customers")]
public sealed class CustomersController(ICustomerService customers) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await customers.ListAsync(page, pageSize, search, ct));

    [HttpGet("lookup")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerLookupItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerLookupItem>>> Lookup(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
        => Ok(await customers.LookupAsync(search, limit, ct));

    [HttpGet("by-ico/{ico}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> GetByIco(string ico, CancellationToken ct)
        => OkOrNotFound(
            await customers.GetByIcoAsync(ico, ct),
            "Customer not found.",
            ErrorCodes.CustomerNotFound);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken ct)
        => OkOrNotFound(
            await customers.GetAsync(id, ct),
            "Customer not found.",
            ErrorCodes.NotFound);

    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(CustomerSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerSummaryResponse>> Summary(Guid id, CancellationToken ct)
        => OkOrNotFound(
            await customers.GetSummaryAsync(id, ct),
            "Customer not found.",
            ErrorCodes.CustomerNotFound);

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        var result = await customers.CreateAsync(request, ct);
        return result.ToActionResult(this, nameof(Get), c => new { id = c.Id });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerResponse>> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken ct)
        => (await customers.UpdateAsync(id, request, ct)).ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
        => (await customers.DeleteAsync(id, ct)).ToActionResult(this);
}
