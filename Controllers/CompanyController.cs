using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/company")]
public sealed class CompanyController(ICompanyService company) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> Get(CancellationToken ct)
        => Ok(await company.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> Update(
        [FromBody] UpdateCompanySettingsRequest request,
        CancellationToken ct)
        => Ok(await company.UpdateAsync(request, ct));
}
