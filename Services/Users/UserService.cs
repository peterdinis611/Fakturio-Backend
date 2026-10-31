using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Users;

public sealed class UserService(
    AppDbContext db,
    IPasswordHasher<AppUser> passwordHasher) : IUserService
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
                u.Email.Contains(term) ||
                (u.FirstName != null && u.FirstName.Contains(term)) ||
                (u.LastName != null && u.LastName.Contains(term)));
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

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : UserMapper.ToResponse(user);
    }

    public async Task<ServiceResult<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateGuid(id, "User", ErrorCodes.EmptyGuid) is { } guidError)
            return Fail<UserResponse>(guidError);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return ServiceResult<UserResponse>.NotFound("User not found.", ErrorCodes.NotFound);

        if (request.Email is not null)
        {
            var email = NormalizeEmail(request.Email);
            if (email is null)
                return ServiceResult<UserResponse>.BadRequest("E-mail je povinný.", ErrorCodes.BadRequest);

            var emailTaken = await db.Users.AnyAsync(u => u.Id != id && u.Email == email, ct);
            if (emailTaken)
                return ServiceResult<UserResponse>.Conflict(
                    "Účet s týmto e-mailom už existuje.",
                    ErrorCodes.DuplicateEmail);

            user.Email = email;
        }

        if (request.FirstName is not null)
            user.FirstName = Normalize(request.FirstName);
        if (request.LastName is not null)
            user.LastName = Normalize(request.LastName);
        if (request.ImageUrl is not null)
            user.ImageUrl = Normalize(request.ImageUrl);

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult<UserResponse>.Ok(UserMapper.ToResponse(user));
    }

    public async Task<ServiceResult> ChangePasswordAsync(
        Guid id,
        ChangePasswordRequest request,
        CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateGuid(id, "User", ErrorCodes.EmptyGuid) is { } guidError)
            return guidError;

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return ServiceResult.BadRequest("Aktuálne aj nové heslo sú povinné.", ErrorCodes.BadRequest);

        if (!IsStrongPassword(request.NewPassword))
            return ServiceResult.BadRequest(
                "Heslo musí mať aspoň 8 znakov, veľké a malé písmeno a špeciálny znak.",
                ErrorCodes.BadRequest);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return ServiceResult.NotFound("User not found.", ErrorCodes.NotFound);

        var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            return ServiceResult.Unauthorized("Neplatné aktuálne heslo.", ErrorCodes.Unauthorized);

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (ServiceGuards.ValidateGuid(id, "User", ErrorCodes.EmptyGuid) is { } guidError)
            return guidError;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return ServiceResult.NotFound("User not found.", ErrorCodes.NotFound);

        var remaining = await db.Users.CountAsync(ct);
        if (remaining <= 1)
            return ServiceResult.Conflict(
                "Posledného používateľa nie je možné zmazať.",
                ErrorCodes.Conflict);

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok(StatusCodes.Status204NoContent);
    }

    private static ServiceResult<T> Fail<T>(ServiceResult error) =>
        ServiceResult<T>.Fail(error.Error!, error.StatusCode, error.ErrorCode);

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsStrongPassword(string password)
    {
        if (password.Length < 8) return false;
        var hasUpper = false;
        var hasLower = false;
        var hasSpecial = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
        }
        return hasUpper && hasLower && hasSpecial;
    }
}
