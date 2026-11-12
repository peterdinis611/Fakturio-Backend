using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/invoices/{invoiceId:guid}/attachments")]
public sealed class InvoiceAttachmentsController(IInvoiceAttachmentService attachments) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceAttachmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceAttachmentResponse>>> List(
        Guid invoiceId,
        CancellationToken ct)
        => (await attachments.ListAsync(invoiceId, ct)).ToActionResult(this);

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(InvoiceAttachmentResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceAttachmentResponse>> Upload(
        Guid invoiceId,
        IFormFile file,
        CancellationToken ct)
        => (await attachments.UploadAsync(invoiceId, file, ct)).ToActionResult(this);
}

[Route("api/attachments")]
public sealed class AttachmentsController(IInvoiceAttachmentService attachments) : ApiControllerBase
{
    [HttpGet("{id:guid}/download")]
    public async Task<ActionResult> Download(Guid id, CancellationToken ct)
        => (await attachments.DownloadAsync(id, ct)).ToFileResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
        => (await attachments.DeleteAsync(id, ct)).ToActionResult(this);
}
