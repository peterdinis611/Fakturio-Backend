using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/recurring-invoices")]
public sealed class RecurringInvoicesController(IRecurringInvoiceService recurring) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<RecurringInvoiceListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<RecurringInvoiceListItemResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? activeOnly = null,
        CancellationToken ct = default)
        => Ok(await recurring.ListAsync(page, pageSize, activeOnly, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RecurringInvoiceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecurringInvoiceResponse>> Get(Guid id, CancellationToken ct)
        => OkOrNotFound(
            await recurring.GetAsync(id, ct),
            "Recurring invoice not found.",
            ErrorCodes.NotFound);

    [HttpPost]
    [ProducesResponseType(typeof(RecurringInvoiceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecurringInvoiceResponse>> Create(
        [FromBody] CreateRecurringInvoiceRequest request,
        CancellationToken ct)
    {
        var result = await recurring.CreateAsync(request, ct);
        return result.ToActionResult(this, nameof(Get), c => new { id = c.Id });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RecurringInvoiceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecurringInvoiceResponse>> Update(
        Guid id,
        [FromBody] UpdateRecurringInvoiceRequest request,
        CancellationToken ct)
        => (await recurring.UpdateAsync(id, request, ct)).ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
        => (await recurring.DeleteAsync(id, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/generate")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceResponse>> Generate(Guid id, CancellationToken ct)
    {
        var result = await recurring.GenerateNowAsync(id, ct);
        return result.ToActionResult(this);
    }
}
