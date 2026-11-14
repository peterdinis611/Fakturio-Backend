using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Integrations;

public sealed class RestCountriesService(
    IHttpClientFactory httpClientFactory,
    ILogger<RestCountriesService> logger) : IRestCountriesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, (DateTimeOffset Exp, CountriesListResponse Value)> Cache =
        new(StringComparer.Ordinal);

    public async Task<ServiceResult<CountriesListResponse>> GetAllAsync(CancellationToken ct = default)
    {
        const string key = "all";
        if (Cache.TryGetValue(key, out var cached) && cached.Exp > DateTimeOffset.UtcNow)
            return ServiceResult<CountriesListResponse>.Ok(cached.Value);

        try
        {
            var client = httpClientFactory.CreateClient("restcountries");
            using var response = await client.GetAsync(
                "all?fields=name,cca2,cca3,idd,currencies,languages",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("REST Countries returned {StatusCode}", (int)response.StatusCode);
                return ServiceResult<CountriesListResponse>.Fail(
                    "Country list is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.RegistryUnavailable);
            }

            var raw = await response.Content.ReadFromJsonAsync<List<RestCountry>>(JsonOptions, ct) ?? [];
            var items = raw
                .Select(c => new CountryItem(
                    Code: c.Cca2?.ToUpperInvariant() ?? "",
                    Code3: c.Cca3?.ToUpperInvariant(),
                    Name: c.Name?.Common ?? c.Name?.Official ?? c.Cca2 ?? "",
                    CallingCode: c.Idd?.Root is not null && c.Idd.Suffixes?.Count > 0
                        ? c.Idd.Root + c.Idd.Suffixes[0]
                        : c.Idd?.Root))
                .Where(c => c.Code.Length == 2)
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new CountriesListResponse(items, "restcountries");
            Cache[key] = (DateTimeOffset.UtcNow.AddDays(7), result);
            return ServiceResult<CountriesListResponse>.Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "REST Countries failed");
            return ServiceResult<CountriesListResponse>.Fail(
                "Country list is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.RegistryUnavailable);
        }
    }

    private sealed class RestCountry
    {
        public CountryName? Name { get; set; }
        public string? Cca2 { get; set; }
        public string? Cca3 { get; set; }
        public CountryIdd? Idd { get; set; }
    }

    private sealed class CountryName
    {
        public string? Common { get; set; }
        public string? Official { get; set; }
    }

    private sealed class CountryIdd
    {
        public string? Root { get; set; }
        public List<string>? Suffixes { get; set; }
    }
}