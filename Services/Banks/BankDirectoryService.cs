using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Services.Banks;

/// <summary>
/// Local NBS (SK) + ČNB (CZ) bank code directories — no remote call.
/// Data files: Data/bank-codes-sk.json, Data/bank-codes-cz.json.
/// </summary>
public sealed class BankDirectoryService : IBankDirectoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IReadOnlyDictionary<string, BankEntry> _sk;
    private readonly IReadOnlyDictionary<string, BankEntry> _cz;

    public BankDirectoryService(IHostEnvironment env, ILogger<BankDirectoryService> logger)
    {
        _sk = Load(env, "bank-codes-sk.json", logger);
        _cz = Load(env, "bank-codes-cz.json", logger);
    }

    public ServiceResult<BankCodeLookupResponse> Lookup(string code, string? country = "SK")
    {
        var cc = NormalizeCountry(country);
        var normalized = NormalizeCode(code);
        if (normalized is null)
        {
            return ServiceResult<BankCodeLookupResponse>.BadRequest(
                "Bank code must be 4 digits.",
                ErrorCodes.BadRequest);
        }

        if (!TryResolve(cc, normalized, out var name, out var bic))
        {
            return ServiceResult<BankCodeLookupResponse>.Ok(new(
                normalized, cc, null, null, false, SourceFor(cc)));
        }

        return ServiceResult<BankCodeLookupResponse>.Ok(new(
            normalized, cc, name, bic, true, SourceFor(cc)));
    }

    public ServiceResult<BankCodeListResponse> List(string? country = "SK")
    {
        var cc = NormalizeCountry(country);
        var map = MapFor(cc);
        var items = map
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new BankCodeListItem(kv.Key, kv.Value.Name, kv.Value.Bic))
            .ToList();
        return ServiceResult<BankCodeListResponse>.Ok(new(cc, items, SourceFor(cc)));
    }

    public bool TryResolve(string country, string bankCode, out string? name, out string? bic)
    {
        name = null;
        bic = null;
        var cc = NormalizeCountry(country);
        var code = NormalizeCode(bankCode);
        if (code is null) return false;
        if (!MapFor(cc).TryGetValue(code, out var entry)) return false;
        name = entry.Name;
        bic = entry.Bic;
        return true;
    }

    private IReadOnlyDictionary<string, BankEntry> MapFor(string country) =>
        country == "CZ" ? _cz : _sk;

    private static string SourceFor(string country) =>
        country == "CZ" ? "cnb-local" : "nbs-local";

    private static string NormalizeCountry(string? country)
    {
        var cc = string.IsNullOrWhiteSpace(country)
            ? "SK"
            : country.Trim().ToUpperInvariant();
        return cc is "CZ" or "SK" ? cc : "SK";
    }

    private static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length is < 3 or > 4) return null;
        return digits.PadLeft(4, '0');
    }

    private static IReadOnlyDictionary<string, BankEntry> Load(
        IHostEnvironment env,
        string fileName,
        ILogger logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", fileName);
        try
        {
            if (!File.Exists(path))
            {
                logger.LogWarning("Bank directory file missing: {Path}", path);
                return new Dictionary<string, BankEntry>(StringComparer.Ordinal);
            }

            var json = File.ReadAllText(path);
            var rows = JsonSerializer.Deserialize<List<BankRow>>(json, JsonOptions) ?? [];
            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Code) && !string.IsNullOrWhiteSpace(r.Name))
                .GroupBy(r => r.Code!.Trim().PadLeft(4, '0'), StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => new BankEntry(g.First().Name!.Trim(), string.IsNullOrWhiteSpace(g.First().Bic) ? null : g.First().Bic!.Trim().ToUpperInvariant()),
                    StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load bank directory {File}", fileName);
            return new Dictionary<string, BankEntry>(StringComparer.Ordinal);
        }
    }

    private sealed record BankEntry(string Name, string? Bic);

    private sealed class BankRow
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Bic { get; set; }
    }
}
