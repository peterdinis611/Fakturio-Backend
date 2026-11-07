using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/registry")]
public sealed class RegistryController(
    ICompanyRegistryService registry,
    IVatVerificationService vies) : ApiControllerBase
{
    /// <summary>
    /// Look up a company in the public CZ (ARES) or SK (ORSF / RPO + FS) register by IČO.
    /// SK responses include DIČ / IČ DPH when available. Country: <c>SK</c> or <c>CZ</c>.
    /// </summary>
    [HttpGet("{country}/{ico}")]
    [ProducesResponseType(typeof(RegistryCompanyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<RegistryCompanyResponse>> Lookup(
        string country,
        string ico,
        CancellationToken ct)
        => (await registry.LookupAsync(country, ico, ct)).ToActionResult(this);

    /// <summary>Fulltext company search (SK via ORSF). Numeric query = IČO lookup.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(RegistrySearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RegistrySearchResponse>> Search(
        [FromQuery] string q,
        [FromQuery] string? country = "SK",
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
        => (await registry.SearchAsync(q, country, limit, ct)).ToActionResult(this);

    /// <summary>Verify an EU VAT ID via VIES (e.g. SK2121585004).</summary>
    [HttpGet("vies")]
    [ProducesResponseType(typeof(VatVerificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VatVerificationResponse>> VerifyVat(
        [FromQuery] string vat,
        CancellationToken ct)
        => (await vies.VerifyAsync(vat, ct)).ToActionResult(this);
}
