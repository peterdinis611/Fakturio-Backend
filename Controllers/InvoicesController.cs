using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[ApiController]
[Route("api/invoices")]
[Produces("application/json")]
public class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<InvoiceListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<InvoiceListItemResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? issueFrom = null,
        [FromQuery] DateOnly? issueTo = null,
        CancellationToken ct = default)
    {
        var query = new InvoiceListQuery(page, pageSize, status, customerId, search, issueFrom, issueTo);
        return Ok(await invoices.ListAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> Get(Guid id, CancellationToken ct)
    {
        var invoice = await invoices.GetByIdAsync(id, ct);
        return invoice is null ? NotFound(new ApiError("Invoice not found.")) : Ok(invoice);
    }

    [HttpGet("by-number/{number}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> GetByNumber(string number, CancellationToken ct)
    {
        var invoice = await invoices.GetByNumberAsync(number, ct);
        return invoice is null ? NotFound(new ApiError("Invoice not found.")) : Ok(invoice);
    }

    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken ct)
    {
        var result = await invoices.CreateAsync(request, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return CreatedAtAction(nameof(Get), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceResponse>> Update(
        Guid id,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken ct)
    {
        var result = await invoices.UpdateAsync(id, request, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return Ok(result.Data);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(InvoiceStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceStatusResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateInvoiceStatusRequest request,
        CancellationToken ct)
    {
        var result = await invoices.UpdateStatusAsync(id, request, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> Duplicate(Guid id, CancellationToken ct)
    {
        var result = await invoices.DuplicateAsync(id, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return CreatedAtAction(nameof(Get), new { id = result.Data!.Id }, result.Data);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await invoices.DeleteAsync(id, ct);
        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiError(result.Error!, result.ErrorCode));

        return NoContent();
    }
}
