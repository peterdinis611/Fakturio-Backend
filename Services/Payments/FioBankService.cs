using System.Globalization;
using System.Text.Json;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Payments;

public sealed partial class FioBankService(
    AppDbContext db,
    ITenantContext tenant,
    IHttpClientFactory httpClientFactory,
    IFioTokenProtector fioTokens,
    ILogger<FioBankService> logger) : IFioBankService, IBankPaymentImport
{
    public string ProviderKey => "fio";

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

        var token = fioTokens.Unprotect(company.FioBankToken);
        if (string.IsNullOrWhiteSpace(token))
            return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.BadRequest(
                "FIO bank token could not be decrypted. Re-save it in company bank settings.",
                ErrorCodes.BadRequest);

        // Lazy-migrate plaintext tokens written before DataProtection.
        if (!fioTokens.LooksProtected(company.FioBankToken))
        {
            var tracked = await db.CompanySettings.FirstOrDefaultAsync(c => c.Id == companyId, ct);
            if (tracked is not null)
            {
                tracked.FioBankToken = fioTokens.Protect(token);
                tracked.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
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
            logger.LogWarning(ex, "FIO API request failed for period {From}–{To}", fromStr, toStr);
            return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.Fail(
                "FIO API is unavailable.",
                StatusCodes.Status503ServiceUnavailable,
                ErrorCodes.BadRequest);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "FIO API returned {StatusCode}: {Body}",
                (int)response.StatusCode,
                body.Length > 500 ? body[..500] : body);
            return ServiceResult<IReadOnlyList<PaymentImportSourceRow>>.BadRequest(
                "FIO API request failed. Check the bank token and try again.",
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

}
