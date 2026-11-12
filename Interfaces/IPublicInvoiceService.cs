using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IPublicInvoiceService
{
    Task<ServiceResult<PublicInvoiceResponse>> GetByTokenAsync(string token, CancellationToken ct = default);

    Task<ServiceResult<InvoicePdfFile>> GetPdfByTokenAsync(string token, CancellationToken ct = default);

    Task<ServiceResult<InvoiceIsdocFile>> GetIsdocByTokenAsync(string token, CancellationToken ct = default);
}

public interface IInvoicePublicLinkService
{
    Task<ServiceResult<PublicLinkResponse>> GetAsync(Guid invoiceId, CancellationToken ct = default);

    Task<ServiceResult<PublicLinkResponse>> SetAsync(
        Guid invoiceId,
        SetPublicLinkRequest request,
        CancellationToken ct = default);
}
