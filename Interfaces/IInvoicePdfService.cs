using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IInvoicePdfService
{
    Task<ServiceResult<InvoicePdfFile>> GenerateAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Generate PDF for an invoice belonging to <paramref name="companyId"/> (no ambient tenant).</summary>
    Task<ServiceResult<InvoicePdfFile>> GenerateForCompanyAsync(
        Guid invoiceId,
        Guid companyId,
        CancellationToken ct = default);
}

public record InvoicePdfFile(byte[] Content, string FileName, string ContentType = "application/pdf");
