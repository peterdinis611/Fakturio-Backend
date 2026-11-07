using System.Net.Http.Json;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

/// <summary>Official ORSR Open Data API (sluzby.orsr.sk) — no API key.</summary>
public sealed class OrsrService(
    IHttpClientFactory httpClientFactory,
    ILogger<OrsrService> logger) : IOrsrService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ServiceResult<OrsrCompanySummaryResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<OrsrCompanySummaryResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("orsr");
            using var searchResponse = await client.GetAsync(
                $"legal-person?Filter.CorporateBodyFullNameOrRegistrationNumber={Uri.EscapeDataString(normalized)}&Take=5",
                ct);

            if (!searchResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("ORSR search returned {StatusCode}", (int)searchResponse.StatusCode);
                return ServiceResult<OrsrCompanySummaryResponse>.Fail(
                    "ORSR is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.OrsrUnavailable);
            }

            var search = await searchResponse.Content.ReadFromJsonAsync<OrsrSearchResponse>(JsonOptions, ct);
            var match = search?.Data?.FirstOrDefault(x =>
                            string.Equals(x.RegistrationNumber, normalized, StringComparison.Ordinal))
                        ?? search?.Data?.FirstOrDefault();

            if (match is null)
            {
                return ServiceResult<OrsrCompanySummaryResponse>.NotFound(
                    $"No ORSR record for IČO {normalized}.",
                    ErrorCodes.RegistryNotFound);
            }

            var file = match.FileReference;
            var oddiel = file?.Section ?? string.Empty;
            var vlozka = file?.InsertNumber ?? 0;
            var sud = file?.Court ?? string.Empty;

            var documents = new List<OrsrDocumentItem>();
            if (!string.IsNullOrWhiteSpace(oddiel) && vlozka > 0 && !string.IsNullOrWhiteSpace(sud))
            {
                try
                {
                    using var docsResponse = await client.GetAsync(
                        $"legal-person/documents?oddiel={Uri.EscapeDataString(oddiel)}&vlozka={vlozka}&sud={Uri.EscapeDataString(sud)}",
                        ct);
                    if (docsResponse.IsSuccessStatusCode)
                    {
                        var docs = await docsResponse.Content.ReadFromJsonAsync<List<OrsrDoc>>(JsonOptions, ct)
                                   ?? await TryReadWrappedDocs(docsResponse, ct);
                        foreach (var d in (docs ?? []).Take(20))
                        {
                            documents.Add(new OrsrDocumentItem(
                                Title: d.Name ?? d.Title ?? d.FormattedName,
                                SubmittedAt: d.DeliveryDate ?? d.SubmittedAt,
                                Type: d.Type?.ToString()));
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "ORSR documents fetch failed for {Ico}", normalized);
                }
            }

            return ServiceResult<OrsrCompanySummaryResponse>.Ok(new OrsrCompanySummaryResponse(
                Ico: normalized,
                Name: match.CorporateBodyFullName?.Trim() ?? normalized,
                FileReference: file?.FormattedValueSpaced,
                CourtName: null,
                Street: match.PhysicalAddressLine1,
                CityPostal: match.PhysicalAddressLine2,
                DocumentsCount: match.DocumentsCount ?? documents.Count,
                Documents: documents,
                Source: "orsr"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ORSR lookup failed for {Ico}", normalized);
            return ServiceResult<OrsrCompanySummaryResponse>.Fail(
                "ORSR is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.OrsrUnavailable);
        }
    }

    private static async Task<List<OrsrDoc>?> TryReadWrappedDocs(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var wrapped = await response.Content.ReadFromJsonAsync<OrsrDocsWrapper>(JsonOptions, ct);
            return wrapped?.Data ?? wrapped?.Items ?? wrapped?.Documents;
        }
        catch
        {
            return null;
        }
    }

    private sealed class OrsrSearchResponse
    {
        public List<OrsrHit>? Data { get; set; }
    }

    private sealed class OrsrHit
    {
        public string? CorporateBodyFullName { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? PhysicalAddressLine1 { get; set; }
        public string? PhysicalAddressLine2 { get; set; }
        public int? DocumentsCount { get; set; }
        public OrsrFileRef? FileReference { get; set; }
    }

    private sealed class OrsrFileRef
    {
        public string? Section { get; set; }
        public int InsertNumber { get; set; }
        public string? Court { get; set; }
        public string? FormattedValueSpaced { get; set; }
    }

    private sealed class OrsrDocsWrapper
    {
        public List<OrsrDoc>? Data { get; set; }
        public List<OrsrDoc>? Items { get; set; }
        public List<OrsrDoc>? Documents { get; set; }
    }

    private sealed class OrsrDoc
    {
        public string? Title { get; set; }
        public string? Name { get; set; }
        public string? FormattedName { get; set; }
        public int? Type { get; set; }
        public string? DeliveryDate { get; set; }
        public string? SubmittedAt { get; set; }
    }
}
