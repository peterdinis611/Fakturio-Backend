using Fakturio.Api.Models;
using Fakturio.Api.Services.Payments;

namespace Fakturio.Api.Services.Invoices.Pdf;

public sealed record InvoicePdfModel(
    Invoice Invoice,
    string Template,
    string? BankName,
    string? SellerEmail,
    string? SellerPhone,
    string? SellerWebsite,
    string? FooterNote,
    IReadOnlyList<string> LegalClauses,
    PaymentQrResult? Qr,
    byte[]? LogoBytes
);
