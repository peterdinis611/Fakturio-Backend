using System.Text.Json;
using System.Text.RegularExpressions;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

/// <summary>
/// GLEIF LEI API — lookup legal entity identifiers by IČO or LEI (no key).
/// </summary>
public sealed partial class LeiLookupService(
    IHttpClientFactory httpClientFactory,
    ILogger<LeiLookupService> logger) : ILeiLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<LeiLookupResponse>> LookupByIcoAsync(
        string ico,
        string? jurisdiction = null,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<LeiLookupResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        var query =
            $"lei-records?filter[entity.registeredAs]={Uri.EscapeDataString(normalized)}&page[size]=10";
        var country = NormalizeJurisdiction(jurisdiction);
        if (country is not null)
            query += $"&filter[entity.jurisdiction]={Uri.EscapeDataString(country)}";

        return await FetchAsync(normalized, query, ct);
    }

    public async Task<ServiceResult<LeiLookupResponse>> LookupByLeiAsync(
        string lei,
        CancellationToken ct = default)
    {
        var normalized = NormalizeLei(lei);
        if (normalized is null)
        {
            return ServiceResult<LeiLookupResponse>.BadRequest(
                "LEI must be 20 alphanumeric characters.",
                ErrorCodes.BadRequest);
        }

        return await FetchAsync(normalized, $"lei-records/{Uri.EscapeDataString(normalized)}", ct);
    }

    private async Task<ServiceResult<LeiLookupResponse>> FetchAsync(
        string queryLabel,
        string path,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("gleif");
            using var response = await client.GetAsync(path, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ServiceResult<LeiLookupResponse>.Ok(new LeiLookupResponse(
                    Query: queryLabel,
                    Found: false,
                    Count: 0,
                    Records: [],
                    Source: "gleif"));
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "GLEIF returned {StatusCode} for {Query}",
                    (int)response.StatusCode,
                    queryLabel);
                return Unavailable();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var records = ParseRecords(doc.RootElement).ToList();

            return ServiceResult<LeiLookupResponse>.Ok(new LeiLookupResponse(
                Query: queryLabel,
                Found: records.Count > 0,
                Count: records.Count,
                Records: records,
                Source: "gleif"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GLEIF lookup failed for {Query}", queryLabel);
            return Unavailable();
        }
    }

    private static IEnumerable<LeiRecordItem> ParseRecords(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var mapped = MapRecord(item);
                    if (mapped is not null)
                        yield return mapped;
                }
                yield break;
            }

            if (data.ValueKind == JsonValueKind.Object)
            {
                var mapped = MapRecord(data);
                if (mapped is not null)
                    yield return mapped;
            }
        }
    }

    private static LeiRecordItem? MapRecord(JsonElement item)
    {
        if (!item.TryGetProperty("attributes", out var attrs))
            return null;

        var lei = attrs.TryGetProperty("lei", out var leiEl)
            ? leiEl.GetString()
            : item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(lei))
            return null;

        string? legalName = null;
        string? jurisdiction = null;
        string? registeredAs = null;
        string? entityStatus = null;
        string? city = null;
        string? country = null;

        if (attrs.TryGetProperty("entity", out var entity))
        {
            if (entity.TryGetProperty("legalName", out var ln)
                && ln.TryGetProperty("name", out var nameEl))
                legalName = nameEl.GetString();

            jurisdiction = GetString(entity, "jurisdiction");
            registeredAs = GetString(entity, "registeredAs");
            entityStatus = GetString(entity, "status");

            if (entity.TryGetProperty("legalAddress", out var addr))
            {
                city = GetString(addr, "city");
                country = GetString(addr, "country");
            }
        }

        string? registrationStatus = null;
        string? nextRenewal = null;
        if (attrs.TryGetProperty("registration", out var reg))
        {
            registrationStatus = GetString(reg, "status");
            nextRenewal = GetString(reg, "nextRenewalDate");
            if (nextRenewal is { Length: >= 10 })
                nextRenewal = nextRenewal[..10];
        }

        return new LeiRecordItem(
            Lei: lei,
            LegalName: legalName,
            Jurisdiction: jurisdiction,
            RegisteredAs: registeredAs,
            EntityStatus: entityStatus,
            RegistrationStatus: registrationStatus,
            NextRenewalDate: nextRenewal,
            City: city,
            Country: country,
            DetailUrl: $"https://search.gleif.org/#/record/{lei}");
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? NormalizeJurisdiction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var code = value.Trim().ToUpperInvariant();
        return code is "SK" or "CZ" or "AT" or "DE" or "PL" or "HU" ? code : null;
    }

    private static string? NormalizeLei(string? lei)
    {
        if (string.IsNullOrWhiteSpace(lei))
            return null;

        var cleaned = lei.Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);
        return LeiRegex().IsMatch(cleaned) ? cleaned : null;
    }

    private static ServiceResult<LeiLookupResponse> Unavailable() =>
        ServiceResult<LeiLookupResponse>.Fail(
            "GLEIF is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.LeiUnavailable);

    [GeneratedRegex(@"^[A-Z0-9]{20}$")]
    private static partial Regex LeiRegex();
}
