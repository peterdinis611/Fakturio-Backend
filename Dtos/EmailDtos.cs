namespace Fakturio.Api.Dtos;

public record EmailStatusResponse(
    bool Configured,
    string Provider,
    string? FromAddress,
    string? FromName,
    bool HasPublicAppBaseUrl,
    bool Simulated = false);

public record SendTestEmailRequest(string? To);

public record SendTestEmailResponse(string To, string Provider);

public record SendInvoiceEmailRequest(
    string? To,
    string? Subject,
    string? Message,
    bool AttachPdf = true,
    bool IncludePublicLink = true);

public record SendInvoiceEmailResponse(
    Guid InvoiceId,
    string Number,
    string To,
    string Provider,
    bool PdfAttached,
    bool PublicLinkIncluded);
