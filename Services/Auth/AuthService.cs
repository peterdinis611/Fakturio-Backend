using Fakturio.Api.Common;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;
using Fakturio.Api.Interfaces;
using Fakturio.Api.Mapping;
using Fakturio.Api.Models;
using Fakturio.Api.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Fakturio.Api.Services.Auth;

public sealed class AuthService(
    AppDbContext db,
    JwtTokenService tokens,
    IPasswordHasher<AppUser> passwordHasher) : IAuthService
{
    public async Task<ServiceResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null)
            return ServiceResult<AuthResponse>.BadRequest("Email is required.", ErrorCodes.BadRequest);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return ServiceResult<AuthResponse>.BadRequest(
                "Password must be at least 8 characters.",
                ErrorCodes.BadRequest);

        var exists = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (exists)
            return ServiceResult<AuthResponse>.Conflict(
                "An account with this email already exists.",
                ErrorCodes.DuplicateEmail);

        var user = new AppUser
        {
            Email = email,
            FirstName = Normalize(request.FirstName),
            LastName = Normalize(request.LastName),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return ServiceResult<AuthResponse>.Created(BuildAuthResponse(user));
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<AuthResponse>.BadRequest(
                "Email and password are required.",
                ErrorCodes.BadRequest);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return ServiceResult<AuthResponse>.Unauthorized(
                "Invalid email or password.",
                ErrorCodes.Unauthorized);

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return ServiceResult<AuthResponse>.Unauthorized(
                "Invalid email or password.",
                ErrorCodes.Unauthorized);

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        user.LastSeenAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ServiceResult<AuthResponse>.Ok(BuildAuthResponse(user));
    }

    public async Task<UserResponse?> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        return UserMapper.ToResponse(user);
    }

    private AuthResponse BuildAuthResponse(AppUser user)
    {
        var (token, expires) = tokens.CreateToken(user);
        return new AuthResponse(token, expires, UserMapper.ToResponse(user));
    }

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
