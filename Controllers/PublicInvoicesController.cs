using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[AllowAnonymous]
[Route("api/public/invoices")]
public sealed class PublicInvoicesController(IPublicInvoiceService invoices) : ControllerBase
{
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(PublicInvoiceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicInvoiceResponse>> Get(string token, CancellationToken ct)
        => (await invoices.GetByTokenAsync(token, ct)).ToActionResult(this);

    [HttpGet("{token}/pdf")]
    [Produces("application/pdf")]
    public async Task<ActionResult> Pdf(string token, CancellationToken ct)
        => (await invoices.GetPdfByTokenAsync(token, ct)).ToFileResult(this);

    [HttpGet("{token}/isdoc")]
    [Produces("application/xml")]
    public async Task<ActionResult> Isdoc(string token, CancellationToken ct)
        => (await invoices.GetIsdocByTokenAsync(token, ct)).ToFileResult(this);
}
