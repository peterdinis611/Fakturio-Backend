using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Notes;

public sealed class NoteService(AppDbContext db) : INoteService
{
    public async Task<PagedResponse<NoteResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool? pinnedOnly,
        CancellationToken ct = default)
    {
        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var term = ServiceGuards.NormalizeOptionalSearch(search);

        var query = db.Notes.AsNoTracking().AsQueryable();
        if (pinnedOnly == true)
            query = query.Where(n => n.IsPinned);
        if (term is not null)
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<NoteResponse>(
            items.Select(NoteMapper.ToResponse).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<NoteResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        var note = await db.Notes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, ct);
        return note is null ? null : NoteMapper.ToResponse(note);
    }

    public async Task<ServiceResult<NoteResponse>> CreateAsync(
        CreateNoteRequest request,
        CancellationToken ct = default)
    {
        var entity = NoteMapper.FromCreate(request);
        db.Notes.Add(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult<NoteResponse>.Created(NoteMapper.ToResponse(entity));
    }

    public async Task<ServiceResult<NoteResponse>> UpdateAsync(
        Guid id,
        UpdateNoteRequest request,
        CancellationToken ct = default)
    {
        var entity = await db.Notes.FindAsync([id], ct);
        if (entity is null)
            return ServiceResult<NoteResponse>.NotFound("Poznámka nebola nájdená.", ErrorCodes.NotFound);

        NoteMapper.ApplyUpdate(entity, request);
        await db.SaveChangesAsync(ct);
        return ServiceResult<NoteResponse>.Ok(NoteMapper.ToResponse(entity));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Notes.FindAsync([id], ct);
        if (entity is null)
            return ServiceResult.NotFound("Poznámka nebola nájdená.", ErrorCodes.NotFound);

        db.Notes.Remove(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }
}
