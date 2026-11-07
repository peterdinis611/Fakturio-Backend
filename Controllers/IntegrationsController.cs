using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

/// <summary>Key-free external lookups: IBAN, FS DPH, Nominatim, ORSR.</summary>
[Route("api/integrations")]
public sealed class IntegrationsController(
    IIbanLookupService ibanLookup,
    IFsVatPayerService fsVat,
    IAddressNormalizeService addresses,
    IOrsrService orsr) : ApiControllerBase
{
    [HttpGet("iban")]
    [ProducesResponseType(typeof(IbanLookupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IbanLookupResponse>> LookupIban(
        [FromQuery] string iban,
        CancellationToken ct)
        => (await ibanLookup.LookupAsync(iban, ct)).ToActionResult(this);

    [HttpGet("fs-vat/{ico}")]
    [ProducesResponseType(typeof(FsVatPayerResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FsVatPayerResponse>> FsVat(
        string ico,
        CancellationToken ct)
        => (await fsVat.LookupByIcoAsync(ico, ct)).ToActionResult(this);

    [HttpGet("address")]
    [ProducesResponseType(typeof(AddressNormalizeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AddressNormalizeResponse>> NormalizeAddress(
        [FromQuery] string q,
        [FromQuery] string? country = "SK",
        CancellationToken ct = default)
        => (await addresses.NormalizeAsync(q, country, ct)).ToActionResult(this);

    [HttpGet("orsr/{ico}")]
    [ProducesResponseType(typeof(OrsrCompanySummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrsrCompanySummaryResponse>> Orsr(
        string ico,
        CancellationToken ct)
        => (await orsr.LookupByIcoAsync(ico, ct)).ToActionResult(this);
}
