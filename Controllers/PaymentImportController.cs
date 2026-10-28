using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/payments/import")]
public sealed class PaymentImportController(IPaymentImportService import) : ApiControllerBase
{
    [HttpPost("preview")]
    [ProducesResponseType(typeof(PaymentImportPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentImportPreviewResponse>> Preview(
        [FromBody] PaymentImportPreviewRequest request,
        CancellationToken ct)
        => (await import.PreviewAsync(request, ct)).ToActionResult(this);

    [HttpPost("confirm")]
    [ProducesResponseType(typeof(PaymentImportConfirmResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentImportConfirmResponse>> Confirm(
        [FromBody] PaymentImportConfirmRequest request,
        CancellationToken ct)
        => (await import.ConfirmAsync(request, ct)).ToActionResult(this);
}
