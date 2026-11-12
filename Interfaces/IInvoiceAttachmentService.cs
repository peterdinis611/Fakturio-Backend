using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IInvoiceAttachmentService
{
    Task<ServiceResult<IReadOnlyList<InvoiceAttachmentResponse>>> ListAsync(
        Guid invoiceId,
        CancellationToken ct = default);

    Task<ServiceResult<InvoiceAttachmentResponse>> UploadAsync(
        Guid invoiceId,
        IFormFile file,
        CancellationToken ct = default);

    Task<ServiceResult<InvoicePdfFile>> DownloadAsync(
        Guid attachmentId,
        CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(Guid attachmentId, CancellationToken ct = default);
}
