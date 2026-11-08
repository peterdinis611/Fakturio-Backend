using System.Text;
using System.Xml.Linq;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Registry;

/// <summary>
/// MF ČR ADISREG — nespolehlivý plátce DPH + zveřejněné účty (SOAP, no key).
/// </summary>
public sealed class AdisregService(
    IHttpClientFactory httpClientFactory,
    ILogger<AdisregService> logger) : IAdisregService
{
    private static readonly XNamespace SoapEnv = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Adis = "http://adis.mfcr.cz/rozhraniCRPDPH/";

    public async Task<ServiceResult<AdisregStatusResponse>> LookupByDicAsync(
        string dic,
        CancellationToken ct = default)
    {
        var normalized = NormalizeCzechDic(dic);
        if (normalized is null)
        {
            return ServiceResult<AdisregStatusResponse>.BadRequest(
                "DIČ must be 8–10 digits (CZ VAT ID without country prefix).",
                ErrorCodes.InvalidVatId);
        }

        try
        {
            var client = httpClientFactory.CreateClient("adisreg");
            var envelope =
                $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
                  <soapenv:Body>
                    <StatusNespolehlivyPlatceRequest xmlns="http://adis.mfcr.cz/rozhraniCRPDPH/">
                      <dic>{normalized}</dic>
                    </StatusNespolehlivyPlatceRequest>
                  </soapenv:Body>
                </soapenv:Envelope>
                """;

            using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            using var request = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress)
            {
                Content = content
            };
            request.Headers.TryAddWithoutValidation(
                "SOAPAction",
                "http://adis.mfcr.cz/rozhraniCRPDPH/getStatusNespolehlivyPlatce");

            using var response = await client.SendAsync(request, ct);
            var xml = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "ADISREG returned {StatusCode} for {Dic}",
                    (int)response.StatusCode,
                    normalized);
                return ServiceResult<AdisregStatusResponse>.Fail(
                    "ADISREG is temporarily unavailable.",
                    StatusCodes.Status502BadGateway,
                    ErrorCodes.AdisregUnavailable);
            }

            return ParseResponse(normalized, xml);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ADISREG lookup failed for {Dic}", normalized);
            return ServiceResult<AdisregStatusResponse>.Fail(
                "ADISREG is temporarily unavailable.",
                StatusCodes.Status502BadGateway,
                ErrorCodes.AdisregUnavailable);
        }
    }

    private ServiceResult<AdisregStatusResponse> ParseResponse(string dic, string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var body = doc.Root?.Element(SoapEnv + "Body")
                       ?? doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Body");
            if (body is null)
            {
                return Unavailable();
            }

            var statusNode = body.Descendants(Adis + "status").FirstOrDefault()
                             ?? body.Descendants().FirstOrDefault(e => e.Name.LocalName == "status");
            var statusCode = statusNode?.Attribute("statusCode")?.Value;
            if (statusCode is "2" or "3")
            {
                return Unavailable();
            }

            var platce = body.Descendants(Adis + "statusPlatceDPH").FirstOrDefault()
                         ?? body.Descendants().FirstOrDefault(e => e.Name.LocalName == "statusPlatceDPH");
            if (platce is null)
            {
                return ServiceResult<AdisregStatusResponse>.Ok(new AdisregStatusResponse(
                    Dic: dic,
                    Status: "NENALEZEN",
                    IsUnreliable: false,
                    Found: false,
                    TaxOfficeCode: null,
                    UnreliablePublishedAt: null,
                    Accounts: [],
                    Source: "adisreg"));
            }

            var status = platce.Attribute("nespolehlivyPlatce")?.Value?.Trim().ToUpperInvariant()
                         ?? "NENALEZEN";
            var found = !string.Equals(status, "NENALEZEN", StringComparison.Ordinal);
            var accounts = new List<AdisregAccountItem>();

            foreach (var ucet in platce.Descendants().Where(e => e.Name.LocalName == "ucet"))
            {
                var published = ucet.Attribute("datumZverejneni")?.Value;
                var standard = ucet.Descendants().FirstOrDefault(e => e.Name.LocalName == "standardniUcet");
                var nonStandard = ucet.Descendants().FirstOrDefault(e => e.Name.LocalName == "nestandardniUcet");

                if (standard is not null)
                {
                    accounts.Add(new AdisregAccountItem(
                        Account: standard.Attribute("cislo")?.Value?.Trim() ?? "",
                        BankCode: standard.Attribute("kodBanky")?.Value?.Trim(),
                        PublishedAt: published,
                        IsStandard: true));
                }
                else if (nonStandard is not null)
                {
                    accounts.Add(new AdisregAccountItem(
                        Account: nonStandard.Attribute("cislo")?.Value?.Trim() ?? "",
                        BankCode: null,
                        PublishedAt: published,
                        IsStandard: false));
                }
            }

            return ServiceResult<AdisregStatusResponse>.Ok(new AdisregStatusResponse(
                Dic: dic,
                Status: status,
                IsUnreliable: status == "ANO",
                Found: found,
                TaxOfficeCode: platce.Attribute("cisloFu")?.Value?.Trim(),
                UnreliablePublishedAt: platce.Attribute("datumZverejneniNespolehlivosti")?.Value?.Trim(),
                Accounts: accounts.Where(a => !string.IsNullOrWhiteSpace(a.Account)).ToList(),
                Source: "adisreg"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ADISREG XML parse failed for {Dic}", dic);
            return Unavailable();
        }
    }

    private static ServiceResult<AdisregStatusResponse> Unavailable() =>
        ServiceResult<AdisregStatusResponse>.Fail(
            "ADISREG is temporarily unavailable.",
            StatusCodes.Status502BadGateway,
            ErrorCodes.AdisregUnavailable);

    /// <summary>Strips CZ prefix and non-digits; accepts 8–10 digit Czech DIČ.</summary>
    internal static string? NormalizeCzechDic(string? dic)
    {
        if (string.IsNullOrWhiteSpace(dic))
            return null;

        var raw = dic.Trim().ToUpperInvariant();
        if (raw.StartsWith("CZ", StringComparison.Ordinal))
            raw = raw[2..];

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length is >= 8 and <= 10 ? digits : null;
    }
}
