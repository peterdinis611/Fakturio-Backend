using System.Globalization;
using Fakturio.Api.Models;

namespace Fakturio.Api.Services.Invoices;

/// <summary>
/// Mandatory / expected Slovak invoice legal clauses that accountants look for.
/// </summary>
public static class InvoiceLegalTexts
{
    private static readonly HashSet<string> EuCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU",
        "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
    };

    public static IReadOnlyList<string> BuildClauses(Invoice invoice, string? commercialRegister)
    {
        var clauses = new List<string>();

        if (invoice.DocumentType == InvoiceDocumentType.Proforma)
        {
            clauses.Add(
                "Tento doklad nie je daňovým dokladom podľa zákona č. 222/2004 Z. z. o dani z pridanej hodnoty.");
        }

        if (invoice.DocumentType == InvoiceDocumentType.CreditNote)
        {
            clauses.Add(
                "Opravný daňový doklad (dobropis) podľa zákona č. 222/2004 Z. z. o dani z pridanej hodnoty.");
            if (!string.IsNullOrWhiteSpace(invoice.RelatedInvoiceNumber))
            {
                clauses.Add($"Opravuje doklad č. {invoice.RelatedInvoiceNumber.Trim()}.");
            }
        }

        if (!invoice.SellerIsVatPayer)
        {
            clauses.Add(
                "Dodávateľ nie je platiteľom dane z pridanej hodnoty podľa § 4 zákona č. 222/2004 Z. z. o dani z pridanej hodnoty.");
        }
        else if (IsReverseCharge(invoice))
        {
            clauses.Add(
                "Faktúra je vystavená v režime prenesenia daňovej povinnosti podľa § 69 zákona č. 222/2004 Z. z. Daň odvedie odberateľ.");
            clauses.Add("Reverse charge — VAT to be accounted for by the recipient.");
        }

        if (!string.IsNullOrWhiteSpace(commercialRegister))
        {
            clauses.Add($"Zapísaný v: {commercialRegister.Trim()}.");
        }

        if (invoice.SellerIsVatPayer
            && invoice.DocumentType == InvoiceDocumentType.Invoice
            && !IsReverseCharge(invoice)
            && invoice.VatTotal == 0
            && invoice.Items.Count > 0
            && invoice.Items.All(i => i.VatRate == 0))
        {
            clauses.Add("Oslobodené od DPH / sadzba 0 % podľa zákona o DPH.");
        }

        return clauses;
    }

    public static string VatRegimeLabel(Invoice invoice)
    {
        if (!invoice.SellerIsVatPayer)
            return "neplatca_dph";
        if (IsReverseCharge(invoice))
            return "reverse_charge";
        if (invoice.VatTotal == 0 && invoice.Items.All(i => i.VatRate == 0))
            return "sadzba_0";
        return "platca_dph";
    }

    public static bool IsReverseCharge(Invoice invoice)
    {
        if (!invoice.SellerIsVatPayer)
            return false;
        if (string.IsNullOrWhiteSpace(invoice.CustomerIcDph))
            return false;

        var buyerCountry = (invoice.CustomerCountry ?? "SK").Trim().ToUpperInvariant();
        var sellerCountry = (invoice.SellerCountry ?? "SK").Trim().ToUpperInvariant();

        if (!EuCountries.Contains(buyerCountry))
            return false;

        // Classic EU B2B reverse charge: different member state + zero VAT on lines.
        var crossBorder = !string.Equals(buyerCountry, sellerCountry, StringComparison.OrdinalIgnoreCase);
        if (!crossBorder)
            return false;

        if (invoice.Items.Count == 0)
            return invoice.VatTotal == 0;

        return invoice.VatTotal == 0 && invoice.Items.All(i => i.VatRate == 0);
    }

    public static string DocumentTypeCode(InvoiceDocumentType type) => type switch
    {
        InvoiceDocumentType.Proforma => "zaloha",
        InvoiceDocumentType.CreditNote => "dobropis",
        _ => "faktura",
    };

    public static string FormatMoneyS3Date(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
