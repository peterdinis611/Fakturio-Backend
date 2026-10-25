using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
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

    [HttpGet("summary")]
    [ProducesResponseType(typeof(CompanySummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySummaryResponse>> Summary(CancellationToken ct)
        => Ok(await company.GetSummaryAsync(ct));

    [HttpGet("status")]
    [ProducesResponseType(typeof(CompanyCompletenessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyCompletenessResponse>> Status(CancellationToken ct)
        => Ok(await company.GetStatusAsync(ct));

    [HttpPut]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> Update(
        [FromBody] UpdateCompanySettingsRequest request,
        CancellationToken ct)
        => (await company.UpdateAsync(request, ct)).ToActionResult(this);

    [HttpPatch("defaults")]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> UpdateDefaults(
        [FromBody] UpdateCompanyDefaultsRequest request,
        CancellationToken ct)
        => (await company.UpdateDefaultsAsync(request, ct)).ToActionResult(this);
}
