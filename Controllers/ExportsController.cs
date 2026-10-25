using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/exports")]
public sealed class ExportsController(IExportService exports) : ApiControllerBase
{
    /// <summary>CSV export of invoices for accountants (Excel-friendly ; separator).</summary>
    [HttpGet("invoices.csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> Invoices(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] InvoiceStatus? status = null,
        CancellationToken ct = default)
        => (await exports.ExportInvoicesCsvAsync(from, to, status, ct)).ToFileResult(this);

    /// <summary>CSV export of VAT lines (per invoice item / rate).</summary>
    [HttpGet("vat.csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> Vat(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => (await exports.ExportVatCsvAsync(from, to, ct)).ToFileResult(this);
}
