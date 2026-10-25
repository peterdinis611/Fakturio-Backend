using System.Globalization;
using System.Text;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Exports;

public class CsvExportService(AppDbContext db) : IExportService
{
    private static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");

    public async Task<ServiceResult<ExportFile>> ExportInvoicesCsvAsync(
        DateOnly? from,
        DateOnly? to,
        InvoiceStatus? status,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail(rangeError);

        var query = db.Invoices.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(i => i.IssueDate >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IssueDate <= to.Value);
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var rows = await query
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Number)
            .Select(i => new
            {
                i.Number,
                Status = i.Status.ToString(),
                i.IssueDate,
                i.DueDate,
                i.PaidAt,
                i.CustomerName,
                i.CustomerIco,
                i.VariableSymbol,
                i.Currency,
                i.Subtotal,
                i.VatTotal,
                i.Total,
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';',
            "Cislo", "Stav", "Datum_vystavenia", "Datum_splatnosti", "Datum_uhrady",
            "Odberatel", "ICO", "VS", "Mena", "Zaklad", "DPH", "Celkom"));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(';',
                Csv(r.Number),
                Csv(r.Status),
                Csv(r.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(r.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(r.PaidAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
                Csv(r.CustomerName),
                Csv(r.CustomerIco ?? ""),
                Csv(r.VariableSymbol ?? ""),
                Csv(r.Currency),
                Decimal(r.Subtotal),
                Decimal(r.VatTotal),
                Decimal(r.Total)));
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Ok(sb.ToString(), $"faktury-{stamp}.csv");
    }

    public async Task<ServiceResult<ExportFile>> ExportVatCsvAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail(rangeError);

        var query = db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Where(i => i.Status == InvoiceStatus.Issued
                || i.Status == InvoiceStatus.Paid
                || i.Status == InvoiceStatus.Overdue);

        if (from.HasValue)
            query = query.Where(i => i.IssueDate >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IssueDate <= to.Value);

        var invoices = await query
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Number)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';',
            "Cislo", "Datum_vystavenia", "Odberatel", "ICO", "Sadzba_DPH",
            "Zaklad", "DPH", "Celkom", "Mena"));

        foreach (var inv in invoices)
        {
            foreach (var item in inv.Items.OrderBy(x => x.SortOrder))
            {
                sb.AppendLine(string.Join(';',
                    Csv(inv.Number),
                    Csv(inv.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Csv(inv.CustomerName),
                    Csv(inv.CustomerIco ?? ""),
                    Decimal(item.VatRate),
                    Decimal(item.Subtotal),
                    Decimal(item.VatAmount),
                    Decimal(item.Total),
                    Csv(inv.Currency)));
            }
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return Ok(sb.ToString(), $"dph-{stamp}.csv");
    }

    private static ServiceResult<ExportFile> Ok(string csv, string fileName)
    {
        // UTF-8 BOM so Excel on Windows opens Slovak diacritics correctly
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return ServiceResult<ExportFile>.Ok(new ExportFile(bytes, fileName));
    }

    private static ServiceResult<ExportFile> Fail(ServiceResult error) =>
        ServiceResult<ExportFile>.Fail(error.Error!, error.StatusCode, error.ErrorCode);

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string Decimal(decimal value) =>
        value.ToString("0.00", Sk);
}
