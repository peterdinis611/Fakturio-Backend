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
/// Register účtovných závierok (RÚZ) — SK accounting unit by IČO (REST, no key).
/// </summary>
public sealed class RegisterUzService(
    IHttpClientFactory httpClientFactory,
    ILogger<RegisterUzService> logger) : IRegisterUzService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<RegisterUzUnitResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<RegisterUzUnitResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("registeruz");
            var listUrl =
                $"uctovne-jednotky?zmenene-od=2000-01-01&max-zaznamov=5&ico={Uri.EscapeDataString(normalized)}";
            using var listResponse = await client.GetAsync(listUrl, ct);

            if (!listResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "RegisterUZ list returned {StatusCode} for {Ico}",
                    (int)listResponse.StatusCode,
                    normalized);
                return Unavailable();
            }

            var list = await listResponse.Content.ReadFromJsonAsync<IdListPayload>(JsonOptions, ct);
            var id = list?.Id?.FirstOrDefault();
            if (id is null or 0)
            {
                return ServiceResult<RegisterUzUnitResponse>.NotFound(
                    "Accounting unit not found in RegisterUZ.",
                    ErrorCodes.RegistryNotFound);
            }

            using var detailResponse = await client.GetAsync($"uctovna-jednotka?id={id}", ct);
            if (!detailResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "RegisterUZ detail returned {StatusCode} for id {Id}",
                    (int)detailResponse.StatusCode,
                    id);
                return Unavailable();
            }

            var unit = await detailResponse.Content.ReadFromJsonAsync<UnitPayload>(JsonOptions, ct);
            if (unit is null || string.IsNullOrWhiteSpace(unit.NazovUJ))
            {
                return ServiceResult<RegisterUzUnitResponse>.NotFound(
                    "Accounting unit not found in RegisterUZ.",
                    ErrorCodes.RegistryNotFound);
            }

            return ServiceResult<RegisterUzUnitResponse>.Ok(new RegisterUzUnitResponse(
                Ico: unit.Ico?.Trim() ?? normalized,
                Dic: string.IsNullOrWhiteSpace(unit.Dic) ? null : unit.Dic.Trim(),
                Name: unit.NazovUJ.Trim(),
                Street: string.IsNullOrWhiteSpace(unit.Ulica) ? null : unit.Ulica.Trim(),
                City: string.IsNullOrWhiteSpace(unit.Mesto) ? null : unit.Mesto.Trim(),
                PostalCode: NormalizePostal(unit.Psc),
                FoundedAt: string.IsNullOrWhiteSpace(unit.DatumZalozenia) ? null : unit.DatumZalozenia.Trim(),
                CancelledAt: string.IsNullOrWhiteSpace(unit.DatumZrusenia) ? null : unit.DatumZrusenia.Trim(),
                StatementsCount: unit.IdUctovnychZavierok?.Count ?? 0,
                AnnualReportsCount: unit.IdVyrocnychSprav?.Count ?? 0,
                Source: "registeruz"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RegisterUZ lookup failed for {Ico}", normalized);
            return Unavailable();
        }
    }

    private static ServiceResult<RegisterUzUnitResponse> Unavailable() =>
        ServiceResult<RegisterUzUnitResponse>.Fail(
            "RegisterUZ is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.RegisterUzUnavailable);

    private static string? NormalizePostal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 5)
            return $"{digits[..3]} {digits[3..]}";
        return raw.Trim();
    }

    private sealed class IdListPayload
    {
        public List<long>? Id { get; set; }
        [JsonPropertyName("existujeDalsieId")]
        public bool ExistujeDalsieId { get; set; }
    }

    private sealed class UnitPayload
    {
        public long Id { get; set; }
        public string? Ico { get; set; }
        public string? Dic { get; set; }
        public string? NazovUJ { get; set; }
        public string? Ulica { get; set; }
        public string? Mesto { get; set; }
        public string? Psc { get; set; }
        public string? DatumZalozenia { get; set; }
        public string? DatumZrusenia { get; set; }
        public List<long>? IdUctovnychZavierok { get; set; }
        public List<long>? IdVyrocnychSprav { get; set; }
    }
}
