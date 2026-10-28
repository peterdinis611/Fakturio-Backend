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

    Task<UserResponse?> GetByClerkIdAsync(string clerkUserId, CancellationToken ct = default);

    Task<ServiceResult<UserResponse>> SyncAsync(
        string clerkUserId,
        SyncUserRequest request,
        CancellationToken ct = default);
}
