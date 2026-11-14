using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Integrations;

/// <summary>
/// Peppol directory soft lookup — public HTTP search when available, else empty soft-fail.
/// </summary>
public sealed class PeppolDirectoryService(
    IHttpClientFactory httpClientFactory,
    ILogger<PeppolDirectoryService> logger) : IPeppolDirectoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<PeppolLookupResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<PeppolLookupResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("peppol");
            using var response = await client.GetAsync(
                $"search/1.0/json?q={Uri.EscapeDataString(normalized)}&country=SK,CZ",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Peppol directory returned {StatusCode}", (int)response.StatusCode);
                return SoftEmpty(normalized, "peppol-soft-fail");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            var matches = ExtractMatches(json).Take(5).ToList();

            return ServiceResult<PeppolLookupResponse>.Ok(new PeppolLookupResponse(
                Ico: normalized,
                Found: matches.Count > 0,
                Participants: matches,
                Source: matches.Count > 0 ? "peppol-directory" : "peppol-empty"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Peppol lookup failed for {Ico}", normalized);
            return SoftEmpty(normalized, "peppol-soft-fail");
        }
    }

    private static ServiceResult<PeppolLookupResponse> SoftEmpty(string ico, string source)
        => ServiceResult<PeppolLookupResponse>.Ok(new PeppolLookupResponse(
            Ico: ico,
            Found: false,
            Participants: [],
            Source: source));

    private static IEnumerable<PeppolParticipantItem> ExtractMatches(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("matches", out var matches) && matches.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in matches.EnumerateArray())
            {
                var name = GetString(m, "name") ?? GetString(m, "entityName");
                var id = GetString(m, "participantID") ?? GetString(m, "id");
                if (name is null && id is null)
                    continue;
                yield return new PeppolParticipantItem(
                    ParticipantId: id,
                    Name: name,
                    Country: GetString(m, "countryCode"),
                    Scheme: GetString(m, "scheme"),
                    DocumentTypes: []);
            }
        }
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }
}