using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IPaymentImportService
{
    Task<ServiceResult<PaymentImportPreviewResponse>> PreviewAsync(
        PaymentImportPreviewRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<PaymentImportConfirmResponse>> ConfirmAsync(
        PaymentImportConfirmRequest request,
        CancellationToken ct = default);
}
