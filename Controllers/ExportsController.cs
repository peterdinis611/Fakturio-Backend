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

    /// <summary>Money S3–oriented XML of posted invoices.</summary>
    [HttpGet("money-s3.xml")]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> MoneyS3(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => (await exports.ExportMoneyS3XmlAsync(from, to, ct)).ToFileResult(this);

    /// <summary>ZIP pack: CSV invoices + VAT + Money S3 XML + README.</summary>
    [HttpGet("accountant-pack.zip")]
    [Produces("application/zip")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> AccountantPack(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
        => (await exports.ExportAccountantPackAsync(from, to, ct)).ToFileResult(this);
}
