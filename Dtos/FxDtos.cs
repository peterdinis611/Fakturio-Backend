namespace Fakturio.Api.Dtos;

public record FxRatesResponse(
    string Base,
    DateOnly Date,
    IReadOnlyDictionary<string, decimal> Rates,
    string Source
);
