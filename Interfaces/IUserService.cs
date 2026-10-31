using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IUserService
{
    Task<PagedResponse<UserResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);

    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default);

    Task<ServiceResult> ChangePasswordAsync(
        Guid id,
        ChangePasswordRequest request,
        CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
