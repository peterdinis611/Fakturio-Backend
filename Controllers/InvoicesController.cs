using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Fakturio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[ApiController]
[Route("api/invoices")]
[Produces("application/json")]
public class InvoicesController(InvoiceService invoices) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<InvoiceListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<InvoiceListItemResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return Ok(await invoices.ListAsync(page, pageSize, status, customerId, search, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> Get(Guid id, CancellationToken ct)
    {
        var invoice = await invoices.GetAsync(id, ct);
        return invoice is null ? NotFound(new ApiError("Invoice not found.")) : Ok(invoice);
    }

    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken ct)
    {
        var (invoice, error, status) = await invoices.CreateAsync(request, ct);
        if (error is not null)
            return StatusCode(status, new ApiError(error));

        return CreatedAtAction(nameof(Get), new { id = invoice!.Id }, invoice);
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
        var (invoice, error, status) = await invoices.UpdateAsync(id, request, ct);
        if (error is not null)
            return StatusCode(status, new ApiError(error));

        return Ok(invoice);
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
        var (result, error, status) = await invoices.UpdateStatusAsync(id, request, ct);
        if (error is not null)
            return StatusCode(status, new ApiError(error));

        return Ok(result);
    }

    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> Duplicate(Guid id, CancellationToken ct)
    {
        var (invoice, error, status) = await invoices.DuplicateAsync(id, ct);
        if (error is not null)
            return StatusCode(status, new ApiError(error));

        return CreatedAtAction(nameof(Get), new { id = invoice!.Id }, invoice);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (ok, error, status) = await invoices.DeleteAsync(id, ct);
        if (!ok)
            return StatusCode(status, new ApiError(error!));

        return NoContent();
    }
}
