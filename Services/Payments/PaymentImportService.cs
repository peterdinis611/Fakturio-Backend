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
    ITenantContext tenant,
    IInvoiceService invoices,
    IAuditLogService audit,
    IFioBankService fio) : IPaymentImportService
{
    private static readonly string[] VsHeaders =
    [
        "vs", "variabilnysymbol", "variabilny", "variablesymbol", "reference", "poznamka", "sprava"
    ];

    private static readonly string[] AmountHeaders =
    [
        "suma", "amount", "castka", "čiastka", "ciastka", "credit", "kredit", "prijem", "objem"
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
        if (noteIndex == vsIndex)
            noteIndex = -1;

        var sourceRows = new List<PaymentImportSourceRow>();
        for (var i = 0; i < parsed.Rows.Count; i++)
        {
            var lineNumber = i + 2;
            var cells = parsed.Rows[i];
            sourceRows.Add(new PaymentImportSourceRow(
                lineNumber,
                GetCell(cells, vsIndex),
                PaymentImportMatcher.ParseAmount(GetCell(cells, amountIndex)),
                PaymentImportMatcher.ParseDate(GetCell(cells, dateIndex)),
                GetCell(cells, partyIndex),
                GetCell(cells, noteIndex)));
        }

        return await PreviewFromRowsAsync(sourceRows, ct);
    }

    public async Task<ServiceResult<PaymentImportPreviewResponse>> PreviewFioAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var fetch = await fio.FetchTransactionsAsync(from, to, ct);
        if (!fetch.Success || fetch.Data is null)
            return ServiceResult<PaymentImportPreviewResponse>.Fail(
                fetch.Error!,
                fetch.StatusCode,
                fetch.ErrorCode);

        if (fetch.Data.Count == 0)
            return ServiceResult<PaymentImportPreviewResponse>.BadRequest(
                "FIO period contains no transactions.",
                ErrorCodes.BadRequest);

        return await PreviewFromRowsAsync(fetch.Data, ct);
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

        var companyId = await tenant.GetCompanyIdAsync(ct);
        await audit.WriteAsync(
            AuditAction.PaymentImportApplied,
            "PaymentImport",
            null,
            null,
            $"Import platieb: {marked} označených ako zaplatené, {failed} zlyhaní.",
            companyId,
            new { marked, failed, total = request.Rows.Count },
            ct);

        return ServiceResult<PaymentImportConfirmResponse>.Ok(
            new PaymentImportConfirmResponse(marked, failed, results));
    }

    private async Task<ServiceResult<PaymentImportPreviewResponse>> PreviewFromRowsAsync(
        IReadOnlyList<PaymentImportSourceRow> sourceRows,
        CancellationToken ct)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var preview = await PaymentImportMatcher.MatchAsync(db, companyId, sourceRows, ct);
        return ServiceResult<PaymentImportPreviewResponse>.Ok(preview);
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
