using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IInvoicePdfService
{
    Task<ServiceResult<InvoicePdfFile>> GenerateAsync(Guid invoiceId, CancellationToken ct = default);
}

public record InvoicePdfFile(byte[] Content, string FileName, string ContentType = "application/pdf");
