using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IVatVerificationService
{
    Task<ServiceResult<VatVerificationResponse>> VerifyAsync(
        string vatId,
        CancellationToken ct = default);
}
