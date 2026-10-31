using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/registry")]
public sealed class RegistryController(ICompanyRegistryService registry) : ApiControllerBase
{
    /// <summary>
    /// Look up a company in the public CZ (ARES) or SK (RPO + FS tax IDs) register by IČO.
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
}
