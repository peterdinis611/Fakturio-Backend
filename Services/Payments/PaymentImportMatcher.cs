using System.Globalization;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Payments;

public sealed record PaymentImportSourceRow(
    int LineNumber,
    string? RawVs,
    decimal? Amount,
    string? BookingDate,
    string? Counterparty,
    string? Note);

internal static class PaymentImportMatcher
{
    public static async Task<PaymentImportPreviewResponse> MatchAsync(
        AppDbContext db,
        Guid companyId,
        IReadOnlyList<PaymentImportSourceRow> sourceRows,
        CancellationToken ct)
    {
        var openInvoices = await db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId)
            .Where(i =>
                i.Status == InvoiceStatus.Issued ||
                i.Status == InvoiceStatus.Overdue ||
                i.Status == InvoiceStatus.Paid)
            .Select(i => new
            {
                i.Id,
                i.Number,
                i.VariableSymbol,
                i.Total,
                i.Currency,
                i.Status
            })
            .ToListAsync(ct);

        var byVs = openInvoices
            .Where(i => !string.IsNullOrWhiteSpace(i.VariableSymbol))
            .GroupBy(i => DigitsOnly(i.VariableSymbol!))
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<PaymentImportRowResult>();
        var matched = 0;
        var ambiguous = 0;
        var unmatched = 0;
        var alreadyPaid = 0;
        var skipped = 0;

        foreach (var source in sourceRows)
        {
            var rawVs = source.RawVs;
            var normalized = DigitsOnly(rawVs ?? "");

            if (string.IsNullOrWhiteSpace(normalized))
            {
                skipped++;
                rows.Add(new PaymentImportRowResult(
                    source.LineNumber, rawVs, null, source.Amount, source.BookingDate, source.Counterparty, source.Note,
                    "skipped", null, null, null, null,
                    "Missing variable symbol."));
                continue;
            }

            if (!byVs.TryGetValue(normalized, out var candidates) || candidates.Count == 0)
            {
                unmatched++;
                rows.Add(new PaymentImportRowResult(
                    source.LineNumber, rawVs, normalized, source.Amount, source.BookingDate, source.Counterparty, source.Note,
                    "unmatched", null, null, null, null,
                    "No invoice with this VS."));
                continue;
            }

            if (candidates.Count > 1)
            {
                ambiguous++;
                rows.Add(new PaymentImportRowResult(
                    source.LineNumber, rawVs, normalized, source.Amount, source.BookingDate, source.Counterparty, source.Note,
                    "ambiguous", null, null, null, null,
                    $"Multiple invoices share VS {normalized}."));
                continue;
            }

            var inv = candidates[0];
            if (inv.Status == InvoiceStatus.Paid)
            {
                alreadyPaid++;
                rows.Add(new PaymentImportRowResult(
                    source.LineNumber, rawVs, normalized, source.Amount, source.BookingDate, source.Counterparty, source.Note,
                    "already_paid", inv.Id, inv.Number, inv.Total, inv.Currency,
                    "Invoice is already paid."));
                continue;
            }

            matched++;
            var message = source.Amount.HasValue && Math.Abs(source.Amount.Value - inv.Total) > 0.05m
                ? $"Amount differs (CSV {source.Amount.Value:0.00} vs invoice {inv.Total:0.00})."
                : null;

            rows.Add(new PaymentImportRowResult(
                source.LineNumber, rawVs, normalized, source.Amount, source.BookingDate, source.Counterparty, source.Note,
                "matched", inv.Id, inv.Number, inv.Total, inv.Currency, message));
        }

        return new PaymentImportPreviewResponse(
            sourceRows.Count,
            matched,
            ambiguous,
            unmatched,
            alreadyPaid,
            skipped,
            rows);
    }

    internal static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    internal static decimal? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var cleaned = raw.Trim()
            .Replace("€", "", StringComparison.Ordinal)
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "")
            .Replace("\u00a0", "");

        if (cleaned.Contains(',') && cleaned.Contains('.'))
            cleaned = cleaned.Replace(".", "").Replace(',', '.');
        else if (cleaned.Contains(','))
            cleaned = cleaned.Replace(',', '.');

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : null;
    }

    internal static string? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim().TrimStart('+');

        var formats = new[]
        {
            "yyyy-MM-dd", "d.M.yyyy", "dd.MM.yyyy", "d/M/yyyy", "dd/MM/yyyy", "d.M.yy"
        };

        if (DateOnly.TryParseExact(
                trimmed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (DateOnly.TryParse(trimmed, CultureInfo.GetCultureInfo("sk-SK"), DateTimeStyles.None, out date))
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return null;
    }
}
