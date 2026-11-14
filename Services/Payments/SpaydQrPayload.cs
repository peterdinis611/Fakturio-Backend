using System.Globalization;
using System.Text;

namespace Fakturio.Api.Services.Payments;

/// <summary>Czech short payment descriptor (SPAYD/SPD) for QR payments.</summary>
public static class SpaydQrPayload
{
    public static string? TryBuild(
        string? iban,
        string? beneficiaryName,
        decimal amount,
        string currency,
        string? variableSymbol = null,
        string? constantSymbol = null,
        string? specificSymbol = null,
        DateOnly? dueDate = null,
        string? message = null)
    {
        if (string.IsNullOrWhiteSpace(iban) || amount <= 0)
            return null;

        currency = (currency ?? "CZK").Trim().ToUpperInvariant();
        if (currency.Length != 3)
            return null;

        var cleanIban = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (!cleanIban.StartsWith("CZ", StringComparison.Ordinal) || cleanIban.Length < 15)
            return null;

        var parts = new List<string> { "SPD", "1.0", $"ACC:{cleanIban}" };

        parts.Add($"AM:{amount.ToString("0.##", CultureInfo.InvariantCulture)}");
        parts.Add($"CC:{currency}");

        if (!string.IsNullOrWhiteSpace(beneficiaryName))
            parts.Add($"RN:{Truncate(beneficiaryName.Trim(), 35)}");

        if (!string.IsNullOrWhiteSpace(variableSymbol))
            parts.Add($"X-VS:{DigitsOnly(variableSymbol)}");

        if (!string.IsNullOrWhiteSpace(constantSymbol))
            parts.Add($"X-KS:{DigitsOnly(constantSymbol)}");

        if (!string.IsNullOrWhiteSpace(specificSymbol))
            parts.Add($"X-SS:{DigitsOnly(specificSymbol)}");

        if (dueDate is { } d)
            parts.Add($"DT:{d:yyyyMMdd}");

        if (!string.IsNullOrWhiteSpace(message))
            parts.Add($"MSG:{Truncate(message.Trim(), 60)}");

        return string.Join('*', parts);
    }

    private static string DigitsOnly(string value)
        => new string(value.Where(char.IsDigit).ToArray());

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}