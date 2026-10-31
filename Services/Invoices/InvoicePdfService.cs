using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Fakturio.Api.Services.Payments;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fakturio.Api.Services.Invoices;

public class InvoicePdfService(AppDbContext db, ITenantContext tenant) : IInvoicePdfService
{
    private static readonly string Accent = "#5B8DEF";
    private static readonly string AccentSoft = "#E8F0FE";
    private static readonly string HeaderGray = "#F0F2F5";
    private static readonly string Ink = "#1A1D21";
    private static readonly string Muted = "#5C6570";

    public async Task<ServiceResult<InvoicePdfFile>> GenerateAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var companyId = await tenant.GetCompanyIdAsync(ct);
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, ct);

        if (invoice is null)
            return ServiceResult<InvoicePdfFile>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        var company = await db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, ct);
        var bankName = company?.BankName;
        var footerNote = company?.InvoiceFooter;
        var commercialRegister = company?.CommercialRegister;
        var legalClauses = InvoiceLegalTexts.BuildClauses(invoice, commercialRegister);

        PaymentQrResult? qr = null;
        if (invoice.Status is not (InvoiceStatus.Cancelled or InvoiceStatus.Draft))
        {
            qr = PaymentQr.TryCreate(
                invoice.BankIban,
                invoice.BankBic,
                invoice.SellerName,
                invoice.Total,
                invoice.Currency,
                invoice.DueDate,
                invoice.VariableSymbol,
                invoice.ConstantSymbol,
                invoice.SpecificSymbol,
                paymentNote: invoice.Number);
        }

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginVertical(32);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Ink).FontFamily("Arial"));

                page.Content().Element(c =>
                    ComposePage(c, invoice, bankName, footerNote, legalClauses, qr));
            });
        }).GeneratePdf();

        var fileName = $"faktura-{invoice.Number}.pdf";
        return ServiceResult<InvoicePdfFile>.Ok(new InvoicePdfFile(bytes, fileName));
    }

    private static void ComposePage(
        IContainer container,
        Invoice invoice,
        string? bankName,
        string? footerNote,
        IReadOnlyList<string> legalClauses,
        PaymentQrResult? qr)
    {
        container.Column(col =>
        {
            col.Spacing(0);

            // —— Header: Dodávateľ | Faktúra č. ——
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("DODÁVATEĽ")
                        .FontSize(8).FontColor(Muted).LetterSpacing(0.04f);
                    left.Item().PaddingTop(2).Text(invoice.SellerName ?? "—")
                        .FontSize(13).SemiBold().FontColor(Ink);
                    left.Item().PaddingTop(4).Element(c => AddressBlock(c,
                        invoice.SellerStreet, invoice.SellerCity,
                        invoice.SellerPostalCode, invoice.SellerCountry));
                    left.Item().PaddingTop(6).Element(c => IdLines(c,
                        invoice.SellerIco, invoice.SellerDic, invoice.SellerIcDph,
                        invoice.SellerIsVatPayer));
                });

                row.ConstantItem(220).AlignRight().Column(right =>
                {
                    right.Item().Text($"{DocumentTitle(invoice.DocumentType)} {invoice.Number}")
                        .FontSize(16).Bold().FontColor(Accent);
                    if (!string.IsNullOrWhiteSpace(invoice.RelatedInvoiceNumber))
                    {
                        right.Item().PaddingTop(2)
                            .Text($"K dokladu: {invoice.RelatedInvoiceNumber}")
                            .FontSize(8).FontColor(Muted);
                    }
                    right.Item().PaddingTop(8).Text(StatusLabel(invoice.Status))
                        .FontSize(9).FontColor(Muted);
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // —— Banka + odberateľ + dátumy ——
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(bankName) || !string.IsNullOrWhiteSpace(invoice.BankIban))
                    {
                        left.Item().Text(bankName ?? "Banka")
                            .FontSize(9).SemiBold();
                        if (!string.IsNullOrWhiteSpace(invoice.BankIban))
                            left.Item().Text($"IBAN: {FormatIban(invoice.BankIban)}").FontSize(9);
                        if (!string.IsNullOrWhiteSpace(invoice.BankBic))
                            left.Item().Text($"SWIFT: {invoice.BankBic}").FontSize(9);
                        if (!string.IsNullOrWhiteSpace(invoice.VariableSymbol))
                            left.Item().Text($"Variabilný symbol: {invoice.VariableSymbol}").FontSize(9);
                    }

                    left.Item().PaddingTop(12).Text("ODBERATEĽ")
                        .FontSize(8).FontColor(Muted).LetterSpacing(0.04f);
                    left.Item().PaddingTop(2).Text(invoice.CustomerName)
                        .FontSize(12).SemiBold();
                    left.Item().PaddingTop(4).Element(c => AddressBlock(c,
                        invoice.CustomerStreet, invoice.CustomerCity,
                        invoice.CustomerPostalCode, invoice.CustomerCountry));
                    left.Item().PaddingTop(6).Element(c => IdLines(c,
                        invoice.CustomerIco, invoice.CustomerDic, invoice.CustomerIcDph,
                        vatPayerHint: null));
                });

                row.ConstantItem(200).AlignRight().Column(dates =>
                {
                    DateRow(dates, "Dátum vystavenia", invoice.IssueDate);
                    DateRow(dates, "Dátum dodania", invoice.DeliveryDate ?? invoice.IssueDate);
                    DateRow(dates, "Dátum splatnosti", invoice.DueDate);
                    if (invoice.PaidAt.HasValue)
                        DateRow(dates, "Zaplatené", invoice.PaidAt.Value);
                });
            });

            col.Item().PaddingTop(14).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // —— Položky ——
            var showVat = invoice.SellerIsVatPayer;
            col.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3.2f);
                    columns.ConstantColumn(48);
                    columns.ConstantColumn(52);
                    columns.RelativeColumn(1.2f);
                    if (showVat) columns.ConstantColumn(44);
                    columns.RelativeColumn(1.2f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(TableHeader).Text("NÁZOV A POPIS POLOŽKY");
                    header.Cell().Element(TableHeader).AlignRight().Text("POČET");
                    header.Cell().Element(TableHeader).AlignRight().Text("JEDNOTKA");
                    header.Cell().Element(TableHeader).AlignRight().Text("JEDN. CENA");
                    if (showVat)
                        header.Cell().Element(TableHeader).AlignRight().Text("DPH %");
                    header.Cell().Element(TableHeader).AlignRight().Text("CELKOM");
                });

                foreach (var item in invoice.Items.OrderBy(i => i.SortOrder))
                {
                    table.Cell().Element(TableBody).Text(item.Description);
                    table.Cell().Element(TableBody).AlignRight()
                        .Text(item.Quantity.ToString("0.####"));
                    table.Cell().Element(TableBody).AlignRight().Text(item.Unit);
                    table.Cell().Element(TableBody).AlignRight()
                        .Text(Money(item.UnitPrice, invoice.Currency));
                    if (showVat)
                        table.Cell().Element(TableBody).AlignRight()
                            .Text($"{item.VatRate:0.##}");
                    table.Cell().Element(TableBody).AlignRight()
                        .Text(Money(item.Total, invoice.Currency));
                }
            });

            col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // —— Poznámka + QR + suma + podpis ——
            col.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        left.Item().Text("Poznámka").FontSize(8).FontColor(Muted);
                        left.Item().PaddingTop(2).Text(invoice.Notes.Trim()).FontSize(9);
                    }

                    if (qr is not null)
                    {
                        left.Item().PaddingTop(10).Row(qrRow =>
                        {
                            qrRow.ConstantItem(108).Column(q =>
                            {
                                q.Item().Width(100).Height(100).Image(qr.Png);
                            });
                            qrRow.RelativeItem().PaddingLeft(8).AlignMiddle().Column(lbl =>
                            {
                                lbl.Item().Text(qr.Label).SemiBold().FontSize(10);
                                lbl.Item().PaddingTop(2)
                                    .Text("Naskenuj v bankovej aplikácii")
                                    .FontSize(8).FontColor(Muted);
                            });
                        });
                    }
                });

                row.ConstantItem(200).Column(right =>
                {
                    if (showVat && !InvoiceLegalTexts.IsReverseCharge(invoice))
                    {
                        TotalsRow(right, "Základ", Money(invoice.Subtotal, invoice.Currency));
                        TotalsRow(right, "DPH", Money(invoice.VatTotal, invoice.Currency));
                    }
                    else if (InvoiceLegalTexts.IsReverseCharge(invoice))
                    {
                        TotalsRow(right, "Základ", Money(invoice.Subtotal, invoice.Currency));
                        TotalsRow(right, "DPH (prenesená)", Money(0m, invoice.Currency));
                    }

                    right.Item().PaddingTop(4).AlignRight().Text("Celková suma")
                        .FontSize(9).FontColor(Muted);
                    right.Item().AlignRight().Text(Money(invoice.Total, invoice.Currency))
                        .FontSize(16).Bold().FontColor(Accent);

                    right.Item().PaddingTop(18).AlignCenter().Column(sig =>
                    {
                        sig.Item().Height(48);
                        sig.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        sig.Item().PaddingTop(4).AlignCenter()
                            .Text("Podpis a pečiatka")
                            .FontSize(8).FontColor(Muted);
                    });
                });
            });

            if (legalClauses.Count > 0)
            {
                col.Item().PaddingTop(12).Element(c => LegalBlock(c, legalClauses));
            }

            if (!string.IsNullOrWhiteSpace(footerNote))
            {
                col.Item().PaddingTop(8).Text(footerNote).FontSize(8).FontColor(Muted);
            }

            if (invoice.Status == InvoiceStatus.Cancelled && !string.IsNullOrWhiteSpace(invoice.CancelReason))
            {
                col.Item().PaddingTop(8).Background(Colors.Red.Lighten4).Padding(8)
                    .Text($"Zrušená: {invoice.CancelReason} ({invoice.CancelledAt:dd.MM.yyyy})");
            }

            // —— Platobný pás ——
            col.Item().PaddingTop(16).Element(c => PaymentStrip(c, invoice));
        });
    }

    private static void PaymentStrip(IContainer container, Invoice invoice)
    {
        container.Background(AccentSoft).Padding(10).Row(row =>
        {
            StripCell(row, "IBAN", string.IsNullOrWhiteSpace(invoice.BankIban)
                ? "—"
                : FormatIban(invoice.BankIban!));
            StripCell(row, "Variabilný symbol",
                string.IsNullOrWhiteSpace(invoice.VariableSymbol) ? "—" : invoice.VariableSymbol!);
            StripCell(row, "Dátum splatnosti", invoice.DueDate.ToString("dd.MM.yyyy"));
            StripCell(row, "Suma na úhradu", Money(invoice.Total, invoice.Currency), emphasize: true);
        });
    }

    private static void StripCell(RowDescriptor row, string label, string value, bool emphasize = false)
    {
        row.RelativeItem().PaddingHorizontal(6).Column(col =>
        {
            col.Item().Text(label).FontSize(7.5f).FontColor(Muted);
            col.Item().PaddingTop(2).Text(value)
                .FontSize(emphasize ? 11 : 9)
                .SemiBold()
                .FontColor(emphasize ? Accent : Ink);
        });
    }

    private static void DateRow(ColumnDescriptor col, string label, DateOnly date)
    {
        col.Item().PaddingBottom(3).Row(r =>
        {
            r.RelativeItem().AlignRight().Text(label).FontSize(9).FontColor(Muted);
            r.ConstantItem(8);
            r.ConstantItem(72).AlignRight().Text(date.ToString("dd.MM.yyyy"))
                .FontSize(9).SemiBold();
        });
    }

    private static void TotalsRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().AlignRight().Text(label).FontSize(9).FontColor(Muted);
            r.ConstantItem(90).AlignRight().Text(value)
                .FontSize(9).SemiBold();
        });
    }

    private static void IdLines(
        IContainer container,
        string? ico,
        string? dic,
        string? icDph,
        bool? vatPayerHint)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(ico))
                col.Item().Text($"IČO: {ico}").FontSize(9);
            if (!string.IsNullOrWhiteSpace(dic))
                col.Item().Text($"DIČ: {dic}").FontSize(9);
            if (vatPayerHint == true && !string.IsNullOrWhiteSpace(icDph))
                col.Item().Text($"IČ DPH: {icDph}").FontSize(9);
            else if (vatPayerHint == false)
                col.Item().Text("Nie sme platiteľmi DPH").FontSize(8).FontColor(Muted);
            else if (!string.IsNullOrWhiteSpace(icDph))
                col.Item().Text($"IČ DPH: {icDph}").FontSize(9);
        });
    }

    private static void LegalBlock(IContainer container, IReadOnlyList<string> clauses)
    {
        container
            .Border(0.75f)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .Padding(8)
            .Column(col =>
            {
                col.Item().Text("PRÁVNE ÚDAJE")
                    .FontSize(7.5f).SemiBold().FontColor(Muted).LetterSpacing(0.04f);
                foreach (var clause in clauses)
                {
                    col.Item().PaddingTop(3).Text(clause).FontSize(8).FontColor(Ink);
                }
            });
    }

    private static IContainer TableHeader(IContainer container) =>
        container.DefaultTextStyle(x => x.FontSize(7.5f).SemiBold().FontColor(Muted))
            .Background(HeaderGray)
            .PaddingVertical(6)
            .PaddingHorizontal(5);

    private static IContainer TableBody(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(7).PaddingHorizontal(5)
            .DefaultTextStyle(x => x.FontSize(9));

    private static string Money(decimal value, string currency) =>
        $"{value.ToString("N2", CultureInfoSk)} {currency}";

    private static readonly System.Globalization.CultureInfo CultureInfoSk =
        System.Globalization.CultureInfo.GetCultureInfo("sk-SK");

    private static string FormatIban(string iban)
    {
        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return string.Join(' ', Enumerable.Range(0, (clean.Length + 3) / 4)
            .Select(i => clean.Substring(i * 4, Math.Min(4, clean.Length - i * 4))));
    }

    private static void AddressBlock(
        IContainer container,
        string? street,
        string? city,
        string? postal,
        string? country)
    {
        container.Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(street))
                col.Item().Text(street!).FontSize(9);
            var cityLine = $"{postal} {city}".Trim();
            if (!string.IsNullOrWhiteSpace(cityLine))
                col.Item().Text(cityLine).FontSize(9);
            if (!string.IsNullOrWhiteSpace(country) && country is not "SK")
                col.Item().Text(country!).FontSize(9);
        });
    }

    private static string StatusLabel(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Koncept",
        InvoiceStatus.Issued => "Vystavená",
        InvoiceStatus.Paid => "Zaplatená",
        InvoiceStatus.Cancelled => "Zrušená",
        InvoiceStatus.Overdue => "Po splatnosti",
        _ => status.ToString()
    };

    private static string DocumentTitle(InvoiceDocumentType type) => type switch
    {
        InvoiceDocumentType.Proforma => "Zálohová faktúra",
        InvoiceDocumentType.CreditNote => "Dobropis",
        _ => "Faktúra"
    };
}
