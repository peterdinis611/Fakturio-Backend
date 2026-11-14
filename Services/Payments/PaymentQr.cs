namespace Fakturio.Api.Services.Payments;

public enum PaymentQrKind
{
    None,
    Epc,
    Spayd,
    PayBySquare,
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
        if (amount <= 0 || string.IsNullOrWhiteSpace(iban))
            return null;

        var cleanIban = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var isSk = cleanIban.StartsWith("SK", StringComparison.Ordinal);
        var isCz = cleanIban.StartsWith("CZ", StringComparison.Ordinal);

        if (isSk)
        {
            var pbs = PayBySquareEncoder.TryBuildPayload(
                cleanIban,
                beneficiaryName,
                amount,
                currency,
                variableSymbol,
                dueDate,
                paymentNote);
            var pbsPng = EpcQrImage.TryCreatePng(pbs, pixelsPerModule: 5);
            if (pbsPng is { Length: > 0 })
                return new PaymentQrResult(PaymentQrKind.PayBySquare, pbsPng, "QR Platba");
        }

        if (isCz)
        {
            var spayd = SpaydQrPayload.TryBuild(
                cleanIban,
                beneficiaryName,
                amount,
                currency,
                variableSymbol,
                constantSymbol,
                specificSymbol,
                dueDate,
                paymentNote);
            var spaydPng = EpcQrImage.TryCreatePng(spayd, pixelsPerModule: 5);
            if (spaydPng is { Length: > 0 })
                return new PaymentQrResult(PaymentQrKind.Spayd, spaydPng, "QR Platba (SPD)");
        }

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
            var label = isSk ? "QR Platba (EPC)" : "Platba QR (EPC)";
            return new PaymentQrResult(PaymentQrKind.Epc, epcPng, label);
        }

        return null;
    }
}
