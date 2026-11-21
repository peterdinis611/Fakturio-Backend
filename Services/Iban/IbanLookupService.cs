using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Iban;

public sealed class IbanLookupService(
    IHttpClientFactory httpClientFactory,
    IBankDirectoryService banks,
    IVatComplyService vatComply,
    ILogger<IbanLookupService> logger) : IIbanLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, IbanLookupResponse> Cache = new(StringComparer.Ordinal);

    public async Task<ServiceResult<IbanLookupResponse>> LookupAsync(
        string iban,
        CancellationToken ct = default)
    {
        var normalized = (iban ?? string.Empty)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToUpperInvariant();

        if (normalized.Length < 15 || !SlovakValidators.BeValidIban(normalized))
        {
            return ServiceResult<IbanLookupResponse>.BadRequest(
                "IBAN is invalid.",
                ErrorCodes.InvalidIban);
        }

        if (Cache.TryGetValue(normalized, out var cached))
            return ServiceResult<IbanLookupResponse>.Ok(cached);

        var country = normalized[..2];
        string? bankCode = null;
        string? bankName = null;
        string? bic = null;
        string? city = null;
        var source = "local";

        if (normalized.Length >= 8)
        {
            bankCode = normalized.Substring(4, 4);
            if (banks.TryResolve(country, bankCode, out var name, out var localBic))
            {
                bankName = name;
                bic = localBic;
                source = country is "CZ" ? "cnb-local" : "nbs-local";
            }
        }

        try
        {
            var client = httpClientFactory.CreateClient("openiban");
            var url =
                $"validate/{Uri.EscapeDataString(normalized)}?getBIC=true&validateBankCode=true";
            using var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<OpenIbanPayload>(JsonOptions, ct);
                if (payload is not null)
                {
                    if (!string.IsNullOrWhiteSpace(payload.BankData?.Name))
                    {
                        bankName = payload.BankData.Name.Trim();
                        source = source.StartsWith("nbs", StringComparison.Ordinal) || source.StartsWith("cnb", StringComparison.Ordinal)
                            ? $"{source}+openiban"
                            : "openiban";
                    }

                    if (!string.IsNullOrWhiteSpace(payload.BankData?.Bic))
                        bic = payload.BankData.Bic.Trim().ToUpperInvariant();

                    if (!string.IsNullOrWhiteSpace(payload.BankData?.BankCode))
                        bankCode = payload.BankData.BankCode.Trim();

                    if (!string.IsNullOrWhiteSpace(payload.BankData?.City))
                        city = payload.BankData.City.Trim();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "openiban enrichment failed for {Iban}", normalized);
        }

        if (string.IsNullOrWhiteSpace(bankName) || string.IsNullOrWhiteSpace(bic))
        {
            var (ok, vcName, vcBic, _) = await vatComply.TryLookupIbanAsync(normalized, ct);
            if (ok)
            {
                bankName ??= vcName;
                bic ??= vcBic;
                source = string.IsNullOrWhiteSpace(bankName) ? source : $"{source}+vatcomply".Trim('+');
            }
        }

        var result = new IbanLookupResponse(
            IsValid: true,
            Iban: normalized,
            CountryCode: country,
            BankCode: bankCode,
            BankName: bankName,
            Bic: bic,
            City: city,
            Source: source);

        Cache[normalized] = result;
        return ServiceResult<IbanLookupResponse>.Ok(result);
    }

    private sealed class OpenIbanPayload
    {
        public bool Valid { get; set; }
        public OpenIbanBankData? BankData { get; set; }
    }

    private sealed class OpenIbanBankData
    {
        public string? BankCode { get; set; }
        public string? Name { get; set; }
        public string? Bic { get; set; }
        public string? City { get; set; }
        public string? Zip { get; set; }
    }
}
