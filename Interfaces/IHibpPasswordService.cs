using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IHibpPasswordService
{
    Task<ServiceResult<PasswordCheckResponse>> CheckAsync(string password, CancellationToken ct = default);
}
