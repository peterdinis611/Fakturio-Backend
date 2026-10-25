using QRCoder;

namespace Fakturio.Api.Services.Payments;

public static class EpcQrImage
{
    /// <summary>Renders EPC payload as PNG bytes for QuestPDF.</summary>
    public static byte[]? TryCreatePng(string? payload, int pixelsPerModule = 4)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
