namespace Fakturio.Api.Services.Payments;

public enum PaymentQrKind
{
    None,
    Epc,
}

public sealed record PaymentQrResult(PaymentQrKind Kind, byte[] Png, string Label);

/// <summary>
/// Payment QR for invoices via EPC SEPA (QRCoder). Slovak banks accept EPC for EUR.
/// </summary>
public static class PaymentQr
{
    public static PaymentQrResult? TryCreate(
        string? iban,
        string? bic,
        string? beneficiaryName,
        decimal amount,
        string currency,
        DateOnly? dueDate,
        string? variableSymbol,
        string? constantSymbol = null,
        string? specificSymbol = null,
        string? paymentNote = null)
    {
        _ = dueDate;
        _ = constantSymbol;
        _ = specificSymbol;
        _ = paymentNote;

        if (amount <= 0 || string.IsNullOrWhiteSpace(iban))
            return null;

        var cleanIban = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var isSk = cleanIban.StartsWith("SK", StringComparison.Ordinal);

        var epc = EpcQrPayload.TryBuild(
            cleanIban,
            beneficiaryName,
            amount,
            currency,
            bic,
            remittance: variableSymbol);
        var epcPng = EpcQrImage.TryCreatePng(epc, pixelsPerModule: 5);
        if (epcPng is { Length: > 0 })
        {
            var label = isSk ? "QR Platba" : "Platba QR (EPC)";
            return new PaymentQrResult(PaymentQrKind.Epc, epcPng, label);
        }

        return null;
    }
}
