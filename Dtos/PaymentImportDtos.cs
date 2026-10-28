namespace Fakturio.Api.Dtos;

public record PaymentImportPreviewRequest(string CsvText);

public record PaymentImportConfirmRequest(
    IReadOnlyList<PaymentImportConfirmRow> Rows
);

public record PaymentImportConfirmRow(
    Guid InvoiceId,
    string? PaidAt
);

public record PaymentImportPreviewResponse(
    int TotalRows,
    int Matched,
    int Ambiguous,
    int Unmatched,
    int AlreadyPaid,
    int Skipped,
    IReadOnlyList<PaymentImportRowResult> Rows
);

public record PaymentImportRowResult(
    int LineNumber,
    string? RawVariableSymbol,
    string? NormalizedVariableSymbol,
    decimal? Amount,
    string? BookingDate,
    string? Counterparty,
    string? Note,
    string MatchStatus,
    Guid? InvoiceId,
    string? InvoiceNumber,
    decimal? InvoiceTotal,
    string? InvoiceCurrency,
    string? Message
);

public record PaymentImportConfirmResponse(
    int MarkedPaid,
    int Failed,
    IReadOnlyList<PaymentImportConfirmResult> Results
);

public record PaymentImportConfirmResult(
    Guid InvoiceId,
    bool Success,
    string? Message
);
