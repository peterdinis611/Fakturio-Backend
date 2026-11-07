using Fakturio.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fakturio.Api.Services.Invoices.Pdf;

/// <summary>Traditional SK invoice — two-column parties, formal table, payment strip.</summary>
internal static class ClassicInvoicePdf
{
    private const string Accent = "#1F4B7A";
    private const string AccentSoft = "#E8EEF5";
    private const string HeaderGray = "#F0F2F5";
    private const string Ink = "#1A1D21";
    private const string Muted = "#5C6570";

    public static void Compose(IContainer container, InvoicePdfModel model)
    {
        var invoice = model.Invoice;
        container.Column(col =>
        {
            col.Spacing(0);

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Element(c => InvoicePdfShared.Logo(c, model.LogoBytes, 42));
                    left.Item().PaddingTop(model.LogoBytes is null ? 0 : 6).Text("DODÁVATEĽ")
                        .FontSize(8).FontColor(Muted).LetterSpacing(0.04f);
                    left.Item().PaddingTop(2).Text(invoice.SellerName ?? "—")
                        .FontSize(13).SemiBold().FontColor(Ink);
                    left.Item().PaddingTop(4).Element(c => InvoicePdfShared.AddressBlock(c,
                        invoice.SellerStreet, invoice.SellerCity,
                        invoice.SellerPostalCode, invoice.SellerCountry));
                    left.Item().PaddingTop(6).Element(c => InvoicePdfShared.IdLines(c,
                        invoice.SellerIco, invoice.SellerDic, invoice.SellerIcDph,
                        invoice.SellerIsVatPayer));
                    left.Item().PaddingTop(4).Element(c => InvoicePdfShared.ContactLines(c,
                        model.SellerEmail, model.SellerPhone, model.SellerWebsite));
                });

                row.ConstantItem(220).AlignRight().Column(right =>
                {
                    right.Item().Text($"{InvoicePdfShared.DocumentTitle(invoice.DocumentType)}")
                        .FontSize(11).FontColor(Muted).LetterSpacing(0.06f);
                    right.Item().Text(invoice.Number)
                        .FontSize(18).Bold().FontColor(Accent);
                    if (!string.IsNullOrWhiteSpace(invoice.RelatedInvoiceNumber))
                    {
                        right.Item().PaddingTop(2)
                            .Text($"K dokladu: {invoice.RelatedInvoiceNumber}")
                            .FontSize(8).FontColor(Muted);
                    }
                    right.Item().PaddingTop(8).Text(InvoicePdfShared.StatusLabel(invoice.Status))
                        .FontSize(9).FontColor(Muted);
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("ODBERATEĽ")
                        .FontSize(8).FontColor(Muted).LetterSpacing(0.04f);
                    left.Item().PaddingTop(2).Text(invoice.CustomerName)
                        .FontSize(12).SemiBold();
                    left.Item().PaddingTop(4).Element(c => InvoicePdfShared.AddressBlock(c,
                        invoice.CustomerStreet, invoice.CustomerCity,
                        invoice.CustomerPostalCode, invoice.CustomerCountry));
                    left.Item().PaddingTop(6).Element(c => InvoicePdfShared.IdLines(c,
                        invoice.CustomerIco, invoice.CustomerDic, invoice.CustomerIcDph,
                        vatPayerHint: null));
                });

                row.ConstantItem(200).AlignRight().Column(dates =>
                {
                    InvoicePdfShared.DateRow(dates, "Dátum vystavenia", invoice.IssueDate, Muted);
                    InvoicePdfShared.DateRow(dates, "Dátum dodania", invoice.DeliveryDate ?? invoice.IssueDate, Muted);
                    InvoicePdfShared.DateRow(dates, "Dátum splatnosti", invoice.DueDate, Muted);
                    if (invoice.PaidAt.HasValue)
                        InvoicePdfShared.DateRow(dates, "Zaplatené", invoice.PaidAt.Value, Muted);
                    var pay = InvoicePdfShared.PaymentMethodLabel(invoice.PaymentMethod);
                    if (!string.IsNullOrEmpty(pay))
                    {
                        dates.Item().PaddingTop(4).AlignRight()
                            .Text(pay).FontSize(8).FontColor(Muted);
                    }
                });
            });

            col.Item().PaddingTop(14).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            col.Item().PaddingTop(10).Element(c =>
                InvoicePdfShared.ItemsTable(c, invoice, HeaderGray, Muted));

            col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            col.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                    {
                        left.Item().Text("Poznámka").FontSize(8).FontColor(Muted);
                        left.Item().PaddingTop(2).Text(invoice.Notes.Trim()).FontSize(9);
                    }

                    InvoicePdfShared.QrBlock(left, model);
                });

                row.ConstantItem(200).Column(right =>
                {
                    InvoicePdfShared.TotalsBlock(right, invoice, Accent, Muted);
                    right.Item().PaddingTop(18).AlignCenter().Column(sig =>
                    {
                        sig.Item().Height(40);
                        sig.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        sig.Item().PaddingTop(4).AlignCenter()
                            .Text("Podpis / pečiatka")
                            .FontSize(8).FontColor(Muted);
                    });
                });
            });

            if (model.LegalClauses.Count > 0)
                col.Item().PaddingTop(12).Element(c =>
                    InvoicePdfShared.LegalBlock(c, model.LegalClauses, Ink, Muted));

            if (!string.IsNullOrWhiteSpace(model.FooterNote))
                col.Item().PaddingTop(8).Text(model.FooterNote).FontSize(8).FontColor(Muted);

            InvoicePdfShared.CancelBanner(col, invoice);

            col.Item().PaddingTop(16).Background(AccentSoft).Padding(10).Row(strip =>
            {
                Strip(strip, "IBAN", string.IsNullOrWhiteSpace(invoice.BankIban)
                    ? "—"
                    : InvoicePdfShared.FormatIban(invoice.BankIban!));
                Strip(strip, "VS",
                    string.IsNullOrWhiteSpace(invoice.VariableSymbol) ? "—" : invoice.VariableSymbol!);
                Strip(strip, "Splatnosť", invoice.DueDate.ToString("dd.MM.yyyy"));
                Strip(strip, "Suma", InvoicePdfShared.Money(invoice.Total, invoice.Currency), true);
            });
        });
    }

    private static void Strip(RowDescriptor row, string label, string value, bool emphasize = false)
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
}
