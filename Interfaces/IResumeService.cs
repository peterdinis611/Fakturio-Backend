using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface IResumeService
{
    Task<ResumeProfileResponse> GetProfileAsync(CancellationToken ct = default);

    Task<ServiceResult<ResumeProfileResponse>> UpdateProfileAsync(
        UpdateResumeProfileRequest request,
        CancellationToken ct = default);

    Task<PagedResponse<ResumeEntryResponse>> ListEntriesAsync(
        int page,
        int pageSize,
        string? type,
        string? search,
        CancellationToken ct = default);

    Task<ResumeEntryResponse?> GetEntryAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<ResumeEntryResponse>> CreateEntryAsync(
        CreateResumeEntryRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<ResumeEntryResponse>> UpdateEntryAsync(
        Guid id,
        UpdateResumeEntryRequest request,
        CancellationToken ct = default);

    Task<ServiceResult> DeleteEntryAsync(Guid id, CancellationToken ct = default);
}
