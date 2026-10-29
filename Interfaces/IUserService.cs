using Fakturio.Api.Dtos;

namespace Fakturio.Api.Interfaces;

public interface IUserService
{
    Task<PagedResponse<UserResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);

    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
