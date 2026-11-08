using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

/// <summary>Key-free external lookups: IBAN, FS DPH, Nominatim, ORSR, ADISREG, ISIR, RegisterUZ, PSČ.</summary>
[Route("api/integrations")]
public sealed class IntegrationsController(
    IIbanLookupService ibanLookup,
    IFsVatPayerService fsVat,
    IAddressNormalizeService addresses,
    IOrsrService orsr,
    IAdisregService adisreg,
    IIsirService isir,
    IRegisterUzService registerUz,
    IPostalValidateService postal) : ApiControllerBase
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

    [HttpGet("adisreg/{dic}")]
    [ProducesResponseType(typeof(AdisregStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdisregStatusResponse>> Adisreg(
        string dic,
        CancellationToken ct)
        => (await adisreg.LookupByDicAsync(dic, ct)).ToActionResult(this);

    [HttpGet("isir")]
    [ProducesResponseType(typeof(IsirStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IsirStatusResponse>> Isir(
        [FromQuery] string ico,
        CancellationToken ct)
        => (await isir.LookupByIcoAsync(ico, ct)).ToActionResult(this);

    [HttpGet("register-uz")]
    [ProducesResponseType(typeof(RegisterUzUnitResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisterUzUnitResponse>> RegisterUz(
        [FromQuery] string ico,
        CancellationToken ct)
        => (await registerUz.LookupByIcoAsync(ico, ct)).ToActionResult(this);

    [HttpGet("postal")]
    [ProducesResponseType(typeof(PostalValidateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PostalValidateResponse>> ValidatePostal(
        [FromQuery] string code,
        [FromQuery] string? country = "SK",
        [FromQuery] string? city = null,
        CancellationToken ct = default)
        => (await postal.ValidateAsync(code, country, city, ct)).ToActionResult(this);
}
