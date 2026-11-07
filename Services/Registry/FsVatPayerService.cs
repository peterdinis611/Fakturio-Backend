using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

/// <summary>
/// SK DPH status via Subjekt (FS-backed tax IDs) — no API key.
/// </summary>
public sealed class FsVatPayerService(
    IHttpClientFactory httpClientFactory,
    ILogger<FsVatPayerService> logger) : IFsVatPayerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<FsVatPayerResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<FsVatPayerResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("subjekt");
            using var response = await client.GetAsync(
                $"entity/{Uri.EscapeDataString(normalized)}?country=sk",
                ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ServiceResult<FsVatPayerResponse>.Ok(new FsVatPayerResponse(
                    IsVatPayer: false,
                    Ico: normalized,
                    Dic: null,
                    IcDph: null,
                    Name: null,
                    Source: "subjekt+fs"));
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Subjekt/FS returned {StatusCode} for {Ico}",
                    (int)response.StatusCode,
                    normalized);
                return ServiceResult<FsVatPayerResponse>.Fail(
                    "FS VAT register is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.FsUnavailable);
            }

            var tax = await response.Content.ReadFromJsonAsync<SubjektEntity>(JsonOptions, ct);
            var icDph = Normalize(tax?.IcDph)?.ToUpperInvariant();
            if (icDph is not null && !SlovakValidators.BeValidIcDph(icDph))
                icDph = null;

            var dic = Normalize(tax?.Dic);
            if (dic is not null && !SlovakValidators.BeValidDic(dic))
                dic = null;

            return ServiceResult<FsVatPayerResponse>.Ok(new FsVatPayerResponse(
                IsVatPayer: icDph is not null,
                Ico: normalized,
                Dic: dic,
                IcDph: icDph,
                Name: Normalize(tax?.Name),
                Source: "subjekt+fs"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FS VAT lookup failed for {Ico}", normalized);
            return ServiceResult<FsVatPayerResponse>.Fail(
                "FS VAT register is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.FsUnavailable);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class SubjektEntity
    {
        public string? Name { get; set; }
        public string? Dic { get; set; }
        [JsonPropertyName("ic_dph")]
        public string? IcDph { get; set; }
    }
}
