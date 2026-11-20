using System.Text.Json;

namespace Fakturio.Api.Services.Payments;

public sealed partial class FioBankService
{
    // Official FIO column IDs (https://www.fio.cz/bankovni-sluzby/api-bankovnictvi)
    // plus name fallbacks so API renames/aliases do not silently drop fields.
    private static readonly (int Id, string[] Names) ColDate = (0, ["Datum", "Date", "date"]);
    private static readonly (int Id, string[] Names) ColAmount = (1, ["Objem", "Amount", "amount", "Suma"]);
    private static readonly (int Id, string[] Names) ColCounterparty =
        (10, ["Název protiúčtu", "Nazev protiuctu", "Protiúčet", "Counterparty"]);
    private static readonly (int Id, string[] Names) ColCounterpartyAlt =
        (7, ["Uživatelská identifikace", "Uzivatelska identifikace", "User identification"]);
    private static readonly (int Id, string[] Names) ColVs =
        (22, ["VS", "Variabilní symbol", "Variabilny symbol", "Variable symbol"]);
    private static readonly (int Id, string[] Names) ColNote =
        (25, ["Komentář", "Komentar", "Comment", "Poznámka", "Note"]);

    private static PaymentImportSourceRow MapTransaction(JsonElement tx, int lineNumber)
    {
        var vs = GetColumn(tx, ColVs);
        var amountRaw = GetColumn(tx, ColAmount);
        var dateRaw = GetColumn(tx, ColDate);
        var counterparty = FirstNonEmpty(
            GetColumn(tx, ColCounterparty),
            GetColumn(tx, ColCounterpartyAlt));
        var note = GetColumn(tx, ColNote);

        return new PaymentImportSourceRow(
            lineNumber,
            vs,
            PaymentImportMatcher.ParseAmount(amountRaw),
            PaymentImportMatcher.ParseDate(dateRaw),
            counterparty,
            note);
    }

    private static string? GetColumn(JsonElement tx, (int Id, string[] Names) spec)
    {
        var byId = GetColumnValue(tx, spec.Id);
        if (!string.IsNullOrWhiteSpace(byId))
            return byId;

        foreach (var name in spec.Names)
        {
            var byName = GetColumnValueByName(tx, name);
            if (!string.IsNullOrWhiteSpace(byName))
                return byName;
        }

        return null;
    }

    private static string? GetColumnValue(JsonElement tx, int columnId)
    {
        var key = $"column{columnId}";
        if (!tx.TryGetProperty(key, out var column))
            return null;
        return ReadColumnNode(column);
    }

    private static string? GetColumnValueByName(JsonElement tx, string name)
    {
        foreach (var prop in tx.EnumerateObject())
        {
            if (!prop.Name.StartsWith("column", StringComparison.OrdinalIgnoreCase))
                continue;
            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;
            if (!prop.Value.TryGetProperty("name", out var nameNode))
                continue;
            var columnName = nameNode.GetString();
            if (string.Equals(columnName, name, StringComparison.OrdinalIgnoreCase))
                return ReadColumnNode(prop.Value);
        }

        return null;
    }

    private static string? ReadColumnNode(JsonElement column)
    {
        if (column.ValueKind == JsonValueKind.Object && column.TryGetProperty("value", out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.Null => null,
                _ => value.ToString()
            };
        }

        return column.ValueKind == JsonValueKind.String ? column.GetString() : column.ToString();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
