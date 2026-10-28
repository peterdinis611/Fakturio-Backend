using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/invoices")]
public sealed class InvoicesController(
    IInvoiceService invoices,
    IInvoicePdfService pdfs) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<InvoiceListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<InvoiceListItemResponse>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] InvoiceDocumentType? documentType = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? issueFrom = null,
        [FromQuery] DateOnly? issueTo = null,
        [FromQuery] bool? overdueOnly = null,
        CancellationToken ct = default)
        => Ok(await invoices.ListAsync(
            new InvoiceListQuery(page, pageSize, status, documentType, customerId, search, issueFrom, issueTo, overdueOnly),
            ct));

    [HttpGet("stats")]
    [ProducesResponseType(typeof(InvoiceStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceStatsResponse>> Stats(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => Ok(await invoices.GetStatsAsync(from, to, ct));

    [HttpGet("reminders")]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceReminderItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceReminderItemResponse>>> Reminders(
        [FromQuery] int withinDays = 7,
        [FromQuery] bool onlyOverdue = false,
        CancellationToken ct = default)
        => Ok(await invoices.GetRemindersAsync(withinDays, onlyOverdue, ct));

    [HttpPost("sync-overdue")]
    [ProducesResponseType(typeof(SyncOverdueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncOverdueResponse>> SyncOverdue(CancellationToken ct)
        => Ok(await invoices.SyncOverdueAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceResponse>> Get(Guid id, CancellationToken ct)
        => OkOrNotFound(
            await invoices.GetByIdAsync(id, ct),
            "Invoice not found.",
            ErrorCodes.InvoiceNotFound);

    [HttpGet("by-number/{number}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceResponse>> GetByNumber(string number, CancellationToken ct)
        => OkOrNotFound(
            await invoices.GetByNumberAsync(number, ct),
            "Invoice not found.",
            ErrorCodes.InvoiceNotFound);

    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> DownloadPdf(Guid id, CancellationToken ct)
        => (await pdfs.GenerateAsync(id, ct)).ToFileResult(this);

    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken ct)
    {
        var result = await invoices.CreateAsync(request, ct);
        return result.ToActionResult(this, nameof(Get), i => new { id = i.Id });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceResponse>> Update(
        Guid id,
        [FromBody] UpdateInvoiceRequest request,
        CancellationToken ct)
        => (await invoices.UpdateAsync(id, request, ct)).ToActionResult(this);

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(InvoiceStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceStatusResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateInvoiceStatusRequest request,
        CancellationToken ct)
        => (await invoices.UpdateStatusAsync(id, request, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(typeof(InvoiceStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceStatusResponse>> MarkPaid(
        Guid id,
        [FromBody] MarkInvoicePaidRequest? request,
        CancellationToken ct)
        => (await invoices.MarkPaidAsync(id, request ?? new MarkInvoicePaidRequest(null), ct))
            .ToActionResult(this);

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(InvoiceStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceStatusResponse>> Cancel(
        Guid id,
        [FromBody] CancelInvoiceRequest request,
        CancellationToken ct)
        => (await invoices.CancelAsync(id, request, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reminders/mark-sent")]
    [ProducesResponseType(typeof(InvoiceReminderItemResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceReminderItemResponse>> MarkReminderSent(
        Guid id,
        CancellationToken ct)
        => (await invoices.MarkReminderSentAsync(id, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceResponse>> Duplicate(Guid id, CancellationToken ct)
    {
        var result = await invoices.DuplicateAsync(id, ct);
        return result.ToActionResult(this, nameof(Get), i => new { id = i.Id });
    }

    [HttpPost("{id:guid}/credit-note")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceResponse>> CreateCreditNote(
        Guid id,
        [FromBody] CreateCreditNoteRequest? request,
        CancellationToken ct)
    {
        var result = await invoices.CreateCreditNoteAsync(
            id,
            request ?? new CreateCreditNoteRequest(null, null, null, null),
            ct);
        return result.ToActionResult(this, nameof(Get), i => new { id = i.Id });
    }

    [HttpPost("{id:guid}/finalize-proforma")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<InvoiceResponse>> FinalizeProforma(
        Guid id,
        [FromBody] FinalizeProformaRequest? request,
        CancellationToken ct)
    {
        var result = await invoices.FinalizeProformaAsync(
            id,
            request ?? new FinalizeProformaRequest(null, null, null),
            ct);
        return result.ToActionResult(this, nameof(Get), i => new { id = i.Id });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
        => (await invoices.DeleteAsync(id, ct)).ToActionResult(this);
}
