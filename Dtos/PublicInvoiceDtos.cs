using Fakturio.Api.Models;

namespace Fakturio.Api.Dtos;

public record PublicInvoiceResponse(
    string Number,
    InvoiceDocumentType DocumentType,
    InvoiceStatus Status,
    InvoicePartyResponse Seller,
    InvoicePartyResponse Customer,
    bool SellerIsVatPayer,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateOnly? DeliveryDate,
    DateOnly? PaidAt,
    string Currency,
    string? VariableSymbol,
    string? ConstantSymbol,
    string? SpecificSymbol,
    string? PaymentMethod,
    string? BankIban,
    string? BankBic,
    string? Notes,
    decimal Subtotal,
    decimal VatTotal,
    decimal Total,
    bool IsOverdue,
    IReadOnlyList<InvoiceItemResponse> Items
);

public record PublicLinkResponse(
    bool Enabled,
    string? Token,
    string? PublicPath
);

public record SetPublicLinkRequest(bool Enabled);
