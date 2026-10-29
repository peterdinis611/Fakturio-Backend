using Fakturio.Api.Dtos;
using Fakturio.Api.Services.Common;

namespace Fakturio.Api.Interfaces;

public interface INoteService
{
    Task<PagedResponse<NoteResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool? pinnedOnly,
        CancellationToken ct = default);

    Task<NoteResponse?> GetAsync(Guid id, CancellationToken ct = default);

    Task<ServiceResult<NoteResponse>> CreateAsync(
        CreateNoteRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<NoteResponse>> UpdateAsync(
        Guid id,
        UpdateNoteRequest request,
        CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
