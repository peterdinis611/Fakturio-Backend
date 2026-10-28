using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Payments;

public sealed class PaymentImportService(
    AppDbContext db,
    IInvoiceService invoices) : IPaymentImportService
{
    private static readonly string[] VsHeaders =
    [
        "vs", "variabilnysymbol", "variabilny", "variablesymbol", "reference", "poznamka", "sprava"
    ];

    private static readonly string[] AmountHeaders =
    [
        "suma", "amount", "castka", "čiastka", "ciastka", "credit", "kredit", "prijem"
    ];

    private static readonly string[] DateHeaders =
    [
        "datum", "date", "bookingdate", "valuedate", "datumpohybu", "datumzauctovania", "datumvaluty"
    ];

    private static readonly string[] CounterpartyHeaders =
    [
        "protistrana", "partner", "nazov", "counterparty", "meno", "odberatel", "platca"
    ];

    private static readonly string[] NoteHeaders =
    [
        "poznamka", "note", "sprava", "popis", "informacia"
    ];

    public async Task<ServiceResult<PaymentImportPreviewResponse>> PreviewAsync(
        PaymentImportPreviewRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CsvText))
            return ServiceResult<PaymentImportPreviewResponse>.BadRequest(
                "CSV text is required.",
                ErrorCodes.BadRequest);

        var parsed = BankCsvParser.Parse(request.CsvText);
        if (parsed.Rows.Count == 0)
            return ServiceResult<PaymentImportPreviewResponse>.BadRequest(
                "CSV contains no data rows.",
                ErrorCodes.BadRequest);

        var vsIndex = FindColumn(parsed.Headers, VsHeaders);
        if (vsIndex < 0)
            return ServiceResult<PaymentImportPreviewResponse>.BadRequest(
                "Could not find a variable symbol column (VS / Variabilný symbol).",
                ErrorCodes.BadRequest);

        var amountIndex = FindColumn(parsed.Headers, AmountHeaders);
        var dateIndex = FindColumn(parsed.Headers, DateHeaders);
        var partyIndex = FindColumn(parsed.Headers, CounterpartyHeaders);
        var noteIndex = FindColumn(parsed.Headers, NoteHeaders);
        // Prefer dedicated note column over VS column when both map to "poznamka"
        if (noteIndex == vsIndex)
            noteIndex = -1;

        var openInvoices = await db.Invoices.AsNoTracking()
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

        for (var i = 0; i < parsed.Rows.Count; i++)
        {
            var lineNumber = i + 2; // header is line 1
            var cells = parsed.Rows[i];
            var rawVs = GetCell(cells, vsIndex);
            var normalized = DigitsOnly(rawVs ?? "");
            var amount = ParseAmount(GetCell(cells, amountIndex));
            var bookingDate = ParseDate(GetCell(cells, dateIndex));
            var counterparty = GetCell(cells, partyIndex);
            var note = GetCell(cells, noteIndex);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                skipped++;
                rows.Add(new PaymentImportRowResult(
                    lineNumber, rawVs, null, amount, bookingDate, counterparty, note,
                    "skipped", null, null, null, null,
                    "Missing variable symbol."));
                continue;
            }

            if (!byVs.TryGetValue(normalized, out var candidates) || candidates.Count == 0)
            {
                unmatched++;
                rows.Add(new PaymentImportRowResult(
                    lineNumber, rawVs, normalized, amount, bookingDate, counterparty, note,
                    "unmatched", null, null, null, null,
                    "No invoice with this VS."));
                continue;
            }

            if (candidates.Count > 1)
            {
                ambiguous++;
                rows.Add(new PaymentImportRowResult(
                    lineNumber, rawVs, normalized, amount, bookingDate, counterparty, note,
                    "ambiguous", null, null, null, null,
                    $"Multiple invoices share VS {normalized}."));
                continue;
            }

            var inv = candidates[0];
            if (inv.Status == InvoiceStatus.Paid)
            {
                alreadyPaid++;
                rows.Add(new PaymentImportRowResult(
                    lineNumber, rawVs, normalized, amount, bookingDate, counterparty, note,
                    "already_paid", inv.Id, inv.Number, inv.Total, inv.Currency,
                    "Invoice is already paid."));
                continue;
            }

            matched++;
            var message = amount.HasValue && Math.Abs(amount.Value - inv.Total) > 0.05m
                ? $"Amount differs (CSV {amount.Value:0.00} vs invoice {inv.Total:0.00})."
                : null;

            rows.Add(new PaymentImportRowResult(
                lineNumber, rawVs, normalized, amount, bookingDate, counterparty, note,
                "matched", inv.Id, inv.Number, inv.Total, inv.Currency, message));
        }

        return ServiceResult<PaymentImportPreviewResponse>.Ok(new PaymentImportPreviewResponse(
            parsed.Rows.Count,
            matched,
            ambiguous,
            unmatched,
            alreadyPaid,
            skipped,
            rows));
    }

    public async Task<ServiceResult<PaymentImportConfirmResponse>> ConfirmAsync(
        PaymentImportConfirmRequest request,
        CancellationToken ct = default)
    {
        if (request.Rows is null || request.Rows.Count == 0)
            return ServiceResult<PaymentImportConfirmResponse>.BadRequest(
                "At least one row is required.",
                ErrorCodes.BadRequest);

        var results = new List<PaymentImportConfirmResult>();
        var marked = 0;
        var failed = 0;

        foreach (var row in request.Rows)
        {
            DateOnly? paidAt = null;
            if (!string.IsNullOrWhiteSpace(row.PaidAt) &&
                DateOnly.TryParse(row.PaidAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                paidAt = parsedDate;
            }

            var result = await invoices.MarkPaidAsync(
                row.InvoiceId,
                new MarkInvoicePaidRequest(paidAt),
                ct);

            if (result.Success)
            {
                marked++;
                results.Add(new PaymentImportConfirmResult(row.InvoiceId, true, null));
            }
            else
            {
                failed++;
                results.Add(new PaymentImportConfirmResult(
                    row.InvoiceId,
                    false,
                    result.Error ?? "Failed to mark paid."));
            }
        }

        return ServiceResult<PaymentImportConfirmResponse>.Ok(
            new PaymentImportConfirmResponse(marked, failed, results));
    }

    private static int FindColumn(IReadOnlyList<string> headers, string[] aliases)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var key = NormalizeHeader(headers[i]);
            if (aliases.Any(a => key == a || key.Contains(a)))
                return i;
        }
        return -1;
    }

    private static string NormalizeHeader(string header) =>
        Regex.Replace(
            header.Trim().ToLowerInvariant()
                .Replace("á", "a").Replace("ä", "a").Replace("č", "c")
                .Replace("ď", "d").Replace("é", "e").Replace("í", "i")
                .Replace("ľ", "l").Replace("ĺ", "l").Replace("ň", "n")
                .Replace("ó", "o").Replace("ô", "o").Replace("ŕ", "r")
                .Replace("š", "s").Replace("ť", "t").Replace("ú", "u")
                .Replace("ý", "y").Replace("ž", "z"),
            @"[^a-z0-9]",
            "");

    private static string? GetCell(IReadOnlyList<string> cells, int index) =>
        index < 0 || index >= cells.Count ? null : NullIfEmpty(cells[index]);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string DigitsOnly(string value) =>
        new(value.Where(char.IsDigit).ToArray());

    private static decimal? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var cleaned = raw.Trim()
            .Replace("€", "", StringComparison.Ordinal)
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "")
            .Replace("\u00a0", "");

        // SK/CZ often use 1.234,56
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

    private static string? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var formats = new[]
        {
            "d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d.M.yy"
        };

        if (DateOnly.TryParseExact(
                raw.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return DateOnly.TryParse(raw.Trim(), CultureInfo.GetCultureInfo("sk-SK"), DateTimeStyles.None, out date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }
}

internal static class BankCsvParser
{
    public sealed record Result(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

    public static Result Parse(string text, int maxRows = 2000)
    {
        var raw = text;
        if (raw.Length > 0 && raw[0] == '\ufeff')
            raw = raw[1..];

        raw = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return new Result([], []);

        var delimiter = DetectDelimiter(lines[0]);
        var headers = ParseLine(lines[0], delimiter);
        var rows = new List<IReadOnlyList<string>>();
        for (var i = 1; i < lines.Length && rows.Count < maxRows; i++)
            rows.Add(ParseLine(lines[i], delimiter));

        return new Result(headers, rows);
    }

    private static char DetectDelimiter(string sample)
    {
        var semis = sample.Count(c => c == ';');
        var commas = sample.Count(c => c == ',');
        return semis >= commas ? ';' : ',';
    }

    private static List<string> ParseLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var cur = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                cells.Add(cur.ToString().Trim());
                cur.Clear();
                continue;
            }

            cur.Append(ch);
        }

        cells.Add(cur.ToString().Trim());
        return cells;
    }
}
