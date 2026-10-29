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
            return ServiceResult<AuthResponse>.BadRequest("E-mail je povinný.", ErrorCodes.BadRequest);

        if (string.IsNullOrWhiteSpace(request.Password) || !IsStrongPassword(request.Password))
            return ServiceResult<AuthResponse>.BadRequest(
                "Heslo musí mať aspoň 8 znakov, veľké a malé písmeno a špeciálny znak.",
                ErrorCodes.BadRequest);

        var exists = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (exists)
            return ServiceResult<AuthResponse>.Conflict(
                "Účet s týmto e-mailom už existuje.",
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
                "E-mail a heslo sú povinné.",
                ErrorCodes.BadRequest);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return ServiceResult<AuthResponse>.Unauthorized(
                "Neplatný e-mail alebo heslo.",
                ErrorCodes.Unauthorized);

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return ServiceResult<AuthResponse>.Unauthorized(
                "Neplatný e-mail alebo heslo.",
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
