using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IAdminService
{
    Task<ServiceResult<AdminOverviewResponse>> GetOverviewAsync(CancellationToken ct = default);

    Task<ServiceResult<PagedResponse<AdminUserSummaryDto>>> ListUsersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default);
}
