using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/emails")]
public sealed class EmailsController(IInvoiceEmailService emails) : ApiControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(EmailStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<EmailStatusResponse> Status() => Ok(emails.GetStatus());

    [HttpPost("test")]
    [ProducesResponseType(typeof(SendTestEmailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SendTestEmailResponse>> SendTest(
        [FromBody] SendTestEmailRequest? request,
        CancellationToken ct)
        => (await emails.SendTestAsync(request ?? new SendTestEmailRequest(null), ct))
            .ToActionResult(this);
}
