using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Integrations;

/// <summary>
/// Thin CKAN-style search against data.gov.sk / data.gov.cz.
/// </summary>
public sealed class OpenDataSearchService(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenDataSearchService> logger) : IOpenDataSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ServiceResult<OpenDataSearchResponse>> SearchAsync(
        string q,
        string? country = "SK",
        CancellationToken ct = default)
    {
        var query = (q ?? "").Trim();
        if (query.Length < 2)
        {
            return ServiceResult<OpenDataSearchResponse>.BadRequest(
                "Query must be at least 2 characters.",
                ErrorCodes.BadRequest);
        }

        var cc = string.IsNullOrWhiteSpace(country)
            ? "SK"
            : country.Trim().ToUpperInvariant();
        var clientName = cc == "CZ" ? "datagovcz" : "datagovsk";

        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            var path = $"api/3/action/package_search?q={Uri.EscapeDataString(query)}&rows=8";
            using var response = await client.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Open data {Client} HTTP {Status}", clientName, (int)response.StatusCode);
                return Soft(query, cc);
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            var datasets = Extract(json, cc).Take(8).ToList();
            return ServiceResult<OpenDataSearchResponse>.Ok(new(
                query,
                cc,
                datasets.Count > 0,
                datasets,
                cc == "CZ" ? "data-gov-cz" : "data-gov-sk"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Open data search failed for {Query}", query);
            return Soft(query, cc);
        }
    }

    private static ServiceResult<OpenDataSearchResponse> Soft(string q, string cc) =>
        ServiceResult<OpenDataSearchResponse>.Ok(new(
            q, cc, false, [], "open-data-soft-fail"));

    private static IEnumerable<OpenDataDatasetItem> Extract(JsonElement root, string country)
    {
        if (!root.TryGetProperty("result", out var result)) yield break;
        if (!result.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            yield break;

        var portal = country == "CZ"
            ? "https://data.gov.cz/datová-sada?iri="
            : "https://data.gov.sk/dataset/";

        foreach (var el in results.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var title = Str(el, "title") ?? Str(el, "name");
            var name = Str(el, "name");
            var notes = Str(el, "notes");
            string? org = null;
            if (el.TryGetProperty("organization", out var o) && o.ValueKind == JsonValueKind.Object)
                org = Str(o, "title") ?? Str(o, "name");

            var url = Str(el, "url");
            if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(name))
                url = country == "CZ"
                    ? $"https://data.gov.cz/datová-sada/{name}"
                    : $"https://data.gov.sk/dataset/{name}";

            yield return new OpenDataDatasetItem(title, url, org, notes);
        }
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}
