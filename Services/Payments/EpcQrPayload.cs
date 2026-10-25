using System.Globalization;
using System.Text;

namespace Fakturio.Api.Services.Payments;

/// <summary>
/// Builds EPC QR (SEPA Credit Transfer) payload per EPC069-12.
/// Slovak banks scan this for IBAN + amount + VS remittance.
/// </summary>
public static class EpcQrPayload
{
    public static string? TryBuild(
        string? iban,
        string? beneficiaryName,
        decimal amount,
        string currency,
        string? bic = null,
        string? remittance = null)
    {
        if (string.IsNullOrWhiteSpace(iban) || string.IsNullOrWhiteSpace(beneficiaryName))
            return null;

        if (amount <= 0)
            return null;

        currency = (currency ?? "EUR").Trim().ToUpperInvariant();
        if (currency.Length != 3)
            return null;

        var cleanIban = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleanIban.Length < 15)
            return null;

        var name = Truncate(beneficiaryName.Trim(), 70);
        var bicClean = string.IsNullOrWhiteSpace(bic)
            ? string.Empty
            : new string(bic.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        // Amount: EUR12.34 (dot decimal, max 2 fraction digits)
        var amountPart = currency + amount.ToString("0.00", CultureInfo.InvariantCulture);

        var remittanceText = string.IsNullOrWhiteSpace(remittance)
            ? string.Empty
            : Truncate(remittance.Trim(), 140);

        // BCD / 002 / 1 / SCT / BIC / Name / IBAN / Amount / Purpose / Remittance / Info
        var sb = new StringBuilder();
        sb.Append("BCD\n");
        sb.Append("002\n");
        sb.Append("1\n");
        sb.Append("SCT\n");
        sb.Append(bicClean).Append('\n');
        sb.Append(name).Append('\n');
        sb.Append(cleanIban).Append('\n');
        sb.Append(amountPart).Append('\n');
        sb.Append('\n'); // Purpose (empty)
        sb.Append(remittanceText).Append('\n');
        // Beneficiary to originator info — omit trailing empty line per common bank practice
        return sb.ToString().TrimEnd('\n') + "\n";
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
