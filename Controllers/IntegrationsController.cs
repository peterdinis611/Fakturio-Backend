using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fakturio.Api.Controllers;

/// <summary>Key-free external lookups and local calendars.</summary>
[EnableRateLimiting(RateLimitingExtensions.IntegrationsPolicy)]
[Route("api/integrations")]
public sealed class IntegrationsController(
    IIbanLookupService ibanLookup,
    IFsVatPayerService fsVat,
    IAddressNormalizeService addresses,
    IOrsrService orsr,
    IAdisregService adisreg,
    IIsirService isir,
    IRegisterUzService registerUz,
    IPostalValidateService postal,
    IAresVrService aresVr,
    IBusinessDayService businessDays,
    IRpvsService rpvs,
    IRegistrSmluvService registrSmluv,
    ILeiLookupService lei,
    ICzAddressService czAddress,
    ISkAddressService skAddress,
    ISanctionsLookupService sanctions,
    ISkObchodnyVestnikService obchodnyVestnik,
    IRestCountriesService countries,
    IEmailTypoHintService emailHints,
    IPeppolDirectoryService peppol) : ApiControllerBase
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

    [HttpGet("cz-address")]
    [ProducesResponseType(typeof(AddressNormalizeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AddressNormalizeResponse>> LookupCzAddress(
        [FromQuery] string q,
        CancellationToken ct = default)
        => (await czAddress.LookupAsync(q, ct)).ToActionResult(this);

    [HttpGet("sk-address")]
    [ProducesResponseType(typeof(AddressNormalizeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AddressNormalizeResponse>> LookupSkAddress(
        [FromQuery] string q,
        CancellationToken ct = default)
        => (await skAddress.LookupAsync(q, ct)).ToActionResult(this);

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

    [HttpGet("register-uz/statements")]
    [ProducesResponseType(typeof(RegisterUzStatementsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RegisterUzStatementsResponse>> RegisterUzStatements(
        [FromQuery] string ico,
        [FromQuery] int take = 5,
        CancellationToken ct = default)
        => (await registerUz.LookupStatementsByIcoAsync(ico, take, ct)).ToActionResult(this);

    [HttpGet("ares-vr/{ico}")]
    [ProducesResponseType(typeof(AresVrSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AresVrSummaryResponse>> AresVr(
        string ico,
        CancellationToken ct)
        => (await aresVr.LookupByIcoAsync(ico, ct)).ToActionResult(this);

    [HttpGet("postal")]
    [ProducesResponseType(typeof(PostalValidateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PostalValidateResponse>> ValidatePostal(
        [FromQuery] string code,
        [FromQuery] string? country = "SK",
        [FromQuery] string? city = null,
        CancellationToken ct = default)
        => (await postal.ValidateAsync(code, country, city, ct)).ToActionResult(this);

    [HttpGet("business-day")]
    [ProducesResponseType(typeof(BusinessDayResponse), StatusCodes.Status200OK)]
    public ActionResult<BusinessDayResponse> BusinessDay(
        [FromQuery] DateOnly from,
        [FromQuery] int addDays = 0,
        [FromQuery] string? country = "SK")
        => businessDays.AddBusinessDays(from, addDays, country).ToActionResult(this);

    [HttpGet("rpvs")]
    [ProducesResponseType(typeof(RpvsStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RpvsStatusResponse>> Rpvs(
        [FromQuery] string ico,
        CancellationToken ct)
        => (await rpvs.LookupByIcoAsync(ico, ct)).ToActionResult(this);

    [HttpGet("registr-smluv")]
    [ProducesResponseType(typeof(RegistrSmluvStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RegistrSmluvStatusResponse>> RegistrSmluv(
        [FromQuery] string ico,
        CancellationToken ct)
        => (await registrSmluv.LookupByIcoAsync(ico, ct)).ToActionResult(this);

    [HttpGet("lei")]
    [ProducesResponseType(typeof(LeiLookupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeiLookupResponse>> LeiByIco(
        [FromQuery] string ico,
        [FromQuery] string? jurisdiction = null,
        CancellationToken ct = default)
        => (await lei.LookupByIcoAsync(ico, jurisdiction, ct)).ToActionResult(this);

    [HttpGet("lei/{leiCode}")]
    [ProducesResponseType(typeof(LeiLookupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeiLookupResponse>> LeiByCode(
        string leiCode,
        CancellationToken ct)
        => (await lei.LookupByLeiAsync(leiCode, ct)).ToActionResult(this);

    [HttpGet("sanctions")]
    [ProducesResponseType(typeof(SanctionsSearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SanctionsSearchResponse>> Sanctions(
        [FromQuery] string q,
        CancellationToken ct = default)
        => (await sanctions.SearchAsync(q, ct)).ToActionResult(this);

    [HttpGet("obchodny-vestnik")]
    [ProducesResponseType(typeof(SkObchodnyVestnikResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SkObchodnyVestnikResponse>> ObchodnyVestnik(
        [FromQuery] string ico,
        CancellationToken ct = default)
        => (await obchodnyVestnik.LookupByIcoAsync(ico, ct)).ToActionResult(this);

    [HttpGet("countries")]
    [ProducesResponseType(typeof(CountriesListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CountriesListResponse>> Countries(CancellationToken ct = default)
        => (await countries.GetAllAsync(ct)).ToActionResult(this);

    [HttpGet("email-typo")]
    [ProducesResponseType(typeof(EmailTypoHintResponse), StatusCodes.Status200OK)]
    public ActionResult<EmailTypoHintResponse> EmailTypo([FromQuery] string email)
        => emailHints.Suggest(email).ToActionResult(this);

    [HttpGet("peppol")]
    [ProducesResponseType(typeof(PeppolLookupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PeppolLookupResponse>> Peppol(
        [FromQuery] string ico,
        CancellationToken ct = default)
        => (await peppol.LookupByIcoAsync(ico, ct)).ToActionResult(this);
}
