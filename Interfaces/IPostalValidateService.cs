using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IPostalValidateService
{
    Task<ServiceResult<PostalValidateResponse>> ValidateAsync(
        string code,
        string? country = "SK",
        string? city = null,
        CancellationToken ct = default);
}
