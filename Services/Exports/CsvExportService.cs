using System.Globalization;
using System.IO.Compression;
using System.Text;
using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Services.Invoices;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Exports;

public class CsvExportService(AppDbContext db, ITenantContext tenant) : IExportService
{
    private static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<ServiceResult<ExportFile>> ExportInvoicesCsvAsync(
        DateOnly? from,
        DateOnly? to,
        InvoiceStatus? status,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail(rangeError);

        var invoices = await (await QueryInvoicesAsync(from, to, status, ct))
            .Include(i => i.Items)
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Number)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';',
            "Cislo", "Typ_dokladu", "Stav", "Rezim_DPH", "Preneseni_DPH",
            "Datum_vystavenia", "Datum_dodania", "Datum_splatnosti", "Datum_uhrady",
            "Dodavatel", "ICO_dodavatel", "DIC_dodavatel", "ICDPH_dodavatel",
            "Odberatel", "ICO_odberatel", "DIC_odberatel", "ICDPH_odberatel", "Krajina_odberatel",
            "VS", "KS", "SS", "Mena", "Zaklad", "DPH", "Celkom", "IBAN", "Poznamka"));

        foreach (var i in invoices)
        {
            sb.AppendLine(string.Join(';',
                Csv(i.Number),
                Csv(InvoiceLegalTexts.DocumentTypeCode(i.DocumentType)),
                Csv(i.Status.ToString()),
                Csv(InvoiceLegalTexts.VatRegimeLabel(i)),
                Csv(InvoiceLegalTexts.IsReverseCharge(i) ? "1" : "0"),
                Csv(i.IssueDate.ToString("yyyy-MM-dd", Inv)),
                Csv((i.DeliveryDate ?? i.IssueDate).ToString("yyyy-MM-dd", Inv)),
                Csv(i.DueDate.ToString("yyyy-MM-dd", Inv)),
                Csv(i.PaidAt?.ToString("yyyy-MM-dd", Inv) ?? ""),
                Csv(i.SellerName ?? ""),
                Csv(i.SellerIco ?? ""),
                Csv(i.SellerDic ?? ""),
                Csv(i.SellerIcDph ?? ""),
                Csv(i.CustomerName),
                Csv(i.CustomerIco ?? ""),
                Csv(i.CustomerDic ?? ""),
                Csv(i.CustomerIcDph ?? ""),
                Csv(i.CustomerCountry),
                Csv(i.VariableSymbol ?? ""),
                Csv(i.ConstantSymbol ?? ""),
                Csv(i.SpecificSymbol ?? ""),
                Csv(i.Currency),
                Decimal(i.Subtotal),
                Decimal(i.VatTotal),
                Decimal(i.Total),
                Csv(i.BankIban ?? ""),
                Csv(i.Notes ?? "")));
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", Inv);
        return OkCsv(sb.ToString(), $"faktury-{stamp}.csv");
    }

    public async Task<ServiceResult<ExportFile>> ExportVatCsvAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail(rangeError);

        var invoices = await (await QueryPostedAsync(from, to, ct))
            .Include(i => i.Items)
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Number)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';',
            "Cislo", "Typ_dokladu", "Rezim_DPH", "Preneseni_DPH",
            "Datum_vystavenia", "Datum_dodania",
            "Odberatel", "ICO", "DIC", "ICDPH", "Krajina",
            "Popis", "Sadzba_DPH", "Zaklad", "DPH", "Celkom", "Mena"));

        foreach (var inv in invoices)
        {
            foreach (var item in inv.Items.OrderBy(x => x.SortOrder))
            {
                sb.AppendLine(string.Join(';',
                    Csv(inv.Number),
                    Csv(InvoiceLegalTexts.DocumentTypeCode(inv.DocumentType)),
                    Csv(InvoiceLegalTexts.VatRegimeLabel(inv)),
                    Csv(InvoiceLegalTexts.IsReverseCharge(inv) ? "1" : "0"),
                    Csv(inv.IssueDate.ToString("yyyy-MM-dd", Inv)),
                    Csv((inv.DeliveryDate ?? inv.IssueDate).ToString("yyyy-MM-dd", Inv)),
                    Csv(inv.CustomerName),
                    Csv(inv.CustomerIco ?? ""),
                    Csv(inv.CustomerDic ?? ""),
                    Csv(inv.CustomerIcDph ?? ""),
                    Csv(inv.CustomerCountry),
                    Csv(item.Description),
                    Decimal(item.VatRate),
                    Decimal(item.Subtotal),
                    Decimal(item.VatAmount),
                    Decimal(item.Total),
                    Csv(inv.Currency)));
            }
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", Inv);
        return OkCsv(sb.ToString(), $"dph-{stamp}.csv");
    }

    public async Task<ServiceResult<ExportFile>> ExportMoneyS3XmlAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail(rangeError);

        var companyId = await tenant.GetCompanyIdAsync(ct);
        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var invoices = await (await QueryPostedAsync(from, to, ct))
            .Include(i => i.Items)
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.Number)
            .ToListAsync(ct);

        var bytes = MoneyS3XmlExport.Build(invoices, company?.Ico);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", Inv);
        return ServiceResult<ExportFile>.Ok(new ExportFile(
            bytes,
            $"money-s3-faktury-{stamp}.xml",
            "application/xml; charset=utf-8"));
    }

    public async Task<ServiceResult<ExportFile>> ExportAccountantPackAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateDateRange(from, to) is { } rangeError)
            return Fail(rangeError);

        var invoicesCsv = await ExportInvoicesCsvAsync(from, to, status: null, ct);
        if (!invoicesCsv.Success || invoicesCsv.Data is null)
            return Fail(invoicesCsv);

        var vatCsv = await ExportVatCsvAsync(from, to, ct);
        if (!vatCsv.Success || vatCsv.Data is null)
            return Fail(vatCsv);

        var xml = await ExportMoneyS3XmlAsync(from, to, ct);
        if (!xml.Success || xml.Data is null)
            return Fail(xml);

        var readme = Encoding.UTF8.GetBytes(
            """
            Fakturio — balík pre účtovníctvo
            ================================

            Obsah:
            - faktury-*.csv     … zoznam dokladov (Excel, ;, UTF-8 BOM)
            - dph-*.csv         … riadky DPH podľa sadzieb
            - money-s3-*.xml    … XML orientované na Money S3 (SeznamFaktVyd)

            Tip: CSV otváraj v Exceli / LibreOffice s oddeľovačom bodkočiarka.
            XML je UTF-8; pri importe do Money over kódovanie podľa verzie.
            """);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, invoicesCsv.Data.FileName, invoicesCsv.Data.Content);
            WriteEntry(zip, vatCsv.Data.FileName, vatCsv.Data.Content);
            WriteEntry(zip, xml.Data.FileName, xml.Data.Content);
            WriteEntry(zip, "README.txt", readme);
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd", Inv);
        return ServiceResult<ExportFile>.Ok(new ExportFile(
            ms.ToArray(),
            $"fakturio-uctovnictvo-{stamp}.zip",
            "application/zip"));
    }

    private async Task<IQueryable<Invoice>> QueryInvoicesAsync(DateOnly? from, DateOnly? to, InvoiceStatus? status, CancellationToken ct)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var query = db.Invoices.AsNoTracking().Where(i => i.CompanyId == companyId);
        if (from.HasValue)
            query = query.Where(i => i.IssueDate >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IssueDate <= to.Value);
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);
        return query;
    }

    private async Task<IQueryable<Invoice>> QueryPostedAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = await QueryInvoicesAsync(from, to, null, ct);
        return query.Where(i => i.Status == InvoiceStatus.Issued
            || i.Status == InvoiceStatus.Paid
            || i.Status == InvoiceStatus.Overdue
            || i.Status == InvoiceStatus.Cancelled);
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static ServiceResult<ExportFile> OkCsv(string csv, string fileName)
    {
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
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string Decimal(decimal value) =>
        value.ToString("0.00", Sk);
}
