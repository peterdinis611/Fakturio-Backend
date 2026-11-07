using Fakturio.Api.Dtos;
using Fakturio.Api.Extensions;
using Fakturio.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fakturio.Api.Controllers;

[Route("api/fx")]
public sealed class FxController(IFxRateService fx) : ApiControllerBase
{
    /// <summary>
    /// FX rates (ČNB preferred for CZK, else Frankfurter/ECB).
    /// Optional <paramref name="date"/> = historical (yyyy-MM-dd).
    /// </summary>
    [HttpGet("rates")]
    [ProducesResponseType(typeof(FxRatesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FxRatesResponse>> Rates(
        [FromQuery] string @base = "EUR",
        [FromQuery] string? symbols = null,
        [FromQuery] DateOnly? date = null,
        CancellationToken ct = default)
    {
        var list = string.IsNullOrWhiteSpace(symbols)
            ? null
            : symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (date is { } d)
            return (await fx.GetOnDateAsync(d, @base, list, ct)).ToActionResult(this);

        return (await fx.GetLatestAsync(@base, list, ct)).ToActionResult(this);
    }
}
