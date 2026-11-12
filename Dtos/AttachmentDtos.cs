namespace Fakturio.Api.Dtos;

public record InvoiceAttachmentResponse(
    Guid Id,
    Guid InvoiceId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt
);
