using System.Globalization;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Payments;

public sealed class FioBankService(
    AppDbContext db,
    ITenantContext tenant,
    IHttpClientFactory httpClientFactory) : IFioBankService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<ServiceResult<IReadOnlyList<PaymentImportSourceRow>>> FetchTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        if (to < from)
            return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.BadRequest(
                "End date must be on or after start date.",
                ErrorCodes.InvalidDateRange);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var company = await db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        if (company is null || string.IsNullOrWhiteSpace(company.FioBankToken))
            return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.BadRequest(
                "FIO bank token is not configured. Set it in company bank settings.",
                ErrorCodes.BadRequest);

        var token = company.FioBankToken.Trim();
        var fromStr = from.ToString("yyyy-MM-dd", Inv);
        var toStr = to.ToString("yyyy-MM-dd", Inv);
        var url = $"https://fioapi.fio.cz/v1/rest/periods/{token}/{fromStr}/{toStr}/transactions.json";

        var client = httpClientFactory.CreateClient("fio");
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.Fail(
                "FIO API is unavailable.",
                StatusCodes.Status503ServiceUnavailable,
                ErrorCodes.BadRequest);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.BadRequest(
                string.IsNullOrWhiteSpace(body)
                    ? $"FIO API returned {(int)response.StatusCode}."
                    : $"FIO API error: {body.Trim()}",
                ErrorCodes.BadRequest);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var rows = ParseTransactions(doc.RootElement);
        return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.Ok(rows);
    }

    private static IReadOnlyList<PaymentImportSourceRow> ParseTransactions(JsonElement root)
    {
        if (!root.TryGetProperty("accountStatement", out var statement)
            || !statement.TryGetProperty("transactionList", out var list)
            || !list.TryGetProperty("transaction", out var transactions))
        {
            return [];
        }

        var items = new List<PaymentImportSourceRow>();
        var line = 1;

        if (transactions.ValueKind == JsonValueKind.Array)
        {
            foreach (var tx in transactions.EnumerateArray())
                items.Add(MapTransaction(tx, line++));
        }
        else if (transactions.ValueKind == JsonValueKind.Object)
        {
            items.Add(MapTransaction(transactions, line));
        }

        return items;
    }

    private static PaymentImportSourceRow MapTransaction(JsonElement tx, int lineNumber)
    {
        var vs = GetColumnValue(tx, 22);
        var amountRaw = GetColumnValue(tx, 1);
        var dateRaw = GetColumnValue(tx, 0);
        var counterparty = FirstNonEmpty(
            GetColumnValue(tx, 10),
            GetColumnValue(tx, 7));
        var note = GetColumnValue(tx, 25);

        return new PaymentImportSourceRow(
            lineNumber,
            vs,
            PaymentImportMatcher.ParseAmount(amountRaw),
            PaymentImportMatcher.ParseDate(dateRaw),
            counterparty,
            note);
    }

    private static string? GetColumnValue(JsonElement tx, int columnId)
    {
        var key = $"column{columnId}";
        if (!tx.TryGetProperty(key, out var column))
            return null;

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
