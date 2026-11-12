using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IInvoiceIsdocService
{
    Task<ServiceResult<InvoiceIsdocFile>> GenerateIsdocAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<InvoiceIsdocFile>> GenerateForCompanyAsync(
        Guid id,
        Guid companyId,
        CancellationToken ct = default);
}

public record InvoiceIsdocFile(byte[] Content, string FileName, string ContentType = "application/xml");
