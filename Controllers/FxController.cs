using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/fx")]
public sealed class FxController(IFxRateService fx) : ApiControllerBase
{
    /// <summary>Latest FX rates from Frankfurter (ECB). Default base EUR → CZK,USD.</summary>
    [HttpGet("rates")]
    [ProducesResponseType(typeof(FxRatesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FxRatesResponse>> Latest(
        [FromQuery] string @base = "EUR",
        [FromQuery] string? symbols = null,
        CancellationToken ct = default)
    {
        var list = string.IsNullOrWhiteSpace(symbols)
            ? null
            : symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (await fx.GetLatestAsync(@base, list, ct)).ToActionResult(this);
    }
}
