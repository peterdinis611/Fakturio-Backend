namespace Fakturio.Api.Services.Fx;

internal static class FxHelpers
{
    public static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var trimmed = code.Trim().ToUpperInvariant();
        return trimmed.Length == 3 ? trimmed : null;
    }

    public static string[] NormalizeSymbols(IReadOnlyList<string>? symbols, string baseCode) =>
        (symbols ?? ["CZK", "USD"])
            .Select(NormalizeCode)
            .Where(c => c is not null && c != baseCode)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();
}
