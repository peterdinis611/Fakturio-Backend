using Fakturio.Api.Models;

namespace Fakturio.Api.Dtos;

public record CreateInvoiceItemRequest(
    string Description,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate
);

public record CreateInvoiceRequest(
    Guid CustomerId,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    DateOnly? DeliveryDate,
    string? Currency,
    string? VariableSymbol,
    string? ConstantSymbol,
    string? SpecificSymbol,
    string? PaymentMethod,
    string? BankIban,
    string? BankBic,
    string? Notes,
    InvoiceStatus? Status,
    InvoiceDocumentType? DocumentType,
    IReadOnlyList<CreateInvoiceItemRequest> Items
);

public record UpdateInvoiceRequest(
    Guid CustomerId,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateOnly? DeliveryDate,
    string? Currency,
    string? VariableSymbol,
    string? ConstantSymbol,
    string? SpecificSymbol,
    string? PaymentMethod,
    string? BankIban,
    string? BankBic,
    string? Notes,
    IReadOnlyList<CreateInvoiceItemRequest> Items
);

public record UpdateInvoiceStatusRequest(InvoiceStatus Status);

public record MarkInvoicePaidRequest(DateOnly? PaidAt);

public record CancelInvoiceRequest(string Reason);

public record CreateCreditNoteRequest(
    DateOnly? IssueDate,
    DateOnly? DueDate,
    string? Notes,
    IReadOnlyList<CreateInvoiceItemRequest>? Items
);

public record FinalizeProformaRequest(
    DateOnly? IssueDate,
    DateOnly? DueDate,
    string? Notes
);

public record InvoiceItemResponse(
    Guid Id,
    string Description,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    int SortOrder
);

public record InvoiceListItemResponse(
    Guid Id,
    string Number,
    InvoiceDocumentType DocumentType,
    InvoiceStatus Status,
    Guid CustomerId,
    string CustomerName,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateOnly? PaidAt,
    string Currency,
    decimal Total,
    bool IsOverdue,
    int? DaysUntilDue,
    Guid? RelatedInvoiceId,
    string? RelatedInvoiceNumber,
    DateTime CreatedAt
);

public record InvoicePartyResponse(
    string Name,
    string? Ico,
    string? Dic,
    string? IcDph,
    string? Email,
    string? Street,
    string? City,
    string? PostalCode,
    string? Country
);

public record InvoiceResponse(
    Guid Id,
    string Number,
    InvoiceDocumentType DocumentType,
    InvoiceStatus Status,
    Guid CustomerId,
    InvoicePartyResponse Customer,
    InvoicePartyResponse? Seller,
    bool SellerIsVatPayer,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateOnly? DeliveryDate,
    DateOnly? PaidAt,
    string? CancelReason,
    DateOnly? CancelledAt,
    DateTime? LastReminderSentAt,
    int ReminderCount,
    string Currency,
    string? VariableSymbol,
    string? ConstantSymbol,
    string? SpecificSymbol,
    string? PaymentMethod,
    string? BankIban,
    string? BankBic,
    string? Notes,
    Guid? RelatedInvoiceId,
    string? RelatedInvoiceNumber,
    decimal Subtotal,
    decimal VatTotal,
    decimal Total,
    bool IsOverdue,
    int? DaysUntilDue,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<InvoiceItemResponse> Items
);

public record InvoiceStatusResponse(
    Guid Id,
    string Number,
    InvoiceDocumentType DocumentType,
    InvoiceStatus Status,
    DateOnly? PaidAt,
    string? CancelReason,
    DateOnly? CancelledAt
);

public record InvoiceStatsResponse(
    DateOnly? From,
    DateOnly? To,
    int TotalCount,
    int DraftCount,
    int IssuedCount,
    int PaidCount,
    int OverdueCount,
    int CancelledCount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    decimal OverdueAmount,
    string Currency
);

public record InvoiceReminderItemResponse(
    Guid Id,
    string Number,
    InvoiceDocumentType DocumentType,
    InvoiceStatus Status,
    Guid CustomerId,
    string CustomerName,
    string? CustomerEmail,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Currency,
    decimal Total,
    bool IsOverdue,
    int DaysUntilDue,
    int ReminderCount,
    DateTime? LastReminderSentAt
);

public record SyncOverdueResponse(int UpdatedCount);
