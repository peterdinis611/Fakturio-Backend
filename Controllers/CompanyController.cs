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

    [HttpGet("bank")]
    [ProducesResponseType(typeof(CompanyBankResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyBankResponse>> Bank(CancellationToken ct)
        => Ok(await company.GetBankAsync(ct));

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

    [HttpPatch("bank")]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> UpdateBank(
        [FromBody] UpdateCompanyBankRequest request,
        CancellationToken ct)
        => (await company.UpdateBankAsync(request, ct)).ToActionResult(this);

    [HttpPatch("branding")]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> UpdateBranding(
        [FromBody] UpdateCompanyBrandingRequest request,
        CancellationToken ct)
        => (await company.UpdateBrandingAsync(request, ct)).ToActionResult(this);

    [HttpPatch("contact")]
    [ProducesResponseType(typeof(CompanySettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanySettingsResponse>> UpdateContact(
        [FromBody] UpdateCompanyContactRequest request,
        CancellationToken ct)
        => (await company.UpdateContactAsync(request, ct)).ToActionResult(this);
}
