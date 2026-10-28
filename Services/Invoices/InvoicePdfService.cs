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

public class InvoicePdfService(AppDbContext db) : IInvoicePdfService
{
    public async Task<ServiceResult<InvoicePdfFile>> GenerateAsync(
        Guid invoiceId,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice is null)
            return ServiceResult<InvoicePdfFile>.NotFound("Invoice not found.", ErrorCodes.InvoiceNotFound);

        var epcPayload = invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Draft
            ? null
            : EpcQrPayload.TryBuild(
                invoice.BankIban,
                invoice.SellerName,
                invoice.Total,
                invoice.Currency,
                invoice.BankBic,
                remittance: invoice.VariableSymbol);

        var qrPng = EpcQrImage.TryCreatePng(epcPayload);

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, invoice));
                page.Content().Element(c => ComposeContent(c, invoice, qrPng));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Fakturio · strana ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        var fileName = $"faktura-{invoice.Number}.pdf";
        return ServiceResult<InvoicePdfFile>.Ok(new InvoicePdfFile(bytes, fileName));
    }

    private static void ComposeHeader(IContainer container, Invoice invoice)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(invoice.SellerName ?? "Dodávateľ")
                        .FontSize(16).SemiBold().FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrWhiteSpace(invoice.SellerIco))
                        left.Item().Text($"IČO: {invoice.SellerIco}");
                    if (!string.IsNullOrWhiteSpace(invoice.SellerDic))
                        left.Item().Text($"DIČ: {invoice.SellerDic}");
                    if (invoice.SellerIsVatPayer)
                    {
                        if (!string.IsNullOrWhiteSpace(invoice.SellerIcDph))
                            left.Item().Text($"IČ DPH: {invoice.SellerIcDph}");
                    }
                    else
                    {
                        left.Item().Text("Nie som platca DPH").FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                    left.Item().Text(FormatAddress(
                        invoice.SellerStreet, invoice.SellerCity,
                        invoice.SellerPostalCode, invoice.SellerCountry));
                });

                row.ConstantItem(220).AlignRight().Column(right =>
                {
                    right.Item().Text("FAKTÚRA").FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                    right.Item().Text($"č. {invoice.Number}").FontSize(14).SemiBold();
                    right.Item().PaddingTop(6).Text($"Stav: {StatusLabel(invoice.Status)}");
                });
            });

            col.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, Invoice invoice, byte[]? qrPng)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(c => PartyBox(c, "Odberateľ",
                    invoice.CustomerName,
                    invoice.CustomerIco,
                    invoice.CustomerDic,
                    invoice.CustomerIcDph,
                    FormatAddress(
                        invoice.CustomerStreet, invoice.CustomerCity,
                        invoice.CustomerPostalCode, invoice.CustomerCountry),
                    invoice.CustomerEmail));

                row.ConstantItem(16);
                row.RelativeItem().Element(c => MetaBox(c, invoice));
            });

            var showVat = invoice.SellerIsVatPayer;

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    if (showVat)
                        columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("#");
                    header.Cell().Element(HeaderCell).Text("Popis");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Množ.");
                    header.Cell().Element(HeaderCell).AlignRight().Text("MJ");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Cena");
                    if (showVat)
                        header.Cell().Element(HeaderCell).AlignRight().Text("DPH %");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Spolu");
                });

                var index = 1;
                foreach (var item in invoice.Items.OrderBy(i => i.SortOrder))
                {
                    table.Cell().Element(BodyCell).Text(index++.ToString());
                    table.Cell().Element(BodyCell).Text(item.Description);
                    table.Cell().Element(BodyCell).AlignRight().Text(item.Quantity.ToString("0.####"));
                    table.Cell().Element(BodyCell).AlignRight().Text(item.Unit);
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(item.UnitPrice, invoice.Currency));
                    if (showVat)
                        table.Cell().Element(BodyCell).AlignRight().Text($"{item.VatRate:0.##}");
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(item.Total, invoice.Currency));
                }
            });

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(payment =>
                {
                    if (qrPng is { Length: > 0 })
                    {
                        payment.Item().Text("Platba QR (EPC)").SemiBold().FontSize(9);
                        payment.Item().Width(110).Height(110).Image(qrPng);
                        payment.Item().PaddingTop(2).Text("Naskenuj v bankovej appke")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                });

                row.ConstantItem(220).Column(totals =>
                {
                    if (showVat)
                    {
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Základ:");
                            r.ConstantItem(100).AlignRight().Text(Money(invoice.Subtotal, invoice.Currency));
                        });
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("DPH:");
                            r.ConstantItem(100).AlignRight().Text(Money(invoice.VatTotal, invoice.Currency));
                        });
                    }

                    totals.Item().PaddingTop(showVat ? 4 : 0).Row(r =>
                    {
                        r.RelativeItem().Text("Celkom:").SemiBold().FontSize(12);
                        r.ConstantItem(100).AlignRight().Text(Money(invoice.Total, invoice.Currency))
                            .SemiBold().FontSize(12);
                    });
                });
            });

            if (!string.IsNullOrWhiteSpace(invoice.Notes))
            {
                col.Item().PaddingTop(8).Column(notes =>
                {
                    notes.Item().Text("Poznámka").SemiBold();
                    notes.Item().Text(invoice.Notes);
                });
            }

            if (invoice.Status == InvoiceStatus.Cancelled && !string.IsNullOrWhiteSpace(invoice.CancelReason))
            {
                col.Item().Background(Colors.Red.Lighten4).Padding(8).Text(
                    $"Zrušená: {invoice.CancelReason} ({invoice.CancelledAt:dd.MM.yyyy})");
            }
        });
    }

    private static void PartyBox(
        IContainer container,
        string title,
        string name,
        string? ico,
        string? dic,
        string? icDph,
        string address,
        string? email)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text(title).FontSize(9).FontColor(Colors.Grey.Darken1);
            col.Item().Text(name).SemiBold();
            if (!string.IsNullOrWhiteSpace(ico)) col.Item().Text($"IČO: {ico}");
            if (!string.IsNullOrWhiteSpace(dic)) col.Item().Text($"DIČ: {dic}");
            if (!string.IsNullOrWhiteSpace(icDph)) col.Item().Text($"IČ DPH: {icDph}");
            if (!string.IsNullOrWhiteSpace(address)) col.Item().Text(address);
            if (!string.IsNullOrWhiteSpace(email)) col.Item().Text(email);
        });
    }

    private static void MetaBox(IContainer container, Invoice invoice)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            Meta(col, "Dátum vystavenia", invoice.IssueDate.ToString("dd.MM.yyyy"));
            Meta(col, "Dátum splatnosti", invoice.DueDate.ToString("dd.MM.yyyy"));
            if (invoice.DeliveryDate.HasValue)
                Meta(col, "Dátum dodania", invoice.DeliveryDate.Value.ToString("dd.MM.yyyy"));
            if (invoice.PaidAt.HasValue)
                Meta(col, "Zaplatené", invoice.PaidAt.Value.ToString("dd.MM.yyyy"));
            if (!string.IsNullOrWhiteSpace(invoice.VariableSymbol))
                Meta(col, "VS", invoice.VariableSymbol);
            if (!string.IsNullOrWhiteSpace(invoice.BankIban))
                Meta(col, "IBAN", invoice.BankIban);
            if (!string.IsNullOrWhiteSpace(invoice.BankBic))
                Meta(col, "BIC", invoice.BankBic);
            Meta(col, "Platba", invoice.PaymentMethod ?? "bank_transfer");
        });
    }

    private static void Meta(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().AlignRight().Text(value).SemiBold();
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.White))
            .Background(Colors.Blue.Darken3)
            .Padding(5);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(4);

    private static string Money(decimal value, string currency) =>
        $"{value:0.00} {currency}";

    private static string FormatAddress(string? street, string? city, string? postal, string? country)
    {
        var parts = new[] { street, $"{postal} {city}".Trim(), country }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
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
}
