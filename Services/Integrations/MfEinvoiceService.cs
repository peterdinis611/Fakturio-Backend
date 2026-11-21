using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Integrations;

/// <summary>
/// MF SR eInvoice public metadata (UBL/D16B) — soft-fail when API unavailable.
/// </summary>
public sealed class MfEinvoiceService(
    IHttpClientFactory httpClientFactory,
    ILogger<MfEinvoiceService> logger) : IMfEinvoiceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ServiceResult<MfEinvoiceLookupResponse>> SearchByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<MfEinvoiceLookupResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        foreach (var clientName in new[] { "mfeinvoice", "mfeinvoice-dev" })
        {
            try
            {
                var client = httpClientFactory.CreateClient(clientName);
                var path =
                    $"invoices?limit=10&customerIco={Uri.EscapeDataString(normalized)}"
                    + $"&supplierIco={Uri.EscapeDataString(normalized)}";
                using var response = await client.GetAsync(
                    $"invoices?limit=10&CustomerIco={Uri.EscapeDataString(normalized)}",
                    ct);

                // Try supplier filter if customer empty
                if (!response.IsSuccessStatusCode)
                {
                    using var response2 = await client.GetAsync(
                        $"invoices?limit=10&SupplierIco={Uri.EscapeDataString(normalized)}",
                        ct);
                    if (!response2.IsSuccessStatusCode)
                    {
                        logger.LogDebug(
                            "MF eInvoice {Client} HTTP {Status}",
                            clientName,
                            (int)response2.StatusCode);
                        continue;
                    }

                    return await ParseAsync(normalized, response2, clientName, ct);
                }

                return await ParseAsync(normalized, response, clientName, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "MF eInvoice {Client} failed for {Ico}", clientName, normalized);
            }
        }

        return Soft(normalized);
    }

    private static async Task<ServiceResult<MfEinvoiceLookupResponse>> ParseAsync(
        string ico,
        HttpResponseMessage response,
        string clientName,
        CancellationToken ct)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        var items = Extract(json).Take(10).ToList();
        var source = clientName.Contains("dev", StringComparison.Ordinal)
            ? "mfsr-einvoice-dev"
            : "mfsr-einvoice";
        return ServiceResult<MfEinvoiceLookupResponse>.Ok(new(
            ico, items.Count > 0, items, items.Count > 0 ? source : $"{source}-empty"));
    }

    private static ServiceResult<MfEinvoiceLookupResponse> Soft(string ico) =>
        ServiceResult<MfEinvoiceLookupResponse>.Ok(new(
            ico, false, [], "mfsr-einvoice-soft-fail"));

    private static IEnumerable<MfEinvoiceItem> Extract(JsonElement root)
    {
        JsonElement array = default;
        if (root.ValueKind == JsonValueKind.Array) array = root;
        else if (root.TryGetProperty("invoices", out var inv)) array = inv;
        else if (root.TryGetProperty("data", out var data)) array = data;
        else yield break;

        if (array.ValueKind != JsonValueKind.Array) yield break;

        foreach (var el in array.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            yield return new MfEinvoiceItem(
                Str(el, "id"),
                Str(el, "format"),
                Str(el, "customerName") ?? Str(el, "customer_name"),
                Str(el, "supplierName") ?? Str(el, "supplier_name"),
                Str(el, "customerIco") ?? Str(el, "customer_ico"),
                Str(el, "supplierIco") ?? Str(el, "supplier_ico"),
                Str(el, "issueDate") ?? Str(el, "issuedAt") ?? Str(el, "uploadTime"));
        }
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : p.ValueKind == JsonValueKind.Number
                ? p.ToString()
                : null;
}
