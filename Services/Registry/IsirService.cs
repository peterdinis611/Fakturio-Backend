using System.Text;
using System.Xml.Linq;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Validation;

namespace Fakturio.Api.Services.Registry;

/// <summary>
/// ISIR CUZK WS — brief insolvency proceedings lookup by IČO (SOAP, no key).
/// </summary>
public sealed class IsirService(
    IHttpClientFactory httpClientFactory,
    ILogger<IsirService> logger) : IIsirService
{
    public async Task<ServiceResult<IsirStatusResponse>> LookupByIcoAsync(
        string ico,
        CancellationToken ct = default)
    {
        var normalized = SlovakValidators.NormalizeIco(ico);
        if (normalized is null || !SlovakValidators.BeValidIco(normalized))
        {
            return ServiceResult<IsirStatusResponse>.BadRequest(
                "IČO must be 8 digits with a valid checksum.",
                ErrorCodes.InvalidIco);
        }

        try
        {
            var client = httpClientFactory.CreateClient("isir");
            // Children must be unqualified (elementFormDefault=unqualified).
            var envelope =
                $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
                                  xmlns:typ="http://isirws.cca.cz/types/">
                  <soapenv:Body>
                    <typ:getIsirWsCuzkDataRequest>
                      <ic>{normalized}</ic>
                      <maxPocetVysledku>20</maxPocetVysledku>
                    </typ:getIsirWsCuzkDataRequest>
                  </soapenv:Body>
                </soapenv:Envelope>
                """;

            using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            using var response = await client.PostAsync(client.BaseAddress, content, ct);
            var xml = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "ISIR returned {StatusCode} for {Ico}",
                    (int)response.StatusCode,
                    normalized);
                return ServiceResult<IsirStatusResponse>.Fail(
                    "ISIR is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.IsirUnavailable);
            }

            return ParseResponse(normalized, xml);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ISIR lookup failed for {Ico}", normalized);
            return ServiceResult<IsirStatusResponse>.Fail(
                "ISIR is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.IsirUnavailable);
        }
    }

    private ServiceResult<IsirStatusResponse> ParseResponse(string ico, string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault is not null)
            {
                var faultString = fault.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
                logger.LogWarning("ISIR SOAP fault for {Ico}: {Fault}", ico, faultString);
                return ServiceResult<IsirStatusResponse>.Fail(
                    "ISIR is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.IsirUnavailable);
            }

            var stav = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "stav");
            var kodChyby = Local(stav, "kodChyby");
            // WS2 = empty result (no matching proceedings) — treat as clean.
            if (string.Equals(kodChyby, "WS2", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<IsirStatusResponse>.Ok(new IsirStatusResponse(
                    Ico: ico,
                    HasProceedings: false,
                    Count: 0,
                    Proceedings: [],
                    Source: "isir"));
            }

            if (!string.IsNullOrWhiteSpace(kodChyby)
                && !string.Equals(kodChyby, "0", StringComparison.Ordinal)
                && !string.Equals(kodChyby, "WS0", StringComparison.OrdinalIgnoreCase))
            {
                var text = Local(stav, "textChyby") ?? Local(stav, "popisChyby") ?? kodChyby;
                logger.LogWarning("ISIR error {Code} for {Ico}: {Text}", kodChyby, ico, text);
                return ServiceResult<IsirStatusResponse>.Fail(
                    "ISIR is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.IsirUnavailable);
            }

            var items = new List<IsirProceedingItem>();
            foreach (var data in doc.Descendants().Where(e => e.Name.LocalName == "data"))
            {
                var senat = Local(data, "cisloSenatu");
                var druh = Local(data, "druhVec");
                var bc = Local(data, "bcVec");
                var rocnik = Local(data, "rocnik");
                var file = string.Join(
                    " ",
                    new[] { senat, druh, bc is null ? null : $"{bc}/{rocnik}" }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));

                var name = FirstNonEmpty(
                    Local(data, "nazevOsoby"),
                    Local(data, "nazevOrganizace"),
                    JoinName(Local(data, "jmeno"), Local(data, "nazevOsoby")));

                items.Add(new IsirProceedingItem(
                    Name: name,
                    CourtFile: string.IsNullOrWhiteSpace(file) ? null : file,
                    State: Local(data, "druhStavKonkursu"),
                    City: Local(data, "mesto"),
                    DetailUrl: Local(data, "urlDetailRizeni")));
            }

            return ServiceResult<IsirStatusResponse>.Ok(new IsirStatusResponse(
                Ico: ico,
                HasProceedings: items.Count > 0,
                Count: items.Count,
                Proceedings: items,
                Source: "isir"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ISIR XML parse failed for {Ico}", ico);
            return ServiceResult<IsirStatusResponse>.Fail(
                "ISIR is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.IsirUnavailable);
        }
    }

    private static string? Local(XElement? parent, string name) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value?.Trim()
        is { Length: > 0 } v
            ? v
            : null;

    private static string? JoinName(string? first, string? last)
    {
        var joined = string.Join(" ", new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
