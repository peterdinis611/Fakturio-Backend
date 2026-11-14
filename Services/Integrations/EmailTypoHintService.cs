using System.Text.RegularExpressions;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Integrations;

public sealed class EmailTypoHintService : IEmailTypoHintService
{
    private static readonly Dictionary<string, string> DomainFixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gmial.com"] = "gmail.com",
        ["gmal.com"] = "gmail.com",
        ["gmail.con"] = "gmail.com",
        ["gmail.co"] = "gmail.com",
        ["gnail.com"] = "gmail.com",
        ["gmai.com"] = "gmail.com",
        ["hotnail.com"] = "hotmail.com",
        ["hotmal.com"] = "hotmail.com",
        ["hotmail.con"] = "hotmail.com",
        ["yaho.com"] = "yahoo.com",
        ["yahooo.com"] = "yahoo.com",
        ["outlok.com"] = "outlook.com",
        ["outlook.con"] = "outlook.com",
        ["seznam.cz"] = "seznam.cz",
        ["seznan.cz"] = "seznam.cz",
        ["seznam.cy"] = "seznam.cz",
        ["azet.sk"] = "azet.sk",
        ["centrum.cz"] = "centrum.cz",
        ["centrum.cy"] = "centrum.cz",
        ["post.sk"] = "post.sk",
        ["zoznam.sk"] = "zoznam.sk",
        ["zoznam.sy"] = "zoznam.sk",
    };

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@([^@\s]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ServiceResult<EmailTypoHintResponse> Suggest(string email)
    {
        var raw = (email ?? string.Empty).Trim();
        if (raw.Length < 5)
        {
            return ServiceResult<EmailTypoHintResponse>.BadRequest(
                "Enter an e-mail address.",
                ErrorCodes.BadRequest);
        }

        var match = EmailRegex.Match(raw);
        if (!match.Success)
        {
            return ServiceResult<EmailTypoHintResponse>.Ok(new EmailTypoHintResponse(
                Email: raw,
                HasSuggestion: false,
                SuggestedEmail: null,
                Note: null,
                Source: "local"));
        }

        var local = raw[..raw.IndexOf('@', StringComparison.Ordinal)];
        var domain = match.Groups[1].Value.ToLowerInvariant();

        if (DomainFixes.TryGetValue(domain, out var fixedDomain) && fixedDomain != domain)
        {
            return ServiceResult<EmailTypoHintResponse>.Ok(new EmailTypoHintResponse(
                Email: raw,
                HasSuggestion: true,
                SuggestedEmail: $"{local}@{fixedDomain}",
                Note: $"Common typo: {domain} → {fixedDomain}",
                Source: "local"));
        }

        return ServiceResult<EmailTypoHintResponse>.Ok(new EmailTypoHintResponse(
            Email: raw,
            HasSuggestion: false,
            SuggestedEmail: null,
            Note: null,
            Source: "local"));
    }
}