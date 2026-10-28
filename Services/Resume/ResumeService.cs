using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Resume;

public sealed class ResumeService(AppDbContext db) : IResumeService
{
    public async Task<ResumeProfileResponse> GetProfileAsync(CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileAsync(ct);
        return ResumeMapper.ToResponse(profile);
    }

    public async Task<ServiceResult<ResumeProfileResponse>> UpdateProfileAsync(
        UpdateResumeProfileRequest request,
        CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileAsync(ct);
        ResumeMapper.Apply(profile, request);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ResumeProfileResponse>.Ok(ResumeMapper.ToResponse(profile));
    }

    public async Task<PagedResponse<ResumeEntryResponse>> ListEntriesAsync(
        int page,
        int pageSize,
        string? type,
        string? search,
        CancellationToken ct = default)
    {
        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var term = ServiceGuards.NormalizeOptionalSearch(search);
        var typeFilter = string.IsNullOrWhiteSpace(type)
            ? null
            : type.Trim().ToLowerInvariant();

        var query = db.ResumeEntries.AsNoTracking().AsQueryable();
        if (typeFilter is not null && ResumeEntryTypes.IsValid(typeFilter))
            query = query.Where(e => e.Type == typeFilter);
        if (term is not null)
        {
            query = query.Where(e =>
                e.Title.Contains(term) ||
                e.Organization.Contains(term) ||
                e.Description.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(e => e.SortOrder)
            .ThenByDescending(e => e.StartDate)
            .ThenBy(e => e.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<ResumeEntryResponse>(
            items.Select(ResumeMapper.ToResponse).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ResumeEntryResponse?> GetEntryAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        var entry = await db.ResumeEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        return entry is null ? null : ResumeMapper.ToResponse(entry);
    }

    public async Task<ServiceResult<ResumeEntryResponse>> CreateEntryAsync(
        CreateResumeEntryRequest request,
        CancellationToken ct = default)
    {
        var entity = ResumeMapper.FromCreate(request);
        db.ResumeEntries.Add(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ResumeEntryResponse>.Created(ResumeMapper.ToResponse(entity));
    }

    public async Task<ServiceResult<ResumeEntryResponse>> UpdateEntryAsync(
        Guid id,
        UpdateResumeEntryRequest request,
        CancellationToken ct = default)
    {
        var entity = await db.ResumeEntries.FindAsync([id], ct);
        if (entity is null)
            return ServiceResult<ResumeEntryResponse>.NotFound(
                "Resume entry not found.",
                ErrorCodes.NotFound);

        ResumeMapper.ApplyUpdate(entity, request);
        await db.SaveChangesAsync(ct);
        return ServiceResult<ResumeEntryResponse>.Ok(ResumeMapper.ToResponse(entity));
    }

    public async Task<ServiceResult> DeleteEntryAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.ResumeEntries.FindAsync([id], ct);
        if (entity is null)
            return ServiceResult.NotFound("Resume entry not found.", ErrorCodes.NotFound);

        db.ResumeEntries.Remove(entity);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    private async Task<ResumeProfile> GetOrCreateProfileAsync(CancellationToken ct)
    {
        var profile = await db.ResumeProfiles.FirstOrDefaultAsync(ct);
        if (profile is not null)
            return profile;

        profile = new ResumeProfile();
        db.ResumeProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }
}
