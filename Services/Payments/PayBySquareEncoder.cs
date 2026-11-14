using System.Globalization;
using System.IO;
using System.Text;
using SevenZip.Compression.LZMA;

namespace Fakturio.Api.Services.Payments;

/// <summary>Slovak Pay by Square binary → Base32 payload for QR.</summary>
public static class PayBySquareEncoder
{
    private static readonly char[] Base32Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();

    public static string? TryBuildPayload(
        string? iban,
        string? beneficiaryName,
        decimal amount,
        string currency,
        string? variableSymbol = null,
        DateOnly? dueDate = null,
        string? paymentNote = null)
    {
        if (string.IsNullOrWhiteSpace(iban) || amount <= 0)
            return null;

        currency = (currency ?? "EUR").Trim().ToUpperInvariant();
        var cleanIban = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (!cleanIban.StartsWith("SK", StringComparison.Ordinal) || cleanIban.Length < 15)
            return null;

        var fields = new List<string>();
        fields.Add($"ACC:{cleanIban}");
        fields.Add($"AM:{amount.ToString("0.00", CultureInfo.InvariantCulture)}");
        fields.Add($"CC:{currency}");

        if (!string.IsNullOrWhiteSpace(beneficiaryName))
            fields.Add($"RN:{Truncate(beneficiaryName.Trim(), 35)}");

        if (!string.IsNullOrWhiteSpace(variableSymbol))
            fields.Add($"X-VS:{new string(variableSymbol.Where(char.IsDigit).ToArray())}");

        if (dueDate is { } d)
            fields.Add($"DT:{d:yyyyMMdd}");

        if (!string.IsNullOrWhiteSpace(paymentNote))
            fields.Add($"MSG:{Truncate(paymentNote.Trim(), 60)}");

        var data = string.Join('\t', fields) + '\t';
        var plain = Encoding.UTF8.GetBytes(data);
        var compressed = LzmaCompress(plain);
        if (compressed is null || compressed.Length == 0)
            return null;

        var body = new byte[2 + compressed.Length + 4];
        body[0] = 0x00;
        body[1] = 0x01;
        Buffer.BlockCopy(compressed, 0, body, 2, compressed.Length);
        var crc = Crc32(compressed);
        body[^4] = (byte)(crc & 0xFF);
        body[^3] = (byte)((crc >> 8) & 0xFF);
        body[^2] = (byte)((crc >> 16) & 0xFF);
        body[^1] = (byte)((crc >> 24) & 0xFF);

        return EncodeBase32(body);
    }

    private static byte[]? LzmaCompress(byte[] input)
    {
        try
        {
            using var inStream = new MemoryStream(input);
            using var outStream = new MemoryStream();
            var encoder = new Encoder();
            encoder.SetCoderProperties(
            [
                CoderPropID.DictionarySize, 1 << 16,
                CoderPropID.PosStateBits, 2,
                CoderPropID.LitContextBits, 3,
                CoderPropID.LitPosBits, 0,
                CoderPropID.Algorithm, 2,
                CoderPropID.NumFastBytes, 32,
                CoderPropID.MatchFinder, "BT4",
                CoderPropID.EndMarker, false
            ]);

            encoder.WriteCoderProperties(outStream);
            encoder.Code(inStream, outStream, -1, -1, null);
            var withProps = outStream.ToArray();
            // Pay by Square uses raw LZMA stream without property bytes in some implementations;
            // strip first 5 bytes (props) + 8 bytes (uncompressed size) when present.
            if (withProps.Length > 13)
                return withProps[13..];
            return withProps;
        }
        catch
        {
            return null;
        }
    }

    private static string EncodeBase32(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                var index = (buffer >> (bitsLeft - 5)) & 0x1F;
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[index]);
            }
        }

        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 0x1F;
            sb.Append(Base32Alphabet[index]);
        }

        return sb.ToString();
    }

    private static uint Crc32(byte[] data)
    {
        const uint poly = 0xEDB88320u;
        uint crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (poly & (uint)(-(int)(crc & 1)));
        }

        return ~crc;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}