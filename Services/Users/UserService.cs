using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Users;

public sealed class UserService(AppDbContext db) : IUserService
{
    public async Task<PagedResponse<UserResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        (page, pageSize) = ServiceGuards.NormalizePaging(page, pageSize);
        var term = ServiceGuards.NormalizeOptionalSearch(search);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (term is not null)
        {
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(term)) ||
                (u.FirstName != null && u.FirstName.Contains(term)) ||
                (u.LastName != null && u.LastName.Contains(term)) ||
                u.ClerkUserId.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<UserResponse>(
            items.Select(UserMapper.ToResponse).ToList(),
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<UserResponse?> GetByClerkIdAsync(
        string clerkUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clerkUserId))
            return null;

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.ClerkUserId == clerkUserId, ct);
        return user is null ? null : UserMapper.ToResponse(user);
    }

    public async Task<ServiceResult<UserResponse>> SyncAsync(
        string clerkUserId,
        SyncUserRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clerkUserId))
            return ServiceResult<UserResponse>.BadRequest(
                "Clerk user id is required.",
                ErrorCodes.BadRequest);

        var user = await db.Users.FirstOrDefaultAsync(u => u.ClerkUserId == clerkUserId, ct);
        if (user is null)
        {
            user = new AppUser { ClerkUserId = clerkUserId.Trim() };
            UserMapper.Apply(user, request);
            user.CreatedAt = DateTime.UtcNow;
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return ServiceResult<UserResponse>.Created(UserMapper.ToResponse(user));
        }

        UserMapper.Apply(user, request);
        await db.SaveChangesAsync(ct);
        return ServiceResult<UserResponse>.Ok(UserMapper.ToResponse(user));
    }
}
