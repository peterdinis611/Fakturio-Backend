using System.Text.RegularExpressions;

namespace Fakturio.Api.Validation;

public static partial class SlovakValidators
{
    private static readonly HashSet<decimal> AllowedVatRates = [0m, 5m, 19m, 23m];

    /// <summary>Removes spaces / whitespace so "57 267 073" is treated as IČO.</summary>
    public static string? NormalizeIco(string? ico)
    {
        if (string.IsNullOrWhiteSpace(ico))
            return null;

        var digits = new string(ico.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    public static bool BeValidIco(string? ico)
    {
        var normalized = NormalizeIco(ico);
        if (normalized is null)
            return true;

        if (!EightDigitsRegex().IsMatch(normalized))
            return false;

        // Official SK/CZ IČO checksum (mod 11)
        var sum = 0;
        for (var i = 0; i < 7; i++)
            sum += (normalized[i] - '0') * (8 - i);

        var remainder = sum % 11;
        var check = remainder == 0 ? 1 : remainder == 1 ? 0 : 11 - remainder;
        return check == normalized[7] - '0';
    }

    public static bool BeValidDic(string? dic)
    {
        if (string.IsNullOrWhiteSpace(dic))
            return true;

        return TenDigitsRegex().IsMatch(dic);
    }

    public static bool BeValidIcDph(string? icDph)
    {
        if (string.IsNullOrWhiteSpace(icDph))
            return true;

        return IcDphRegex().IsMatch(icDph.Trim().ToUpperInvariant());
    }

    public static bool BeValidIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return true;

        var normalized = iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (!IbanRegex().IsMatch(normalized))
            return false;

        // ISO 13616 mod-97 check
        var rearranged = normalized[4..] + normalized[..4];
        var numeric = string.Concat(rearranged.Select(c =>
            char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString()));

        var remainder = 0;
        foreach (var ch in numeric)
            remainder = (remainder * 10 + (ch - '0')) % 97;

        return remainder == 1;
    }

    public static bool BeValidBic(string? bic)
    {
        if (string.IsNullOrWhiteSpace(bic))
            return true;

        return BicRegex().IsMatch(bic.Trim().ToUpperInvariant());
    }

    public static bool BeValidCountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return true;

        return CountryCodeRegex().IsMatch(country.Trim());
    }

    public static bool BeValidCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return true;

        return CurrencyRegex().IsMatch(currency.Trim());
    }

    public static bool BeAllowedVatRate(decimal rate) => AllowedVatRates.Contains(rate);

    public static bool BeValidVariableSymbol(string? vs)
    {
        if (string.IsNullOrWhiteSpace(vs))
            return true;

        return VariableSymbolRegex().IsMatch(vs);
    }

    public static bool BeValidPostalCode(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
            return true;

        return PostalCodeRegex().IsMatch(postalCode.Trim());
    }

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex EightDigitsRegex();

    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex TenDigitsRegex();

    [GeneratedRegex(@"^SK\d{10}$")]
    private static partial Regex IcDphRegex();

    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex BicRegex();

    [GeneratedRegex(@"^[A-Za-z]{2}$")]
    private static partial Regex CountryCodeRegex();

    [GeneratedRegex(@"^[A-Za-z]{3}$")]
    private static partial Regex CurrencyRegex();

    [GeneratedRegex(@"^\d{1,10}$")]
    private static partial Regex VariableSymbolRegex();

    [GeneratedRegex(@"^\d{3}\s?\d{2}$")]
    private static partial Regex PostalCodeRegex();
}
