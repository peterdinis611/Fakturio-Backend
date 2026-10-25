using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return Ok(await customers.ListAsync(page, pageSize, search, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken ct)
    {
        var customer = await customers.GetAsync(id, ct);
        return customer is null ? NotFound(new ApiError("Customer not found.")) : Ok(customer);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        var result = await customers.CreateAsync(request, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return CreatedAtAction(nameof(Get), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken ct)
    {
        var result = await customers.UpdateAsync(id, request, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await customers.DeleteAsync(id, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return NoContent();
    }
}
