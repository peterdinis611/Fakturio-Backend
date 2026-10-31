using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/gdpr")]
public sealed class GdprController(IGdprService gdpr) : ApiControllerBase
{
    /// <summary>ZIP export of account + company-scoped business data (GDPR Art. 15/20).</summary>
    [HttpGet("export.zip")]
    [Produces("application/zip")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> Export(CancellationToken ct)
        => (await gdpr.ExportMyDataAsync(ct)).ToFileResult(this);

    /// <summary>Permanently delete the current user account and all company data.</summary>
    [HttpPost("delete-account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAccount(
        [FromBody] DeleteAccountRequest request,
        CancellationToken ct)
        => (await gdpr.DeleteMyAccountAsync(request, ct)).ToActionResult(this);
}
