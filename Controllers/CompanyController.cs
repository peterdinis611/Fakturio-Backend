using Fakturio.Api.Dtos;
using Fakturio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[ApiController]
[Route("api/company")]
[Produces("application/json")]
public class CompanyController(CompanyService company) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> Get(CancellationToken ct)
    {
        return Ok(await company.GetAsync(ct));
    }

    [HttpPut]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanySettingsResponse>> Update(
        [FromBody] UpdateCompanySettingsRequest request,
        CancellationToken ct)
    {
        return Ok(await company.UpdateAsync(request, ct));
    }
}
