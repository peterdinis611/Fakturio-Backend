using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Compliance;

public sealed class OpenSanctionsService(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenSanctionsService> logger) : ISanctionsLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<SanctionsSearchResponse>> SearchAsync(
        string query,
        CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2)
        {
            return ServiceResult<SanctionsSearchResponse>.BadRequest(
                "Query must be at least 2 characters.",
                ErrorCodes.BadRequest);
        }

        try
        {
            var client = httpClientFactory.CreateClient("opensanctions");
            using var response = await client.GetAsync(
                $"search/default?q={Uri.EscapeDataString(q)}",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenSanctions returned {StatusCode}", (int)response.StatusCode);
                return ServiceResult<SanctionsSearchResponse>.Fail(
                    "Sanctions search is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.RegistryUnavailable);
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenSanctionsPayload>(JsonOptions, ct);
            var results = payload?.Results ?? [];

            var items = results
                .Take(10)
                .Select(MapHit)
                .Where(i => i is not null)
                .Cast<SanctionsHitItem>()
                .ToList();

            return ServiceResult<SanctionsSearchResponse>.Ok(new SanctionsSearchResponse(
                Query: q,
                Count: items.Count,
                Hits: items,
                Source: "opensanctions"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenSanctions failed for {Query}", q);
            return ServiceResult<SanctionsSearchResponse>.Fail(
                "Sanctions search is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.RegistryUnavailable);
        }
    }

    private static SanctionsHitItem? MapHit(OpenSanctionsHit hit)
    {
        var name = First(hit.Caption, hit.Name, hit.Properties?.Name?.FirstOrDefault());
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var countries = hit.Properties?.Country?.Take(3).ToList() ?? [];
        var topics = hit.Properties?.Topics?.Take(5).ToList() ?? [];

        return new SanctionsHitItem(
            Id: hit.Id,
            Name: name.Trim(),
            Schema: hit.Schema,
            Countries: countries,
            Topics: topics,
            DetailUrl: string.IsNullOrWhiteSpace(hit.Id)
                ? null
                : $"https://www.opensanctions.org/entities/{Uri.EscapeDataString(hit.Id!)}/");
    }

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private sealed class OpenSanctionsPayload
    {
        public List<OpenSanctionsHit>? Results { get; set; }
    }

    private sealed class OpenSanctionsHit
    {
        public string? Id { get; set; }
        public string? Caption { get; set; }
        public string? Name { get; set; }
        public string? Schema { get; set; }
        public OpenSanctionsProperties? Properties { get; set; }
    }

    private sealed class OpenSanctionsProperties
    {
        public List<string>? Name { get; set; }
        public List<string>? Country { get; set; }
        public List<string>? Topics { get; set; }
    }
}